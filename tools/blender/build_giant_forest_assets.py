"""Giant Forest 2.0: isolated model-review library, never a gameplay-map builder.
Run with Blender --background --factory-startup --python this_file, GF2_PROJECT_ROOT set.
Writes ONLY ArtSource/Environment/GiantForest2; no live Blender or Unity automation.
"""
import bpy, bmesh, math, random, os, json, struct, zlib
import numpy as np
from pathlib import Path
from mathutils import Vector

if not bpy.app.background or not os.environ.get('GF2_PROJECT_ROOT'):
    raise RuntimeError('Use an OWNED --background --factory-startup Blender with GF2_PROJECT_ROOT')
if bpy.data.filepath:raise RuntimeError('Refusing a loaded .blend; start an OWNED --factory-startup worker')
ROOT=Path(os.environ['GF2_PROJECT_ROOT']).resolve()
OUT=ROOT/'ArtSource/Environment/GiantForest2'; TEX=OUT/'Textures'; EXPORT=OUT/'Exports'
for p in (OUT,TEX,EXPORT): p.mkdir(parents=True,exist_ok=True)
# Only the disposable factory scene is cleared. Never run against an interactive file.
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob,do_unlink=True)
scene=bpy.context.scene; scene.name='GF2_ModelReview'; scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
assets={}; mats={}; current=None

def png(name,a):
    a=np.clip(a*255,0,255).astype(np.uint8); h,w,c=a.shape
    def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    raw=b''.join(b'\0'+row.tobytes() for row in a[::-1])
    (TEX/name).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6 if c==4 else 2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))

def noise(n,seed):
    rng=np.random.default_rng(seed);u,v=np.meshgrid(np.arange(n)/n,np.arange(n)/n);s=np.zeros((n,n));amp=1;total=0
    for scale in (2,4,8,16,32,64,128):
        grid=rng.random((scale+1,scale+1));grid[-1,:]=grid[0,:];grid[:,-1]=grid[:,0];x=u*scale;y=v*scale;ix=x.astype(int);iy=y.astype(int);fx=x-ix;fy=y-iy
        fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
        s+=amp*((grid[iy,ix]*(1-fx)+grid[iy,ix+1]*fx)*(1-fy)+(grid[iy+1,ix]*(1-fx)+grid[iy+1,ix+1]*fx)*fy)
        total+=amp;amp*=.53
    return s/total

def maps(name,color,height,rough):
    png(name+'_BaseColor.png',color)
    dy,dx=np.gradient(height);norm=np.stack((-dx*18,-dy*18,np.ones_like(dx)),2);norm/=np.linalg.norm(norm,axis=2)[:,:,None]
    png(name+'_Normal.png',norm*.5+.5)
    png(name+'_Roughness.png',np.repeat(np.clip(rough,0,1)[:,:,None],3,2))

N=2048;u,v=np.meshgrid(np.arange(N)/N,np.arange(N)/N);n=noise(N,41);fine=noise(N,145)
warp=u+.014*np.sin(v*math.tau*3)+.017*(n-.5)+.004*np.sin(v*math.tau*13+u*math.tau*2)
ridge=.64*np.abs(np.sin(warp*math.tau*26+np.sin(v*math.tau*2)*.4))**.38+.36*np.abs(np.sin(warp*math.tau*43))**.6
h=ridge*.47+n*.38+fine*.15
col=np.stack((.23+.20*h,.18+.17*h,.125+.14*h),2)
maps('GF2_Bark',col,h,.8+.15*(1-h))
stonenoise=noise(N,78);stonefine=noise(N,710)
h=stonenoise*.7+stonefine*.3
col=np.stack((.43+.20*h,.435+.19*h,.405+.17*h),2)
maps('GF2_Stone',col,h*.5,.78+.15*stonefine)
h=noise(N,910);col=np.stack((.15+.18*h,.12+.16*h,.075+.12*h),2)
# Leaf litter/needle forms are actually in the exported texture, not preview-only nodes.
rng=np.random.default_rng(92)
for i in range(2400):
    cx,cy=rng.integers(0,N,2);a=rng.uniform(0,math.tau);length=rng.uniform(3,22);width=rng.uniform(1.5,7)
    r=int(length+2);x0=max(0,cx-r);x1=min(N,cx+r);y0=max(0,cy-r);y1=min(N,cy+r)
    yy,xx=np.mgrid[y0:y1,x0:x1];x=(xx-cx)*math.cos(a)+(yy-cy)*math.sin(a);y=-(xx-cx)*math.sin(a)+(yy-cy)*math.cos(a)
    mask=(x/length)**2+(y/width)**2<1
    tint=np.array([.23,.17,.085])*rng.uniform(.65,1.5)
    col[y0:y1,x0:x1][mask]=tint;h[y0:y1,x0:x1][mask]+=.06
