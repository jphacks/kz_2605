import bpy, os, math
from mathutils import Vector
base=os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for i in range(4):
 with bpy.data.libraries.load(os.path.join(base,'Food%d.blend'%i)) as (src,dst):dst.objects=src.objects
 for obj in dst.objects:
  bpy.context.collection.objects.link(obj);obj.location.x+=(i-1.5)*.23
bpy.ops.object.camera_add(location=(.2,-.7,.7));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.05;bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(0,-.3,.8));bpy.context.object.data.energy=45;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=.8
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.world.color=(.24,.24,.24);s.render.resolution_x=1200;s.render.resolution_y=500;s.render.resolution_percentage=100;s.render.filepath=os.path.join(base,'food-preview.png');bpy.ops.render.render(write_still=True)
