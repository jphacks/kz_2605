"""Run with Blender 4.3: blender --background --python SourceArt/Turtle/create_turtle.py"""
import bpy
import bmesh
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt' / 'Turtle'
EXPORT = ROOT / 'Assets' / '_Project' / 'Art' / 'Models' / 'Turtle'
OUT.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, roughness=.48):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    return m

skin = material('Skin • sea jade', (.19, .45, .33))
light = material('Skin • pale jade', (.43, .65, .43))
shell = material('Shell • deep seams', (.055, .16, .115))
scute = material('Shell • olive jade', (.27, .40, .16))
scute2 = material('Shell • golden olive', (.37, .47, .20))
rim = material('Shell • warm rim', (.61, .66, .34))
belly = material('Plastron • sand', (.72, .69, .40))
black = material('Eyes • obsidian', (.009, .022, .023), .16)
white = material('Eyes • catchlight', (.92, 1, .94), .2)
parts = []
# Linear colors: richer natural pigment, with restrained highlights.
for mat, color in [(skin,(.075,.19,.12)), (light,(.24,.34,.16)),
                   (shell,(.038,.052,.019)), (scute,(.17,.20,.055)),
                   (scute2,(.27,.24,.075)), (rim,(.38,.32,.13)),
                   (belly,(.56,.43,.22))]:
    mat.diffuse_color = (*color,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*color,1)

def finish(obj, name, mat, bone):
    obj.name = name
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    if bone:
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    parts.append(obj)
    return obj

def ellipsoid(name, loc, scale, mat, bone='Body', segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, bone)

# Blender coordinates: head points toward -Y; up is +Z. Overall length ~2 m.
ellipsoid('Body', (0, 0, .06), (.66, .85, .27), skin)
ellipsoid('Plastron', (0, .015, -.09), (.655, .85, .16), belly)
ellipsoid('Carapace rim', (0, .02, .08), (.78, 1.00, .14), rim)
ellipsoid('Carapace', (0, .03, .16), (.745, .965, .40), shell)

# Individually modelled scutes sit on the curved shell with dark gaps.
groove = material('Shell growth lines', (.20, .28, .105))
amber = material('Iris amber', (.47, .29, .065), .28)

def tube(name, points, radius, mat, bone='Body'):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for p, co in zip(spline.points, points):
        p.co = (*co, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object, name, mat, bone)

def shell_height(x, y):
    return .16 + .40 * math.sqrt(max(.015, 1 - (x/.745)**2 - ((y-.03)/.965)**2))

def plate(name, points, mat):
    cx = sum(p[0] for p in points) / len(points)
    cy = sum(p[1] for p in points) / len(points)
    points = [(cx+(x-cx)*.987, cy+(y-cy)*.987) for x,y in points]
    verts = [(cx, cy, shell_height(cx,cy)+.008)]
    verts += [(x,y,shell_height(x,y)+.008) for x,y in points]
    faces = [(0, i+1, (i+1)%len(points)+1) for i in range(len(points))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=3, use_grid_fill=True)
    for vertex in bm.verts:
        vertex.co.z = shell_height(vertex.co.x, vertex.co.y) + .008
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, name, mat, 'Body')
    solid = obj.modifiers.new('Scute thickness', 'SOLIDIFY')
    solid.thickness = .006
    bevel = obj.modifiers.new('Soft scute edges', 'BEVEL')
    bevel.width = .003
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Three concentric raised growth lines follow the dome, exported as geometry.
    for inset in [.42, .58, .73, .86]:
        line = []
        for j in range(len(points)):
            a, b = points[j], points[(j+1)%len(points)]
            for step in range(5):
                t = step/5
                x = cx + ((a[0]*(1-t)+b[0]*t)-cx)*inset
                y = cy + ((a[1]*(1-t)+b[1]*t)-cy)*inset
                line.append((x, y, shell_height(x,y)+.009))
        tube(name+' growth', line+[line[0]], .0009, groove)

