"""Owned Scout model-only art pass. Blender 5.2 background process only.
Reads the approved source library; writes exclusively to a marker-owned worker.
Original rig/cape/weapons and existing Unity files are never overwritten here.
"""
import bpy
import json
import math
import os
import hashlib
import struct
import zlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
import numpy as np

ROOT = Path(os.environ['SCOUT_SOURCE_ROOT']).resolve()
WORK = Path(os.environ['SCOUT_MODEL_WORKER']).resolve()
if os.environ.get('ODM_SCOUT_MODEL_OWNED') != '1' or not bpy.app.background:
    raise RuntimeError('Only an explicitly owned background worker may author this model')
if WORK == ROOT or (WORK / '.scout-model-overhaul-owned').read_text() != str(ROOT):
    raise RuntimeError('Worker ownership mismatch')
OUT = WORK / 'Assets/Art/Player/Scout/ModelOverhaul'
ART = WORK / 'ArtSource/Player/Scout/ModelOverhaul'
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
with bpy.data.libraries.load(str(ROOT / 'ArtSource/Player/Scout/ScoutPlayer.blend'), link=False) as (a, b):
    b.collections = ['ScoutPlayer_v1']
col = b.collections[0]
bpy.context.scene.collection.children.link(col)
rig = next(o for o in col.objects if o.type == 'ARMATURE')
original = next(o for o in col.objects if o.name == 'ScoutBody')
protected_mesh_names = {o.name for o in col.objects if o.type == 'MESH' and o != original}

def protected_snapshot():
    def mesh(o):
        return {'name': o.name, 'matrix': [list(row) for row in o.matrix_local],
                'parent': o.parent.name if o.parent else None, 'parent_type': o.parent_type,
                'parent_bone': o.parent_bone,
                'vertices': [list(v.co) for v in o.data.vertices],
                'faces': [list(p.vertices) for p in o.data.polygons],
                'uv': [[list(v.uv) for v in layer.data] for layer in o.data.uv_layers],
                'weights': [[(g.group, g.weight) for g in v.groups] for v in o.data.vertices],
                'groups': [g.name for g in o.vertex_groups],
                'materials': [m.name for m in o.data.materials]}
    d = {'bones': [(b.name, b.parent.name if b.parent else None,
                    [list(row) for row in b.matrix_local], list(b.head_local), list(b.tail_local))
                   for b in rig.data.bones],
         'rig_matrix': [list(row) for row in rig.matrix_world],
         'meshes': [mesh(o) for o in col.objects if o.type == 'MESH' and o.name in protected_mesh_names],
         'markers': [(o.name, [list(row) for row in o.matrix_local], o.parent_bone)
                     for o in col.objects if o.type == 'EMPTY']}
    return hashlib.sha256(json.dumps(d, sort_keys=True).encode()).hexdigest()
protected_before = protected_snapshot()
old_bounds = [[min(v.co[i] for v in original.data.vertices), max(v.co[i] for v in original.data.vertices)] for i in range(3)]
bone_heads = {b.name: list(b.head_local) for b in rig.data.bones}

# One body-only portable PBR atlas. Cape and weapons retain the original atlas.
PALETTE = [
    ('Skin', (211, 171, 144), 0, .22), ('Jacket', (102, 70, 49), 0, .20),
    ('Cream', (191, 190, 172), 0, .18), ('Leather', (49, 39, 32), 0, .28),
    ('Cloak', (48, 75, 44), 0, .19), ('Hair', (36, 27, 22), 0, .16),
    ('Steel', (140, 150, 153), .8, .55), ('DarkMetal', (57, 63, 65), .65, .35),
    ('EyeWhite', (222, 217, 198), 0, .24), ('EyeDark', (73, 103, 83), 0, .38),
    ('Brass', (134, 133, 113), .7, .46), ('Stitch', (132, 104, 73), 0, .17),
    ('Insignia', (191, 202, 192), 0, .26), ('InnerSkin', (117, 74, 61), 0, .18),
    ('Shirt', (163, 161, 139), 0, .18), ('SteelEdge', (194, 204, 207), .9, .64)]
