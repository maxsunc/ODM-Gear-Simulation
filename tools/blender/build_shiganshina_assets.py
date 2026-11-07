"""Original Shiganshina-inspired architecture. Owned factory Blender only; no live addon.
SG_PROJECT_ROOT=source; SG_WORKER=marker-owned Unity project. Writes only new SG art.
"""
import os, math, json, random, struct, zlib
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

ROOT=Path(os.environ['SG_PROJECT_ROOT']); WORK=Path(os.environ['SG_WORKER'])
assert bpy.app.background and not bpy.data.filepath
assert (WORK/'.shiganshina-owned').read_text()==str(ROOT)
OUT=ROOT/'ArtSource/Environment/Shiganshina'; GAME=WORK/'Assets/Art/Environment/Shiganshina'
for p in [OUT,GAME/'Models',GAME/'Textures']:p.mkdir(parents=True,exist_ok=True)
if (OUT/'Shiganshina_Architecture.blend').exists():
 assert os.environ.get('SG_REVISE_ART_OWNED')=='1','Explicit revision required'
 import shutil
 backup=ROOT/'Library/ShiganshinaDraftSources';backup.mkdir(exist_ok=True)
 shutil.copy2(OUT/'Shiganshina_Architecture.blend',backup/'Architecture_BeforeGateContact.blend')
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
random.seed(73481)

