"""Render the generated Titan collection without changing the user's active scene."""
import bpy
from mathutils import Vector
import os

output = globals().get('TITAN_PREVIEW_PATH', os.path.abspath('ArtSource/Titan/TitanPreview.png'))
scene_name = globals().get('TITAN_PREVIEW_SCENE', 'Titan_Preview')
scene = bpy.data.scenes.get(scene_name) or bpy.data.scenes.new(scene_name)
collection = bpy.data.collections[globals().get('TITAN_COLLECTION', 'TitanPrototype_v1')]
if collection.name not in scene.collection.children:
    scene.collection.children.link(collection)
for ob in list(scene.objects):
    if ob.name.startswith('TitanPreview_'):
        bpy.data.objects.remove(ob, do_unlink=True)
cam_data = bpy.data.cameras.new('TitanPreview_Camera')
cam = bpy.data.objects.new('TitanPreview_Camera', cam_data)
scene.collection.objects.link(cam)
cam.location = globals().get('TITAN_CAMERA_POSITION', (16, -29, 13))
target = Vector(globals().get('TITAN_CAMERA_TARGET', (0, 0, 6)))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.type = 'ORTHO'
cam_data.ortho_scale = globals().get('TITAN_CAMERA_SCALE', 15.4)
scene.camera = cam
for name, position, energy, size in [('Key', (2,-12,17),2300,8), ('Fill',(-10,-5,10),1600,8), ('Rim',(4,5,15),2600,6)]:
    data = bpy.data.lights.new('TitanPreview_' + name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    ob = bpy.data.objects.new('TitanPreview_' + name, data)
    scene.collection.objects.link(ob)
    ob.location = position
    ob.rotation_euler = (target - ob.location).to_track_quat('-Z','Y').to_euler()
scene.world = bpy.data.worlds.get('TitanPreview_World') or bpy.data.worlds.new('TitanPreview_World')
scene.world.color = (.16, .16, .16)
scene.render.engine = 'CYCLES'
scene.cycles.samples = globals().get('TITAN_RENDER_SAMPLES', 32)
scene.render.resolution_x = 1100
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = output
scene.view_settings.view_transform = 'AgX'
bpy.ops.render.render(write_still=True, scene=scene.name)
print('TITAN_PREVIEW_OK', output)