maps('GF2_Floor',col,h,.94-n*.08)
r=np.sqrt((u-.5)**2+(v-.5)**2);rings=np.sin((r+.004*np.sin(np.arctan2(v-.5,u-.5)*11))*900)
h=.5+.12*rings+.2*noise(N,441)
col=np.stack((.48+.22*h,.31+.22*h,.16+.20*h),2);maps('GF2_EndGrain',col,h,.85+0*h)
# Four RGBA branch sprays; organic leaf silhouettes, transparent spaces and stem veins.
S=1024;fol=np.zeros((S,S,4),dtype=float);rng=np.random.default_rng(221)
def line(im,a,b,width,color):
    x0,y0=a;x1,y1=b;r=width+2;xa=max(0,int(min(x0,x1)-r));xb=min(im.shape[1],int(max(x0,x1)+r+1));ya=max(0,int(min(y0,y1)-r));yb=min(im.shape[0],int(max(y0,y1)+r+1))
    yy,xx=np.mgrid[ya:yb,xa:xb];vx=x1-x0;vy=y1-y0;t=np.clip(((xx-x0)*vx+(yy-y0)*vy)/max(.01,vx*vx+vy*vy),0,1)
    mask=(xx-x0-t*vx)**2+(yy-y0-t*vy)**2<width**2;im[ya:yb,xa:xb][mask]=color