def png(path,a):
 a=np.uint8(np.clip(a,0,1)*255); h,w,c=a.shape
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(b'\0'+row.tobytes() for row in a[::-1]),6))+chunk(b'IEND',b''))
N=1024;u,v=np.meshgrid(np.arange(N)/N,np.arange(N)/N);rng=np.random.default_rng(714)
fine=rng.random((N,N));cloud=(np.sin(u*math.tau*3+np.sin(v*math.tau*2))*.5+.5)*.5+fine*.5
materials={}; material_index={}; tex_scales={}
def material(name,color,kind='plain',scale=4):
 h=cloud*.12; col=np.ones((N,N,3))*np.array(color)
 if kind in ['stone','brick','paver']:
  rows=8 if kind=='stone' else 16; cols=4 if kind=='stone' else 8
  yy=v*rows;xx=u*cols+(np.floor(yy)%2)*.5
  fx=xx%1;fy=yy%1;mortar=(fx<.035)|(fx>.965)|(fy<.06)|(fy>.94)
  cells=(np.sin(np.floor(xx)*23+np.floor(yy)*17)*.5+.5)*.18+.82
  col*=cells[:,:,None]*(.87+cloud[:,:,None]*.23);col[mortar]=np.array(color)*.53
  h=np.where(mortar,.12,.7)+fine*.06
 elif kind=='tile':
  yy=v*12;xx=u*10+(np.floor(yy)%2)*.5;fx=xx%1;fy=yy%1
  ridge=np.sin(fx*math.pi)**.5;seam=(fy<.07)|(fx<.018)
  col*=((.68+.32*ridge)*(.88+.12*fy))[:,:,None];col[seam]*=.50
  col*= (.88+cloud*.23)[:,:,None];h=np.where(seam,.1,.4+ridge*.45+fy*.1)
 elif kind=='wood':
  grain=np.sin(u*math.tau*53+np.sin(v*math.tau*2)*2)*.5+.5
  col*= (.66+cloud*.20+grain*.19)[:,:,None];h=grain*.4+fine*.10
 elif kind=='plaster':
  col*= (.83+cloud*.2+fine*.05)[:,:,None];h=cloud*.3+fine*.1
 else:col*= (.91+cloud*.13)[:,:,None]
 png(GAME/'Textures'/(name+'_BaseColor.png'),col)
 dy,dx=np.gradient(h);normal=np.stack((-dx*7,-dy*7,np.ones_like(h)),2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
 png(GAME/'Textures'/(name+'_Normal.png'),normal*.5+.5)
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.88
 for suffix,socket in [('_BaseColor.png','Base Color'),('_Normal.png',None)]:
  image=bpy.data.images.load(str(GAME/'Textures'/(name+suffix)));image.pack();node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
  if socket:m.node_tree.links.new(node.outputs['Color'],bs.inputs[socket])
  else:
   image.colorspace_settings.name='Non-Color';normalnode=m.node_tree.nodes.new('ShaderNodeNormalMap');normalnode.inputs['Strength'].default_value=.4;m.node_tree.links.new(node.outputs['Color'],normalnode.inputs['Color']);m.node_tree.links.new(normalnode.outputs['Normal'],bs.inputs['Normal'])
 material_index[name]=len(materials);materials[name]=m;tex_scales[name]=scale
material('SG_Plaster',(.83,.74,.57),'plaster',7)
material('SG_Timber',(.25,.15,.085),'wood',4)
material('SG_Roof',(.52,.21,.13),'tile',5)
material('SG_Stone',(.52,.50,.44),'stone',12)
material('SG_Brick',(.48,.25,.17),'brick',5)
material('SG_Window',(.075,.105,.10),'plain',2)
material('SG_Iron',(.16,.18,.17),'plain',3)
material('SG_Cloth',(.18,.32,.30),'plaster',4)
material('SG_Paver',(.43,.42,.37),'paver',10)

class Batch:
 def __init__(self):self.v=[];self.f=[];self.mi=[];self.cv=[];self.cf=[]
 def poly(self,verts,faces,mat,collision=False):
  off=len(self.v);self.v.extend(verts);self.f.extend([tuple(i+off for i in f) for f in faces]);self.mi.extend([material_index[mat]]*len(faces))
  if collision:
   off=len(self.cv);self.cv.extend(verts);self.cf.extend([tuple(i+off for i in f) for f in faces])
 def box(self,c,s,mat='SG_Stone',collision=False):
  x,y,z=c;a,b,h=[q/2 for q in s]
  vs=[(x+X,y+Y,z+Z) for X,Y,Z in [(-a,-b,-h),(a,-b,-h),(a,b,-h),(-a,b,-h),(-a,-b,h),(a,-b,h),(a,b,h),(-a,b,h)]]
  self.poly(vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],mat,collision)
 def beam(self,a,b,w=.28,mat='SG_Timber',collision=False):
  A,B=Vector(a),Vector(b);d=(B-A).normalized();ref=Vector((0,0,1)) if abs(d.z)<.95 else Vector((0,1,0));x=d.cross(ref).normalized()*w/2;y=d.cross(x).normalized()*w/2
  vs=[tuple(p+sx*x+sy*y) for p in [A,B] for sx,sy in [(-1,-1),(1,-1),(1,1),(-1,1)]]
  self.poly(vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],mat,collision)
 def cyl(self,c,r,h,mat='SG_Stone',n=16,top=None,collision=False):
  x,y,z=c;t=r if top is None else top;vs=[(x+rr*math.cos(i*math.tau/n),y+rr*math.sin(i*math.tau/n),z+hh) for rr,hh in [(r,0),(t,h)] for i in range(n)]
  fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  self.poly(vs,fs,mat,collision)
 def wedge(self,x,y,w,d,z,ridge,mat='SG_Plaster',collision=False):
  v=[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x,y-d/2,ridge),(x-w/2,y+d/2,z),(x+w/2,y+d/2,z),(x,y+d/2,ridge)]
  self.poly(v,[(0,1,2),(3,5,4),(0,3,4,1),(0,2,5,3),(2,1,4,5)],mat,collision)
 def roof(self,x,y,w,d,eave,rise,detail):
  # Closed solid gable core + two thin physical roof planes, not a roof AABB.
  self.wedge(x,y,w,d,eave,eave+rise,'SG_Plaster',True)
  W=w/2+.7;D=d/2+.65
  for side in [-1,1]:
   vv=[(x,y-D,eave+rise+.16),(x+side*W,y-D,eave-.05),(x+side*W,y+D,eave-.05),(x,y+D,eave+rise+.16)]
   # Reverse one slope so normals point OUT/up.
   if side<0:vv=list(reversed(vv))
   bottom=[(a,b,c-.24) for a,b,c in vv];self.poly(vv+bottom,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'SG_Roof',True)
   if detail==0:
    # Rounded tile courses catch real silhouette/light, backed by a portable tile texture.
    courses=max(4,int(W/1.15));columns=max(5,int(d/1.2))
    for i in range(courses):
     xa=(i+.02)/courses*W;xb=(i+1.10)/courses*W
     for j in range(columns):
      ya=-D+j*(2*D/columns);yb=ya+2*D/columns*.97;verts=[]
      for xx in [xa,xb]:
       for k in range(4):
        yy=ya+(yb-ya)*k/3;zz=eave+rise+.18-(rise+.21)*xx/W+.10*math.sin(k*math.pi/3)
        verts.append((x+side*xx,y+yy,zz))
      fs=[(k,k+1,k+5,k+4) for k in range(3)]
      if side<0:fs=[tuple(reversed(f)) for f in fs]
      self.poly(verts,fs,'SG_Roof')
  self.beam((x,y-D,eave+rise+.22),(x,y+D,eave+rise+.22),.48,'SG_Roof')

