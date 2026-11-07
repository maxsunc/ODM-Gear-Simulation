"""Build the original 12m modular Titan blockout in Blender 4.5.
Run via Blender MCP execute_code, or: blender --background --python this_file -- OUTPUT_DIR
Never clears the user's scene: all generated content lives in TitanPrototype_v1.
Blender +Z is up, -Y is forward; FBX exports Unity Y-up / +Z-forward.
"""
import bpy
import math
import os
import sys
from mathutils import Vector, Matrix

OUT = globals().get('TITAN_OUTPUT_DIR')
if not OUT:
    OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.abspath('ArtSource/Titan')
os.makedirs(OUT, exist_ok=True)
COLLECTION = 'TitanPrototype_v1'
old = bpy.data.collections.get(COLLECTION)
if old:
    for ob in list(old.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.collections.remove(old)
collection = bpy.data.collections.new(COLLECTION)
bpy.context.scene.collection.children.link(collection)


def move(ob):
    for col in list(ob.users_collection):
        col.objects.unlink(ob)
    collection.objects.link(ob)
    return ob


def material(name, color, roughness=0.65):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    return mat

skin = material('Titan_WarmClay', (0.48, 0.255, 0.145))
light_skin = material('Titan_RaisedPlanes', (0.60, 0.36, 0.215))
shadow = material('Titan_DeepPlanes', (0.235, 0.095, 0.06))
seam = material('Titan_SeamCap', (0.30, 0.075, 0.055))
hair = material('Titan_CharcoalHair', (0.035, 0.027, 0.023))
eye = material('Titan_EyeIvory', (0.86, 0.78, 0.60), 0.3)
pupil = material('Titan_EyeDark', (0.012, 0.009, 0.008), 0.25)
teeth = material('Titan_Teeth', (0.69, 0.58, 0.38))

parts = {}


def finish(ob, part, bone, mat):
    move(ob)
    ob.data.materials.append(mat)
    # Freeze mesh transforms before parenting/skinning; bind pose is in model space.
    ob.data.transform(ob.matrix_world)
    ob.matrix_world = Matrix.Identity(4)
    group = ob.vertex_groups.new(name=bone)
    group.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
    for polygon in ob.data.polygons:
        polygon.use_smooth = True
    parts.setdefault(part, []).append(ob)
    return ob


def ellipsoid(name, center, scale, part, bone, mat=skin, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.scale = scale
    bpy.context.view_layer.update()
    return finish(ob, part, bone, mat)


def link_shape(name, start, end, width, depth, part, bone, mat=skin, profile=None):
    """Closed elliptical muscle loft, with explicit seam caps on BOTH ends."""
    a, b = Vector(start), Vector(end)
    axis = (b - a).normalized()
    ref = Vector((0, 1, 0)) if abs(axis.y) < 0.95 else Vector((1, 0, 0))
    u = axis.cross(ref).normalized()
    v = axis.cross(u).normalized()
    profile = profile or [(0, .72), (.15, 1), (.40, 1.05), (.75, .83), (1, .60)]
    n = 16
    verts, faces = [], []
    for t, r in profile:
        c = a.lerp(b, t)
        for j in range(n):
            angle = j * 2 * math.pi / n
            verts.append(c + u * math.cos(angle) * width * r + v * math.sin(angle) * depth * r)
    for i in range(len(profile) - 1):
        for j in range(n):
            faces.append((i*n+j, i*n+(j+1)%n, (i+1)*n+(j+1)%n, (i+1)*n+j))
    # Triangle fans are watertight, including when limbs are separated.
    verts.extend((a, b))
    for j in range(n):
        faces.append((len(verts)-2, (j+1)%n, j))
        k = (len(profile)-1)*n
        faces.append((len(verts)-1, k+j, k+(j+1)%n))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    collection.objects.link(ob)
    return finish(ob, part, bone, mat)

# Proportions: huge shoulders and hands, long legs, compact pelvis, heavy brow/jaw.
# All anatomy is non-explicit. Pelvis is a continuous sculpted shell.
ellipsoid('Pelvis', (0, 0, 5.95), (1.10, .65, .91), 'Torso', 'Hips')
ellipsoid('AbdomenCore', (0, .015, 7.2), (.92, .55, 1.22), 'Torso', 'Spine')
ellipsoid('Ribcage', (0, .04, 8.65), (1.65, .79, 1.22), 'Torso', 'Chest')
link_shape('NeckMass', (0, 0, 9.15), (0, 0, 10.38), .62, .53, 'Torso', 'Neck')
for side, sign in [('Left', 1), ('Right', -1)]:
    ellipsoid(side+'Pectoral', (.77*sign, -.60, 8.65), (.83, .29, .53), 'Torso', 'Chest', light_skin)
    ellipsoid(side+'Trapezius', (.69*sign, .12, 9.27), (.85, .50, .47), 'Torso', 'Chest')
    ellipsoid(side+'Lat', (1.12*sign, .38, 8.27), (.52, .49, .81), 'Torso', 'Chest')
    for i in range(3):
        ellipsoid(side+'Ab'+str(i), (.34*sign, -.50, 7.99-i*.47), (.34, .17, .26), 'Torso', 'Spine', light_skin)
    ellipsoid(side+'Oblique', (.80*sign, -.27, 7.32), (.24, .25, .75), 'Torso', 'Spine')
    # Socket collars remain on the torso when the corresponding branch is detached.
    ellipsoid(side+'ShoulderSocket', (1.66*sign, 0, 9.03), (.46, .48, .48), 'Torso', 'Chest', seam)
    ellipsoid(side+'HipSocket', (.63*sign, .01, 5.53), (.51, .47, .45), 'Torso', 'Hips', seam)
    upper = side+'UpperArm'; lower = side+'LowerArm'; hand_bone = side+'Hand'
    shoulder = (1.78*sign, 0, 9.03); elbow = (3.35*sign, 0, 9.03); wrist = (4.73*sign, 0, 9.03)
    link_shape(side+'UpperArmMesh', shoulder, elbow, .52, .52, upper, upper)
    ellipsoid(side+'Deltoid', (1.95*sign, -.02, 9.03), (.61, .62, .61), upper, upper, light_skin)
    ellipsoid(side+'Bicep', (2.68*sign, -.16, 9.04), (.70, .43, .40), upper, upper, light_skin)
    link_shape(side+'ForearmMesh', elbow, wrist, .43, .42, lower, lower)
    ellipsoid(side+'Elbow', (3.39*sign, .07, 9.03), (.36, .37, .37), lower, lower)
    ellipsoid(side+'Palm', (5.15*sign, -.025, 9.03), (.53, .24, .44), side+'Hand', hand_bone)
    # Five visibly separated fingers. Hand-weighted blockout, ready for finger rig refinement.
    for i in range(4):
        z = 8.70 + i*.215
        start = (5.48*sign, -.03, z)
        end = ((6.02 - abs(i-1.5)*.10)*sign, -.09, z-.04)
        link_shape(side+'Finger'+str(i), start, end, .105, .115, side+'Hand', hand_bone,
                   profile=[(0,.95),(.35,1),(.7,.85),(1,.60)])
        ellipsoid(side+'Knuckle'+str(i), start, (.14,.14,.14), side+'Hand', hand_bone, light_skin, 12, 8)
    link_shape(side+'Thumb', (5.00*sign, -.03, 8.67), (5.35*sign, -.09, 8.30), .17, .17, side+'Hand', hand_bone)
    thigh = side+'UpperLeg'; shin = side+'LowerLeg'; foot = side+'Foot'
    hip = (.63*sign, 0, 5.65); knee = (.68*sign, -.08, 3.05); ankle = (.70*sign, 0, .65)
    link_shape(side+'Thigh', hip, knee, .68, .62, thigh, thigh,
               profile=[(0,.8),(.2,1),(.52,.93),(.85,.65),(1,.57)])
    ellipsoid(side+'Quadricep', (.66*sign, -.33, 4.58), (.53, .39, 1.05), thigh, thigh, light_skin)
    ellipsoid(side+'Kneecap', (.68*sign, -.29, 3.05), (.37,.28,.40), shin, shin, light_skin)
    link_shape(side+'Shin', knee, ankle, .43, .43, shin, shin,
               profile=[(0,.77),(.24,1),(.5,.90),(.83,.58),(1,.55)])
    ellipsoid(side+'Calf', (.70*sign, .19, 2.25), (.36,.37,.69), shin, shin)
    ellipsoid(side+'FootMesh', (.70*sign, -.39, .39), (.44,.87,.38), foot, foot)
    for i in range(5):
        ellipsoid(side+'Toe'+str(i), ((.39+i*.155)*sign, -1.02, .25), (.12,.31-i*.018,.17), foot, foot, skin, 12, 8)

ellipsoid('Cranium', (0, .015, 10.91), (.81,.66,1.00), 'Head', 'Head')
ellipsoid('FacePlate', (0, -.46, 10.78), (.65,.36,.72), 'Head', 'Head', light_skin)
ellipsoid('Chin', (0, -.37, 10.13), (.64,.49,.36), 'Jaw', 'Jaw')
ellipsoid('MouthCavity', (0, -.740, 10.36), (.55,.055,.23), 'Head', 'Head', shadow)
for i in range(10):
    x = (i-4.5)*.101
    ellipsoid('UpperTooth'+str(i), (x,-.796,10.47), (.046,.052,.095), 'Head', 'Head', teeth, 12, 8)
    ellipsoid('LowerTooth'+str(i), (x,-.797,10.25), (.044,.05,.075), 'Jaw', 'Jaw', teeth, 12, 8)
ellipsoid('NoseBridge', (0,-.727,10.85), (.17,.23,.33), 'Head', 'Head')
ellipsoid('NoseTip', (0,-.86,10.69), (.22,.20,.15), 'Head', 'Head', light_skin)
for sign in [-1,1]:
    ellipsoid('EyeSocket', (.34*sign,-.678,11.03), (.31,.11,.22), 'Head', 'Head', shadow)
    ellipsoid('Eye', (.34*sign,-.755,11.035), (.235,.09,.12), 'Head', 'Head', eye)
    ellipsoid('Pupil', (.34*sign,-.837,11.025), (.065,.023,.082), 'Head', 'Head', pupil, 12, 8)
    ellipsoid('HeavyBrow', (.34*sign,-.744,11.21), (.35,.16,.12), 'Head', 'Head')
    ellipsoid('Cheekbone', (.48*sign,-.64,10.70), (.23,.15,.23), 'Head', 'Head', light_skin)
    ellipsoid('Ear', (.79*sign,.015,10.83), (.15,.22,.31), 'Head', 'Head')
    ellipsoid('EarInset', (.875*sign,-.065,10.83), (.025,.10,.16), 'Head', 'Head', shadow, 12, 8)
# Hair is sculpted clumps, not a sphere helmet. Keep eyes/nape exposed.
ellipsoid('HairCap', (0,.14,11.61), (.81,.63,.47), 'Head', 'Head', hair)
for i in range(9):
    x = (i-4)*.17
    ellipsoid('HairLock'+str(i), (x, -.27+abs(i-4)*.035, 11.60-abs(i-4)*.03), (.16,.34,.40+(i%3)*.06), 'Head', 'Head', hair, 12, 8)
# Nape landmark is deliberately small; the actual hitbox is created separately in Unity.
ellipsoid('NapeLandmark', (0,.49,10.02), (.24,.09,.30), 'Torso', 'Neck', seam)

# Stable, animation-friendly humanoid hierarchy. Segments do not share vertices across cuts.
arm_data = bpy.data.armatures.new('TitanSkeleton')
rig = bpy.data.objects.new('TitanRig', arm_data)
collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bones = {}

def bone(name, head, tail, parent=None):
    ob = arm_data.edit_bones.new(name)
    ob.head = head; ob.tail = tail
    if parent: ob.parent = bones[parent]
    bones[name] = ob

bone('Hips', (0,0,5.65), (0,0,6.5))
bone('Spine', (0,0,6.5), (0,0,8.0), 'Hips')
bone('Chest', (0,0,8.0), (0,0,9.35), 'Spine')
bone('Neck', (0,0,9.35), (0,0,10.25), 'Chest')
bone('Head', (0,0,10.25), (0,0,11.8), 'Neck')
bone('Jaw', (0,-.08,10.49), (0,-.52,10.13), 'Head')
for side, sign in [('Left',1),('Right',-1)]:
    bone(side+'Shoulder', (.25*sign,0,9.03), (1.78*sign,0,9.03), 'Chest')
    bone(side+'UpperArm', (1.78*sign,0,9.03), (3.35*sign,0,9.03), side+'Shoulder')
    bone(side+'LowerArm', (3.35*sign,0,9.03), (4.73*sign,0,9.03), side+'UpperArm')
    bone(side+'Hand', (4.73*sign,0,9.03), (5.55*sign,0,9.03), side+'LowerArm')
    bone(side+'UpperLeg', (.63*sign,0,5.65), (.68*sign,-.08,3.05), 'Hips')
    bone(side+'LowerLeg', (.68*sign,-.08,3.05), (.70*sign,0,.65), side+'UpperLeg')
    bone(side+'Foot', (.70*sign,0,.65), (.70*sign,-.95,.25), side+'LowerLeg')
    bone(side+'Toes', (.70*sign,-.95,.25), (.70*sign,-1.25,.25), side+'Foot')
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front = True
rig['authoring_notes'] = '12m blockout; capped skinned segments; Unity +Z forward. Limb roots: Left/RightUpperArm and Left/RightUpperLeg.'

meshes = []
for part, objects in parts.items():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    ob = bpy.context.object
    ob.name = 'Titan_'+part
    # Fuse overlapping anatomical volumes into a continuous sculpted segment.
    # The head/jaw retain distinct eyes, teeth and hair. Every severable branch
    # remains its own closed object; never weld across a dismemberment seam.
    if part not in {'Head', 'Jaw'}:
        remesh = ob.modifiers.new('FuseAnatomy', 'REMESH')
        remesh.mode = 'VOXEL'
        remesh.voxel_size = .045 if part.endswith('Hand') or part.endswith('Foot') else .075
        remesh.use_smooth_shade = True
        bpy.ops.object.modifier_apply(modifier=remesh.name)
        smooth = ob.modifiers.new('SculptSmooth', 'SMOOTH')
        smooth.factor = 0.7
        smooth.iterations = 4
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        decimate = ob.modifiers.new('GameTopology', 'DECIMATE')
        decimate.ratio = .42
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        ob.vertex_groups.clear()
        if part == 'Torso':
            centers = [('Hips', 5.95), ('Spine', 7.2), ('Chest', 8.65), ('Neck', 9.9)]
            groups = {name: ob.vertex_groups.new(name=name) for name, _ in centers}
            for vertex in ob.data.vertices:
                z = vertex.co.z
                if z <= centers[0][1]: groups[centers[0][0]].add([vertex.index], 1, 'REPLACE')
                elif z >= centers[-1][1]: groups[centers[-1][0]].add([vertex.index], 1, 'REPLACE')
                else:
                    for (a, za), (b, zb) in zip(centers, centers[1:]):
                        if za <= z <= zb:
                            t = (z - za) / (zb - za)
                            groups[a].add([vertex.index], 1-t, 'REPLACE')
                            groups[b].add([vertex.index], t, 'REPLACE')
                            break
        else:
            group = ob.vertex_groups.new(name=part)
            group.add(list(range(len(ob.data.vertices))), 1, 'REPLACE')
    ob.parent = rig
    modifier = ob.modifiers.new('TitanSkin', 'ARMATURE')
    modifier.object = rig
    ob['part_id'] = part
    meshes.append(ob)

# Tidy object selection and frame only the new model in the existing workspace.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for ob in meshes: ob.select_set(True)
bpy.context.view_layer.objects.active = rig
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_distance = 20
        area.spaces.active.region_3d.view_location = (0,0,6)
        from mathutils import Quaternion
        area.spaces.active.region_3d.view_rotation = Quaternion((0.82,0.56,0.06,0.10)).normalized()
        area.spaces.active.shading.color_type = 'MATERIAL'

fbx_path = os.path.join(OUT, 'TitanPrototype.fbx')
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={'ARMATURE','MESH'},
                        global_scale=1, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                        add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE',
                        use_armature_deform_only=False, path_mode='AUTO')
# Write ONLY generated datablocks to the source file; do not save/rename user's open .blend.
source_scene = bpy.data.scenes.new('TitanSource')
source_scene.collection.children.link(collection)
bpy.data.libraries.write(os.path.join(OUT, 'TitanPrototype.blend'), {source_scene}, fake_user=True)
bpy.data.scenes.remove(source_scene)
print('TITAN_BUILD_OK', len(meshes), 'segments', len(rig.data.bones), 'bones',
      sum(len(o.data.polygons) for o in meshes), 'polygons', fbx_path)