for slot in range(4):
    im=fol[(slot//2)*512:(slot//2+1)*512,(slot%2)*512:(slot%2+1)*512]
    line(im,(256,26),(258,464),2.1,(.20,.18,.075,1))
    for j in range(19):
        yy=54+j*21
        for side in (-1,1):
            x=256+side*rng.uniform(66,164)*(1-.42*j/19);y=yy+rng.uniform(12,52)
            line(im,(256,yy),(x,y),1.3,(.20,.19,.075,1))
            for k in range(3):
                cx=x-side*k*18;cy=y+k*10;angle=side*rng.uniform(.4,1.1);length=rng.uniform(21,34);width=rng.uniform(8,13)
                xa=max(0,int(cx-40));xb=min(512,int(cx+40));ya=max(0,int(cy-40));yb=min(512,int(cy+40))
                Y,X=np.mgrid[ya:yb,xa:xb];A=(X-cx)*math.cos(angle)+(Y-cy)*math.sin(angle);B=-(X-cx)*math.sin(angle)+(Y-cy)*math.cos(angle)
                mask=(A/length)**2+(B/width)**2<1;light=.78+.2*(B/width)+rng.uniform(0,.23)
                rgb=np.stack((.21*light+0*A,.34*light+0*A,.095*light+0*A,np.ones_like(A)),2)
                sub=im[ya:yb,xa:xb];sub[mask]=rgb[mask]
                vein=mask&(abs(B)<.7);sub[vein]=(.27,.34,.11,1)
png('GF2_Foliage_BaseColorAlpha.png',fol)

def material(name,tex,rough=.85,alpha=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');L=m.node_tree.links
    def image(file,noncolor=False):
        im=bpy.data.images.load(str(TEX/file),check_existing=True)
        if noncolor:im.colorspace_settings.name='Non-Color'
        im.pack();node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;return node
    b=image(tex+'_BaseColorAlpha.png' if alpha else tex+'_BaseColor.png');L.new(b.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=rough
    if alpha:
        L.new(b.outputs['Alpha'],bs.inputs['Alpha']);m.use_backface_culling=False
        if hasattr(m,'surface_render_method'):m.surface_render_method='DITHERED'
        bs.inputs['Roughness'].default_value=.9
        if 'Subsurface Weight' in bs.inputs:bs.inputs['Subsurface Weight'].default_value=.035
    else:
        nm=image(tex+'_Normal.png',True);node=m.node_tree.nodes.new('ShaderNodeNormalMap');node.inputs['Strength'].default_value=.55;L.new(nm.outputs['Color'],node.inputs['Color']);L.new(node.outputs['Normal'],bs.inputs['Normal']);rr=image(tex+'_Roughness.png',True);L.new(rr.outputs['Color'],bs.inputs['Roughness'])
    mats[name]=m;return m
bark=material('GF2_Bark','GF2_Bark');stone=material('GF2_Limestone','GF2_Stone');soil=material('GF2_ForestFloor','GF2_Floor');wood=material('GF2_Heartwood','GF2_EndGrain');leaf=material('GF2_Leaves','GF2_Foliage',alpha=True)
def solid(name,color,rough=1):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough;return m
moss=solid('GF2_RootMoss',(.09,.14,.045));darkstone=solid('GF2_RecessStone',(.20,.22,.20));guide=solid('GF2_ScaleGuide',(.28,.30,.29));brass=solid('GF2_HumanGuide',(.65,.36,.075))
# Actual vertex paint is exported. Blender materials multiply it for art review;
# final Unity shader/prefab hookup intentionally deferred until art approval.
for m in (bark,stone,soil):
    nd=m.node_tree.nodes;L=m.node_tree.links;bs=nd.get('Principled BSDF');old=bs.inputs['Base Color'].links[0].from_socket
    attr=nd.new('ShaderNodeVertexColor');attr.layer_name='Color';mix=nd.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;L.new(old,mix.inputs[1]);L.new(attr.outputs['Color'],mix.inputs[2]);L.new(mix.outputs[0],bs.inputs['Base Color'])

def start(name,kind,description):
    global current
    current=bpy.data.collections.new(name);assets[name]={'collection':current,'kind':kind,'description':description}
    root=bpy.data.objects.new(name,None);current.objects.link(root);assets[name]['root']=root;return root

def mesh(name,verts,faces,mat,uv=None,colors=None,smooth=True):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);current.objects.link(ob);ob.parent=assets[current.name]['root'];me.materials.append(mat)
    me.uv_layers.new(name='UVMap');me.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    # Adding CustomData layers can relocate storage in Blender4.5. Reacquire
    # BOTH RNA handles after allocation or UV writes corrupt float vertex paint.
    layer=me.uv_layers['UVMap'];paint=me.color_attributes['Color']
    for p in me.polygons:
        p.use_smooth=smooth
        for li in p.loop_indices:
            vi=me.loops[li].vertex_index;co=me.vertices[vi].co
            if uv is None:
                axis=max(range(3),key=lambda a:abs(p.normal[a]));ab=[i for i in range(3) if i!=axis];layer.data[li].uv=(co[ab[0]]/4,co[ab[1]]/4)
            else:layer.data[li].uv=uv[vi]
            paint.data[li].color=(*((1,1,1) if colors is None else colors[vi]),1)
    return ob

def tube(name,path,radii,mat=bark,sides=12,flatten=1,seed=0):
    if len(path)>2:
        knots=[Vector(p) for p in path];p2=[];r2=[]
        for i in range(len(path)-1):
            a=knots[max(0,i-1)];b=knots[i];c=knots[i+1];d=knots[min(len(path)-1,i+2)]
            for j in range(4):
                t=j/4;p2.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));r2.append(radii[i]*(1-t)+radii[i+1]*t)
        p2.append(knots[-1]);r2.append(radii[-1]);path=p2;radii=r2
    rng=random.Random(seed);verts=[];uv=[];colors=[];phase=rng.uniform(0,6);length=0
    for i,p in enumerate(path):
        p=Vector(p);t=Vector(path[min(i+1,len(path)-1)])-Vector(path[max(i-1,0)]);t.normalize();ref=Vector((0,0,1)) if abs(t.z)<.93 else Vector((0,1,0));x=t.cross(ref).normalized();y=t.cross(x).normalized()
        if i:length+=(p-Vector(path[i-1])).length
        for j in range(sides+1):
            a=j/sides*math.tau;r=radii[i]*(1+.06*math.sin(a*3+phase));q=p+x*(math.cos(a)*r)+y*(math.sin(a)*r*flatten);verts.append(q);uv.append((j/sides*max(1,radii[0]*math.tau/4),length/6))
            green=max(0,1-q.z/7)*(.2+.3*math.sin(a*3+1)**2);colors.append((1-green*.38,1-green*.04,1-green*.55))
    faces=[]
    for i in range(len(path)-1):
        for j in range(sides):a=i*(sides+1)+j;b=a+sides+1;faces.append((a,a+1,b+1,b))
    faces+=[tuple(reversed(range(sides))),tuple((len(path)-1)*(sides+1)+j for j in range(sides))]
    return mesh(name,verts,faces,mat,uv,colors)

def box(name,center,size,mat=stone,bevel=.055,rotation=0,tint=(1,1,1)):
    x,y,z=center;w,d,h=[a*.5 for a in size];c=math.cos(rotation);s=math.sin(rotation)
    vv=[(x+X*c-Y*s,y+X*s+Y*c,z+Z) for X,Y,Z in [(-w,-d,-h),(w,-d,-h),(w,d,-h),(-w,d,-h),(-w,-d,h),(w,-d,h),(w,d,h),(-w,d,h)]]
    ob=mesh(name,vv,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],mat,colors=[tint]*8,smooth=False)
    if bevel:
        mod=ob.modifiers.new('Small worn stone edge','BEVEL');mod.width=bevel;mod.segments=1
        normal=ob.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');normal.keep_sharp=True
    return ob

