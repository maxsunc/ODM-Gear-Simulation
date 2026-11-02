"""Reference-led Titan: cohesive rounded blocks, a continuous sculpted torso and shaggy hair.
Keeps the exact prototype rest skeleton and all 15 detachable skin identities.
Set TITAN_PROJECT_ROOT when executing via MCP. No user objects are cleared.
"""
import ast
import bpy
import bmesh
import math
import random
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(globals().get('TITAN_PROJECT_ROOT',Path(__file__).resolve().parents[2] if '__file__' in globals() else '.'))
COLLECTION='TitanReference_v3'
old=bpy.data.collections.get(COLLECTION)
if old:
    for ob in list(old.objects): bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.collections.remove(old)
collection=bpy.data.collections.new(COLLECTION)
bpy.context.scene.collection.children.link(collection)
prototype=bpy.data.collections.get('TitanPrototype_v1')
reference=next((o for o in prototype.objects if o.type=='ARMATURE'),None) if prototype else None
if reference is None: raise RuntimeError('Original rest skeleton must be loaded first.')
rig=bpy.data.objects.new('TitanRig_Reference',reference.data.copy())
collection.objects.link(rig)
rig.show_in_front=True
rig['art_version']='Reference-led cohesive rounded-block Titan v3'
parts={}; details={}; rng=random.Random(603)
# Reuse pure authoring helpers only; never execute the old anatomical model build.
module=ast.parse((ROOT/'tools/blender/build_detailed_titan.py').read_text(encoding='utf-8'))
functions={node.name:node for node in module.body if isinstance(node,ast.FunctionDef)}
def install(names):
    exec(compile(ast.fix_missing_locations(ast.Module(body=[functions[n] for n in names],type_ignores=[])),str(ROOT/'tools/blender/build_detailed_titan.py'),'exec'),globals())
install(['material'])
def refmat(name,color,roughness=.70,grain=False):
    mat=material(name+'_Reference',color,roughness,grain)
    if grain:
        for node in mat.node_tree.nodes:
            if node.bl_idname=='ShaderNodeTexNoise':
                if node.inputs['Scale'].default_value>50: node.inputs['Scale'].default_value=85
                else: node.inputs['Scale'].default_value=3.5
            if node.bl_idname=='ShaderNodeBump':
                node.inputs['Strength'].default_value=.10
                node.inputs['Distance'].default_value=.006
            if node.bl_idname=='ShaderNodeValToRGB':
                node.color_ramp.elements[0].color=(*(c*.78 for c in color),1)
                node.color_ramp.elements[1].color=(*(min(1,c*1.08) for c in color),1)
    return mat
skin=refmat('Titan_WarmClay',(.53,.29,.12),grain=True)
raised=refmat('Titan_RaisedPlanes',(.56,.32,.15),grain=True)
shade=refmat('Titan_DeepPlanes',(.145,.058,.023))
cap=refmat('Titan_SeamCap',(.29,.135,.053),grain=True)
hair=refmat('Titan_CharcoalHair',(.028,.016,.008),.76)
eye=refmat('Titan_EyeIvory',(.83,.77,.60),.32)
dark=refmat('Titan_EyeDark',(.009,.007,.004),.32)
teeth=refmat('Titan_Teeth',(.66,.56,.37),.46)
iris=refmat('Titan_Iris',(.16,.21,.13),.38)
hair_light=refmat('Titan_HairStrand',(.055,.030,.013),.78)
install(['register','ell','loft','tube','muscle','crease','bevel_box'])

def join(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1: bpy.ops.object.join()
    ob=bpy.context.view_layer.objects.active
    ob.name=name
    return ob


def rounded_block(name,center,size,bevel,part,bone,mat=skin):
    bpy.ops.mesh.primitive_cube_add(size=2,location=center)
    ob=bpy.context.object; ob.name=name
    ob.scale=tuple(s*.5 for s in size)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=ob.modifiers.new('SoftBlockEdges','BEVEL'); mod.width=bevel; mod.segments=6
    mod.harden_normals=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in ob.data.polygons: p.use_smooth=True
    normal=ob.modifiers.new('BroadFlatPlanes','WEIGHTED_NORMAL'); normal.keep_sharp=True; normal.weight=50
    bpy.ops.object.modifier_apply(modifier=normal.name)
    bpy.context.view_layer.update()
    return register(ob,part,bone,mat)


def signed_power(value,power): return math.copysign(abs(value)**power,value)
def gauss(value,width,power=2): return math.exp(-(abs(value)/width)**power)


def sculpted_surface(name,z0,z1,profile,field,part,bone,sides=96,rows=100,exponent=5):
    """One closed mesh with continuous shallow surface relief, not attached muscle balls."""
    verts=[]; faces=[]
    for j in range(rows+1):
        z=z0+(z1-z0)*j/rows
        rx,ry,cy=profile(z)
        for i in range(sides):
            a=i*2*math.pi/sides
            x=rx*signed_power(math.cos(a),2/exponent)
            y=cy+ry*signed_power(math.sin(a),2/exponent)
            if math.sin(a)<0:
                front=min(1,max(0,(-math.sin(a)-.12)/.55))
                y-=field(x,z)*front
            verts.append((x,y,z))
    for row in range(rows):
        for i in range(sides):
            faces.append((row*sides+i,row*sides+(i+1)%sides,(row+1)*sides+(i+1)%sides,(row+1)*sides+i))
    verts.extend([(0,profile(z0)[2],z0),(0,profile(z1)[2],z1)])
    for i in range(sides):
        faces.append((len(verts)-2,(i+1)%sides,i))
        k=rows*sides
        faces.append((len(verts)-1,k+i,k+(i+1)%sides))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); collection.objects.link(ob)
    return register(ob,part,bone,skin)