DETAIL=0
def window(b,x,y,z,w=1.8,h=2.5,side=False):
 # Framed dark recesses, stone sills and open wooden shutters; closed buildings.
 def box(c,s,mat):
  if side:c=(y,c[0],c[2]);s=(s[1],s[0],s[2])
  b.box(c,s,mat)
 box((x,y,z),(w,.12,h),'SG_Window')
 if DETAIL<2:
  for sign in [-1,1]:
   box((x+sign*(w/2+.12),y-.08,z),(.18,.25,h+.35),'SG_Timber')
   box((x,y-.10,z+sign*h/2),(w+.45,.30,.18),'SG_Timber')
  box((x,y-.13,z),(.12,.24,h),'SG_Timber');box((x,y-.14,z),(w,.24,.12),'SG_Timber')
  box((x,y-.24,z-h/2-.18),(w+.65,.65,.22),'SG_Stone')
  if DETAIL==0:
   for sign in [-1,1]:box((x+sign*(w*.78+.20),y+.08,z),(w*.43,.20,h),'SG_Timber')

def house(b,w,d,eave,rise,shop=False,guild=False):
 b.box((0,0,.65),(w+.5,d+.5,1.3),'SG_Stone',True)
 b.box((0,0,eave/2+.4),(w,d,eave-.8),'SG_Plaster',True)
 b.roof(0,0,w,d,eave,rise,DETAIL)
 floors=max(2,int(eave/4.6))
 if DETAIL<2:
  for level in range(floors+1):
   h=1.4+(eave-1.5)*level/floors
   for y in [-d/2-.10,d/2+.10]:b.box((0,y,h),(w+.45,.27,.32),'SG_Timber')
   for x in [-w/2-.10,w/2+.10]:b.box((x,0,h),(.27,d,.32),'SG_Timber')
  for x in [-w/2,-w*.25,0,w*.25,w/2]:
   for y in [-d/2-.12,d/2+.12]:b.box((x,y,eave/2),(.32,.28,eave),'SG_Timber')
  for x in [-w/2-.12,w/2+.12]:
   for y in [-d/2,0,d/2]:b.box((x,y,eave/2),(.28,.32,eave),'SG_Timber')
  for y in [-d/2-.17,d/2+.17]:
   b.beam((-w/2,y,eave),(0,y,eave+rise-.2),.32);b.beam((w/2,y,eave),(0,y,eave+rise-.2),.32);b.beam((0,y,eave),(0,y,eave+rise-.2),.3)
   if DETAIL==0:
    for x in [-w*.375,w*.125]:b.beam((x,y,2),(x+w*.20,y,5.1),.20)
 for level in range(floors):
  z=3.0+level*(eave-1.7)/floors
  for y in [-d/2-.03,d/2+.03]:
   for x in [-w*.29,0,w*.29]:
    if level==0 and x==0:continue
    window(b,x,y,z)
  for x in [-w/2-.03,w/2+.03]:
   for y in [-d*.3,0,d*.3]:window(b,y,x,z,side=True)
 for y in [-d/2-.10,d/2+.10]:
  b.box((0,y,2),(2.8,.20,4),'SG_Timber')
  if DETAIL<2:
   for s in [-1,1]:b.box((s*1.55,y-.05,2),(.3,.4,4.3),'SG_Stone')
   b.box((0,y-.05,4.15),(3.4,.40,.3),'SG_Stone')
 b.box((w*.26,d*.25,eave+rise*.80),(1.9,1.9,5.4),'SG_Brick',True)
 b.box((w*.26,d*.25,eave+rise*.80+2.6),(2.3,2.3,.4),'SG_Stone',True)
 if DETAIL<2:window(b,0,-d/2-.04,eave+rise*.40,1.5,1.8)
 if shop:
  # Scalloped canvas, front posts and small merchant bay.
  for x in [-w*.4,w*.4]:b.beam((x,-d/2-4.1,0),(x,-d/2-4.1,4.7),.24)
  for i in range(10):
   x0=-w*.43+i*w*.086;x1=x0+w*.086
   b.poly([(x0,-d/2-.2,6),(x1,-d/2-.2,6),(x1,-d/2-4.3,4.8),(x0,-d/2-4.3,4.8)],[(0,1,2,3)],'SG_Cloth' if i%2==0 else 'SG_Plaster')
  if DETAIL==0:
   b.box((0,-d/2-.15,1.8),(w*.68,.18,2.8),'SG_Window')
   b.box((0,-d/2-1,1.3),(w*.7,1.8,.25),'SG_Timber')
 if guild:
  b.box((0,-d/2-1.7,eave*.53),(w*.55,3.4,.42),'SG_Timber',True)
  for x in [-w*.27,0,w*.27]:b.beam((x,-d/2-3.1,eave*.53),(x,-d/2-3.1,eave*.53+1.8),.18)
  b.beam((-w*.27,-d/2-3.1,eave*.53+1.6),(w*.27,-d/2-3.1,eave*.53+1.6),.18)