# Canopy sprites are textured twig/leaf sprays, NOT green sphere placeholders.
def foliage(name,buds,rng):
    verts=[];uv=[];faces=[]
    for center,size in buds:
        for k in range(3):
            az=rng.uniform(0,math.tau);tilt=rng.uniform(.15,.9);normal=Vector((math.cos(az)*tilt,math.sin(az)*tilt,1)).normalized();x=normal.cross(Vector((0,1,0))).normalized();y=normal.cross(x).normalized();spin=rng.uniform(0,math.tau);xx=x*math.cos(spin)+y*math.sin(spin);yy=-x*math.sin(spin)+y*math.cos(spin)
            p=Vector(center)+Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.65,.65)));slot=rng.randrange(4);a=slot%2*.5;b=slot//2*.5;idx=len(verts)
            for X,Y,U,V in [(-1,-1,0,0),(1,-1,1,0),(1,1,1,1),(-1,1,0,1)]:verts.append(p+xx*(X*size*.63)+yy*(Y*size));uv.append((a+.006+U*.488,b+.006+V*.488))
            faces.append((idx,idx+1,idx+2,idx+3))
    return mesh(name,verts,faces,leaf,uv,smooth=False)

def tree(name,height,radius,seed,lean=0,fork=False):
    start(name,'tree','High clear bole; buttress roots; asymmetrical branch-spray crown. No collider/LOD yet.');rng=random.Random(seed)
    centers=[];radii=[]
    for i in range(57):
        t=i/56;z=height*t;centers.append((lean*t*t+.38*math.sin(t*5)*t,.45*math.sin(t*7)*t,z));radii.append(radius*((1-t)**.85*.87+.035)*(1+.38*math.exp(-t*38)))
    # Fluted trunk: large basal ridges fade out smoothly instead of spherical bulges.
    vv=[];uv=[];cols=[];sides=48
    for i,p in enumerate(centers):
        for j in range(sides+1):
            a=j/sides*math.tau;z=p[2];flute=1+.17*math.cos(a*7+.17*math.sin(z*.08))*math.exp(-z/19)+.025*math.sin(a*17+z*.21)
            r=radii[i]*flute;vv.append((p[0]+r*math.cos(a),p[1]+r*math.sin(a),z));uv.append((j/sides*radius*math.tau/4,z/6));m=max(0,1-z/10)*(.3+.3*math.sin(a*4)**2);cols.append((1-m*.55,1-m*.05,1-m*.65))
    ff=[]
    for i in range(56):
        for j in range(sides):a=i*(sides+1)+j;b=a+sides+1;ff.append((a,a+1,b+1,b))
    ff += [tuple(reversed(range(sides))),tuple(56*(sides+1)+j for j in range(sides))]
    mesh('Fluted_Bole',vv,ff,bark,uv,cols)
    for i in range(8):
        a=i/8*math.tau+rng.uniform(-.13,.13);c=math.cos(a);s=math.sin(a);reach=radius*rng.uniform(1.95,2.7)
        path=[(c*radius*.72,s*radius*.72,rng.uniform(3.8,6.4)),(c*radius*1.05,s*radius*1.05,2.8),(c*radius*1.5,s*radius*1.5,.85),(c*reach,s*reach,.12),(c*(reach+1.5),s*(reach+1.5),-.22)]
        tube('ButtressRoot_%02d'%i,path,[radius*.32,radius*.29,radius*.20,radius*.075,.025],sides=14,flatten=.62,seed=i)
    buds=[]
    for tier in range(9):
        z=height*(.59+.042*tier);reach=height*(.20*(1-tier/12))*(1.15 if tier==2 else 1)
        count=5 if tier<6 else 4
        for b in range(count):
            a=b/count*math.tau+tier*2.34+rng.uniform(-.35,.35);reach2=reach*rng.uniform(.7,1.12);base=Vector((lean*(z/height)**2,0,z));end=base+Vector((math.cos(a)*reach2,math.sin(a)*reach2,reach2*rng.uniform(.22,.6)))
            path=[base,base+Vector((math.cos(a)*reach2*.38,math.sin(a)*reach2*.38,-reach2*.08)),base.lerp(end,.73)+Vector((0,0,-1)),end]
            thick=radius*(.16 if tier<5 else .11)*rng.uniform(.7,1.2);tube('CrownLimb',path,[thick,thick*.7,thick*.36,.055],sides=10,seed=tier*20+b)
            for q in range(7):
                f=.35+q*.095;p=Vector(path[1]).lerp(end,f);side=(-1 if q%2 else 1);swing=a+side*rng.uniform(.6,1.4);length=reach2*rng.uniform(.15,.33)
                tip=p+Vector((math.cos(swing)*length,math.sin(swing)*length,length*.4));tube('LeafBearingTwig',[p,p.lerp(tip,.55),tip],[thick*.21,thick*.09,.015],sides=6,seed=q)
                for k in range(7):
                    cc=p.lerp(tip,.36+k*.105)+Vector((rng.uniform(-1.8,1.8),rng.uniform(-1.8,1.8),rng.uniform(-.5,1.4)))
                    buds.append((cc,rng.uniform(1.3,2.5)))
            for k in range(10):buds.append((end+Vector((rng.uniform(-2.6,2.6),rng.uniform(-2.6,2.6),rng.uniform(-1,2.3))),rng.uniform(1.4,2.8)))
    if fork:
        tube('LightningFork',[(lean*.3,0,height*.5),(-radius*1.4,1,height*.66),(-radius*2.3,1.2,height*.87),(-radius*2.8,.5,height*.98)],[radius*.58,radius*.39,radius*.20,.08],sides=20,seed=31)
    # Old branch stubs below crown give readable wood silhouette without low green clutter.
    for i in range(6):
        a=rng.uniform(0,math.tau);z=height*rng.uniform(.39,.62);p=Vector((lean*(z/height)**2,0,z));d=Vector((math.cos(a),math.sin(a),.25))*rng.uniform(2.5,5)
        tube('DeadLimbStub',[p,p+d*.65,p+d],[radius*.13,radius*.08,.07],sides=10,seed=i)
    if not fork:
        for k in range(65):buds.append((Vector((lean+rng.uniform(-2.6,2.6),rng.uniform(-2.6,2.6),height-1+rng.uniform(-2,1))),rng.uniform(1.5,2.5)))
    foliage('LeafSprays',buds,rng)

