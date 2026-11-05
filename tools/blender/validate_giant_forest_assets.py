"""Owned background review-library and FBX roundtrip checks (not Unity gameplay).
GF2_PROJECT_ROOT required. Writes validation-report.json with honest topology limits.
"""
import bpy,os,json,math,bmesh,hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np
if not bpy.app.background or not os.environ.get('GF2_PROJECT_ROOT'):raise RuntimeError('Owned background worker only')
root=Path(os.environ['GF2_PROJECT_ROOT'])/'ArtSource/Environment/GiantForest2';report=json.loads((root/'asset-report.json').read_text());result={'source':{},'fbx_roundtrip':{}}
bpy.ops.wm.open_mainfile(filepath=str(root/'GiantForest2_AssetReview.blend'))
assert bpy.context.scene.name=='GF2_ModelReview','File must OPEN a real review scene, not an empty append library'
for name,spec in report['assets'].items():
    col=bpy.data.collections[name];open_edges=0;meshes=0;solids=0
    for ob in col.objects:
        if ob.type!='MESH':continue
        meshes+=1;me=ob.data;paint=np.empty(len(me.loops)*4,dtype=np.float32);me.color_attributes['Color'].data.foreach_get('color',paint)
        uv=np.empty(len(me.loops)*2,dtype=np.float32);me.uv_layers['UVMap'].data.foreach_get('uv',uv)
        assert np.isfinite(paint).all() and paint.min()>=0 and paint.max()<1.6,(name,ob.name,'paint')
        assert np.isfinite(uv).all() and np.ptp(uv)>.01,(name,ob.name,'UV')
        assert not ob.animation_data and all(m.type not in ('ARMATURE','CLOTH') for m in ob.modifiers),name
        if me.materials[0].name!='GF2_Leaves' and not ob.name.startswith('SnappedHeartwood'):
            bm=bmesh.new();bm.from_mesh(me);count=sum(1 for e in bm.edges if not e.is_manifold);open_edges+=count;solids+=1;bm.free()
    # Solid source components are closed; intentional leaf cards and decorative
    # end-grain overlay discs (over the closed log shell caps) are excluded.
    assert open_edges==0,(name,'Nonmanifold solid-component edges',open_edges)
    result['source'][name]={'meshes':meshes,'closed_solid_components':solids,'nonmanifold_solid_edges':open_edges,'uv_and_paint':'finite, valid'}
for im in bpy.data.images:
    if im.name.startswith('GF2_') and im.source=='FILE':assert im.packed_file is not None,im.name
# Use a new empty owned scene for each import, leaving SOURCE datablocks untouched.
for name,spec in report['assets'].items():
    scene=bpy.data.scenes.new('GF2_Roundtrip_'+name);bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(root/'Exports'/(name+'.fbx')),use_anim=False)
    meshes=[o for o in scene.objects if o.type=='MESH'];assert len(meshes)==1,(name,len(meshes));ob=meshes[0];bpy.context.view_layer.update()
    bounds=[ob.matrix_world@Vector(p) for p in ob.bound_box];lo=[min(p[i] for p in bounds) for i in range(3)];hi=[max(p[i] for p in bounds) for i in range(3)]
    # Bevel can shrink a bound very slightly; allow only2mm roundtrip discrepancy.
    error=max(abs(a-b) for a,b in zip(lo+hi,spec['bounds_min']+spec['bounds_max']));assert error<.002,(name,error)
    assert ob.data.uv_layers and ob.data.color_attributes,(name,'Lost UV/paint')
    assert not any(o.type=='ARMATURE' for o in scene.objects),name
    textures={}
    for mat in ob.data.materials:
        if not mat or not mat.use_nodes:continue
        textures[mat.name]=[n.image.name for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        if any(mat.name.startswith(prefix) for prefix in ('GF2_Bark','GF2_Limestone','GF2_ForestFloor','GF2_Heartwood','GF2_Leaves')):
            assert any('_BaseColor' in image for image in textures[mat.name]),(name,mat.name,'FBX lost albedo image')
    result['fbx_roundtrip'][name]={'meshes':1,'max_bounds_error_m':error,'material_images':textures,'sha256':hashlib.sha256((root/'Exports'/(name+'.fbx')).read_bytes()).hexdigest()}
    print('GF2_ROUNDTRIP_PASS '+name+' error='+str(error),flush=True)
(root/'validation-report.json').write_text(json.dumps(result,indent=2));print('GF2_VALIDATION_COMPLETE',flush=True)
