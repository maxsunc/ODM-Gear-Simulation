"""Scout player v1. Run inside Blender via tools/blender_request.py.
Creates ONLY ScoutPlayer_v1 collection; source outside Assets. T-pose Generic rig,
weighted deformation rings, separate bone-parented swords, one portable PBR atlas.
"""
import bpy, math, random, json, struct, zlib
from pathlib import Path
from mathutils import Vector, Matrix
if 'SCOUT_PROJECT_ROOT' not in globals():raise RuntimeError('Run via tools/blender_request.py --file tools/blender/build_scout_player.py')
ROOT=Path(SCOUT_PROJECT_ROOT)
OUT=ROOT/'Assets/Art/Player/Scout'; SOURCE=ROOT/'ArtSource/Player/Scout'
OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)
old=bpy.data.collections.get('ScoutPlayer_v1')
if old:
    for ob in list(old.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.collections.remove(old)
col=bpy.data.collections.new('ScoutPlayer_v1');bpy.context.scene.collection.children.link(col)
# RGBA atlas has deliberately different cloth/leather/metal response.
PAL=[('Skin',(170,117,89),0,.34),('Jacket',(104,66,39),0,.25),('Cream',(188,185,160),0,.24),
 ('Leather',(48,31,23),0,.37),('Cloak',(48,75,44),0,.19),('Hair',(31,25,23),0,.22),
 ('Steel',(146,155,161),1,.72),('DarkMetal',(50,56,60),.82,.44),('EyeWhite',(211,207,188),0,.45),
 ('EyeDark',(28,43,37),0,.58),('Brass',(151,120,65),.8,.5),('Stitch',(147,116,78),0,.23),
 ('Insignia',(191,202,192),0,.26),('InnerSkin',(128,69,58),0,.3),('Shirt',(144,140,121),0,.22),('SteelEdge',(200,209,215),1,.85)]
PI={p[0]:i for i,p in enumerate(PAL)}
def png(path,w,h,channels,pixels):
    def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    stride=w*channels;raw=b''.join(b'\0'+pixels[y*stride:(y+1)*stride] for y in range(h))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6 if channels==4 else 2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,7))+chunk(b'IEND',b''))
N=1024;C=N//4;base=bytearray(N*N*3);metal=bytearray(N*N*4);normal=bytearray(N*N*3);rng=random.Random(7361)
for y in range(N):
 for x in range(N):
    # PNG top-down and Blender UV bottom-up.
    slot=(3-y//C)*4+x//C;name,color,m,s=PAL[slot];i=y*N+x
    weave=(1.7 if (x%4==0 or y%4==0) else -.4) if name in ('Jacket','Cream','Cloak','Shirt') else 0
    noise=rng.uniform(-3,3)+weave
    for j,c in enumerate(color):base[i*3+j]=max(0,min(255,round(c+noise)))
    metal[i*4:i*4+4]=bytes((round(m*255),0,0,max(0,min(255,round(s*255+noise*1.5)))))
    amp=2 if name in ('Jacket','Cream','Cloak','Leather') else 1
    normal[i*3:i*3+3]=bytes((128+(amp if x%4==0 else -amp if x%4==1 else 0),128+(amp if y%4==0 else -amp if y%4==1 else 0),255))
tex=OUT/'Textures';tex.mkdir(exist_ok=True)
png(tex/'Scout_BaseColor.png',N,N,3,base);png(tex/'Scout_MetallicSmoothness.png',N,N,4,metal);png(tex/'Scout_Normal.png',N,N,3,normal)
mat=bpy.data.materials.get('Scout_Atlas_PBR') or bpy.data.materials.new('Scout_Atlas_PBR');mat.use_nodes=True
nodes=mat.node_tree.nodes;nodes.clear();bs=nodes.new('ShaderNodeBsdfPrincipled');out=nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
def img_node(file,noncolor=False):
 im=bpy.data.images.load(str(tex/file),check_existing=True);im.reload()
 if noncolor:im.colorspace_settings.name='Non-Color'
 im.pack()
 n=nodes.new('ShaderNodeTexImage');n.image=im;return n
b=img_node('Scout_BaseColor.png');m=img_node('Scout_MetallicSmoothness.png',True);n=img_node('Scout_Normal.png',True)
sep=nodes.new('ShaderNodeSeparateColor');inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
norm=nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=.4
if 'Specular IOR Level' in bs.inputs:bs.inputs['Specular IOR Level'].default_value=.27
L=mat.node_tree.links;L.new(b.outputs['Color'],bs.inputs['Base Color']);L.new(m.outputs['Color'],sep.inputs['Color']);L.new(sep.outputs['Red'],bs.inputs['Metallic']);L.new(m.outputs['Alpha'],inv.inputs[1]);L.new(inv.outputs[0],bs.inputs['Roughness']);L.new(n.outputs['Color'],norm.inputs['Color']);L.new(norm.outputs['Normal'],bs.inputs['Normal'])
bodyparts=[];bones={}
# World coordinates in metres, front=-Y, own left=+X. Animation rest is T-pose.
arm=bpy.data.armatures.new('ScoutSkeleton');rig=bpy.data.objects.new('ScoutRig',arm);col.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,a,b,parent=None):
 ob=arm.edit_bones.new(name);ob.head=a;ob.tail=b
 if parent:ob.parent=arm.edit_bones[parent]
 bones[name]=(Vector(a),Vector(b));return ob