print('Building giant trees',flush=True)
tree('GF2_Tree_A_Sentinel',120,5.2,101)
tree('GF2_Tree_B_Leaning',104,3.65,204,lean=5.8)
tree('GF2_Tree_C_OldFork',114,5.5,309,lean=-1.5,fork=True)

# Massive, restrained stone architecture, not a castle crenellation kit.
def wall_body(cx=0,cy=0,angle=0,width=24,seed=0,crown=True):
    rng=random.Random(seed)
    def B(name,x,y,z,w,d,h,material=stone,bevel=.055,tint=(1,1,1)):
        c=math.cos(angle);s=math.sin(angle);return box(name,(cx+x*c-y*s,cy+x*s+y*c,z),(w,d,h),material,bevel,angle,tint)
    B('LoadBearingCore',0,0,29.6,width-.08,7.6,59.2,darkstone,.08)
    courses=30;h=1.9
    for side in (-1,1):
        for row in range(courses):
            cursor=-width/2;step=3.95
            while cursor<width/2-.05:
                w=min((step*.5 if row%2 and cursor==-width/2 else step),width/2-cursor);gray=rng.uniform(.88,1.08);z=.5+(row+.5)*h
                damp=max(0,1-z/17)*rng.uniform(.12,.34)
                B('LimestoneCourse',cursor+w/2,side*3.95,z,w-.035,.42,h-.025,tint=(gray*(1-damp*.65),gray*(1-damp*.25),gray*(1-damp*.8)))
                cursor+=w
    for side in (-1,1):
        for x in (-width/2+.65,width/2-.65):
            for j,(z,h,w,d) in enumerate([(3,6,2.4,2.0),(18,24,1.7,1.35),(43,26,1.35,1.1)]):B('TaperedPilaster',x,side*(4+d*.3),z,w,d,h)
    B('Footing',0,0,.45,width+.18,9.4,.9)
    B('LowerBeltCourse',0,0,9.6,width,8.35,.45)
    if crown:
        B('CrownCornice',0,0,58,width+.18,8.7,1)
        B('WalkwayCap',0,0,59,width+.18,10,1)
        for side in (-1,1):B('LowParapet',0,side*4.5,60,width+.18,.8,1.1)
