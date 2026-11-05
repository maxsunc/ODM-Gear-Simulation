"""Prepare approved GF2 mesh LODs + wood-only static collision in an OWNED staging
project; source art and interactive Blender are never modified. Run factory background.
GF2_PROJECT_ROOT=source project; GF2_GAME_PROJECT_ROOT=owned Unity worker.
"""
import bpy,bmesh,os,random,json,shutil
from pathlib import Path
if not bpy.app.background:raise RuntimeError('Owned background worker only')
SRC=Path(os.environ['GF2_PROJECT_ROOT']);PROJECT=Path(os.environ['GF2_GAME_PROJECT_ROOT'])
assert (PROJECT/'.giant-forest-owned').exists(),'Refusing non-owned output project'
SOURCE=SRC/'ArtSource/Environment/GiantForest2';OUT=PROJECT/'GiantForestArtStaging';MODELS=OUT/'Models'
MODELS.mkdir(parents=True,exist_ok=True);shutil.copytree(SOURCE/'Textures',OUT/'Textures',dirs_exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'GiantForest2_AssetReview.blend'))
report={};portable={}
def material(mat):
 if mat in portable:return portable[mat]
 clone=mat.copy();clone.name=mat.name+'_Game'
 if clone.use_nodes:
  bs=clone.node_tree.nodes.get('Principled BSDF');tex=next((n for n in clone.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_BaseColor' in n.image.name),None)
  if bs and tex:clone.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 portable[mat]=clone;return clone

def export(name,objects,ratio=1,leaf_keep=1,collision=False):
 scene=bpy.data.scenes.new('GF2_Export_'+name);bpy.context.window.scene=scene;copies=[];rng=random.Random(6219)
 # Evaluation happens in source scene because original meshes need an evaluated
 # dependency graph; no source pose/transform/material edits.
 source_scene=bpy.data.scenes['GF2_ModelReview'];bpy.context.window.scene=source_scene;deps=bpy.context.evaluated_depsgraph_get()
 for ob in objects:
  data=bpy.data.meshes.new_from_object(ob.evaluated_get(deps),depsgraph=deps);copy=bpy.data.objects.new(name+'_Part',data);scene.collection.objects.link(copy);copy.matrix_world=ob.matrix_world.copy()
  isleaf=any(m and 'Leaves' in m.name for m in data.materials)
  if isleaf and leaf_keep<1:
   bm=bmesh.new();bm.from_mesh(data)
   for face in list(bm.faces):
    if rng.random()>leaf_keep:bm.faces.remove(face)
    else:
     center=face.calc_center_median()
     for vertex in face.verts:vertex.co=center+(vertex.co-center)*(1.22 if leaf_keep>.2 else 1.75)
   for vertex in list(bm.verts):
    if not vertex.link_faces:bm.verts.remove(vertex)
   bm.to_mesh(data);bm.free()
  elif not isleaf and ratio<1:
   dec=copy.modifiers.new('Wood distance simplification','DECIMATE');dec.ratio=ratio
  copies.append(copy)
 bpy.context.window.scene=scene;bpy.context.view_layer.update()
 if ratio<1:
  evaluated=bpy.context.evaluated_depsgraph_get()
  for copy in copies:
   if copy.modifiers:
    old=copy.data;copy.data=bpy.data.meshes.new_from_object(copy.evaluated_get(evaluated),depsgraph=evaluated);copy.modifiers.clear()
 for copy in copies:copy.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 if len(copies)>1:bpy.ops.object.join()
 ob=bpy.context.object;ob.name=name
 if collision:ob.data.materials.clear()
 else:
  for i,m in enumerate(ob.data.materials):ob.data.materials[i]=material(m)
 ob.data.calc_loop_triangles();report[name]={'triangles':len(ob.data.loop_triangles),'materials':len(ob.data.materials),'leaf_fraction':leaf_keep,'wood_ratio':ratio}
 bpy.ops.export_scene.fbx(filepath=str(MODELS/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,path_mode='AUTO',embed_textures=False,mesh_smooth_type='FACE')
 bpy.context.window.scene=source_scene;bpy.data.scenes.remove(scene)
 print('GF2_GAME_EXPORT '+name,flush=True)

for name,col in [(c.name,c) for c in bpy.data.collections if c.name.startswith('GF2_Tree_')]:
 meshes=[o for o in col.objects if o.type=='MESH']
 shutil.copy2(SOURCE/'Exports'/(name+'.fbx'),MODELS/(name+'_LOD0.fbx'))
 export(name+'_LOD1',meshes,.30,.34);export(name+'_LOD2',meshes,.075,.105)
 wood=[o for o in meshes if o.name.startswith(('Fluted_Bole','ButtressRoot','CrownLimb','DeadLimbStub','LightningFork'))]
 export(name+'_Collision',wood,collision=True)
for f in (SOURCE/'Exports').glob('*.fbx'):
 if not f.name.startswith('GF2_Tree_'):shutil.copy2(f,MODELS/f.name)
(OUT/'game-model-report.json').write_text(json.dumps(report,indent=2));print('GF2_GAME_MODELS_COMPLETE '+str(OUT),flush=True)
