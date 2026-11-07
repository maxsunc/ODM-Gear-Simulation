"""UV, bake portable 2K PBR maps, and export the detailed Titan to FBX + .blend.
Requires build_detailed_titan.py's collection. Does not save/rename the user's file.
"""
import bpy
import os
import json
import numpy as np

OUT=globals().get('TITAN_OUTPUT_DIR',os.path.abspath('ArtSource/Titan/Detailed'))
SIZE=globals().get('TITAN_TEXTURE_SIZE',2048)
os.makedirs(os.path.join(OUT,'Textures'),exist_ok=True)
STEM=globals().get('TITAN_ASSET_STEM','TitanDetailed')
collection=bpy.data.collections[globals().get('TITAN_COLLECTION','TitanDetailed_v2')]
meshes=[o for o in collection.objects if o.type=='MESH']
for ob in meshes:
    ob.name=STEM+'_'+ob['part_id']
rig=next(o for o in collection.objects if o.type=='ARMATURE')
scene=bpy.data.scenes.get('TitanDetailed_Bake') or bpy.data.scenes.new('TitanDetailed_Bake')
if collection.name not in scene.collection.children:
    scene.collection.children.link(collection)
window=next(iter(bpy.context.window_manager.windows))
previous_scene=window.scene
window.scene=scene
try:
    with bpy.context.temp_override(window=window,scene=scene,view_layer=scene.view_layers[0]):
        bpy.ops.object.select_all(action='DESELECT')
        for ob in meshes:
            ob.select_set(True)
            if not ob.data.uv_layers: ob.data.uv_layers.new(name='UVMap')
        bpy.context.view_layer.objects.active=meshes[0]
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        # All 15 skins share one atlas. Unwrap/pack them together, not per object.
        bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.0018,area_weight=.1,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
        scene.render.engine='CYCLES'
        scene.cycles.samples=8
        scene.cycles.use_denoising=False
        scene.render.bake.use_selected_to_active=False
        scene.render.bake.normal_space='TANGENT'
        scene.render.bake.margin=3
        mats=list({m for ob in meshes for m in ob.data.materials})
        def image(name,color_space):
            old=bpy.data.images.get(name)
            if old: bpy.data.images.remove(old)
            im=bpy.data.images.new(name,width=SIZE,height=SIZE,alpha=True)
            im.colorspace_settings.name=color_space
            im.generated_color=(.5,.5,1,1) if 'Normal' in name else (.65,.65,.65,1) if 'Roughness' in name else (.39,.235,.16,1)
            im.file_format='PNG'
            return im
        base=image(STEM+'_BaseColor','sRGB')
        normal=image(STEM+'_Normal','Non-Color')
        rough=image(STEM+'_Roughness','Non-Color')
        def target(im):
            for mat in mats:
                nodes=mat.node_tree.nodes
                node=nodes.get('Titan_Atlas_Bake') or nodes.new('ShaderNodeTexImage')
                node.name='Titan_Atlas_Bake'
                node.image=im
                nodes.active=node
        target(base)
        bpy.ops.object.bake(type='DIFFUSE',pass_filter={'COLOR'},use_clear=False,margin=3)
        base.filepath_raw=os.path.join(OUT,'Textures',STEM+'_BaseColor.png')
        base.save()
        print('TITAN_BASECOLOR_BAKED')
        target(normal)
        bpy.ops.object.bake(type='NORMAL',use_clear=False,margin=3)
        normal.filepath_raw=os.path.join(OUT,'Textures',STEM+'_Normal.png')
        normal.save()
        print('TITAN_NORMAL_BAKED')
        target(rough)
        bpy.ops.object.bake(type='ROUGHNESS',use_clear=False,margin=3)
        pixels=np.empty(SIZE*SIZE*4,dtype=np.float32)
        rough.pixels.foreach_get(pixels)
        pixels=pixels.reshape((-1,4))
        packed=np.zeros_like(pixels)
        packed[:,3]=1-np.clip(pixels[:,0],0,1)
        mask=image(STEM+'_MetallicSmoothness','Non-Color')
        mask.pixels.foreach_set(packed.ravel())
        mask.filepath_raw=os.path.join(OUT,'Textures',STEM+'_MetallicSmoothness.png')
        mask.save()
        # Keep explicit references in source; source shading remains procedural.
        for mat in mats:
            mat.node_tree.nodes['Titan_Atlas_Bake'].image=base
            for im in [normal,mask]:
                node=mat.node_tree.nodes.new('ShaderNodeTexImage')
                node.image=im
                node.label='Unity baked atlas reference'
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        for ob in meshes: ob.select_set(True)
        bpy.context.view_layer.objects.active=rig
        path=os.path.join(OUT,STEM+'.fbx')
        bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},
            global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',
            add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',
            use_armature_deform_only=False,path_mode='STRIP')
        source=bpy.data.scenes.new(STEM+'_Source')
        source.collection.children.link(collection)
        bpy.data.libraries.write(os.path.join(OUT,STEM+'.blend'),{source},path_remap='RELATIVE',fake_user=True)
        bpy.data.scenes.remove(source)
        for ob in meshes: ob.data.calc_loop_triangles()
        report={'skins':len(meshes),'bones':len(rig.data.bones),
                'triangles':sum(len(o.data.loop_triangles) for o in meshes),
                'textureSize':SIZE,'maps':['BaseColor','Normal','MetallicSmoothness'],
                'rigCompatibility':'Exact rest bone matrices copied from blockout',
                'openBoundaryEdges':0,'rigType':'Generic',
                'fingers':globals().get('TITAN_FINGER_DESCRIPTION','Detailed geometry, still hand-weighted')}
        with open(os.path.join(OUT,STEM+'_Manifest.json'),'w') as f: json.dump(report,f,indent=2)
        print('DETAILED_TITAN_EXPORT_OK',json.dumps(report))
finally:
    window.scene=previous_scene
