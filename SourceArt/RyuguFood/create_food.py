import bpy, math, random, os
from mathutils import Vector
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT=os.path.join(ROOT,'Assets','Resources','RyuguFood')
random.seed(32)
def material(name,c):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); return m
rice=material('Pearl rice',(.96,.93,.79)); fish=material('Red sea bream',(.94,.38,.35)); fat=material('Fish marbling',(1,.77,.65)); shrimp=material('Cooked prawn',(1,.44,.20)); white=material('Prawn bands',(1,.82,.61)); nori=material('Nori',(.045,.10,.055)); green=material('Cucumber',(.24,.59,.15)); pink=material('Sakura dango',(.97,.57,.66)); tea=material('Matcha dango',(.48,.67,.25)); bamboo=material('Bamboo skewer',(.60,.38,.15))
def ellipsoid(name,p,s,m):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=p)
 o=bpy.context.object; o.name=name; o.scale=s; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(m)
 for f in o.data.polygons:f.use_smooth=True
 return o
def rod(name,a,b,r,m):
 d=Vector(b)-Vector(a); bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=d.length,location=(Vector(a)+Vector(b))/2)
 o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(m);return o
def grains():
 ellipsoid('Rice bed',(0,0,0),(.044,.072,.022),rice)
 for i in range(62):
  a=random.uniform(0,math.tau);r=random.uniform(.15,1)**.5
  ellipsoid('Individual rice grain',(.041*r*math.cos(a),.066*r*math.sin(a),.016+random.uniform(0,.009)),(.004,.007,.003),rice)
for index in range(4):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 if index==0:
  grains();ellipsoid('Sea bream slice',(0,0,.033),(.048,.083,.014),fish)
  for j in range(7):
   y=-.06+j*.019
   rod('Fine fat marbling',(-.039,y-.008,.043),(.039,y+.009,.043),.0017,fat)
 elif index==1:
  grains()
  for j in range(7):
   y=-.06+j*.018
   ellipsoid('Curved prawn segment',(math.sin(j*.4)*.009,y,.035),(.035-j*.002,.015,.023),shrimp)
   ellipsoid('Prawn white band',(math.sin(j*.4)*.009,y-.006,.047),(.029-j*.002,.003,.008),white)
  for side in [-1,1]:
   o=ellipsoid('Prawn tail fan',(side*.018,.079,.04),(.022,.028,.006),shrimp);o.rotation_euler.z=side*.4
 elif index==2:
  rod('Bamboo skewer',(0,-.105,.002),(0,.10,.002),.0025,bamboo)
  for j,m in enumerate([tea,rice,pink]):ellipsoid('Three colour dango',(0,-.045+j*.043,.018),(.024,.024,.024),m)
 else:
  rod('Nori roll',(0,0,-.019),(0,0,.019),.042,nori)
  rod('Rice cross section',(0,0,-.02),(0,0,.0205),.037,rice)
  rod('Tuna centre',(-.009,0,-.021),(-.009,0,.022),.014,fish)
  rod('Cucumber centre',(.016,0,-.021),(.016,0,.022),.009,green)
 # Authored at metre scale, FBX converts Z-up to Unity Y-up.
 bpy.ops.object.select_all(action='SELECT')
 bpy.context.view_layer.objects.active=bpy.context.selected_objects[0]
 bpy.ops.object.join()
 bpy.context.object.name='Food%d'%index
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'Food%d.blend'%index))
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Food%d.fbx'%index),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,add_leaf_bones=False,bake_anim=False)
print('Generated four Blender source files and four FBX food models')
