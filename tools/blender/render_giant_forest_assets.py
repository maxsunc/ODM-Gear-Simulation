"""Owned offline Blender model-review renders, no live scene/editor access.
GF2_PROJECT_ROOT required; optional GF2_SHOT selects one of the six named views.
"""
import bpy,os,math,json
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
if not bpy.app.background or not os.environ.get('GF2_PROJECT_ROOT'):raise RuntimeError('Owned background worker only')
ROOT=Path(os.environ['GF2_PROJECT_ROOT']).resolve();OUT=ROOT/'ArtSource/Environment/GiantForest2';PRE=OUT/'Review';PRE.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(OUT/'GiantForest2_AssetReview.blend'))
asset_info=json.loads((OUT/'asset-report.json').read_text())['assets']
assets={n:bpy.data.collections[n] for n in asset_info}
gpu=False
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='CUDA';gpu=gpu or device.use
except Exception:pass
print('GF2_RENDER_DEVICE '+('CUDA' if gpu else 'CPU'),flush=True)
# Delete/rebuild ONLY GF2-owned review scenes if re-running against this owned file.
for s in list(bpy.data.scenes):
    if s.name.startswith('GF2_View_'):bpy.data.scenes.remove(s)
world=bpy.data.worlds.new('GF2_StudioWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.20,.235,.25,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45

def solid(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1);m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=1;return m
floor_mat=solid('GF2_StudioFloor',(.16,.18,.155));guide_mat=solid('GF2_Guide60m',(.30,.33,.32));human_mat=solid('GF2_Guide3_6m',(.72,.38,.09));ruler_mat=solid('GF2_Ruler',(.68,.61,.40))
scenes={};annotations={}
def model(scene,name,location=(0,0,0),rotation=0):
    ob=bpy.data.objects.new('Display_'+name,None);ob.instance_type='COLLECTION';ob.instance_collection=assets[name];scene.collection.objects.link(ob);ob.location=location;ob.rotation_euler.z=rotation;return ob

def part(scene,name,location,scale,mat,sphere=False):
    # Operators run only in this dedicated scene/window.
    bpy.context.window.scene=scene
    if sphere:bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=location)
    else:bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    ob=bpy.context.object;ob.name=name;ob.scale=scale;ob.data.materials.append(mat)
    if sphere:
        for p in ob.data.polygons:p.use_smooth=True
    return ob

def person(scene,x,y,h,mat):
    # Clearly abstract reference mannequins, not replacement Scout/Titan artwork.
    part(scene,'ScaleGuide_Torso',(x,y,h*.65),(h*.22,h*.115,h*.31),mat)
    part(scene,'ScaleGuide_Head',(x,y,h*.928),(h*.062,h*.06,h*.072),mat,True)
    part(scene,'ScaleGuide_Neck',(x,y,h*.84),(h*.07,h*.07,h*.10),mat)
    part(scene,'ScaleGuide_Hip',(x,y,h*.445),(h*.15,h*.10,h*.12),mat)
    for side in (-1,1):
        part(scene,'ScaleGuide_Leg',(x+side*h*.052,y,h*.22),(h*.067,h*.078,h*.44),mat)
        arm=part(scene,'ScaleGuide_Arm',(x+side*h*.145,y,h*.60),(h*.052,h*.066,h*.31),mat);arm.rotation_euler.y=side*.10

def view(name,pos,target,scale,res=(1600,1100),perspective=False):
    s=bpy.data.scenes.new('GF2_View_'+name);s.world=world;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.engine='CYCLES'
    # Alpha-card canopies render reliably in Cycles on CPU without Eevee screen-space artifacts.
    s.cycles.device='GPU' if gpu else 'CPU';s.cycles.samples=24;s.cycles.use_denoising=True;s.cycles.max_bounces=6;s.cycles.transparent_max_bounces=24
    s.render.resolution_x,s.render.resolution_y=res;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.film_transparent=False
    s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=.35
    d=bpy.data.cameras.new(name+'_Camera');cam=bpy.data.objects.new(name+'_Camera',d);s.collection.objects.link(cam);s.camera=cam;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    if perspective:d.type='PERSP';d.lens=scale
    else:d.type='ORTHO';d.ortho_scale=scale
    d.clip_end=2500;d.clip_start=.1
    def sun(n,rot,energy,color,angle):
        d=bpy.data.lights.new(n,'SUN');d.energy=energy;d.color=color;d.angle=angle;o=bpy.data.objects.new(n,d);s.collection.objects.link(o);o.rotation_euler=rot
    sun(name+'_Sun',(.42,-.55,-.55),2.5,(1,.88,.71),.12)
    sun(name+'_SkyFill',(.4,.8,2.3),.65,(.69,.82,1),.5)
    # Broad camera-side softbox reveals bark/stone instead of presenting black
    # silhouettes. This is explicit studio lighting, not a proposed map grade.
    p=Vector(target)+(Vector(pos)-Vector(target))*.70;distance=(p-Vector(target)).length
    fill=bpy.data.lights.new(name+'_FrontSoftbox','AREA');fill.energy=distance*distance*4;fill.shape='DISK';fill.size=distance*.9;fill.color=(.93,.96,1)
    ob=bpy.data.objects.new(name+'_FrontSoftbox',fill);s.collection.objects.link(ob);ob.location=p;ob.rotation_euler=(Vector(target)-p).to_track_quat('-Z','Y').to_euler()
    part(s,'StudioFloor',(0,0,-1.55 if name in ('Ground_Props','Roots_Closeup') else -.15),(1600,1600,.25),floor_mat)
    scenes[name]=s;annotations[name]=[];return s