SLOT = {v[0]: i for i, v in enumerate(PALETTE)}
N = 2048
C = N // 4
xx, yy = np.meshgrid(np.linspace(0, 1, C), np.linspace(0, 1, C))
rng = np.random.default_rng(92863)
base = np.empty((N, N, 3), dtype=np.uint8)
metal = np.empty((N, N, 4), dtype=np.uint8)
normal = np.empty((N, N, 3), dtype=np.uint8)
for slot, (name, color, metallic, gloss) in enumerate(PALETTE):
    noise = rng.normal(0, .65, (C, C))
    shade = np.zeros((C, C))
    if name in ('Jacket', 'Cream', 'Shirt', 'Leather'):
        shade = 5 * np.cos((xx - .5) * math.pi) - 5 * (1 - yy) + 1.5 * np.sin(xx * 29 + yy * 9)
        shade -= 6 * np.exp(-((xx - .08) / .035) ** 2) + 6 * np.exp(-((xx - .92) / .035) ** 2)
        shade += 1.1 * np.sin(xx * 370) * np.sin(yy * 420)
    elif name == 'Skin':
        shade = 4 * np.cos((xx - .5) * math.tau) + 3 * yy
    elif name == 'Hair':
        shade = 15 * np.exp(-((xx - .36 - .05 * np.sin(yy * 4)) / .17) ** 2)
        shade -= 8 * (np.abs(xx - .5) * 2) ** 3 + 4 * (1 - yy)
        shade += .6 * np.sin(xx * 17 + yy * 3)
    elif name in ('Steel', 'SteelEdge', 'DarkMetal', 'Brass'):
        shade = 5 * np.cos(xx * math.tau) + 1.2 * np.sin(xx * 210)
    rgb = np.clip(np.array(color) + (shade + noise)[..., None], 0, 255).astype(np.uint8)
    if name == 'EyeDark':
        dx, dy = (xx - .5) * 2, (yy - .5) * 2
        radius = np.sqrt(dx * dx + dy * dy)
        angle = np.arctan2(dy, dx)
        iris = 9 * np.sin(angle * 43 + radius * 14) + 5 * np.sin(angle * 71)
        iris -= 37 * np.clip((radius - .7) / .3, 0, 1)
        iris -= 38 * np.exp(-(radius / .28) ** 4)
        rgb = np.clip(np.array(color) + iris[..., None], 0, 255).astype(np.uint8)
    y0, x0 = (3 - slot // 4) * C, (slot % 4) * C
    base[y0:y0 + C, x0:x0 + C] = rgb[::-1]
    metal[y0:y0 + C, x0:x0 + C] = (round(metallic * 255), 0, 0, round(gloss * 255))
    amp = .9 if name in ('Jacket', 'Cream', 'Leather', 'Shirt') else .12
    nx = 128 + amp * np.sin(xx * 370)
    ny = 128 + amp * np.sin(yy * 420)
    normal[y0:y0 + C, x0:x0 + C] = np.stack((nx, ny, np.full_like(nx, 255)), axis=-1)[::-1].astype(np.uint8)

def png(path, pixels):
    height, width, channels = pixels.shape
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    raw = b''.join(b'\0' + row.tobytes() for row in pixels)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6 if channels == 4 else 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 7)) + chunk(b'IEND', b''))
for name, array in [('BaseColor', base), ('MetallicSmoothness', metal), ('Normal', normal)]:
    png(OUT / ('ScoutRefined_' + name + '.png'), array)
mat = bpy.data.materials.new('Scout_Refined_PBR')
mat.use_nodes = True
nodes = mat.node_tree.nodes
nodes.clear()
bs = nodes.new('ShaderNodeBsdfPrincipled')
output = nodes.new('ShaderNodeOutputMaterial')
links = mat.node_tree.links
links.new(bs.outputs['BSDF'], output.inputs['Surface'])
def image_node(name, linear=False):
    im = bpy.data.images.load(str(OUT / ('ScoutRefined_' + name + '.png')))
    if linear:
        im.colorspace_settings.name = 'Non-Color'
    im.pack()
    node = nodes.new('ShaderNodeTexImage')
    node.image = im
    return node
b = image_node('BaseColor')
m = image_node('MetallicSmoothness', True)
n = image_node('Normal', True)
sep = nodes.new('ShaderNodeSeparateColor')
inv = nodes.new('ShaderNodeMath')
inv.operation = 'SUBTRACT'
inv.inputs[0].default_value = 1
norm = nodes.new('ShaderNodeNormalMap')
norm.inputs['Strength'].default_value = .3
bs.inputs['Specular IOR Level'].default_value = .2
links.new(b.outputs['Color'], bs.inputs['Base Color'])
links.new(m.outputs['Color'], sep.inputs['Color'])
links.new(sep.outputs['Red'], bs.inputs['Metallic'])
links.new(m.outputs['Alpha'], inv.inputs[1])
links.new(inv.outputs[0], bs.inputs['Roughness'])
links.new(n.outputs['Color'], norm.inputs['Color'])
links.new(norm.outputs['Normal'], bs.inputs['Normal'])

