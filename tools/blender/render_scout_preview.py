"""Owned studio render. No scene save or user camera/light modification."""
import bpy, math
from mathutils import Vector, Quaternion
from pathlib import Path
if 'SCOUT_PROJECT_ROOT' not in globals():raise RuntimeError('Run via tools/blender_request.py --file tools/blender/render_scout_preview.py')
ROOT=Path(SCOUT_PROJECT_ROOT)
col=bpy.data.collections['ScoutPlayer_v1'];rig=next(o for o in col.objects if o.type=='ARMATURE')
scene=bpy.context.scene;oldcamera=scene.camera;hidden={o:o.hide_render for o in scene.objects};oldengine=scene.render.engine;oldworld=scene.world
oldrender=(scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage,scene.render.filepath,scene.cycles.samples)
oldpose={p.name:p.rotation_quaternion.copy() for p in rig.pose.bones};owned=[]
def pose(name,axis,degrees):
 p=rig.pose.bones[name];p.rotation_mode='QUATERNION';q=p.bone.matrix_local.to_quaternion();p.rotation_quaternion=q.inverted()@Quaternion(axis,math.radians(degrees))@q
try:
 for o in scene.objects:o.hide_render=o.name not in col.objects
 for side,s in [('Left',1),('Right',-1)]:pose(side+'UpperArm',(0,1,0),s*72);pose(side+'LowerArm',(0,1,0),s*8)
 pose('Head',(0,0,1),-4)
 world=bpy.data.worlds.new('ScoutPreviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.14,.17,.21,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
 def area(name,pos,power,size):
  d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler();owned.append(o)
 area('Scout key',(2,-3,4),500,4);area('Scout fill',(-3,-2,2.8),230,3);area('Scout rim',(1,3,3),650,3)
 d=bpy.data.cameras.new('ScoutReviewCamera');camera=bpy.data.objects.new('ScoutReviewCamera',d);scene.collection.objects.link(camera);owned.append(camera);scene.camera=camera;d.type='ORTHO';d.ortho_scale=2.15
 scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
 out=ROOT/'ArtSource/Player/Scout';out.mkdir(parents=True,exist_ok=True)
 for name,pos in [('Front',(2.3,-6,2.8)),('Back',(-2.3,6,2.6))]:
  camera.location=pos;camera.rotation_euler=(Vector((0,0,.91))-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(out/('Scout_'+name+'.png'));bpy.ops.render.render(write_still=True)
finally:
 for n,q in oldpose.items():rig.pose.bones[n].rotation_quaternion=q
 for o,h in hidden.items():o.hide_render=h
 scene.camera=oldcamera;scene.world=oldworld;scene.render.engine=oldengine
 scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage,scene.render.filepath,scene.cycles.samples=oldrender
 for o in owned:
  data=o.data;bpy.data.objects.remove(o,do_unlink=True)
  if isinstance(data,bpy.types.Camera):bpy.data.cameras.remove(data)
  elif isinstance(data,bpy.types.Light):bpy.data.lights.remove(data)
 bpy.data.worlds.remove(world)
print('Scout front/back previews rendered; temporary studio removed')