specs={
 'SG_House_A_Gable':lambda b:house(b,18,24,15,7.5),
 'SG_House_B_Tall':lambda b:house(b,18,22,22,8),
 'SG_House_C_Shop':lambda b:house(b,24,24,17,8,True),
 'SG_House_D_Wide':lambda b:house(b,28,28,18,10),
 'SG_House_E_Cottage':lambda b:house(b,16,20,11.5,6),
 'SG_House_F_Guild':lambda b:house(b,26,28,24,10,False,True),
 'SG_Warehouse':lambda b:house(b,30,38,19,10,True),
}
def civic(b):
 house(b,46,36,27,16,False,True)
 b.box((0,-22,9),(13,8,18),'SG_Stone',True);b.roof(0,-22,14,9,18,8,DETAIL)
 for x in [-18,-9,0,9,18]:b.box((x,-20,2.3),(4,4,4.6),'SG_Stone',True)
 b.box((0,0,28),(12,12,56),'SG_Stone',True);b.roof(0,0,13,13,56,10,DETAIL)
 for y in [-6.15,6.15]:window(b,0,y,48,3.8,7.5)
specs['SG_CivicHall']=civic

def belltower(b):
 b.box((0,0,2),(20,20,4),'SG_Stone',True);b.box((0,0,29),(14,14,54),'SG_Stone',True)
 for z in [4,20,37,53]:b.box((0,0,z),(15,15,1.0),'SG_Stone',True)
 for x in [-7,7]:
  for y in [-7,7]:b.box((x,y,28),(1.4,1.4,52),'SG_Stone',True)
 for y in [-7.15,7.15]:
  for z in [13,30,46]:window(b,0,y,z,3,6)
 if DETAIL<2:
  for sign in [-1,1]:
   y=sign*7.25;verts=[(2.5*math.cos(i*math.tau/24),y,38+2.5*math.sin(i*math.tau/24)) for i in range(24)]
   b.poly(verts,[tuple(range(24)) if sign<0 else tuple(reversed(range(24)))],'SG_Plaster')
   b.beam((0,y+sign*.10,38),(0,y+sign*.10,39.9),.18,'SG_Iron');b.beam((0,y+sign*.10,38),(1.35,y+sign*.10,37.3),.15,'SG_Iron')
 # Bell chamber has actual open sides above its solid floor.
 b.box((0,0,54),(18,18,1),'SG_Stone',True)
 for x in [-7.4,7.4]:
  for y in [-7.4,7.4]:b.box((x,y,61),(1.7,1.7,14),'SG_Stone',True)
 b.cyl((0,0,57),2.1,5,'SG_Iron',16,top=1)
 b.box((0,0,68),(18,18,1),'SG_Stone',True)
 b.poly([(-10,-10,68),(10,-10,68),(10,10,68),(-10,10,68),(0,0,91)],[(3,2,1,0),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],'SG_Roof',True)
 b.beam((0,0,90),(0,0,95),.35,'SG_Iron');b.beam((-1.8,0,93),(1.8,0,93),.30,'SG_Iron')