# Build one indexed skinned body. All original hand/gear vertices are retained;
# anatomical clothing/head surfaces replace the old blockout components.
verts, faces, face_uvs, weights, face_smooth = [], [], [], [], []
part_stats = []
def tile(name, uv):
    slot = SLOT[name]
    return ((slot % 4 + .035 + .93 * uv[0]) / 4, (slot // 4 + .035 + .93 * uv[1]) / 4)
def add(name, vs, fs, material, skin, uvs=None, raw_uvs=False, smooth=True):
    start = len(verts)
    for v in vs:
        v = Vector(v)
        verts.append(tuple(v))
        ws = {skin: 1.0} if isinstance(skin, str) else skin(v)
        ws = {k: float(w) for k, w in ws.items() if w > .000001}
        total = sum(ws.values())
        weights.append({k: w / total for k, w in ws.items()})
    for f in fs:
        faces.append(tuple(start + i for i in f))
        face_smooth.append(smooth)
        uv = [uvs[i] for i in f] if uvs else [(.5 + vs[i][0] * .8, .15 + vs[i][2] * .4) for i in f]
        face_uvs.append(uv if raw_uvs else [tile(material, q) for q in uv])
    part_stats.append({'name': name, 'vertices': len(vs), 'triangles': sum(len(f) - 2 for f in fs)})

def body_weight(v):
    z = v.z
    if z < 1.06:
        return {'Hips': 1}
    if z < 1.23:
        t = (z - 1.06) / .17
        return {'Hips': 1 - t, 'Spine': t}
    t = min(1, (z - 1.23) / .15)
    return {'Spine': 1 - t, 'Chest': t}
def leg_weight(side):
    def f(v):
        t = max(0, min(1, (v.z - .445) / .15))
        return {side + 'LowerLeg': 1 - t, side + 'UpperLeg': t}
    return f
def arm_weight(side):
    def f(v):
        x = abs(v.x)
        if x < .29:
            t = max(.1, min(1, (x - .15) / .14))
            return {'Chest': 1 - t, side + 'UpperArm': t}
        t = max(0, min(1, (x - .455) / .09))
        return {side + 'UpperArm': 1 - t, side + 'LowerArm': t}
    return f

def loft(name, centers, radii, material, skin, sides=24, front_power=2, omit_front=None, folds=0):
    centers = [Vector(c) for c in centers]
    axis = (centers[-1] - centers[0]).normalized()
    u = Vector((1, 0, 0)) if abs(axis.z) > .85 else Vector((0, 1, 0))
    u = (u - axis * u.dot(axis)).normalized()
    v = axis.cross(u).normalized()
    vs, uv, fs = [], [], []
    for i, (c, (rx, ry)) in enumerate(zip(centers, radii)):
        for j in range(sides + 1):
            angle = math.tau * j / sides
            a, b = math.cos(angle), math.sin(angle)
            if abs(axis.z) > .85 and b < 0 and front_power != 2:
                b = -(max(0, 1 - abs(a) ** front_power) ** (1 / front_power))
            ripple = 1 + folds * math.sin(angle * 5 + i * .63) * math.sin(math.pi * i / (len(centers) - 1))
            vs.append(tuple(c + u * a * rx * ripple + v * b * ry * ripple))
            uv.append((j / sides, i / (len(centers) - 1)))
    stride = sides + 1
    for i in range(len(centers) - 1):
        for j in range(sides):
            f = (i * stride + j, i * stride + j + 1, (i + 1) * stride + j + 1, (i + 1) * stride + j)
            c = sum((Vector(vs[k]) for k in f), Vector()) / 4
            if omit_front and omit_front(c):
                continue
            fs.append(f)
    if not omit_front:
        fs.extend([tuple(range(sides, -1, -1)), tuple((len(centers) - 1) * stride + j for j in range(sides + 1))])
    add(name, vs, fs, material, skin, uv)

def patch(name, vs, material, skin, depth=.0015):
    # Thin closed garment panel, not a double-sided transparent decal.
    n = len(vs)
    vv = list(vs) + [(x, y + depth, z) for x, y, z in vs]
    ff = [tuple(range(n)), tuple(range(n * 2 - 1, n - 1, -1))]
    ff += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    add(name, vv, ff, material, skin, smooth=False)

def tube(name, path, widths, material, skin, sides=8, aspect=1):
    path = [Vector(p) for p in path]
    vs, uv, fs = [], [], []
    for i, c in enumerate(path):
        tangent = (path[min(i + 1, len(path) - 1)] - path[max(0, i - 1)]).normalized()
        u = Vector((0, -1, 0))
        if abs(tangent.dot(u)) > .9:
            u = Vector((1, 0, 0))
        u = (u - tangent * u.dot(tangent)).normalized()
        v = tangent.cross(u).normalized()
        for j in range(sides + 1):
            angle = math.tau * j / sides
            vs.append(tuple(c + u * math.cos(angle) * widths[i] * aspect + v * math.sin(angle) * widths[i]))
            uv.append((j / sides, i / (len(path) - 1)))
    stride = sides + 1
    for i in range(len(path) - 1):
        for j in range(sides):
            a = i * stride + j
            fs.append((a, a + 1, a + 1 + stride, a + stride))
    fs += [tuple(range(sides, -1, -1)), tuple((len(path) - 1) * stride + j for j in range(sides + 1))]
    add(name, vs, fs, material, skin, uv)

def box(name, center, size, material, skin):
    c = Vector(center)
    vv = [tuple(c + Vector((x * size[0] / 2, y * size[1] / 2, z * size[2] / 2))) for x, y, z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    add(name, vv, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], material, skin, smooth=False)
def buckle(name, x, y, z, w, h, skin):
    for dx in [-w / 2, w / 2]:
        box(name, (x + dx, y, z), (.0035, .005, h), 'Brass', skin)
    for dz in [-h / 2, h / 2]:
        box(name, (x, y, z + dz), (w, .005, .0035), 'Brass', skin)
    box(name, (x, y - .001, z), (.002, .005, h), 'Steel', skin)

def ribbon(name, path, width, material, skin):
    vv = []
    for i, c in enumerate(map(Vector, path)):
        p = Vector(path[max(0, i - 1)])
        n = Vector(path[min(len(path) - 1, i + 1)])
        t = n - p
        side = Vector((t.z, 0, -t.x)).normalized() * width / 2
        vv += [tuple(c - side), tuple(c + side)]
    for i in range(len(path) - 1):
        patch(name, [vv[2*i], vv[2*i+1], vv[2*i+3], vv[2*i+2]], material, skin, .002)

# Keep current fingers/palms and hip-mounted hardware, including their exact skin weights.
keep = []
for p in original.data.polygons:
    ids = p.vertices
    used = {original.vertex_groups[g.group].name for i in ids for g in original.data.vertices[i].groups if g.weight > .00001}
    center = sum((original.data.vertices[i].co for i in ids), Vector()) / len(ids)
    hands = bool(used) and all(any(t in n for t in ['Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little']) for n in used)
    hardware = used == {'Hips'} and (abs(center.x) > .18 or center.y > .11) and center.z > .48
    if hands or hardware:
        keep.append(p)
preserved_indices = {}
for p in keep:
    for i in p.vertices:
        if i not in preserved_indices:
            preserved_indices[i] = len(verts)
            verts.append(tuple(original.data.vertices[i].co))
            weights.append({original.vertex_groups[g.group].name: g.weight for g in original.data.vertices[i].groups})
    faces.append(tuple(preserved_indices[i] for i in p.vertices))
    face_smooth.append(p.use_smooth)
    face_uvs.append([tuple(original.data.uv_layers.active.data[i].uv) for i in p.loop_indices])
part_stats.append({'name': 'Preserved original hand and gear surfaces', 'vertices': len(verts), 'triangles': sum(len(f) - 2 for f in faces)})

# Tailored shirt underneath a genuinely open, cropped jacket.
zs = [1.005, 1.04, 1.10, 1.18, 1.26, 1.34, 1.40, 1.445, 1.474]
rr = [(.133,.098),(.133,.099),(.128,.097),(.139,.102),(.157,.111),(.180,.112),(.197,.095),(.171,.077),(.063,.055)]
loft('Fitted undershirt', [(0,.006,z) for z in zs], rr, 'Shirt', body_weight, 32, 3.2, folds=.009)
jzs = [1.115,1.125,1.17,1.24,1.32,1.39,1.43,1.46,1.48]
jrr = [(.140,.110),(.143,.113),(.148,.114),(.164,.123),(.187,.124),(.210,.108),(.201,.094),(.160,.077),(.067,.061)]
loft('Open cropped jacket', [(0,.008,z) for z in jzs], jrr, 'Jacket', body_weight, 40, 3.3,
     omit_front=lambda c: c.y < -.015 and abs(c.x) < (.070 if c.z < 1.37 else .055), folds=.012)
for s in [-1,1]:
    patch('Folded jacket lapel', [(s*.045,-.082,1.485),(s*.133,-.109,1.427),(s*.091,-.125,1.324),(s*.055,-.117,1.399)], 'Jacket', body_weight, .003)
    ribbon('Lapel seam', [(s*.045,-.084,1.483),(s*.127,-.112,1.425),(s*.092,-.128,1.326)], .0015, 'Stitch', body_weight)
    patch('Collar stand', [(s*.054,-.044,1.47),(s*.057,.046,1.477),(s*.069,.044,1.514),(s*.061,-.049,1.508)], 'Jacket', body_weight, .003)
    patch('Chest pocket', [(s*.105,-.122,1.333),(s*.165,-.106,1.338),(s*.166,-.111,1.387),(s*.110,-.126,1.385)], 'Jacket', body_weight)
    ribbon('Pocket flap', [(s*.108,-.129,1.380),(s*.139,-.123,1.375),(s*.165,-.114,1.383)], .009, 'Jacket', body_weight)
    ribbon('Jacket front stitched edge', [(s*.073,-.107,1.122),(s*.073,-.118,1.255),(s*.070,-.122,1.34)], .0014, 'Stitch', body_weight)
    ribbon('Chest webbing', [(s*.16,-.103,1.411),(s*.128,-.128,1.349),(s*.057,-.122,1.231),(-s*.046,-.107,1.048)], .014, 'Leather', body_weight)
    buckle('Chest harness adjustment', s*.084,-.130,1.278,.019,.027,body_weight)
    # Small original Scout-inspired shoulder insignia panel, not copied reference art.
    patch('Shoulder patch', [(s*.218,-.054,1.419),(s*.251,-.051,1.417),(s*.253,-.054,1.449),(s*.218,-.058,1.453)], 'DarkMetal', arm_weight('Left' if s > 0 else 'Right'))
    for j in range(3):
        ribbon('Shoulder feather stitch', [(s*(.222+j*.008),-.059,1.424),(s*(.224+j*.008),-.061,1.445)], .002, 'Insignia', arm_weight('Left' if s > 0 else 'Right'))
loft('Neck anatomy', [(0,.012,z) for z in [1.465,1.49,1.535,1.57]], [(.048,.047),(.049,.048),(.046,.046),(.048,.047)], 'Skin', 'Neck', 24, 2.6)
loft('Shirt standing collar',[(0,.012,z) for z in [1.468,1.49,1.513,1.519]],[(.055,.054),(.054,.053),(.052,.051),(.052,.051)],'Shirt',body_weight,32,2.5)

# One closed head shell with a flattened facial plane and sharper jaw.
HEAD = [(1.551,.027,.032,-.011),(1.565,.046,.046,-.005),(1.585,.065,.062,.001),
        (1.612,.083,.078,.007),(1.635,.092,.087,.010),(1.646,.095,.090,.011),
        (1.655,.097,.092,.012),(1.666,.099,.094,.013),(1.676,.100,.095,.013),
        (1.688,.101,.095,.014),(1.704,.101,.094,.015),(1.738,.099,.092,.016),(1.766,.090,.084,.020),
        (1.789,.070,.063,.019),(1.803,.033,.033,.015)]
head_start = len(verts)
loft('Sculpted anime head', [(0,y,z) for z,rx,ry,y in HEAD], [(rx,ry) for z,rx,ry,y in HEAD], 'Skin', 'Head', 80, 3.3)
for i in range(head_start,len(verts)):
    x,y,z=verts[i]
    if y < -.025:
        bridge=.011*math.exp(-(x/.011)**2-((z-1.680)/.027)**2)
        tip=.023*math.exp(-(x/.014)**2-((z-1.652)/.014)**2)
        verts[i]=(x,y-bridge-tip,z)
def face_y(x, z):
    for a, b in zip(HEAD, HEAD[1:]):
        if z <= b[0]:
            t = max(0, min(1, (z - a[0]) / (b[0] - a[0])))
            rx, ry, cy = [a[i] * (1-t) + b[i] * t for i in [1,2,3]]
            return cy - ry * max(0, 1 - min(.9999, abs(x)/rx) ** 3.3) ** (1/3.3)
    return -.05
# Nose bridge and tip are sculpted directly into the closed head surface.
for s in [-1,1]:
    # Ears are compact and tucked into the head silhouette.
    loft('Ear rim',[(s*.102,.012,z) for z in [1.638,1.644,1.658,1.677,1.687]],[(.003,.004),(.011,.010),(.014,.013),(.011,.010),(.003,.004)],'Skin','Head',20)
    tube('Ear inner fold', [(s*.109,-.000,1.650),(s*.112,-.002,1.666),(s*.108,.000,1.677)],[.0007,.001,.0006],'InnerSkin','Head',8)
    # Almond sclera, curved to the face rather than floating spherical eyeballs.
    outline=[]
    for i in range(32):
        a=math.tau*i/32
        x=s*(.048+.028*math.cos(a))
        z=1.690+(.0075 if math.sin(a)>0 else .009)*math.sin(a)+.004*math.cos(a)
        outline.append((x,face_y(x,z)-.0019,z))
    center=(s*.047,face_y(s*.047,1.690)-.0025,1.690)
    eye_vertices=[center];eye_faces=[]
    for ring in range(1,5):
        t=ring/4
        for x,y,z in outline:
            x=center[0]+(x-center[0])*t;z=center[2]+(z-center[2])*t
            eye_vertices.append((x,face_y(x,z)-.0025,z))
        for j in range(32):
            a=1+(ring-1)*32+j;b=1+(ring-1)*32+(j+1)%32
            if ring==1:eye_faces.append((0,a,b))
            else:eye_faces.append((a-32,a,b,b-32))
    add('Almond sclera',eye_vertices,[tuple(reversed(f)) if s<0 else f for f in eye_faces],'EyeWhite','Head')
    upper=outline[:17]
    tube('Upper lash line',[(x,y-.0007,z+.0001) for x,y,z in upper],[.0008+.0008*math.sin(math.pi*i/16) for i in range(17)],'Hair','Head',6, .65)
    lower=outline[16:]+[outline[0]]
    tube('Lower eyelid',[(x,y-.0003,z-.0005) for x,y,z in lower],[.00065]*17,'InnerSkin','Head',6,.5)
    for name,rx,rz,material,depth in [('Iris',.0077,.0090,'EyeDark',.0027),('Pupil',.0028,.0040,'Hair',.0031)]:
        vv=[(s*.047,face_y(s*.047,1.690)-depth,1.690)]
        uv=[(.5,.5)]
        for i in range(32):
            a=math.tau*i/32;x=s*.047+rx*math.cos(a);z=min(1.697,max(1.682,1.690+rz*math.sin(a)))
            vv.append((x,face_y(x,z)-depth,z));uv.append((.5+.49*math.cos(a),.5+.49*math.sin(a)))
        add(name,vv,[(0,i+1,(i+1)%32+1) for i in range(32)],material,'Head',uv)
    for dx,dz,r in [(-.0025,.0036,.0016),(.002,-.003,.0007)]:
        x=s*.047+dx;z=1.690+dz
        vv=[(x,face_y(x,z)-.0035,z)]+[(x+r*math.cos(i*math.tau/10),face_y(x,z)-.0035,z+r*math.sin(i*math.tau/10)) for i in range(10)]
        add('Eye catchlight',vv,[(0,i+1,(i+1)%10+1) for i in range(10)],'EyeWhite','Head')
    brow=[(s*.020,1.709),(s*.038,1.718),(s*.060,1.721),(s*.077,1.719)]
    ribbon('Expressive eyebrow',[(x,face_y(x,z)-.002,z) for x,z in brow],.0032,'Hair','Head')
mouth=[(-.024,1.609),(-.012,1.610),(0,1.609),(.012,1.610),(.024,1.609)]
tube('Mouth contour',[(x,face_y(x,z)-.0017,z) for x,z in mouth],[.00035,.0007,.0008,.0007,.00035],'InnerSkin','Head',6,.5)
tube('Lower lip light',[(-.012,face_y(-.012,1.604)-.0009,1.604),(0,face_y(0,1.603)-.001,1.603),(.012,face_y(.012,1.604)-.0009,1.604)],[.0004,.0007,.0004],'Skin','Head',6,.5)

# Directional volumetric locks; no alpha cards or extra hair material/pass.
cap_vertices=[];cap_uv=[];cap_faces=[]
for row in range(13):
    t=row/12
    for j in range(65):
        a=math.tau*j/64
        low=1.626+.132*max(0,math.cos(a))**2+.035*math.sin(a)**2
        z=low+(1.824-low)*t
        if z>HEAD[-1][0]:
            f=max(0,(1.828-z)/(1.828-HEAD[-1][0]));rx=.050*f;ry=.050*f;cy=.016
        else:
            for aa,bb in zip(HEAD,HEAD[1:]):
                if z<=bb[0]:
                    f=max(0,min(1,(z-aa[0])/(bb[0]-aa[0])))
                    rx,ry,cy=[aa[k]*(1-f)+bb[k]*f for k in [1,2,3]]
                    rx+=.012;ry+=.014
                    break
        front=(max(0,1-abs(math.sin(a))**3.3)**(1/3.3)) if math.cos(a)>=0 else math.cos(a)
        cap_vertices.append((rx*math.sin(a),cy-ry*front,z));cap_uv.append((j/64,t))
for row in range(12):
    for j in range(64):
        a=row*65+j;cap_faces.append((a,a+1,a+66,a+65))
add('Fitted hair underlayer',cap_vertices,cap_faces,'Hair','Head',cap_uv)
def hair_lock(name,control,width):
    c=[Vector(v) for v in control];path=[];ww=[]
    for i in range(13):
        t=i/12
        path.append(tuple(c[0]*(1-t)**3+c[1]*3*t*(1-t)**2+c[2]*3*t*t*(1-t)+c[3]*t**3))
        ww.append(max(.00035,width*math.sin(math.pi*(.14+.86*t))**.7))
    tube(name,path,ww,'Hair','Head',12,.32)
for control,width in [
    ([(-.014,-.004,1.815),(-.047,-.071,1.815),(-.087,-.101,1.761),(-.080,-.105,1.716)],.022),
    ([(-.006,-.011,1.820),(-.035,-.076,1.810),(-.050,-.106,1.764),(-.038,-.110,1.737)],.020),
    ([(.012,-.012,1.818),(.012,-.078,1.810),(.033,-.106,1.753),(.022,-.110,1.710)],.021),
    ([(.022,-.007,1.816),(.056,-.065,1.807),(.077,-.099,1.770),(.065,-.103,1.733)],.022),
    ([(.037,.004,1.806),(.082,-.045,1.803),(.103,-.074,1.765),(.102,-.073,1.708)],.020),
    ([(-.033,.009,1.809),(-.080,-.041,1.806),(-.101,-.074,1.767),(-.104,-.070,1.696)],.019),
]:
    hair_lock('Swept fringe lock',control,width)
for s in [-1,1]:
    for i in range(3):
        yy=-.010+i*.037
        hair_lock('Temple layered lock',[(s*.065,yy+.015,1.796),(s*.103,yy,1.780),(s*.113,yy-.005,1.720),(s*.103,yy-.012,1.676+i*.009)],.018)
for i in range(8):
    a=math.pi*i/7;x=math.cos(a)*.085;y=.026+math.sin(a)*.074
    hair_lock('Nape layered lock',[(x*.50,y*.55,1.804),(x,y,1.782),(x*1.17,y*1.14,1.718),(x*1.05,y*1.04,1.615+(i%3)*.005)],.019)

for side,s in [('Left',1),('Right',-1)]:
    aw=arm_weight(side);lw=leg_weight(side)
    xs=[.15,.19,.23,.28,.34,.41,.46,.485,.505,.525,.55,.60,.66,.71,.744]
    radii=[(.032,.032),(.061,.051),(.073,.063),(.070,.065),(.065,.061),(.058,.056),(.058,.055),(.061,.055),(.057,.052),(.060,.054),(.055,.050),(.051,.047),(.046,.042),(.041,.037),(.038,.034)]
    loft('Tailored sleeve '+side,[(s*x,.003,1.401 if i==0 else 1.430 if i==1 else 1.44) for i,x in enumerate(xs)],radii,'Jacket',aw,24,folds=.018)
    loft('Raglan shoulder '+side,[(s*x,.008,z) for x,z in [(.10,1.405),(.15,1.409),(.19,1.417),(.235,1.44),(.28,1.44)]],[(.042,.030),(.074,.045),(.080,.056),(.074,.065),(.070,.064)],'Jacket',aw,32)
    loft('Cuff '+side,[(s*x,.003,1.44) for x in [.720,.728,.748,.753]],[(.043,.039),(.044,.039),(.042,.038),(.038,.034)],'Jacket',side+'LowerArm',24)
    # Preserve palm/finger geometry and grip anchors; only expose a short wrist bridge.
    loft('Wrist '+side,[(s*x,0,1.44) for x in [.746,.765,.779]],[(.033,.023),(.031,.022),(.030,.022)],'Skin',side+'Hand',20)
    loft('Tailored trouser '+side,[(s*x,y,z) for x,y,z in [(.105,0,.955),(.107,0,.91),(.110,0,.85),(.113,-.002,.78),(.113,-.005,.69),(.111,-.011,.60),(.110,-.018,.55),(.110,-.019,.52),(.109,-.014,.49),(.108,-.010,.46),(.107,0,.39),(.105,.002,.30),(.105,.002,.20)]],
         [(.084,.081),(.087,.083),(.083,.080),(.076,.074),(.067,.067),(.060,.061),(.058,.058),(.059,.055),(.057,.056),(.056,.055),(.052,.052),(.047,.048),(.039,.040)],'Cream',lw,28,folds=.012)
    loft('Boot shaft '+side,[(s*.105,.004,z) for z in [.115,.15,.23,.31,.37,.385]],[(.043,.048),(.044,.049),(.047,.052),(.052,.057),(.057,.060),(.058,.061)],'Leather',side+'LowerLeg',24)
    loft('Boot cuff '+side,[(s*.105,.004,z) for z in [.371,.387,.403]],[(.060,.063),(.062,.065),(.061,.064)],'Leather',side+'LowerLeg',24)
    loft('Boot foot '+side,[(s*.105,y,z) for y,z in [(-.058,.032),(-.058,.044),(-.059,.071),(-.052,.095),(-.026,.125)]],[(.053,.125),(.055,.125),(.054,.120),(.049,.103),(.042,.063)],'Leather',side+'Foot',28,3.0)
    loft('Boot sole '+side,[(s*.105,-.058,z) for z in [.012,.021,.032,.038]],[(.053,.125),(.057,.129),(.057,.129),(.054,.125)],'Hair',side+'Foot',28,3.0)
    ribbon('Boot front seam '+side,[(s*.105,-.053,.15),(s*.105,-.052,.24),(s*.105,-.054,.365)],.0015,'Stitch',side+'LowerLeg')
    for z,rx,ry in [(.853,.087,.085),(.678,.071,.072),(.414,.056,.057)]:
        loft('Leg webbing '+side,[(s*.11,0,z-.008),(s*.11,0,z+.008)],[(rx,ry),(rx,ry)],'Leather',lw,28)
        buckle('Leg strap adjustment',s*.11,-ry-.003,z,.023,.019,lw)
    ribbon('Thigh harness '+side,[(s*.154,-.069,.980),(s*.142,-.082,.906),(s*.110,-.087,.83),(s*.070,-.069,.69)],.014,'Leather',lw)
loft('Trouser hip yoke',[(0,.006,z) for z in [.875,.92,.977,1.013]],[(.120,.064),(.145,.086),(.141,.094),(.132,.096)],'Cream','Hips',32,3.0)
loft('Waist webbing',[(0,.006,z) for z in [1.004,1.026]],[(.144,.107),(.144,.107)],'Leather','Hips',32,3.0)
buckle('Waist buckle',0,-.104,1.014,.035,.027,'Hips')

# Make the single body renderer and keep the exact original local transform.
data=bpy.data.meshes.new('ScoutBody_RefinedGeometry')
data.from_pydata(verts,[],faces)
data.update()
body=bpy.data.objects.new('ScoutBody_RefinedWorking',data)
col.objects.link(body)
body.parent=original.parent
body.matrix_parent_inverse=original.matrix_parent_inverse.copy()
body.matrix_basis=original.matrix_basis.copy()
data.materials.append(mat)
uv=data.uv_layers.new(name='UVMap')
for p, uvs, smooth in zip(data.polygons,face_uvs,face_smooth):
    p.use_smooth=smooth
    for loop, co in zip(p.loop_indices,uvs):
        uv.data[loop].uv=co
for bone in rig.data.bones:
    group=body.vertex_groups.new(name=bone.name)
    for i, ws in enumerate(weights):
        if bone.name in ws and ws[bone.name]>.000001:
            group.add([i],ws[bone.name],'REPLACE')
modifier=body.modifiers.new('Scout skin','ARMATURE')
modifier.object=rig
if protected_snapshot()!=protected_before:
    raise RuntimeError('Protected skeleton/cape/weapons changed during body authoring')
original.hide_render=True
original.hide_set(True)
original.name='ScoutBody_OriginalReviewOnly'
body.name='ScoutBody'
# Export corner normals/UVs into the native builder input. Binding-space conversion
# is derived from original rest-bone landmarks in Unity, not guessed FBX axes.
data.calc_loop_triangles()
rows=[];triangles=[];lookup={}
for tri in data.loop_triangles:
    ids=[]
    for li in tri.loops:
        loop=data.loops[li];vi=loop.vertex_index
        normal=data.corner_normals[li].vector
        tex=uv.data[li].uv
        key=(vi,tuple(round(float(t),7) for t in normal),tuple(round(float(t),7) for t in tex))
        if key not in lookup:
            lookup[key]=len(rows)
            ws=sorted(weights[vi].items(),key=lambda p:p[1],reverse=True)[:4]
            total=sum(w for _,w in ws)
            rows.append({'p':list(data.vertices[vi].co),'n':list(normal),'uv':list(tex),
                         'bones':[k for k,w in ws],'weights':[w/total for k,w in ws]})
        ids.append(lookup[key])
    triangles.extend(ids)
payload={'vertices':rows,'triangles':triangles,'boneHeads':[{'name':k,'p':v} for k,v in bone_heads.items()],
         'originalMin':[p[0] for p in old_bounds],'originalMax':[p[1] for p in old_bounds],
         'protectedBefore':protected_before,'protectedAfter':protected_before,
         'parts':part_stats,'sourceRoot':str(ROOT)}
(WORK/'body-geometry.json').write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
# Remove the review-only old body before writing the editable library/export.
bpy.data.objects.remove(original,do_unlink=True)
for im in bpy.data.images:
    if im.source=='FILE' and im.has_data:
        im.pack()
bpy.data.libraries.write(str(ART/'ScoutPlayer_Refined.blend'),set(col.objects)|{col},fake_user=True)
bpy.ops.object.select_all(action='DESELECT')
for o in col.objects:
    o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(ART/'ScoutPlayer_Refined.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='AUTO')
report={'blender':bpy.app.version_string,'bodyTriangles':len(triangles)//3,'bodySourceVertices':len(verts),
        'bodyNativeCornerVertices':len(rows),'totalTriangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in col.objects if o.type=='MESH'),
        'bones':len(rig.data.bones),'bodyMaterials':1,'bodyAtlas':N,'protectedSkeletonCapeWeaponsHash':protected_before,
        'originalHandsAndGearPreserved':True,'animationAuthored':False,'sourceAssetsOverwritten':False,'parts':part_stats}
(ART/'authoring-report.json').write_text(json.dumps(report,indent=2))

# Static art previews only. No animation clip is written or retimed.
scene=bpy.context.scene
for side,s in [('Left',1),('Right',-1)]:
    for suffix,angle in [('UpperArm',72),('LowerArm',8)]:
        p=rig.pose.bones[side+suffix];p.rotation_mode='QUATERNION'
        q=p.bone.matrix_local.to_quaternion()
        p.rotation_quaternion=q.inverted()@Quaternion((0,1,0),math.radians(s*angle))@q
world=bpy.data.worlds.new('Refined Scout preview world');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.14,.18,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.42
scene.world=world
for name,pos,power,size in [('Key',(2,-3,4),350,4),('Fill',(-3,-2,2.8),170,3),('Rim',(1,3,3),400,3)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
    ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=pos
    ob.rotation_euler=(Vector((0,0,1.1))-ob.location).to_track_quat('-Z','Y').to_euler()
cd=bpy.data.cameras.new('Refined Scout art camera');camera=bpy.data.objects.new(cd.name,cd);scene.collection.objects.link(camera);scene.camera=camera;cd.type='ORTHO'
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.view_settings.view_transform='AgX'
scene.render.resolution_percentage=100
for name,pos,target,scale,res in [
    ('Blender_Front',(0,-6,1.40),(0,0,.93),2.03,(900,1100)),
    ('Blender_ThreeQuarter',(2.3,-6,2.6),(0,0,.93),2.10,(900,1100)),
    ('Blender_Back',(-2.3,6,2.6),(0,0,.93),2.10,(900,1100)),
    ('Blender_Face',(1,-5,1.90),(0,-.015,1.678),.42,(1000,1000)),
]:
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    cd.ortho_scale=scale
    scene.render.resolution_x,scene.render.resolution_y=res
    scene.render.filepath=str(ART/(name+'.png'))
    bpy.ops.render.render(write_still=True)
print('SCOUT_MODEL_OVERHAUL '+json.dumps({k:v for k,v in report.items() if k!='parts'}))
