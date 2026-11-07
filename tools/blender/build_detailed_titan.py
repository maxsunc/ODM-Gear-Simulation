"""Detailed anatomical Titan, compatible with the existing 22-bone prototype rig.
Builds only TitanDetailed_v2; keeps the old source collection and user objects.
Run in Blender 4.5: --python this_file -- ArtSource/Titan/Detailed
Geometry and material baking are separate so either stage can be repeated.
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector, Matrix

OUT = globals().get('TITAN_OUTPUT_DIR')
if not OUT:
    OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.abspath('ArtSource/Titan/Detailed')
os.makedirs(OUT, exist_ok=True)
COLLECTION = 'TitanDetailed_v2'
rng = random.Random(1729)
old = bpy.data.collections.get(COLLECTION)
if old:
    for ob in list(old.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.collections.remove(old)
collection = bpy.data.collections.new(COLLECTION)
bpy.context.scene.collection.children.link(collection)
prototype = bpy.data.collections.get('TitanPrototype_v1')
reference = next((o for o in prototype.objects if o.type == 'ARMATURE'), None) if prototype else None
if reference is None:
    raise RuntimeError('Build/open the original Titan source first; its exact rest skeleton is required.')
# Clone REST data only, not animated pose transforms. Bone names, rolls and lengths stay exact.
rig = bpy.data.objects.new('TitanRig_Detailed', reference.data.copy())
collection.objects.link(rig)
rig.show_in_front = True
rig['art_version'] = 'Detailed anatomical pass v2; same 22 rest bones as blockout'


def material(name, color, roughness=.62, skin_detail=False):
    key = name + '_Detailed'
    mat = bpy.data.materials.get(key) or bpy.data.materials.new(key)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    mat.node_tree.links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    if skin_detail:
        coord = nodes.new('ShaderNodeTexCoord')
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 8
        noise.inputs['Detail'].default_value = 4
        noise.inputs['Roughness'].default_value = .72
        mat.node_tree.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        ramp = nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = .18
        ramp.color_ramp.elements[0].color = (color[0]*.68, color[1]*.62, color[2]*.66, 1)
        ramp.color_ramp.elements[1].position = .8
        ramp.color_ramp.elements[1].color = (min(1,color[0]*1.16), min(1,color[1]*1.08), min(1,color[2]*1.02), 1)
        mat.node_tree.links.new(noise.outputs['Fac'], ramp.inputs[0])
        mat.node_tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
        pores = nodes.new('ShaderNodeTexNoise')
        pores.inputs['Scale'].default_value = 110
        pores.inputs['Detail'].default_value = 2
        mat.node_tree.links.new(coord.outputs['Object'], pores.inputs['Vector'])
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .24
        bump.inputs['Distance'].default_value = .014
        mat.node_tree.links.new(pores.outputs['Fac'], bump.inputs['Height'])
        mat.node_tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        rough = nodes.new('ShaderNodeMapRange')
        rough.inputs['From Min'].default_value = 0
        rough.inputs['From Max'].default_value = 1
        rough.inputs['To Min'].default_value = .48
        rough.inputs['To Max'].default_value = .76
        mat.node_tree.links.new(noise.outputs['Fac'], rough.inputs['Value'])
        mat.node_tree.links.new(rough.outputs[0], bsdf.inputs['Roughness'])
    return mat

skin = material('Titan_WarmClay', (.39,.235,.16), skin_detail=True)
raised = material('Titan_RaisedPlanes', (.46,.29,.205), skin_detail=True)
shade = material('Titan_DeepPlanes', (.105,.036,.026))
cap = material('Titan_SeamCap', (.25,.105,.075), skin_detail=True)
hair = material('Titan_CharcoalHair', (.022,.016,.012), .49)
eye = material('Titan_EyeIvory', (.76,.69,.52), .22)
dark = material('Titan_EyeDark', (.012,.009,.006), .16)
teeth = material('Titan_Teeth', (.56,.45,.29), .34)
parts = {}
details = {}


def register(ob, part, bone, mat, detail=False):
    for col in list(ob.users_collection):
        col.objects.unlink(ob)
    collection.objects.link(ob)
    if ob.type == 'MESH':
        ob.data.transform(ob.matrix_world)
        ob.matrix_world = Matrix.Identity(4)
        ob.data.materials.append(mat)
        group = ob.vertex_groups.new(name=bone)
        group.add(list(range(len(ob.data.vertices))), 1, 'REPLACE')
        for poly in ob.data.polygons:
            poly.use_smooth = True
    (details if detail else parts).setdefault(part, []).append(ob)
    return ob


def ell(name, center, radii, part, bone, mat=skin, rotation=None, detail=False):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=14, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.scale = radii
    if rotation:
        ob.rotation_euler = rotation
    bpy.context.view_layer.update()
    return register(ob, part, bone, mat, detail)


def loft(name, rings, part, bone, mat=skin, sides=32):
    # rings: (x,y,z, radius_x, radius_y), horizontal closed surface.
    verts, faces = [], []
    for x,y,z,rx,ry in rings:
        for i in range(sides):
            angle = i * 2 * math.pi / sides
            verts.append((x+rx*math.cos(angle), y+ry*math.sin(angle), z))
    for row in range(len(rings)-1):
        for i in range(sides):
            faces.append((row*sides+i,row*sides+(i+1)%sides,(row+1)*sides+(i+1)%sides,(row+1)*sides+i))
    verts.extend([rings[0][:3], rings[-1][:3]])
    for i in range(sides):
        faces.append((len(verts)-2,(i+1)%sides,i))
        j=(len(rings)-1)*sides
        faces.append((len(verts)-1,j+i,j+(i+1)%sides))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    ob=bpy.data.objects.new(name,mesh)
    collection.objects.link(ob)
    return register(ob,part,bone,mat)


def tube(name, points, widths, depths, part, bone, mat=skin, sides=12, detail=False, grooves=0):
    points=[Vector(p) for p in points]
    verts,faces=[],[]
    previous_u=None
    for j,p in enumerate(points):
        tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
        ref=Vector((0,1,0)) if abs(tangent.y)<.94 else Vector((1,0,0))
        u=(previous_u-tangent*previous_u.dot(tangent)).normalized() if previous_u is not None else tangent.cross(ref).normalized()
        v=tangent.cross(u).normalized()
        previous_u=u
        for i in range(sides):
            angle=i*2*math.pi/sides
            rib=1+(.065*math.cos(angle*grooves) if grooves else 0)
            verts.append(p+u*math.cos(angle)*widths[j]*rib+v*math.sin(angle)*depths[j]*rib)
    for j in range(len(points)-1):
        for i in range(sides):
            faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
    verts.extend([points[0],points[-1]])
    for i in range(sides):
        faces.append((len(verts)-2,(i+1)%sides,i))
        j=(len(points)-1)*sides
        faces.append((len(verts)-1,j+i,j+(i+1)%sides))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); collection.objects.link(ob)
    return register(ob,part,bone,mat,detail)


def muscle(name, start, end, width, depth, part, bone, mat=skin):
    a,b=Vector(start),Vector(end)
    profile=[.48,.80,1,.96,.76,.50,.34]
    return tube(name,[a.lerp(b,i/6) for i in range(7)], [width*r for r in profile], [depth*r for r in profile],part,bone,mat,20)


def crease(name, points, radius, part, bone, mat=raised):
    n=len(points)
    profile=[radius*(.28+.72*math.sin(math.pi*i/(n-1))**.45) for i in range(n)]
    return tube(name,points,profile,profile,part,bone,mat,8,True)


def bevel_box(name, center, scale, part, bone, mat, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=2, location=center)
    ob=bpy.context.object; ob.name=name; ob.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=ob.modifiers.new('SoftEdges','BEVEL'); mod.width=bevel; mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.update()
    return register(ob,part,bone,mat,True)

# Continuous tapered torso. The added muscle volumes are fused, not loose spheres.
loft('TorsoCore',[(0,0,5.10,.30,.30),(0,0,5.35,.75,.48),(0,0,5.75,1.02,.62),(0,0,6.15,1.00,.59),
                 (0,.02,6.55,.82,.48),(0,.03,7.0,.82,.48),(0,.04,7.55,1.03,.55),
                 (0,.05,8.10,1.36,.67),(0,.05,8.65,1.56,.73),(0,.08,9.08,1.43,.62),
                 (0,.08,9.35,.95,.47)],'Torso','Chest')
muscle('NeckColumn',(0,.03,9.1),(0,.01,10.3),.47,.44,'Torso','Neck')
for side,s in [('Left',1),('Right',-1)]:
    ell('Pectoralis',(.76*s,-.63,8.66),(.82,.22,.32),'Torso','Chest',raised,rotation=(0,s*.10,0))
    muscle('Clavicle',(.12*s,-.46,9.15),(1.58*s,-.24,9.17),.105,.105,'Torso','Chest',raised)
    muscle('Trapezius',(.21*s,.20,9.58),(1.65*s,.18,9.08),.30,.32,'Torso','Chest')
    muscle('NeckTendon',(.18*s,-.26,10.21),(.57*s,-.35,9.32),.115,.13,'Torso','Neck',raised)
    ell('Latissimus',(1.03*s,.42,8.18),(.52,.36,.92),'Torso','Chest')
    ell('Scapula',(.84*s,.61,8.77),(.57,.20,.50),'Torso','Chest',raised)
    muscle('BackErector',(.22*s,.49,6.15),(.25*s,.66,8.65),.14,.16,'Torso','Spine')
    for i in range(3):
        ell('RectusAbdominis',(.31*s,-.50-i*.012,7.97-i*.48),(.30-i*.012,.13,.24),'Torso','Spine',raised)
    for i in range(5):
        ell('Serratus',((1.12-i*.08)*s,-.43,8.34-i*.19),(.26,.10,.10),'Torso','Chest',skin,rotation=(0,s*.45,0))
    muscle('ExternalOblique',(.88*s,-.25,7.72),(.69*s,-.39,6.60),.19,.18,'Torso','Spine')
    muscle('IliacCrest',(.95*s,-.26,6.26),(.28*s,-.49,5.85),.125,.15,'Torso','Hips',raised)
    ell('Glute',(.50*s,.44,5.89),(.53,.26,.59),'Torso','Hips')
    ell('ShoulderSocket',(1.68*s,0,9.03),(.40,.43,.43),'Torso','Chest',cap)
    ell('HipSocket',(.63*s,0,5.54),(.48,.44,.40),'Torso','Hips',cap)
    upper=side+'UpperArm'; lower=side+'LowerArm'; hand=side+'Hand'
    muscle('UpperArmCore',(1.78*s,0,9.03),(3.35*s,0,9.03),.40,.43,upper,upper)
    ell('Deltoid',(1.99*s,0,9.03),(.54,.53,.53),upper,upper,raised)
    muscle('Biceps',(2.08*s,-.18,9.03),(3.17*s,-.12,9.05),.36,.37,upper,upper,raised)
    muscle('Triceps',(2.03*s,.19,9.05),(3.29*s,.12,9.04),.35,.36,upper,upper)
    muscle('Brachialis',(2.65*s,-.03,8.72),(3.31*s,0,8.84),.14,.19,upper,upper)
    ell('Elbow',(3.35*s,.045,9.03),(.28,.31,.30),lower,lower)
    muscle('ForearmCore',(3.35*s,0,9.03),(4.74*s,0,9.03),.37,.34,lower,lower)
    muscle('Brachioradialis',(3.43*s,-.18,9.20),(4.66*s,-.08,9.16),.23,.21,lower,lower,raised)
    muscle('ForearmFlexor',(3.48*s,-.19,8.88),(4.58*s,-.05,8.96),.20,.18,lower,lower)
    for z in [8.85,9.03,9.21]:
        muscle('WristTendon',(4.22*s,-.15,z),(4.91*s,-.11,z),.035,.045,lower,lower,raised)
    ell('Palm',(5.13*s,0,9.03),(.48,.205,.40),hand,hand)
    ell('ThenarPad',(4.99*s,-.11,8.78),(.24,.12,.25),hand,hand,raised)
    # Articulated-looking phalanges, nails and creases; still weighted to Hand.
    for i in range(4):
        z=8.72+i*.215
        length=.57-abs(i-1.2)*.065
        points=[(5.41*s,-.015,z),((5.41+length*.38)*s,-.035,z),
                ((5.41+length*.72)*s,-.075,z-.025),((5.41+length)*s,-.12,z-.055)]
        tube('Finger',points,[.105,.103,.087,.065],[.105,.102,.085,.065],hand,hand,skin,16,True)
        for f in [.38,.72]:
            x=(5.41+length*f)*s
            crease('FingerFold',[(x,-.126,z-.07),(x,-.142,z),(x,-.126,z+.07)],.009,hand,hand,cap)
        bevel_box('Fingernail',((5.41+length*.83)*s,.014,z-.033),(.095,.018,.063),hand,hand,teeth,.015)
        ell('Knuckle',(5.43*s,.035,z),(.13,.13,.12),hand,hand,raised,detail=True)
    tube('Thumb',[(4.98*s,0,8.75),(5.15*s,-.03,8.48),(5.42*s,-.10,8.39)],
         [.17,.14,.095],[.15,.13,.09],hand,hand,skin,16,True)
    for n in range(2):
        crease('PalmCrease',[(4.98*s,-.199,8.82+n*.18),(5.15*s,-.218,8.83+n*.18),(5.37*s,-.19,8.91+n*.18)],.009,hand,hand,cap)
    crease('ForearmVein',[(3.62*s,-.32,9.07),(3.91*s,-.32,9.12),(4.22*s,-.24,9.08),(4.58*s,-.16,9.11)],.016,lower,lower)
    thigh=side+'UpperLeg'; shin=side+'LowerLeg'; foot=side+'Foot'
    muscle('ThighCore',(.63*s,0,5.65),(.68*s,-.08,3.05),.56,.57,thigh,thigh)
    muscle('RectusFemoris',(.57*s,-.36,5.37),(.65*s,-.37,3.28),.29,.25,thigh,thigh,raised)
    muscle('VastusLateralis',(.95*s,-.16,5.18),(.89*s,-.22,3.37),.28,.33,thigh,thigh)
    muscle('VastusMedialis',(.35*s,-.22,4.43),(.45*s,-.29,3.17),.23,.26,thigh,thigh,raised)
    muscle('Hamstring',(.65*s,.36,5.22),(.66*s,.24,3.13),.38,.30,thigh,thigh)
    muscle('Adductor',(.30*s,.02,5.36),(.41*s,-.02,3.68),.20,.23,thigh,thigh)
    ell('Patella',(.68*s,-.30,3.05),(.29,.20,.33),shin,shin,raised)
    muscle('ShinCore',(.68*s,-.08,3.05),(.70*s,0,.65),.29,.30,shin,shin)
    muscle('Gastrocnemius',(.70*s,.21,2.68),(.70*s,.16,1.24),.35,.36,shin,shin,raised)
    muscle('TibialisAnterior',(.79*s,-.19,2.82),(.75*s,-.13,.88),.14,.15,shin,shin,raised)
    muscle('Achilles',(.70*s,.24,1.61),(.70*s,.13,.61),.10,.11,shin,shin,raised)
    ell('AnkleInner',(.48*s,0,.72),(.12,.14,.15),foot,foot,raised)
    ell('AnkleOuter',(.90*s,0,.67),(.11,.15,.14),foot,foot)
    ell('Foot',(.70*s,-.32,.37),(.40,.78,.34),foot,foot)
    muscle('FootArch',(.66*s,.03,.51),(.66*s,-.77,.43),.24,.20,foot,foot,raised)
    for i in range(5):
        x=(.39+i*.155)*s
        ell('Toe',(x,-1.03,.23),(.115,.285-i*.017,.145),foot,foot,skin,detail=True)
        bevel_box('Toenail',(x,-1.17+i*.016,.35),(.055,.07-i*.005,.009),foot,foot,teeth,.015)
    # Subtle healed marks, not painted-on graphic wounds.
    crease('ShoulderScar',[(2.05*s,-.49,9.14),(2.17*s,-.51,9.03),(2.30*s,-.43,8.88)],.014,upper,upper)

# A narrower, sculpted face with real mouth opening and a separate angular mandible.
loft('Cranium',[(0,.02,10.21,.48,.41),(0,.03,10.47,.62,.49),(0,.045,10.76,.65,.55),
               (0,.05,11.04,.69,.59),(0,.055,11.34,.73,.61),(0,.075,11.59,.68,.55),
               (0,.08,11.79,.46,.38),(0,.08,11.88,.12,.12)],'Head','Head')
for s in [-1,1]:
    ell('BrowRidge',(.31*s,-.53,11.20),(.34,.18,.11),'Head','Head',skin,rotation=(0,s*.12,0))
    ell('ZygomaticArch',(.50*s,-.42,10.79),(.18,.17,.27),'Head','Head',raised)
    muscle('Masseter',(.57*s,-.16,10.77),(.48*s,-.25,10.25),.18,.24,'Head','Head')
    ell('NoseWing',(.115*s,-.77,10.65),(.10,.12,.095),'Head','Head')
muscle('NoseBridge',(0,-.54,11.17),(0,-.80,10.70),.11,.14,'Head','Head',raised)
ell('NoseTip',(0,-.84,10.70),(.145,.17,.135),'Head','Head',raised)
loft('Mandible',[(0,-.12,9.97,.30,.27),(0,-.14,10.04,.46,.37),
                 (0,-.14,10.16,.55,.43),(0,-.08,10.31,.57,.38)],'Jaw','Jaw')
for s in [-1,1]:
    muscle('JawAngle',(.46*s,-.08,10.28),(.31*s,-.36,10.04),.14,.16,'Jaw','Jaw',raised)
    ell('Ear',(.73*s,.055,10.91),(.13,.18,.29),'Head','Head')
    ell('EarConcha',(.845*s,.015,10.91),(.018,.092,.16),'Head','Head',cap,detail=True)
    curve=[(.833*s,.045+.125*math.cos(i*2*math.pi/24),10.91+.23*math.sin(i*2*math.pi/24)) for i in range(25)]
    tube('EarHelix',curve,[.026]*25,[.026]*25,'Head','Head',skin,10,True)
    ell('EyeSocket',(.315*s,-.601,11.045),(.235,.09,.163),'Head','Head',shade,detail=True)
    ell('Sclera',(.315*s,-.678,11.055),(.180,.103,.083),'Head','Head',eye,detail=True)
    ell('Iris',(.315*s,-.786,11.055),(.052,.018,.058),'Head','Head',cap,detail=True)
    ell('Pupil',(.315*s,-.801,11.055),(.025,.009,.038),'Head','Head',dark,detail=True)
    for upper in [True,False]:
        points=[]
        for i in range(17):
            t=i/16; x=(.315+(t-.5)*.40)*s
            z=11.055+(1 if upper else -1)*math.sin(t*math.pi)*(.087 if upper else .066)
            points.append((x,-.755-.025*math.sin(t*math.pi),z))
        crease('Eyelid',points,.028 if upper else .021,'Head','Head',skin)
    crease('BrowHair',[(.10*s,-.685,11.22),(.29*s,-.716,11.25),(.48*s,-.68,11.21),(.60*s,-.57,11.17)],.012,'Head','Head',hair)
    crease('UnderEye',[(.16*s,-.68,10.88),(.34*s,-.69,10.85),(.51*s,-.60,10.91)],.013,'Head','Head',cap)
    crease('NasoLabial',[(.15*s,-.858,10.65),(.29*s,-.78,10.56),(.44*s,-.65,10.39),(.53*s,-.55,10.30)],.017,'Head','Head',raised)
    ell('Nostril',(.10*s,-.947,10.63),(.040,.014,.025),'Head','Head',shade,detail=True)
# All inner surfaces are closed. Teeth are rounded boxes, not strings of spheres.
ell('OralCavity',(0,-.39,10.35),(.53,.32,.22),'Head','Head',shade,detail=True)
for upper in [True,False]:
    part,bone=('Head','Head') if upper else ('Jaw','Jaw')
    gum=[]
    for i in range(25):
        x=(i/24-.5)*1.03
        y=-.80+.17*(abs(x)/.515)**2
        z=(10.535 if upper else 10.195)+.045*(abs(x)/.515)**2
        gum.append((x,y,z))
    tube('GumLine',gum,[.043]*25,[.047]*25,part,bone,cap,12,True)
    for i in range(12):
        x=(i-5.5)*.083
        y=-.835+.17*(abs(x)/.50)**2
        z=(10.43 if upper else 10.265)+.035*(abs(x)/.50)**2
        length=.075 if upper else .055
        if i in [2,9]: length*=1.15
        bevel_box('Tooth',(x,y,z),(.035,.057,length),part,bone,teeth,.015)
    # Lip rims frame the opening but do not cover the Titan grin.
    crease('LipRim',[(p[0],p[1]-.015,p[2]+(.025 if upper else -.025)) for p in gum],.032,part,bone,skin)
crease('ForeheadFold',[(-.37,-.52,11.48),(0,-.59,11.51),(.37,-.52,11.48)],.017,'Head','Head',raised)
crease('TempleScar',[(.56,-.48,11.44),(.62,-.51,11.24),(.58,-.48,11.07)],.013,'Head','Head',raised)
# Hair has a compact scalp base with many layered, pointed, grooved locks.
ell('Scalp',(0,.11,11.68),(.70,.52,.22),'Head','Head',hair,detail=True)
for i in range(27):
    x=(i/26-.5)*1.40
    root=(x*.82,.10,11.93-.11*abs(x))
    end=(x*1.04,-.56+.12*abs(x),11.29+.12*abs(x)+rng.uniform(-.14,.10))
    mid=Vector(root).lerp(Vector(end),.52)+Vector((.07*math.sin(i),-.15,.23))
    points=[Vector(root).lerp(mid,t/3) for t in range(3)]+[mid.lerp(Vector(end),t/4) for t in range(5)]
    profile=[.45,.8,1,1,.9,.7,.4,.035]
    w=.065+rng.random()*.025
    tube('FringeLock',points,[w*p for p in profile],[.042*p for p in profile],'Head','Head',hair,16,True,6)
for i in range(23):
    x=(i/22-.5)*1.32
    points=[(x*.84,.38,11.65),(x*.95,.19,11.90),(x,-.04,11.99-abs(x)*.13),
            (x*1.06,-.29,11.83-abs(x)*.13),(x*1.10,-.48,11.54+rng.uniform(-.08,.08))]
    tube('CrownLock',points,[.025,.067,.075,.058,.003],[.025,.040,.044,.035,.002],'Head','Head',hair,16,True,6)
for s in [-1,1]:
    for i in range(15):
        t=i/14
        start=(s*(.40+.27*t),.10+.30*t,11.80-.20*t)
        end=(s*(.72+.08*math.sin(i)),.02+.48*t,10.76-.35*t+rng.uniform(-.08,.08))
        middle=Vector(start).lerp(Vector(end),.48)+Vector((s*.14,.01,.04))
        points=[Vector(start).lerp(middle,j/3) for j in range(3)]+[middle.lerp(Vector(end),j/4) for j in range(5)]
        profile=[.40,.8,1,.94,.85,.67,.38,.03]
        tube('SideBackLock',points,[.074*p for p in profile],[.046*p for p in profile],'Head','Head',hair,16,True,6)
# Nape stays visible below the back hairline.
ell('NapeLandmark',(0,.43,10.02),(.21,.075,.24),'Torso','Neck',cap,detail=True)


def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    ob=bpy.context.object; ob.name=name
    return ob

meshes=[]
for part, objects in parts.items():
    ob=join(objects,'TitanDetailed_'+part)
    remesh=ob.modifiers.new('AnatomicalSculptUnion','REMESH')
    remesh.mode='VOXEL'
    remesh.voxel_size=.025 if part in {'Head','Jaw'} else .028 if part.endswith('Hand') else .035
    remesh.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth=ob.modifiers.new('SculptRelax','SMOOTH'); smooth.factor=.42; smooth.iterations=3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    if part == 'Head':
        # A true inset mouth opening in the skin, with a separate closed inner mouth.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=(0,-.63,10.37))
        cutter=bpy.context.object; cutter.scale=(.565,.40,.175)
        bpy.context.view_layer.update()
        bpy.context.view_layer.objects.active=ob
        boolean=ob.modifiers.new('MouthOpening','BOOLEAN'); boolean.operation='DIFFERENCE'; boolean.object=cutter
        bpy.ops.object.modifier_apply(modifier=boolean.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
    decimate=ob.modifiers.new('GameSurface','DECIMATE'); decimate.ratio=.36 if part not in {'Head','Jaw'} else .55
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    ob.vertex_groups.clear()
    if part == 'Torso':
        centers=[('Hips',5.95),('Spine',7.2),('Chest',8.65),('Neck',9.9)]
        groups={name:ob.vertex_groups.new(name=name) for name,_ in centers}
        for vertex in ob.data.vertices:
            z=vertex.co.z
            if z<=centers[0][1]: groups['Hips'].add([vertex.index],1,'REPLACE')
            elif z>=centers[-1][1]: groups['Neck'].add([vertex.index],1,'REPLACE')
            else:
                for (a,za),(b,zb) in zip(centers,centers[1:]):
                    if za<=z<=zb:
                        t=(z-za)/(zb-za)
                        groups[a].add([vertex.index],1-t,'REPLACE'); groups[b].add([vertex.index],t,'REPLACE')
                        break
    else:
        group=ob.vertex_groups.new(name=part); group.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    if part in details:
        ob=join([ob]+details[part],'TitanDetailed_'+part)
    # Consistent outward normals for all closed components, including tiny hair locks.
    bm=bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(ob.data); bm.free()
    ob.parent=rig
    modifier=ob.modifiers.new('TitanSkin','ARMATURE'); modifier.object=rig
    ob['part_id']=part
    meshes.append(ob)
# No parts exist only as details; all 15 have a sculpted base.
expected={'Torso','Head','Jaw'}|{s+p for s in ['Left','Right'] for p in ['UpperArm','LowerArm','Hand','UpperLeg','LowerLeg','Foot']}
assert {o['part_id'] for o in meshes} == expected
assert len(rig.data.bones)==22
for bone in rig.data.bones:
    assert all(abs(bone.matrix_local[i][j]-reference.data.bones[bone.name].matrix_local[i][j])<1e-6 for i in range(4) for j in range(4))
triangles=0
for ob in meshes:
    ob.data.calc_loop_triangles(); triangles+=len(ob.data.loop_triangles)
    bm=bmesh.new(); bm.from_mesh(ob.data)
    boundary=sum(e.is_boundary for e in bm.edges)
    bm.free()
    if boundary: raise RuntimeError(ob.name+' has open edges: '+str(boundary))
if prototype:
    prototype.hide_render=True
    prototype.hide_viewport=True
for window in bpy.context.window_manager.windows:
    for area in window.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=(0,0,6)
            area.spaces.active.region_3d.view_distance=19
            area.spaces.active.region_3d.view_rotation=Vector((8,-25,5)).to_track_quat('Z','Y')
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
print('DETAILED_TITAN_GEOMETRY_OK',len(meshes),'skins',len(rig.data.bones),'identical rest bones',triangles,'triangles, all meshes closed')