specs['SG_Belltower']=belltower

def windmill(b):
 b.cyl((0,0,0),9,28,'SG_Stone',20,top=6,collision=True);b.cyl((0,0,28),7,11,'SG_Roof',20,top=.2,collision=True)
 for z in [7,18]:window(b,0,-7.9,z,2.2,3)
 center=Vector((0,-9,30))
 for i in range(4):
  angle=.25+i*math.pi/2;d=Vector((math.cos(angle),0,math.sin(angle)));s=Vector((-math.sin(angle),0,math.cos(angle)))
  a=center+d*3;c=center+d*25;b.beam(tuple(center),tuple(c),.5,collision=True)
  verts=[tuple(center+d*r+s*w) for r,w in [(6,0),(6,3.1),(24,4.5),(24,0)]];b.poly(verts,[(0,1,2,3)],'SG_Cloth')
  for k in range(7):
   a=center+d*(7+k*2.6);b.beam(tuple(a),tuple(a+s*4),.13)
 b.cyl((0,-9,29),1.2,2,'SG_Timber',12) # structural hub
specs['SG_Windmill']=windmill

def wall(b):
 b.box((0,0,57),(32.4,14,114),'SG_Stone',True);b.box((0,0,1.2),(33,18,2.4),'SG_Stone',True)
 b.box((0,0,114),(33,18,2),'SG_Stone',True)
 for y in [-8.2,8.2]:
  b.box((0,y,117),(33,1.4,5),'SG_Stone',True)
  if DETAIL<2:
   for x in [-12,-4,4,12]:b.box((x,y,120),(4,1.6,2),'SG_Stone',True)
 if DETAIL<2:
  for x in [-15,15]:
   for y in [-7.4,7.4]:b.box((x,y,53),(1.5,1.5,104),'SG_Stone')
  for z in [12,58,108]:b.box((0,0,z),(32.6,14.6,.7),'SG_Stone')
specs['SG_Wall_Bay_32x120']=wall