# Clip a shared tessellation against the shell outline: adjoining scutes fit
# exactly, rather than independent polygons leaving broad empty channels.
seeds = [(0,-.72+i*.36) for i in range(5)]
seeds += [(s*.54,-.57+i*.38) for s in [-1,1] for i in range(4)]
outline = [(.865*math.cos(i*2*math.pi/96), .865*math.sin(i*2*math.pi/96)) for i in range(96)]
for index, (sx,sy) in enumerate(seeds):
    poly = outline[:]
    for tx,ty in seeds:
        if (tx,ty)==(sx,sy): continue
        nx,ny=tx-sx,ty-sy
        limit=(tx*tx+ty*ty-sx*sx-sy*sy)/2
        clipped=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            da=a[0]*nx+a[1]*ny-limit
            db=b[0]*nx+b[1]*ny-limit
            if da<=0: clipped.append(a)
            if (da<=0)!=(db<=0):
                t=da/(da-db)
                clipped.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
        poly=clipped
        if not poly: break
    if len(poly)>=3:
        plate(('Vertebral' if index<5 else 'Costal')+' scute %02d'%index,
              [(.745*x,.03+.965*y) for x,y in poly], scute2 if index%3 else scute)

# A continuous ring of 26 marginal shields makes the silhouette read as a shell.
for i in range(26):
    a0, a1 = 2*math.pi*(i+.045)/26, 2*math.pi*(i+.955)/26
    pts = []
    for r, angles in [(.87,[a0,(a0+a1)/2,a1]),(.994,[a1,(a0+a1)/2,a0])]:
        for a in angles:
            pts.append((.745*r*math.cos(a), .03+.965*r*math.sin(a)))
    plate('Marginal shield %02d'%i, pts, rim if i%3 else scute2)

ellipsoid('Neck', (0,-.92,.015), (.245,.36,.20), skin, 'Head')
# A single continuous head surface replaces the spherical head/muzzle/jaw stack.
# Cross sections taper into a short beak and blend back into the neck.
head_sections = [(-1.65,.018,.042,.005),(-1.62,.105,.077,.012),
                 (-1.54,.195,.115,.020),(-1.43,.247,.163,.030),
                 (-1.30,.265,.192,.035),(-1.16,.252,.190,.035),
                 (-1.02,.210,.162,.025),(-.89,.170,.140,.015)]
verts=[]
for y,w,h,cz in head_sections:
    for k in range(32):
        a=2*math.pi*k/32
        verts.append((w*math.cos(a),y,cz+h*math.sin(a)))
faces=[]
for j in range(len(head_sections)-1):
    for k in range(32):
        faces.append((j*32+k,(j+1)*32+k,(j+1)*32+(k+1)%32,j*32+(k+1)%32))
faces += [tuple(reversed(range(32))),tuple((len(head_sections)-1)*32+k for k in range(32))]
mesh=bpy.data.meshes.new('Continuous head')
mesh.from_pydata(verts,[],faces)
mesh.update()
obj=bpy.data.objects.new('Head',mesh)
bpy.context.collection.objects.link(obj)
finish(obj,'Head',skin,'Head')
bm=bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(mesh)
bm.free()
bpy.context.view_layer.objects.active=obj
sub=obj.modifiers.new('Continuous head contours','SUBSURF')
sub.levels=2
bpy.ops.object.modifier_apply(modifier=sub.name)

def head_surface(y,z):
    a,b=next((a,b) for a,b in zip(head_sections,head_sections[1:]) if a[0]<=y<=b[0])
    t=(y-a[0])/(b[0]-a[0])
    w,h,cz=[a[k]*(1-t)+b[k]*t for k in range(1,4)]
    return w*math.sqrt(max(0,1-((z-cz)/h)**2))