start('GF2_Wall_Straight_24x60','wall','24m center-to-center repeat, 60m wall, 8m core; shallow cap/walkway.');wall_body(seed=63)
start('GF2_Wall_Corner_24x60','wall','L corner on 24m centerline grid; origin at intersection.');wall_body(12,0,seed=77,crown=False);wall_body(0,12,math.pi/2,seed=78,crown=False);box('CornerPier',(0,0,29.25),(9,9,58.5))
def Lcap(name,width,z0,z1):
    a=width*.5;outline=[(-a,-a),(24,-a),(24,a),(a,a),(a,24),(-a,24)];vv=[(x,y,z) for z in (z0,z1) for x,y in outline];ff=[tuple(reversed(range(6))),tuple(range(6,12))]
    for i in range(6):j=(i+1)%6;ff.append((i,j,j+6,i+6))
    mesh(name,vv,ff,stone,smooth=False)
Lcap('OnePieceCornerCornice',8.7,57.5,58.5);Lcap('OnePieceCornerWalkway',10,58.5,59.5)
for name,center,size in [('OuterCapX',(9.5,-4.5,60),(29,.8,1.1)),('OuterCapY',(-4.5,9.5,60),(.8,29,1.1)),('InnerCapX',(14.5,4.5,60),(19,.8,1.1)),('InnerCapY',(4.5,14.5,60),(.8,19,1.1))]:box(name,center,size)
start('GF2_Wall_Gateway_48x60','wall','48m module with real arched opening: 26m clear width, 39m crown.');rng=random.Random(85)
# Side towers and real extruded arch/spandrels, no black decal pretending to be a hole.
for side in (-1,1):wall_body(side*18.5,0,width=11,seed=85+(side+1),crown=False)
R=13;spring=26;outer=15.4
for i in range(22):
    a=i/22*math.pi;b=(i+1)/22*math.pi;vv=[]
    for y in (-4.15,4.15):
        vv.extend([(R*math.cos(a),y,spring+R*math.sin(a)),(R*math.cos(b),y,spring+R*math.sin(b)),(outer*math.cos(b),y,spring+outer*math.sin(b)),(outer*math.cos(a),y,spring+outer*math.sin(a))])
    mesh('ArchVoussoir',vv,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],stone,smooth=False)
# Fill above curved extrados with closed wedge strips, not an intersecting solid box.
for i in range(26):
    x0=-13+i;x1=x0+1;z0=spring+math.sqrt(max(0,outer*outer-x0*x0));z1=spring+math.sqrt(max(0,outer*outer-x1*x1))
    vv=[(x0,-3.8,z0),(x1,-3.8,z1),(x1,-3.8,58.5),(x0,-3.8,58.5),(x0,3.8,z0),(x1,3.8,z1),(x1,3.8,58.5),(x0,3.8,58.5)]
    mesh('ArchSpandrel',vv,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],stone,smooth=False)
for side in (-1,1):
    for row in range(9):
        for x in range(-12,13,3):box('GateFaceCourse',(x,side*3.97,42.5+row*1.8),(2.965,.4,1.775),bevel=.045)
box('GateCornice',(0,0,58),(48,8.7,1))
box('GateCap',(0,0,59),(48,10,1))
for side in (-1,1):box('GateParapet',(0,side*4.5,60),(48,.8,1.1))

for variant in range(3):
    start(['GF2_Ground_Loam_32','GF2_Ground_MossBank_32','GF2_Ground_WornTrail_32'][variant],'ground','32x32m closed review patch. Equal zero-height boundaries; no Unity Terrain yet.')
    vv=[];uv=[];cc=[];res=48
    for j in range(res+1):
        for i in range(res+1):
            x=-16+32*i/res;y=-16+32*j/res;edge=(math.sin(i/res*math.pi)*math.sin(j/res*math.pi))**2
            h=(.27*math.sin(x*.28)*math.cos(y*.22)+(.8*math.exp(-((x-4)**2+(y-1)**2)/90) if variant==1 else .08*math.sin(y*.63)))*edge
            vv.append((x,y,h));uv.append((x/8,y/8));m=(.15+.42*math.exp(-((x+4)**2+(y-2)**2)/70)) if variant==1 else .06
            if variant==2:m=0;hue=.85+.22*math.exp(-(x/3)**2);cc.append((hue,hue*.95,hue*.87))
            else:cc.append((1-m*.5,1+m*.16,1-m*.55))
    ff=[]
    for j in range(res):
        for i in range(res):a=j*(res+1)+i;ff.append((a,a+1,a+res+2,a+res+1))
    perimeter=list(range(res+1))+[j*(res+1)+res for j in range(1,res+1)]+[res*(res+1)+i for i in range(res-1,-1,-1)]+[j*(res+1) for j in range(res-1,0,-1)]
    bottom=[]
    for a in perimeter:bottom.append(len(vv));vv.append((vv[a][0],vv[a][1],-1.2));uv.append(uv[a]);cc.append((.65,.65,.65))
    for j,a in enumerate(perimeter):b=perimeter[(j+1)%len(perimeter)];ff.append((a,b,bottom[(j+1)%len(perimeter)],bottom[j]))
    ff.append(tuple(reversed(bottom)));mesh('ClosedSoilPatch',vv,ff,soil,uv,cc)