def gate(b):
 # 48m opening under a real segmented arch; door panels are intentionally closed.
 for x in [-40,40]:
  b.box((x,0,65),(30,24,130),'SG_Stone',True);b.box((x,0,131),(34,28,3),'SG_Stone',True)
  b.box((x,0,138),(26,20,12),'SG_Stone',True)
  if DETAIL<2:
   for y in [-12.2,12.2]:
    for z in [22,52,82,112]:window(b,x,y,z,4,9)
 for x in [-62,62]:b.box((x,0,60),(18,20,120),'SG_Stone',True)
 # Elliptical arch with solid spandrels; no daylight gaps above a closed gate.
 for i in range(18):
  a=i*math.pi/18;aa=(i+1)*math.pi/18
  pts=[(r*math.cos(t),y,56+r*math.sin(t)) for y in [-12,12] for r,t in [(24,a),(24,aa),(34,aa),(34,a)]]
  b.poly(pts,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'SG_Stone',True)
  xa,xb=34*math.cos(a),34*math.cos(aa);za,zb=56+34*math.sin(a),56+34*math.sin(aa)
  v=[(xa,-12,za),(xb,-12,zb),(xb,-12,94),(xa,-12,94),(xa,12,za),(xb,12,zb),(xb,12,94),(xa,12,94)]
  b.poly(v,[tuple(reversed(f)) for f in [(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]],'SG_Stone',True)
  # Closed door upper semicircle, extruded through the same4m plank depth.
  v=[(0,-2,56),(25*math.cos(a),-2,56+25*math.sin(a)),(25*math.cos(aa),-2,56+25*math.sin(aa)),(0,2,56),(25*math.cos(a),2,56+25*math.sin(a)),(25*math.cos(aa),2,56+25*math.sin(aa))]
  b.poly(v,[(0,1,2),(3,5,4),(1,4,5,2),(0,3,4,1),(0,2,5,3)],'SG_Timber',True)
 b.box((0,0,108),(52,24,30),'SG_Stone',True);b.box((0,0,124),(58,28,3),'SG_Stone',True)
 b.box((0,0,28),(50,4,56),'SG_Timber',True)
 for y in [-2.2,2.2]:
  for z in [9,27,45]:b.box((0,y,z),(47,.35,1),'SG_Iron')
  for x in [-18,-9,0,9,18]:b.box((x,y,29),(.45,.35,55),'SG_Iron')
 for x in [-40,40]:
  b.poly([(x-4,-12.5,96),(x+4,-12.5,96),(x+4,-12.5,75),(x,-12.5,72),(x-4,-12.5,75)],[(0,1,2,3,4)],'SG_Cloth')
specs['SG_Gate_Fortress']=gate

def bridge(b):
 # Width48, depth72; deck rises smoothly2m from ground endpoints.
 zvalues=[-36,-24,-12,0,12,24,36];heights=[0,.7,1.65,2,1.65,.7,0]
 for j in range(6):
  z0,z1=zvalues[j:j+2];h0,h1=heights[j:j+2]
  vs=[(-24,z0,h0),(24,z0,h0),(24,z1,h1),(-24,z1,h1),(-24,z0,h0-1.1),(24,z0,h0-1.1),(24,z1,h1-1.1),(-24,z1,h1-1.1)]
  b.poly(vs,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'SG_Paver',True)
  for x in [-23.3,23.3]:
   b.beam((x,z0,h0+1.3),(x,z1,h1+1.3),.5,'SG_Stone',True)
   b.box((x,z0,h0+.65),(.9,1,1.7),'SG_Stone',True)
 for x in [-21,21]:
  for z in [-17,17]:b.box((x,z,-3),(4,5,9),'SG_Stone',True)
specs['SG_Canal_Bridge']=bridge

def gantry(b):
 for x in [-10,10]:b.box((x,0,12),(1.2,1.2,24),'SG_Timber',True)
 b.box((0,0,24),(23,1.6,1.3),'SG_Timber',True)
 for x in [-10,10]:b.beam((x,0,18),(x*.5,0,24),.5)
 b.box((0,0,24.9),(24,3,.5),'SG_Timber',True)
specs['SG_Rooftop_Gantry']=gantry

def market(b):
 for x in [-3,3]:
  for y in [-2,2]:b.box((x,y,2.5),(.20,.20,5),'SG_Timber')
 b.box((0,0,1.2),(6.4,4,2.4),'SG_Timber',True);b.roof(0,0,7,5,4.5,1.2,2)
 for j in range(6):b.box((-2.5+j,0,1.3),(.8,3.7,2.5),'SG_Cloth' if j%2 else 'SG_Plaster')
specs['SG_Market_Stall']=market

def fountain(b):
 b.cyl((0,0,0),8,.6,'SG_Stone',24,collision=True);b.cyl((0,0,.6),6,1.3,'SG_Stone',24,collision=True)
 b.cyl((0,0,1.9),4.8,.12,'SG_Window',24);b.cyl((0,0,1.9),1,6,'SG_Stone',16,top=.65,collision=True);b.cyl((0,0,6),2.8,.6,'SG_Stone',20,collision=True)
specs['SG_Plaza_Fountain']=fountain

def crate(b):
 b.box((0,0,1),(2,2,2),'SG_Timber',True)
 for z in [.15,1.85]:b.box((0,0,z),(2.15,2.15,.18),'SG_Iron')
specs['SG_Crate']=crate

report={};source_objects=[]
def make_object(name,verts,faces,indices=None):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob)
 if indices is not None:
  for mat in materials.values():me.materials.append(mat)
  for poly,idx in zip(me.polygons,indices):poly.material_index=idx
  me.uv_layers.new(name='UVMap');uv=me.uv_layers.active
  names=list(materials)
  for poly in me.polygons:
   axis=max(range(3),key=lambda i:abs(poly.normal[i]));ab=[i for i in range(3) if i!=axis];scale=tex_scales[names[poly.material_index]]
   for li in poly.loop_indices:
    co=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(co[ab[0]]/scale,co[ab[1]]/scale)
 return ob