for s in [-1,1]:
    # Small lateral eyes seated in the head, without a projecting pale socket.
    ellipsoid('Eye lid', (s*.231,-1.335,.122), (.025,.060,.051), skin,'Head')
    ellipsoid('Iris', (s*.250,-1.344,.125), (.013,.035,.033), amber,'Head')
    ellipsoid('Eye', (s*.259,-1.350,.126), (.009,.025,.027), black,'Head')
    ellipsoid('Eye glint', (s*.266,-1.362,.139), (.003,.006,.006), white,'Head',12,8)
    ellipsoid('Nostril', (s*.055,-1.616,.063), (.010,.006,.006), black,'Head',12,8)
    mouth=[]
    for i in range(33):
        y=-1.645+i*.009
        z=-.027+.012*(i/32)**2
        mouth.append((s*(head_surface(y,z)-.003),y,z))
    tube('Beak mouth seam',mouth,.0025,shell,'Head')

def flipper(name, side, front):
    bone = ('Front' if front else 'Rear') + ('.L' if side>0 else '.R')
    x0, y0 = side*(.52 if front else .46), (-.53 if front else .65)
    # Closed elliptical sections form a tapered, swept hydrofoil.
    sections = [(0,0,.16,.075),(.25,.04,.23,.075),(.58,.22,.21,.052),(.88,.43,.12,.028),(1.02,.57,.015,.009)] if front else [(0,0,.14,.06),(.23,.08,.18,.05),(.44,.26,.15,.032),(.57,.43,.015,.008)]
    verts=[]
    for dx,dy,width,thick in sections:
        for k in range(12):
            a=2*math.pi*k/12
            verts.append((x0+side*dx, y0+dy+math.cos(a)*width, -.045+math.sin(a)*thick-.06*dx))
    faces=[]
    for j in range(len(sections)-1):
        for k in range(12):
            faces.append((j*12+k,j*12+(k+1)%12,(j+1)*12+(k+1)%12,(j+1)*12+k))
    faces += [tuple(reversed(range(12))),tuple((len(sections)-1)*12+k for k in range(12))]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj,name,skin,bone)
    sub=obj.modifiers.new('Rounded hydrofoil','SUBSURF')
    sub.levels=2
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=sub.name)
    for j in range(13 if front else 6):
        dx=.10+j*.063
        a,b=next((a,b) for a,b in zip(sections,sections[1:]) if a[0]<=dx<=b[0])
        t=(dx-a[0])/(b[0]-a[0])
        dy,width,thick=[a[k]*(1-t)+b[k]*t for k in range(1,4)]
        for row in [-2,-1,0,1,2]:
            fraction=row*.31
            z=-.045-.06*dx+thick*math.sqrt(1-fraction*fraction)
            ellipsoid('Flipper mosaic scale', (x0+side*dx,y0+dy+fraction*width,z-.003), (.029,width*.145,.004), light if (j+row)%3 else scute,bone,10,6)
    tube('Flipper leading edge', [(x0+side*dx,y0+dy-width*.78,-.045-.06*dx+.01) for dx,dy,width,thick in sections[1:]], .007, light, bone)
    return bone,(x0,y0,-.045),(x0+side*.5,y0+.18,-.07)

bones=[]
for side in [-1,1]:
    bones.append(flipper('Front flipper',side,True))
    bones.append(flipper('Rear flipper',side,False))
tail=ellipsoid('Tail',(0,.98,-.035),(.09,.28,.065),skin,'Tail')

