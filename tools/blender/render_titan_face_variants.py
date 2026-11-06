"""Owned Blender MCP face lineup, original/broad/gaunt. Does not save preview edits."""
import bpy,math
from pathlib import Path
from mathutils import Vector
folder=Path(bpy.data.filepath).parent;worker=folder.parent
assert (worker/'.titan-face-owned').is_file()
for ob in bpy.data.objects:ob.hide_render=True
for offset,key in [(-2.7,'Reference'),(0,'Broad'),(2.7,'Gaunt')]:
 rig=bpy.data.objects['TitanRig_'+key];rig.location.x=offset
 for part in ['Head','Jaw']:
  ob=bpy.data.objects[('TitanReference_' if key=='Reference' else 'Titan'+key+'_')+part];ob.hide_render=False
  if ob.parent is None:ob.location.x=offset
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=2100;scene.render.resolution_y=760;scene.render.resolution_percentage=100
if scene.world is None:scene.world=bpy.data.worlds.new('OwnedFaceWorld')
scene.world.color=(.055,.065,.08)
def point(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,position,energy,size in [('Key',(-4,-6,14),1700,6),('Fill',(5,-3,11),900,5),('Rim',(0,3,13),1600,5)]:
 data=bpy.data.lights.new('OwnedFace'+name,'AREA');data.energy=energy;data.shape='DISK';data.size=size;ob=bpy.data.objects.new('OwnedFace'+name,data);scene.collection.objects.link(ob);ob.location=position;point(ob,(0,0,10.5))
data=bpy.data.cameras.new('OwnedFaceCamera');camera=bpy.data.objects.new('OwnedFaceCamera',data);scene.collection.objects.link(camera);camera.location=(0,-14,10.65);point(camera,(0,0,10.65));data.type='ORTHO';data.ortho_scale=9;scene.camera=camera
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/'Faces_Blender_Lineup.png');bpy.ops.render.render(write_still=True)
print('OWNED_FACE_LINEUP',scene.render.filepath)
