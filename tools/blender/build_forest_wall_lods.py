"""Non-destructive distance meshes from the approved GF2 wall source. Blender4.5 background only.
Original .blend/FBXs stay read-only; new visual-only LODs go to an owned Unity worker.
"""
import bpy, bmesh, os, json
from pathlib import Path
R=Path(os.environ['ODM_SOURCE_ROOT']).resolve();W=Path(os.environ['ODM_LOD_WORKER']).resolve()
if not bpy.app.background or bpy.app.version[:2]!=(4,5) or (W/'.shared-gameplay-prefabs-owned').read_text()!=str(R):
    raise RuntimeError('Closed owned Blender4.5 worker required')
OUT=W/'Assets/Art/Environment/GiantForest2/WallDistance';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(R/'ArtSource/Environment/GiantForest2/GiantForest2_AssetReview.blend'))
source=bpy.data.scenes['GF2_ModelReview'];report={}
for name in ['GF2_Wall_Straight_24x60','GF2_Wall_Corner_24x60','GF2_Wall_Gateway_48x60']:
    col=bpy.data.collections[name]
    for level in [1,2]:
        scene=bpy.data.scenes.new(name+'_Distance'+str(level));bpy.context.window.scene=scene
        copies=[]
        for ob in col.objects:
            if ob.type!='MESH':continue
            # Source box bevel modifiers are intentionally omitted at distance;
            # UVs, vertex paint, arch openings, structural pieces remain intact.
            data=ob.data.copy()
            if level==2 and ob.name.startswith(('LimestoneCourse','GateFaceCourse')):
                # At far range retain the two broad masonry faces, not the tiny
                # internal side/top/bottom faces of every disconnected brick.
                # Narrow edge slivers stay whole. This is NEVER collision data.
                coords=[v.co for v in data.vertices]
                span=[max(v[i] for v in coords)-min(v[i] for v in coords) for i in range(3)]
                if min(span[0],span[1])>.6 or max(span[0],span[1])>1:
                    bm=bmesh.new();bm.from_mesh(data);largest=max(f.calc_area() for f in bm.faces)
                    for face in list(bm.faces):
                        if face.calc_area()<largest*.95:bm.faces.remove(face)
                    for v in list(bm.verts):
                        if not v.link_faces:bm.verts.remove(v)
                    bm.to_mesh(data);bm.free();data.update()
            cp=bpy.data.objects.new(name+'_LOD'+str(level),data);scene.collection.objects.link(cp);cp.matrix_world=ob.matrix_world.copy();copies.append(cp)
        bpy.context.view_layer.update()
        for cp in copies:cp.select_set(True)
        bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();result=bpy.context.object;result.name=name+'_LOD'+str(level)
        # FBX exporter needs direct texture links rather than Blender-only vertex
        # paint node multiplication. Unity's existing material applies the paint.
        for i,mat in enumerate(result.data.materials):
            clone=mat.copy();clone.name=mat.name+'_Distance'
            if clone.use_nodes:
                bs=clone.node_tree.nodes.get('Principled BSDF');tex=next((n for n in clone.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_BaseColor' in n.image.name),None)
                if bs and tex:clone.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
            result.data.materials[i]=clone
        result.data.calc_loop_triangles();report[result.name]={'triangles':len(result.data.loop_triangles),'materials':[m.name for m in result.data.materials],'visualOnly':True}
        bpy.ops.export_scene.fbx(filepath=str(OUT/(result.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,path_mode='AUTO',embed_textures=False,mesh_smooth_type='FACE')
        bpy.context.window.scene=source;bpy.data.scenes.remove(scene)
(W/'Library/forest-wall-lod-art.json').write_text(json.dumps(report,indent=2));print('FOREST_WALL_LOD_ART_COMPLETE '+json.dumps(report),flush=True)