start('GF2_FallenTrunk_32','prop','32m snapped section of giant trunk; closed splintered end grain.');rng=random.Random(632)
path=[(-16+i*4,0,2.9+.25*math.sin(i*.55)) for i in range(9)];tube('BarkShell',path,[3.1-i*.07 for i in range(9)],sides=40,seed=632)
for side in (-1,1):
    vv=[(side*16.04,0,2.9)];uv=[(.5,.5)]
    for i in range(41):a=i/40*math.tau;rr=(3.1 if side<0 else 2.54)*rng.uniform(.94,1.03);vv.append((side*(16.1+rng.uniform(-.18,.3)),rr*math.cos(a),2.9+rr*math.sin(a)));uv.append((.5+math.cos(a)*.49,.5+math.sin(a)*.49))
    ff=[(0,i+1,i+2) if side>0 else (0,i+2,i+1) for i in range(40)];mesh('SnappedHeartwood',vv,ff,wood,uv,smooth=False)
    for i in range(8):
        a=rng.uniform(0,math.tau);rr=2.2;point=(side*16,rr*math.cos(a),2.9+rr*math.sin(a));tip=(side*rng.uniform(16.5,18.1),point[1]+rng.uniform(-.15,.15),point[2]+rng.uniform(-.15,.15));tube('SplitWoodFiber',[point,tip],[.22,.015],wood,sides=5,seed=i)
for i in range(3):tube('BrokenBranch',[(-8+i*8,0,4.4),(-9+i*8,1.7,5.6),(-10+i*8,2.6,5.9)],[.55,.3,.05],sides=10,seed=i)
for i in range(3):
    start('GF2_Boulder_'+chr(65+i),'prop','Weathered rock, low broad silhouette; moss paint on top.');rng=random.Random(741+i)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1);temp=bpy.context.object;vv=[];cc=[]
    for vtx in temp.data.vertices:
        q=vtx.co;shape=1+.13*math.sin(q.x*7+q.z*4)+.08*math.cos(q.y*9-q.x*5);z=q.z*(1.8+i*.4)*shape+(1.3+i*.25);vv.append((q.x*(3.6+i*.35)*shape,q.y*(2.7+i*.25)*shape,z));m=max(0,q.z)*.4;cc.append((1-m*.5,1-m*.1,1-m*.5))
    ff=[tuple(p.vertices) for p in temp.data.polygons];bpy.data.objects.remove(temp,do_unlink=True);mesh('WeatheredBoulder',vv,ff,stone,colors=cc,smooth=False)

# Weld UV seams without losing loop UVs/paint, consistently orient closed solids.
# Leaf cards are deliberately open, two-sided alpha geometry.
for asset in assets.values():
    for ob in asset['collection'].objects:
        if ob.type!='MESH' or ob.data.materials[0]==leaf:continue
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
for asset in assets.values():
    for ob in asset['collection'].objects:
        if ob.type!='MESH':continue
        colors=np.empty(len(ob.data.loops)*4,dtype=np.float32);ob.data.color_attributes['Color'].data.foreach_get('color',colors)
        uvcheck=np.empty(len(ob.data.loops)*2,dtype=np.float32);ob.data.uv_layers['UVMap'].data.foreach_get('uv',uvcheck)
        assert np.isfinite(colors).all() and colors.min()>=0 and colors.max()<1.6,('Corrupt vertex paint',ob.name,colors.min(),colors.max())
        assert np.isfinite(uvcheck).all() and np.ptp(uvcheck)>.01,('Corrupt UVs',ob.name)

# Asset review scenes use instances, never move the source models away from origin.
studio=bpy.data.collections.new('GF2_StudioOnly');scene.collection.children.link(studio)
def instance(asset,location):
    ob=bpy.data.objects.new('Review_'+asset,None);ob.instance_type='COLLECTION';ob.instance_collection=assets[asset]['collection'];studio.objects.link(ob);ob.location=location;return ob