def torso_profile(z):
    belly=gauss(z-6.58,1.1)
    rx=1.39+.15*belly
    ry=.68+.20*belly
    edge=min(z-5.35,9.48-z)
    if edge<.13:
        rounding=math.sqrt(max(0,1-(1-max(0,edge)/.13)**2))
        rx-=.11*(1-rounding); ry-=.10*(1-rounding)
    return rx,ry,.025


def torso_relief(x,z):
    pecs=sum(.125*gauss(x-s*.66,.64,6)*gauss(z-8.88,.39,6) for s in [-1,1])
    sternum=-.045*gauss(x,.065)*gauss(z-8.85,.43,4)
    under=-.030*gauss(z-(8.38+.035*abs(x)),.045)*gauss(x,1.20,8)
    navel=-.035*gauss(x,.075)*gauss(z-6.62,.075)
    return pecs+sternum+under+navel
sculpted_surface('UnifiedTorso',5.35,9.48,torso_profile,torso_relief,'Torso','Chest')
rounded_block('ShortNeck',(0,.02,9.62),(.79,.80,.50),.18,'Torso','Neck')
for side,s in [('Left',1),('Right',-1)]:
    upper=side+'UpperArm'; lower=side+'LowerArm'; hand=side+'Hand'
    rounded_block('UpperArm',(2.42*s,0,9.03),(1.98,1.02,1.08),.11,upper,upper)
    rounded_block('LowerArm',(4.03*s,0,9.03),(1.48,1.00,1.06),.10,lower,lower)
    rounded_block('BlockHand',(5.11*s,-.005,9.03),(.88,1.00,1.04),.10,hand,hand)
    # The reference uses mitten-like block hands; indicate fingers as shallow lines.
    for k in range(3):
        z=8.77+k*.24
        crease('FingerLine',[(5.17*s,-.512,z),(5.35*s,-.514,z),(5.47*s,-.49,z)],.006,hand,hand,cap)
    thigh=side+'UpperLeg'; shin=side+'LowerLeg'; foot=side+'Foot'
    rounded_block('UpperLeg',(.70*s,-.025,4.35),(1.17,1.13,2.72),.095,thigh,thigh)
    rounded_block('LowerLeg',(.70*s,0,1.84),(1.15,1.11,2.57),.09,shin,shin)
    rounded_block('FlatFoot',(.70*s,-.20,.36),(1.17,1.44,.70),.08,foot,foot)

# Organic Titan face on a restrained rounded head, with no floating cheek/brow blobs.
def head_profile(z):
    t=(z-10.22)/(11.82-10.22)
    rx=.75-.15*gauss(t,.14)-.32*gauss(t-1,.13)
    ry=.59-.11*gauss(t,.14)-.25*gauss(t-1,.13)
    return rx,ry,.015


def head_relief(x,z):
    nose=.13*gauss(x,.10)*gauss(z-10.87,.30)+.13*gauss(x,.14)*gauss(z-10.67,.11)
    cheeks=.025*sum(gauss(x-s*.47,.21)*gauss(z-10.63,.26) for s in [-1,1])
    orbit=0
    for s in [-1,1]:
        r=math.sqrt(((x-s*.315)/.225)**2+((z-11.045)/.20)**2)
        orbit+=.065*gauss(r-1,.23)-.035*gauss(r,.78,4)
    brow=.035*sum(gauss(x-s*.31,.28,4)*gauss(z-11.24,.07) for s in [-1,1])
    mouth=.04*gauss(x,.49,6)*gauss(z-10.40,.16,4)
    return nose+cheeks+orbit+brow+mouth