bpy.ops.object.select_all(action='DESELECT')
arm_data=bpy.data.armatures.new('SeaTurtleSkeleton')
rig=bpy.data.objects.new('SeaTurtle',arm_data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=arm_data.edit_bones.new(name)
    b.head=head
    b.tail=tail
    if parent: b.parent=arm_data.edit_bones[parent]
bone('Root',(0,0,-.3),(0,0,-.1))
bone('Body',(0,.4,0),(0,-.4,0),'Root')
bone('Head',(0,-.85,0),(0,-1.4,.04),'Body')
bone('Tail',(0,.8,0),(0,1.2,0),'Body')
for name,head,tip in bones: bone(name,head,tip,'Body')
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True

# One skinned mesh with material slots, convenient for Unity import.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
model=bpy.context.object
model.name='SeaTurtle_Mesh'
mod=model.modifiers.new('SeaTurtle rig','ARMATURE')
mod.object=rig
model.parent=rig

scene=bpy.context.scene
scene.render.fps=30
rig.animation_data_create()
for action_name, end, strength in [('Swim',61,1.0),('Float',121,.22)]:
    action=bpy.data.actions.new(action_name)
    rig.animation_data.action=action
    for frame in range(1,end+1):
        phase=(frame-1)/(end-1)*2*math.pi
        for pb in rig.pose.bones:
            pb.rotation_mode='XYZ'
            pb.rotation_euler=(0,0,0)
            pb.location=(0,0,0)
            if pb.name.startswith('Front'):
                sign=1 if pb.name.endswith('.L') else -1
                pb.rotation_euler=(strength*.15*math.cos(phase),strength*sign*.52*math.sin(phase),strength*sign*.12*math.cos(phase))
            elif pb.name.startswith('Rear'):
                sign=1 if pb.name.endswith('.L') else -1
                pb.rotation_euler=(strength*.07*math.sin(phase+.6),sign*strength*.16*math.sin(phase+.7),0)
            elif pb.name=='Head':
                pb.rotation_euler=(strength*.055*math.sin(phase-.4),0,.035*math.sin(phase))
            elif pb.name=='Tail': pb.rotation_euler.z=strength*.12*math.sin(phase+.6)
            elif pb.name=='Body':
                pb.location.z=.025*math.sin(phase)*strength
                pb.rotation_euler=(.022*math.sin(phase)*strength, .018*math.cos(phase)*strength,0)
            pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=pb.name)
            if pb.name=='Body': pb.keyframe_insert(data_path='location',frame=frame,group=pb.name)
    action.use_fake_user=True
    for curve in action.fcurves:
        for key in curve.keyframe_points: key.interpolation='BEZIER'
        curve.modifiers.new('CYCLES')
rig.animation_data.action=bpy.data.actions['Swim']
scene.frame_start=1
scene.frame_end=61
scene.frame_set(1)

# Export only the rig and mesh. Animation remains in place; movement is controlled by the game.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
model.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(EXPORT/'SeaTurtle.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,path_mode='AUTO')

# Studio setup is kept in a separate collection and excluded from the FBX.
studio=bpy.data.collections.new('Studio • preview only')
scene.collection.children.link(studio)
def studio_object(obj):
    for collection in list(obj.users_collection): collection.objects.unlink(obj)
    studio.objects.link(obj)
def aim(obj,point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3.3,-4.7,3.2))
camera=bpy.context.object
camera.name='Preview camera'
aim(camera,(0,-.25,.04))
camera.data.type='ORTHO'
camera.data.ortho_scale=4.7
scene.camera=camera
studio_object(camera)
for name,loc,power,size in [('Key', (1,-3,5),420,4),('Fill',(-3,-1,2),230,3),('Rim',(0,4,3),500,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    lamp=bpy.context.object
    lamp.name=name
    lamp.data.energy=power
    lamp.data.shape='DISK'
    lamp.data.size=size
    aim(lamp,(0,0,0))
    studio_object(lamp)
scene.world.color=(.13,.13,.13)
scene.render.engine='CYCLES'
scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.render.resolution_x=1100
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(OUT/'SeaTurtle_Preview.png')
scene.render.film_transparent=True
for obj in studio.objects: obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=5
            area.spaces.active.region_3d.view_location=(0,-.2,.1)
            area.spaces.active.shading.type='MATERIAL'
scene.render.film_transparent=True
scene.world.color=(.055,.065,.08)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SeaTurtle.blend'))
bpy.ops.render.render(write_still=True)
print('TURTLE_DONE',len(model.data.vertices),'vertices',len(model.data.polygons),'faces',list(bpy.data.actions.keys()))