def label(name,point,text):annotations[name].append({'point':point,'text':text})
s=view('Trees_Lineup',(205,-350,150),(0,0,59),220,(1800,1200))
for x,name,tag in [(-60,'GF2_Tree_A_Sentinel','A / SENTINEL / 120 m'),(0,'GF2_Tree_B_Leaning','B / LEANING / 104 m'),(60,'GF2_Tree_C_OldFork','C / OLD FORK / 114 m')]:model(s,name,(x,0,0));label('Trees_Lineup',(x,-4,0),tag)
person(s,-98,-3,60,guide_mat);person(s,-92,-10,3.6,human_mat);label('Trees_Lineup',(-98,-3,63),'60 m TITAN scale guide');label('Trees_Lineup',(-92,-10,0),'3.6 m PLAYER')
s=view('Roots_Closeup',(24,-34,17),(0,0,6.5),45,(1500,1100),True);model(s,'GF2_Tree_A_Sentinel');model(s,'GF2_Ground_MossBank_32');person(s,-7,-4,3.6,human_mat);label('Roots_Closeup',(-7,-4,4.4),'3.6 m PLAYER scale')
s=view('Canopy_Closeup',(42,-53,109),(0,0,93),47,(1500,1100),True);model(s,'GF2_Tree_C_OldFork')
s=view('Wall_Modules',(150,-255,124),(6,0,29),163,(1800,1100))
for name,loc,rot,tag in [('GF2_Wall_Straight_24x60',(-58,0,0),0,'STRAIGHT / 24 m bay'),('GF2_Wall_Corner_24x60',(-17,0,0),0,'CORNER / 24 m grid'),('GF2_Wall_Gateway_48x60',(50,0,0),0,'GATE / 26 x 39 m opening')]:model(s,name,loc,rot);label('Wall_Modules',(loc[0],-8,0),tag)
person(s,8,-12,3.6,human_mat);label('Wall_Modules',(8,-12,4.3),'3.6 m PLAYER')
s=view('Stone_Closeup',(22,-36,21),(0,-3,17),48,(1500,1100),True);model(s,'GF2_Wall_Straight_24x60');person(s,-6,-8,3.6,human_mat);label('Stone_Closeup',(-6,-8,4.5),'3.6 m PLAYER scale')
s=view('Ground_Props',(86,-120,103),(0,-9,0),132,(1800,1100))
for x,name,tag in [(-36,'GF2_Ground_Loam_32','LOAM / 32 m tile'),(0,'GF2_Ground_MossBank_32','MOSS BANK / 32 m tile'),(36,'GF2_Ground_WornTrail_32','WORN SOIL / 32 m tile')]:model(s,name,(x,8,0));label('Ground_Props',(x,-8,0),tag)
model(s,'GF2_FallenTrunk_32',(-20,-27,-1.425-asset_info['GF2_FallenTrunk_32']['bounds_min'][2]),.08);label('Ground_Props',(-20,-34,0),'32 m SNAPPED TRUNK')
for i in range(3):
    name='GF2_Boulder_'+chr(65+i);model(s,name,(17+i*9,-26,-1.425-asset_info[name]['bounds_min'][2]),i*.8)
label('Ground_Props',(25,-33,0),'BOULDER VARIANTS')
before=set(s.objects);person(s,4,-29,3.6,human_mat)
for ob in set(s.objects)-before:ob.location.z-=1.425
# Pack review scenes so opening the blend offers six editable, reusable studio views.
for name,s in scenes.items():
    s.render.filepath=str(PRE/(name+'_Raw.png'))
    for a in annotations[name]:
        p=world_to_camera_view(s,s.camera,Vector(a['point']));a['pixel']=[p.x*s.render.resolution_x,(1-p.y)*s.render.resolution_y]
(PRE/'annotations.json').write_text(json.dumps(annotations,indent=2))
bpy.context.window.scene=scenes['Trees_Lineup']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GiantForest2_ReviewStudios.blend'),compress=True,check_existing=False)
selected=os.environ.get('GF2_SHOT')
for name,s in scenes.items():
    if selected and selected!=name:continue
    print('GF2_RENDER_START '+name,flush=True);bpy.context.window.scene=s;bpy.ops.render.render(write_still=True,scene=s.name);print('GF2_RENDER_DONE '+name,flush=True)
print('GF2_REVIEWS_COMPLETE',flush=True)