for i,name in enumerate(assets):instance(name,((i%4)*65,(i//4)*80,0))
# Export each source at origin in a transient scene, restoring review window afterward.
review_window=bpy.context.window;export_scene=bpy.data.scenes.new('GF2_ExportTemporary')
report={'units':'metres, Blender Z-up; FBX Y-up, -Z forward','status':'art review only; no LOD/collider/nav/prefab/map integration','assets':{}}
export_materials={}
def portable_material(mat):
    if mat in export_materials:return export_materials[mat]
    # FBX cannot serialize a Blender vertex-paint multiply graph. Preserve that
    # paint on the mesh, but bind the actual color map directly in the EXPORT
    # material so albedo is not silently dropped. Source review shader untouched.
    clone=mat.copy();clone.name=mat.name+'_FBX'
    if clone.use_nodes:
        bs=clone.node_tree.nodes.get('Principled BSDF')
        tex=next((n for n in clone.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_BaseColor' in n.image.name),None)
        if bs and tex:clone.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    export_materials[mat]=clone;return clone
try:
    review_window.scene=export_scene
    for name,a in assets.items():
        export_scene.collection.children.link(a['collection']);bpy.context.view_layer.update()
        for ob in export_scene.objects:ob.select_set(True)
        meshes=[o for o in a['collection'].objects if o.type=='MESH'];vertices=0;triangles=0;bounds=[]
        for ob in meshes:
            assert all(math.isfinite(c) for vtx in ob.data.vertices for c in vtx.co),ob.name
            ob.data.calc_loop_triangles();vertices+=len(ob.data.vertices);triangles+=len(ob.data.loop_triangles)
            bounds.extend([ob.matrix_world@Vector(p) for p in ob.bound_box])
        lo=[min(p[i] for p in bounds) for i in range(3)];hi=[max(p[i] for p in bounds) for i in range(3)]
        report['assets'][name]={'kind':a['kind'],'description':a['description'],'mesh_objects':len(meshes),'source_vertices':vertices,'source_triangles_before_bevel':triangles,'bounds_min':lo,'bounds_max':hi,'size':[hi[i]-lo[i] for i in range(3)]}
        # Editable source keeps separate branches/blocks; export ONE evaluated mesh
        # per asset with material submeshes, never hundreds of renderers per tree.
        for ob in export_scene.objects:ob.select_set(False)
        deps=bpy.context.evaluated_depsgraph_get();copies=[]
        for ob in meshes:
            data=bpy.data.meshes.new_from_object(ob.evaluated_get(deps),depsgraph=deps);copy=bpy.data.objects.new(name+'_Part',data);export_scene.collection.objects.link(copy);copy.matrix_world=ob.matrix_world.copy();copies.append(copy)
        export_scene.collection.children.unlink(a['collection'])
        for ob in copies:ob.select_set(True)
        bpy.context.view_layer.objects.active=copies[0]
        if len(copies)>1:bpy.ops.object.join()
        combined=bpy.context.object;combined.name=name;combined.data.calc_loop_triangles()
        for i,mat in enumerate(combined.data.materials):combined.data.materials[i]=portable_material(mat)
        report['assets'][name]['export_meshes']=1;report['assets'][name]['export_triangles']=len(combined.data.loop_triangles);report['assets'][name]['export_materials']=len(combined.data.materials)
        bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',bake_anim=False,path_mode='COPY',embed_textures=True,use_custom_props=False)
        bpy.data.objects.remove(combined,do_unlink=True);print('Exported '+name,flush=True)
finally:
    review_window.scene=scene;bpy.data.scenes.remove(export_scene)
# Individual EDIT scenes expose real source meshes (not just gallery instances).
# All source pivots remain zero so exports and subsequent iterations are stable.
for name,a in assets.items():
    edit=bpy.data.scenes.new('GF2_Source_'+name.removeprefix('GF2_'));edit.unit_settings.system='METRIC';edit.unit_settings.scale_length=1;edit.collection.children.link(a['collection'])
bpy.context.window.scene=scene
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=Vector((95,115,35));area.spaces.active.region_3d.view_distance=420
(OUT/'asset-report.json').write_text(json.dumps(report,indent=2))
# This whole process is an OWNED factory-startup background Blender. Save a real
# openable scene (libraries.write creates an append library with no active scene).
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GiantForest2_AssetReview.blend'),compress=True,check_existing=False)
print('GF2_BUILD_COMPLETE '+str(OUT),flush=True)