head=sculpted_surface('ContinuousFace',10.22,11.82,head_profile,head_relief,'Head','Head',rows=90,exponent=3.1)
# A proper narrow mouth opening, with closed oral lining behind it.
bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=(0,-.59,10.36))
cutter=bpy.context.object; cutter.scale=(.535,.29,.115)
bpy.context.view_layer.update(); bpy.context.view_layer.objects.active=head
mod=head.modifiers.new('NarrowGrin','BOOLEAN'); mod.operation='DIFFERENCE'; mod.object=cutter
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cutter,do_unlink=True)
rounded_block('Chin',(0,-.025,10.10),(1.08,1.00,.32),.15,'Jaw','Jaw')
ell('MouthLining',(0,-.53,10.355),(.50,.15,.105),'Head','Head',shade,detail=True)
for s in [-1,1]:
    ell('EyeSocket',(.315*s,-.574,11.045),(.205,.026,.177),'Head','Head',cap,detail=True)
    ell('Eye',(.315*s,-.578,11.045),(.164,.105,.139),'Head','Head',eye,detail=True)
    ell('Iris',(.315*s,-.677,11.045),(.062,.013,.074),'Head','Head',iris,detail=True)
    ell('Pupil',(.315*s,-.690,11.045),(.025,.006,.045),'Head','Head',dark,detail=True)
    for upper_lid in [True,False]:
        curve=[]
        for j in range(17):
            t=j/16
            curve.append(((.315+(t-.5)*.37)*s,-.598-.085*math.sin(t*math.pi),11.045+(1 if upper_lid else -1)*.142*math.sin(t*math.pi)))
        crease('IntegratedLid',curve,.019,'Head','Head',skin)
    crease('Brow',[(.13*s,-.608,11.25),(.30*s,-.646,11.28),(.48*s,-.61,11.25)],.009,'Head','Head',hair)
    ell('Nostril',(.077*s,-.777,10.65),(.024,.010,.017),'Head','Head',shade,detail=True)
    rounded_block('Ear',(.755*s,.015,10.87),(.18,.23,.36),.085,'Head','Head')
    crease('EarFold',[(.846*s,-.04,10.98),(.85*s,-.07,10.87),(.84*s,-.03,10.75)],.014,'Head','Head',cap)
for upper_row in [True,False]:
    part,bone=('Head','Head') if upper_row else ('Jaw','Jaw')
    for j in range(12):
        x=(j-5.5)*.078
        y=-.695+.10*(abs(x)/.47)**2
        z=(10.405 if upper_row else 10.306)+.025*(abs(x)/.47)**2
        bevel_box('SmallTooth',(x,y,z),(.033,.036,.049 if upper_row else .031),part,bone,teeth,.010)
    lip=[]
    for j in range(25):
        x=(j/24-.5)*1.03
        lip.append((x,-.680+.10*(abs(x)/.515)**2,(10.466 if upper_row else 10.267)+.020*(abs(x)/.515)**2))
    crease('ThinLip',lip,.014,part,bone,cap)

# One continuous closed hair shell; a few broad sweeps define the parting.
verts=[]; faces=[]; sides=96; rows=20
for inner in [False,True]:
    for j in range(rows):
        t=.025+.975*j/(rows-1)
        for i in range(sides):
            a=i*2*math.pi/sides
            front=max(0,-math.sin(a))
            bottom=10.35+1.13*front**5
            z=11.98-(11.98-bottom)*t-(.045 if inner else 0)
            radius=min(1,math.sqrt(max(.001,(11.985-z)/.35)))
            rx=.82-(.045 if inner else 0); ry=.69-(.045 if inner else 0)
            x=rx*math.cos(a)*radius
            y=.055+ry*math.sin(a)*radius
            verts.append((x,y,z))
    offset=(sides*rows) if inner else 0
    for row in range(rows-1):
        for i in range(sides):
            f=(offset+row*sides+i,offset+row*sides+(i+1)%sides,offset+(row+1)*sides+(i+1)%sides,offset+(row+1)*sides+i)
            faces.append(tuple(reversed(f)) if inner else f)
# Close top and lower rim; no open cards/alpha hair needed.
verts.extend([(0,.055,11.98),(0,.055,11.935)])
for i in range(sides):
    faces.append((len(verts)-2,i,(i+1)%sides))
    k=sides*rows
    faces.append((len(verts)-1,k+(i+1)%sides,k+i))
    a=(rows-1)*sides+i; b=(rows-1)*sides+(i+1)%sides
    faces.append((a,b,k+b,k+a))
mesh=bpy.data.meshes.new('UnifiedHair'); mesh.from_pydata(verts,[],faces); mesh.update()
bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
ob=bpy.data.objects.new('UnifiedHair',mesh); collection.objects.link(ob)
register(ob,'Head','Head',hair,True)