def export(ob,name):
 bpy.ops.object.select_all(action='DESELECT');ob.hide_set(False);ob.select_set(True);bpy.context.view_layer.objects.active=ob
 original_name=ob.name;original_mesh=ob.data.name;ob.name=original_name.replace('_LOD','_Detail');ob.data.name=original_mesh.replace('_LOD','_Detail')
 bpy.ops.export_scene.fbx(filepath=str(GAME/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,path_mode='AUTO',embed_textures=False,mesh_smooth_type='FACE',use_mesh_modifiers=True)
 ob.name=original_name;ob.data.name=original_mesh;ob.data.calc_loop_triangles();return len(ob.data.loop_triangles)

for i,(name,fn) in enumerate(specs.items()):
 record={};collider=None
 for detail in range(3):
  DETAIL=detail;b=Batch();fn(b);ob=make_object(name+'_LOD'+str(detail),b.v,b.f,b.mi);record['LOD'+str(detail)]=export(ob,ob.name)
  if detail==0:
   source_objects.append(ob);collider=make_object(name+'_Collision',b.cv,b.cf);record['collisionTriangles']=export(collider,collider.name);bpy.data.objects.remove(collider,do_unlink=True)
  else:bpy.data.objects.remove(ob,do_unlink=True)
 report[name]=record;print('SG_ASSET '+name+' '+json.dumps(record),flush=True)

# Retain a packed, editable native model studio with arranged archetypes and scale.
for i,ob in enumerate(source_objects):
 ob.location=((i%5)*110,(i//5)*150,0)
# Dedicated beauty view of a varied street cluster, using linked original meshes.
studio=bpy.data.collections.new('Street Composition [review only]');scene.collection.children.link(studio)
for i,(name,pos,yaw) in enumerate([
 ('SG_House_A_Gable',(-26,0,0),0),('SG_House_C_Shop',(26,0,0),0),('SG_House_B_Tall',(-28,40,0),0),('SG_House_F_Guild',(30,42,0),0),('SG_House_E_Cottage',(-24,-35,0),0),('SG_Belltower',(0,115,0),0)]):
 src=next(o for o in source_objects if o.name==name+'_LOD0');copy=src.copy();copy.data=src.data;studio.objects.link(copy);copy.location=pos;copy.rotation_euler.z=yaw
for ob in source_objects:ob.hide_render=True
world=bpy.data.worlds.new('Owned warm sky');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.40,.49,.59,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65
sun=bpy.data.lights.new('Afternoon sun','SUN');sun.energy=3;sun.angle=.12;o=bpy.data.objects.new('Afternoon sun',sun);scene.collection.objects.link(o);o.rotation_euler=(.6,-.4,-.55)
# Physical studio ground is not exported into the game library.
bpy.ops.mesh.primitive_plane_add(size=600,location=(0,35,-.04));floor=bpy.context.object;floor.name='Studio ground [not exported]';floor.data.materials.append(materials['SG_Paver'])
for uv in floor.data.uv_layers.active.data:uv.uv*=60
camera_data=bpy.data.cameras.new('Street Review');camera=bpy.data.objects.new('Street Review',camera_data);scene.collection.objects.link(camera);camera.location=(135,-165,110);camera.rotation_euler=(Vector((0,34,28))-camera.location).to_track_quat('-Z','Y').to_euler();camera_data.lens=38;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Shiganshina_Architecture.blend'))
(OUT/'asset-report.json').write_text(json.dumps({'source':'Original procedural architecture inspired by the Shiganshina district, not ripped game/anime assets','units':'metres','assets':report,'materials':list(materials),'lodCount':3,'collision':'Separate closed structure meshes, roofs shaped to match visible slopes'},indent=2))
scene.render.filepath=str(OUT/'Blender_Street_Architecture.png');bpy.ops.render.render(write_still=True)
print('SG_BLENDER_COMPLETE '+str(OUT),flush=True)