bone('Hips',(0,0,.94),(0,0,1.07));bone('Spine',(0,0,1.07),(0,0,1.23),'Hips');bone('Chest',(0,0,1.23),(0,0,1.45),'Spine');bone('Neck',(0,0,1.45),(0,0,1.57),'Chest');bone('Head',(0,0,1.57),(0,0,1.79),'Neck')
for side,s in [('Left',1),('Right',-1)]:
 bone(side+'Shoulder',(s*.045,0,1.43),(s*.215,0,1.44),'Chest');bone(side+'UpperArm',(s*.215,0,1.44),(s*.50,0,1.44),side+'Shoulder');bone(side+'LowerArm',(s*.50,0,1.44),(s*.76,0,1.44),side+'UpperArm');bone(side+'Hand',(s*.76,0,1.44),(s*.87,0,1.44),side+'LowerArm')
 for j,label in enumerate(['Index','Middle','Ring','Little']):
  yy=-.033+j*.021;bone(side+label+'1',(s*.822,yy,1.43),(s*.854,yy,1.408),side+'Hand');bone(side+label+'2',(s*.854,yy,1.408),(s*.825,yy,1.399),side+label+'1')
 bone(side+'Thumb1',(s*.785,-.048,1.441),(s*.817,-.064,1.415),side+'Hand');bone(side+'Thumb2',(s*.817,-.064,1.415),(s*.837,-.044,1.405),side+'Thumb1')
 bone(side+'UpperLeg',(s*.105,0,.94),(s*.11,-.018,.52),'Hips');bone(side+'LowerLeg',(s*.11,-.018,.52),(s*.105,0,.13),side+'UpperLeg');bone(side+'Foot',(s*.105,0,.13),(s*.105,-.155,.055),side+'LowerLeg');bone(side+'Toes',(s*.105,-.155,.055),(s*.105,-.24,.055),side+'Foot')
bone('CapeUpper',(0,.11,1.43),(0,.17,1.15),'Chest');bone('CapeLower',(0,.17,1.15),(0,.22,.87),'CapeUpper')
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig['version']='Scout v1 / portable atlas / dedicated hand sword meshes'
def weights_body(v):
 z=v.z
 if z<1.05:return {'Hips':1}
 if z<1.23:
  f=(z-1.05)/.18;return {'Hips':1-f,'Spine':f}
 f=min(1,(z-1.23)/.15);return {'Spine':1-f,'Chest':f}
def blend_axis(v,axis,pivot,width,b1,b2):
 t=max(0,min(1,(v[axis]-pivot+width)/(2*width)));return {b1:1-t,b2:t}