def bezier(p0,p1,p2,p3,t):
    return (1-t)**3*Vector(p0)+3*(1-t)**2*t*Vector(p1)+3*(1-t)*t*t*Vector(p2)+t**3*Vector(p3)
for s in [-1,1]:
    for j in range(6):
        f=j/5
        p0=(-.12,-.13,11.95)
        p1=(s*(.24+.12*f),-.44,11.91)
        p2=(s*(.55+.22*f),-.64,11.54-.23*f)
        p3=(s*(.39+.47*f),-.64+.08*f,11.24-.57*f)
        points=[bezier(p0,p1,p2,p3,i/16) for i in range(17)]
        width=[.008+(.10+.045*f)*math.sin(math.pi*i/16)**.65 for i in range(17)]
        depth=[.010+.025*math.sin(math.pi*i/16) for i in range(17)]
        tube('SweptBang',points,width,depth,'Head','Head',hair,12,True)
        if j%2==0:
            crease('HairFineStrand',[(p.x,p.y-.028,p.z) for p in points[2:-2]],.0035,'Head','Head',hair_light)
    for j in range(4):
        x=s*(.72+j*.027)
        points=[(x,.10,11.55),(s*(.85+j*.02),-.02,11.05),(s*(.81+j*.06),-.08,10.50-j*.065)]
        tube('TempleTuft',points,[.08,.09,.004],[.025,.035,.003],'Head','Head',hair,12,True)
# Fine flow lines across the continuous cap, not separate thick clumps.
for j in range(22):
    a=j/22*2*math.pi
    front=max(0,-math.sin(a)); bottom=10.35+1.13*front**5
    curve=[]
    for k in range(12):
        t=.10+.83*k/11
        z=11.984-(11.98-bottom)*t
        r=min(1,math.sqrt(max(.001,(11.989-z)/.35)))
        curve.append((.824*math.cos(a)*r,.055+.695*math.sin(a)*r,z))
    crease('CapHairLine',curve,.0028,'Head','Head',hair_light)

# Finish without a voxel muscle union: the silhouette is already a unified surface.
meshes=[]
for part,objects in parts.items():
    ob=join(objects,'TitanReference_'+part)
    if part in details: ob=join([ob]+details[part],'TitanReference_'+part)
    # Seat the wider head close to the shoulders, as in the reference, rather
    # than leaving the old anatomical pass's long exposed neck. Rest bones stay exact.
    if part in {'Head','Jaw'}:
        for v in ob.data.vertices:
            v.co.x*=1.12
            v.co.z-=.38
    ob.vertex_groups.clear()
    if part=='Torso':
        centers=[('Hips',5.95),('Spine',7.2),('Chest',8.65),('Neck',9.9)]
        groups={name:ob.vertex_groups.new(name=name) for name,_ in centers}
        for v in ob.data.vertices:
            z=v.co.z
            if z<=5.95: groups['Hips'].add([v.index],1,'REPLACE')
            elif z>=9.9: groups['Neck'].add([v.index],1,'REPLACE')
            else:
                for (a,za),(b,zb) in zip(centers,centers[1:]):
                    if za<=z<=zb:
                        t=(z-za)/(zb-za); groups[a].add([v.index],1-t,'REPLACE'); groups[b].add([v.index],t,'REPLACE'); break
    else:
        group=ob.vertex_groups.new(name=part); group.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    ob.parent=rig
    mod=ob.modifiers.new('TitanSkin','ARMATURE'); mod.object=rig
    ob['part_id']=part
    meshes.append(ob)
triangles=0
for ob in meshes:
    ob.data.calc_loop_triangles(); triangles+=len(ob.data.loop_triangles)
    bm=bmesh.new(); bm.from_mesh(ob.data); boundary=sum(e.is_boundary for e in bm.edges); bm.free()
    if boundary: raise RuntimeError(ob.name+' open edges: '+str(boundary))
assert len(meshes)==15 and len(rig.data.bones)==22
for bone in rig.data.bones:
    assert all(abs(bone.matrix_local[i][j]-reference.data.bones[bone.name].matrix_local[i][j])<1e-6 for i in range(4) for j in range(4))
for name in ['TitanPrototype_v1','TitanDetailed_v2']:
    col=bpy.data.collections.get(name)
    if col: col.hide_viewport=True; col.hide_render=True
for window in bpy.context.window_manager.windows:
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=(0,0,6)
            area.spaces.active.region_3d.view_distance=19
            area.spaces.active.region_3d.view_rotation=Vector((8,-25,5)).to_track_quat('Z','Y')
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
print('REFERENCE_TITAN_GEOMETRY_OK',len(meshes),'skins',len(rig.data.bones),'identical rest bones',triangles,'triangles, all meshes closed')