def mesh(name,verts,faces,material,weights=None,body=True):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();ob=bpy.data.objects.new(name,data);col.objects.link(ob);data.materials.append(mat)
 slot=PI[material];uv=data.uv_layers.new(name='UVMap');mn=[min(v.co[k] for v in data.vertices) for k in range(3)];mx=[max(v.co[k] for v in data.vertices) for k in range(3)]
 for poly in data.polygons:
  axis=max(range(3),key=lambda a:abs(poly.normal[a]));axes=[a for a in range(3) if a!=axis];poly.use_smooth=True
  for li in poly.loop_indices:
   co=data.vertices[data.loops[li].vertex_index].co
   u=.07+.86*(co[axes[0]]-mn[axes[0]])/max(.00001,mx[axes[0]]-mn[axes[0]]);v=.07+.86*(co[axes[1]]-mn[axes[1]])/max(.00001,mx[axes[1]]-mn[axes[1]])
   uv.data[li].uv=((slot%4+u)/4,(slot//4+v)/4)
 if weights:
  groups={}
  for v in data.vertices:
   ws={weights:1} if isinstance(weights,str) else weights(v.co)
   for bn,w in ws.items():
    if w<=.00001:continue
    if bn not in groups:groups[bn]=ob.vertex_groups.new(name=bn)
    groups[bn].add([v.index],w,'REPLACE')
 if body:bodyparts.append(ob)
 return ob

def loft(name,centres,radii,material,weights,sides=12):
 centres=[Vector(c) for c in centres];axis=(centres[-1]-centres[0]).normalized();u=Vector((1,0,0)) if abs(axis.z)>.85 else Vector((0,1,0));u=(u-axis*u.dot(axis)).normalized();v=axis.cross(u).normalized();verts=[]
 for c,(r1,r2) in zip(centres,radii):
  verts.extend([tuple(c+u*math.cos(j*2*math.pi/sides)*r1+v*math.sin(j*2*math.pi/sides)*r2) for j in range(sides)])
 faces=[tuple(range(sides-1,-1,-1))]
 for i in range(len(centres)-1):
  for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
 faces.append(tuple((len(centres)-1)*sides+j for j in range(sides)))
 return mesh(name,verts,faces,material,weights)

def ell(name,center,radius,material,weights,seg=16,rings=10):
 c=Vector(center);verts=[]
 for i in range(rings+1):
  a=.001+(math.pi-.002)*i/rings
  for j in range(seg):
   b=j*math.tau/seg;verts.append(tuple(c+Vector((radius[0]*math.sin(a)*math.cos(b),radius[1]*math.sin(a)*math.sin(b),radius[2]*math.cos(a)))))
 faces=[]
 for i in range(rings):
  for j in range(seg):a=i*seg+j;b=i*seg+(j+1)%seg;faces.append((a,b,b+seg,a+seg))
 faces.extend([tuple(range(seg-1,-1,-1)),tuple(rings*seg+j for j in range(seg))]);return mesh(name,verts,faces,material,weights)

def box(name,center,size,material,weights,bevel=.007,body=True):
 c=Vector(center);sx,sy,sz=(s/2 for s in size);verts=[tuple(c+Vector((x*sx,y*sy,z*sz))) for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 ob=mesh(name,verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],material,weights,body)
 if bevel:
  # Applied bevel carries UV/weights; gives actual readable metal/boot/jacket edges.
  bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Edge highlights','BEVEL');mod.width=bevel;mod.segments=2
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return ob

def strap(name,a,b,width,material,weights):
 a=Vector(a);b=Vector(b);d=b-a;u=Vector((d.z,0,-d.x)).normalized()*width/2
 return mesh(name,[tuple(a-u),tuple(a+u),tuple(b+u),tuple(b-u)],[(0,1,2,3)],material,weights)

# Continuous ring-based body clothing. Joint weights overlap across elbows/knees.
loft('Jacket tailored torso',[(0,0,z) for z in [1.0,1.04,1.12,1.23,1.34,1.40,1.44,1.47]],[(.14,.105),(.15,.11),(.137,.105),(.155,.12),(.195,.125),(.215,.105),(.19,.09),(.075,.065)],'Jacket',weights_body,20)
loft('Pelvis trousers',[(0,0,z) for z in [.86,.91,1.015]],[(.13,.095),(.16,.105),(.137,.10)],'Cream','Hips',16)
mesh('Shirt front',[(-.103,-.145,1.04),(.103,-.145,1.04),(.13,-.151,1.33),(.075,-.122,1.445),(-.075,-.122,1.445),(-.13,-.151,1.33)],[(0,1,2,3,4,5)],'Shirt',weights_body)
for s in [-1,1]:
 mesh('Jacket lapel',[(s*.065,-.137,1.46),(s*.16,-.158,1.405),(s*.10,-.168,1.29),(s*.036,-.157,1.395)],[(0,1,2,3)],'Jacket',weights_body)
 strap('Front diagonal harness',(s*.16,-.174,1.40),(-s*.085,-.166,1.06),.025,'Leather',weights_body)
 box('Harness buckle',(s*.075,-.178,1.12),(.045,.012,.035),'Brass',weights_body,.003)
loft('Neck skin',[(0,0,1.43),(0,0,1.59)],[(.06,.058),(.055,.055)],'Skin','Neck',14)
# Custom jaw/chin profile instead of a featureless sphere.
loft('Head shaped jaw',[(0,y,z) for y,z in [(-.016,1.545),(-.012,1.565),(0,1.61),(.005,1.67),(.006,1.73),(.012,1.78),(.016,1.805)]],[(.045,.048),(.068,.07),(.09,.09),(.109,.103),(.108,.099),(.09,.083),(.035,.035)],'Skin','Head',20)
ell('Nose bridge',(0,-.103,1.677),(.021,.026,.040),'Skin','Head',10,6);ell('Nose tip',(0,-.123,1.650),(.026,.025,.017),'Skin','Head',10,6)
strap('Mouth line',(-.025,-.089,1.604),(.025,-.089,1.604),.004,'InnerSkin','Head')
for s in [-1,1]:
 ell('Ear',(s*.107,.006,1.665),(.022,.026,.044),'Skin','Head',12,7);ell('Ear inner',(s*.12,-.008,1.664),(.008,.019,.025),'InnerSkin','Head',10,6)
 ell('Eye socket',(s*.046,-.097,1.688),(.026,.006,.012),'InnerSkin','Head',12,6)
 ell('Eye white',(s*.046,-.102,1.688),(.020,.003,.007),'EyeWhite','Head',12,6);ell('Iris',(s*.046,-.105,1.688),(.006,.002,.007),'EyeDark','Head',12,6)
 loft('Upper eyelid',[(s*(.025+i*.01),-.103,1.694+.003*math.sin(i*math.pi/4)) for i in range(5)],[(.004,.004)]*5,'Skin','Head',8)
 strap('Eyebrow',(s*.022,-.109,1.718),(s*.072,-.097,1.720),.010,'Hair','Head')
# Hair cap + shaped fringe. Separate faceted locks, not a helmet over the face.
loft('Hair cap',[(0,y,z) for y,z in [(.025,1.665),(.018,1.73),(.013,1.79),(.017,1.83)]],[(.108,.083),(.116,.104),(.099,.09),(.024,.025)],'Hair','Head',18)
# Expose eyes/forehead by pushing the lowest front vertices back.
hair=bodyparts[-1]
for v in hair.data.vertices:
 if v.co.y<0 and v.co.z<1.72:v.co.z=1.725
for i in range(7):
 x=-.085+i*.028;z=1.75+(.01 if i%2 else 0)
 mesh('Hair fringe',[ (x-.022,-.091,z+.041),(x+.023,-.091,z+.034),(x+.012,-.112,z-.028),(x-.007,-.11,z-.045)],[(0,1,2,3)],'Hair','Head')
for side,s in [('Left',1),('Right',-1)]:
 def aw(v,side=side):
  x=abs(v.x)
  if x<.28:
   t=.6+.4*max(0,min(1,(x-.19)/.09));return {'Chest':1-t,side+'UpperArm':t}
  t=max(0,min(1,(x-.455)/.09));return {side+'UpperArm':1-t,side+'LowerArm':t}
 ell(side+' deltoid',(s*.215,0,1.425),(.082,.087,.070),'Jacket',lambda v,side=side:{'Chest':.4,side+'UpperArm':.6},16,9)
 loft(side+' sleeve',[(s*x,0,1.44) for x in [.19,.24,.34,.44,.49,.51,.56,.66,.75]],[(.055,.062),(.076,.072),(.072,.069),(.063,.062),(.064,.060),(.064,.059),(.06,.055),(.052,.048),(.043,.040)],'Jacket',aw,14)
 loft(side+' cuff',[(s*x,0,1.44) for x in [.718,.75,.766]],[(.050,.044),(.049,.043),(.045,.039)],'Leather',side+'LowerArm',12)
 ell(side+' palm',(s*.806,0,1.438),(.055,.045,.024),'Skin',side+'Hand',12,6)
 for j,label in enumerate(['Index','Middle','Ring','Little']):
  yy=-.033+j*.021
  loft(side+label,[(s*.822,yy,1.43),(s*.854,yy,1.408),(s*.84,yy,1.394),(s*.821,yy,1.403)],[(.010,.010),(.010,.010),(.009,.009),(.007,.007)],'Skin',side+label+'1',8)
 loft(side+' thumb',[(s*.785,-.048,1.441),(s*.817,-.064,1.415),(s*.837,-.044,1.405)],[(.014,.013),(.012,.012),(.009,.009)],'Skin',side+'Thumb1',8)
 def lw(v,side=side):return blend_axis(v,2,.52,.075,side+'LowerLeg',side+'UpperLeg')
 loft(side+' trousers',[(s*x,y,z) for x,y,z in [(.105,0,.95),(.11,0,.85),(.115,-.003,.73),(.11,-.014,.59),(.11,-.018,.52),(.108,-.015,.45),(.106,0,.34),(.105,0,.27)]],[(.092,.09),(.093,.09),(.079,.078),(.067,.066),(.065,.060),(.062,.059),(.057,.055),(.052,.051)],'Cream',lw,14)
 loft(side+' boot shaft',[(s*.105,0,z) for z in [.12,.18,.30,.395]],[(.055,.06),(.058,.062),(.062,.064),(.066,.065)],'Leather',side+'LowerLeg',12)
 box(side+' boot sole',(s*.105,-.067,.034),(.125,.255,.045),'Hair',side+'Foot',.018)
 ell(side+' boot foot',(s*.105,-.064,.082),(.065,.129,.055),'Leather',side+'Foot',16,8)
 for zz in [.41,.67,.85]:
  rr=.068 if zz<.5 else .087
  loft(side+' leg harness',[(s*.11,0,zz-.015),(s*.11,0,zz+.015)],[(rr,rr+.003),(rr,rr+.003)],'Leather',lw,14)
  box(side+' leg buckle',(s*.11,-rr-.006,zz),(.045,.014,.035),'Brass',lw,.004)
 strap(side+' thigh harness',(s*.16,-.102,.95),(s*.08,-.082,.66),.022,'Leather',lw)
 # Mechanically readable gear: canister, protective casing, side blade magazines.
 box(side+' gear housing',(s*.245,.035,.91),(.13,.18,.25),'DarkMetal','Hips',.014)
 loft(side+' gas canister',[(s*.245,.153,z) for z in [.82,.84,1.03,1.05]],[(.023,.023),(.047,.047),(.047,.047),(.025,.025)],'Steel','Hips',14)
 for zz in [.875,.99]:box(side+' canister band',(s*.245,.15,zz),(.098,.085,.018),'Leather','Hips',.006)
 for i in range(4):box(side+' spare blade',(s*(.208+i*.024),-.015,.70),(.015,.085,.36),'Steel','Hips',.003)
 loft(side+' launcher',[(s*.26,y,.99) for y in [-.065,-.16,-.18]],[(.029,.029),(.027,.027),(.019,.019)],'Steel','Hips',12)
 box(side+' launcher bore',(s*.26,-.183,.99),(.025,.005,.025),'DarkMetal','Hips',.002)
# Waist belts and back control unit.
loft('Waist belt',[(0,0,z) for z in [.99,1.03]],[(.154,.118),(.154,.118)],'Leather','Hips',20)
box('Belt buckle',(0,-.126,1.01),(.060,.015,.043),'Brass','Hips',.005)
box('ODM back unit',(0,.16,1.04),(.19,.085,.12),'DarkMetal','Hips',.015)
# Short skinned cape, shoulders free; removable through separate mesh when installed.
cape=[]
for z,w,y in [(1.47,.12,.10),(1.37,.23,.14),(1.18,.25,.19),(.98,.26,.24),(.85,.235,.27)]:
 for j in range(9):x=(j/8*2-1)*w;cape.append((x,y+.018*math.cos(j*math.pi),z+(.016*math.cos(j*math.pi) if z<.9 else 0)))
faces=[]
for row in range(4):
 for j in range(8):a=row*9+j;faces.append((a,a+1,a+10,a+9))
def cw(v):return blend_axis(v,2,1.18,.09,'CapeLower','CapeUpper')
capeob=mesh('ScoutCape',cape,faces,'Cloak',cw);bodyparts.remove(capeob)
# Wings silhouette belongs to removable cape, not the body renderer.
capeParts=[capeob]
for s in [-1,1]:
 for j in range(5):
  x=s*(.012+j*.023);z=1.32-j*.017;y=.184
  wing=mesh('Wing insignia',[(x,y,z),(x+s*.019,y,z+.042),(x+s*.043,y,z+.088),(x+s*.028,y,z+.015)],[(0,3,2,1)],'Insignia','CapeUpper');bodyparts.remove(wing);capeParts.append(wing)
# Join body for a single skinned atlas draw. Cape remains separately removable.
def join(obs,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name;return ob
capeob=join(capeParts,'ScoutCape')
body=join(bodyparts,'ScoutBody')
for ob in [body,capeob]:
 ob.parent=rig;mod=ob.modifiers.new('Scout skin','ARMATURE');mod.object=rig
# Physical swords are rigid meshes with origins at the actual grip, not distant world origin.
weapons=[]
for side,s,suffix in [('Left',1,'L'),('Right',-1,'R')]:
 before=len(bodyparts);grip=Vector((s*.833,-.015,1.419));parts=[]
 # grip + trigger/guard/hardware intentionally separate from cutting mesh.
 parts.append(box('Grip',tuple(grip),(.027,.115,.033),'Leather',None,.006,False))
 parts.append(box('Pommel',tuple(grip+Vector((0,.061,0))),(.043,.018,.045),'Steel',None,.005,False))
 parts.append(box('Guard',tuple(grip+Vector((s*.057,-.015,0))),(.018,.112,.057),'Steel',None,.004,False))
 parts.append(box('Blade socket',tuple(grip+Vector((s*.062,-.015,0))),(.07,.048,.036),'DarkMetal',None,.004,False))
 parts.append(box('Trigger housing',tuple(grip+Vector((s*.029,.006,-.025))),(.028,.063,.021),'DarkMetal',None,.005,False))
 parts.append(box('Trigger',tuple(grip+Vector((s*.027,-.025,-.024))),(.012,.015,.022),'Brass',None,.003,False))
 hilt=join(parts,'ScoutHilt_'+suffix)
 # Real bevel diamond section, tapered tip; local blade length .68m.
 vv=[]
 for distance,w in [(.09,.022),(.12,.022),(.66,.019),(.77,.002)]:
  vv.extend([tuple(grip+Vector((s*distance,y-.015,z))) for y,z in [(-w,0),(0,.004),(w,0),(0,-.004)]])
 ff=[(3,2,1,0)]
 for r in range(3):
  for j in range(4):a=r*4+j;b=r*4+(j+1)%4;ff.append((a,b,b+4,a+4))
 ff.append((12,13,14,15));blade=mesh('ScoutBlade_'+suffix,vv,ff,'Steel',None,False)
 # Metallic blade edge is actual geometry, atlas palette remains shared.
 for poly in blade.data.polygons:poly.use_smooth=False
 for ob in [hilt,blade]:
  for v in ob.data.vertices:v.co-=grip
  ob.matrix_world=Matrix.Translation(grip);world=ob.matrix_world.copy();ob.parent=rig;ob.parent_type='BONE';ob.parent_bone=side+'Hand';ob.matrix_world=world;weapons.append(ob)
# Rope emitters follow the actual animated hip-mounted launchers, not hidden old gear.
for side,s,suffix in [('Left',1,'L'),('Right',-1,'R')]:
 ob=bpy.data.objects.new('ScoutLaunch_'+suffix,None);col.objects.link(ob);ob.matrix_world=Matrix.Translation((s*.26,-.185,.99));world=ob.matrix_world.copy();ob.parent=rig;ob.parent_type='BONE';ob.parent_bone='Hips';ob.matrix_world=world
# Named orientation markers survive export and remove guesses about FBX axes.
for name,pos in [('ScoutForward',(0,-1,0)),('ScoutUp',(0,0,1))]:
 ob=bpy.data.objects.new(name,None);col.objects.link(ob);ob.parent=rig;ob.location=pos
bpy.context.view_layer.update()
# Explicit triangulation gives Unity the same concave cloth/cap faces as Blender.
for ob in col.objects:
 if ob.type=='MESH':
  bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Portable triangles','TRIANGULATE');mod.quad_method='SHORTEST_DIAGONAL';mod.ngon_method='BEAUTY'
  while ob.modifiers.find(mod.name)>0:bpy.ops.object.modifier_move_up(modifier=mod.name)
  bpy.ops.object.modifier_apply(modifier=mod.name)
# Export only owned content; no user object deletion, no application scene save/replace.
bpy.ops.object.select_all(action='DESELECT')
for ob in col.objects:ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'ScoutPlayer.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='AUTO')
bpy.data.libraries.write(str(SOURCE/'ScoutPlayer.blend'),set(col.objects)|{col},fake_user=True)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in col.objects if o.type=='MESH')
report={'version':1,'heightMetres':1.83,'triangles':triangles,'bones':len(arm.bones),'objects':[o.name for o in col.objects],'atlas':1024,'materials':1,'front':'-Y','unityVisualScale':2,'fbx':str(OUT/'ScoutPlayer.fbx')}
(SOURCE/'build-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
