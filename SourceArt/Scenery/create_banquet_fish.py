"""Blender 4.3: sculpted red sea bream and left-eyed flounder for the banquet."""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt/Scenery'
EXPORT = ROOT / 'Assets/Resources/Scenery'
report = {}

def material(name, color, rough=.38):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = rough
    return m

def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for p in data.polygons: p.use_smooth = True
    return obj

def ell(name, p, size, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=p)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for f in obj.data.polygons: f.use_smooth = True
    return obj

def line(name, pts, r, mat):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = r
    curve.bevel_resolution = 0
    sp = curve.splines.new('POLY')
    sp.points.add(len(pts)-1)
    for p, v in zip(sp.points, pts): p.co = (*v, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.object
    obj.data.materials.append(mat)
    obj.select_set(False)
    return obj

def fin(name, pts, mat):
    mid = tuple(sum(p[i] for p in pts)/len(pts) for i in range(3))
    obj = mesh(name, [mid]+pts, [(0, i+1, (i+1)%len(pts)+1) for i in range(len(pts))], mat)
    mod = obj.modifiers.new('Membrane thickness', 'SOLIDIFY')
    mod.thickness = .003
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def marker(name, p):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.location = p
    return obj

def parent(obj, pivot):
    obj.parent = pivot
    obj.matrix_parent_inverse = pivot.matrix_world.inverted()

def save(name, target, view):
    groups = {}
    for obj in list(bpy.context.scene.objects):
        if obj.type == 'MESH': groups.setdefault(obj.parent, []).append(obj)
    for pivot, items in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in items: obj.select_set(True)
        bpy.context.view_layer.objects.active = items[0]
        bpy.ops.object.join()
        bpy.context.object.name = (pivot.name if pivot else 'Sculpted body')+' mesh'
    objs = [o for o in bpy.context.scene.objects if o.type in {'MESH','EMPTY'}]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')), use_selection=True,
        object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False)
    report[name] = {'meshes': sum(o.type=='MESH' for o in objs),
        'vertices': sum(len(o.data.vertices) for o in objs if o.type=='MESH'),
        'triangles': sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs if o.type=='MESH')}
    bpy.ops.object.camera_add(location=view)
    camera = bpy.context.object
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.55
    scene = bpy.context.scene
    scene.camera = camera
    for pos, energy in [((2,-3,4),350),((-3,-1,2),210),((1,3,2),260)]:
        bpy.ops.object.light_add(type='AREA', location=pos)
        lamp = bpy.context.object
        lamp.data.energy = energy
        lamp.data.size = 3
        lamp.rotation_euler = (Vector(target)-lamp.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 850
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.world.color = (.14,.14,.14)
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(OUT/(name+'_Preview.png'))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(name+'.blend')))
    bpy.ops.render.render(write_still=True)

for flat, name in [(False, 'BanquetTai'), (True, 'BanquetHirame')]:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    ink = material(name+' pupil', (.018,.025,.023), .2)
    pearl = material(name+' pearl', (.92,.84,.67), .3)
    iris = material(name+' iris', (.83,.51,.13), .24)
    bodymat = material(name+' body', (.29,.23,.12) if flat else (.8,.19,.24))
    back = material(name+' dorsal color', (.19,.18,.08) if flat else (.48,.07,.14))
    belly = material(name+' belly', (.78,.73,.54) if flat else (.96,.66,.56))
    membrane = material(name+' fin membrane', (.45,.36,.17) if flat else (.84,.28,.32), .45)
    raymat = material(name+' fin rays', (.65,.52,.25) if flat else (.98,.54,.45))
    scale = material(name+' scale edges', (.42,.36,.18) if flat else (.96,.45,.42), .32)
    blue = material(name+' markings', (.10,.13,.065) if flat else (.23,.56,.70))
    # Both species face -Y; Blender Z becomes Unity's vertical axis.
    width, height = (.29,.055) if flat else (.135,.255)
    def profile(y): return max(.001, math.sin(math.pi*(y+.46)/.92))**.72
    verts, faces = [], []
    rings, segments = 36, 32
    for j in range(rings+1):
        y = -.46+j*.92/rings
        p = profile(y)
        for k in range(segments):
            a = k*2*math.pi/segments
            verts.append((width*p*math.cos(a),y,height*p*math.sin(a)))
    for j in range(rings):
        for k in range(segments):
            faces.append((j*segments+k,j*segments+(k+1)%segments,(j+1)*segments+(k+1)%segments,(j+1)*segments+k))
    body = mesh('Tapered anatomical body', verts, faces, bodymat)
    body.data.materials.append(back); body.data.materials.append(belly)
    for f in body.data.polygons:
        z = sum(body.data.vertices[v].co.z for v in f.vertices)/len(f.vertices)
        f.material_index = 1 if z>height*.5 else 2 if z< -height*.35 else 0
    marker('Nose', (0,-.49,0))
    ell('Mouth opening',(0,-.457,-.012),(.035,.015,.018),ink)
    line('Sculpted upper lip', [(-.034,-.464,-.01),(0,-.478,.003),(.034,-.464,-.01)],.007,belly)
    line('Sculpted lower lip', [(-.029,-.465,-.02),(0,-.476,-.028),(.029,-.465,-.02)],.006,belly)
    # Flounder has both eyes raised on its upper, left side.
    for s in [-1,1]:
        eye = (s*.055-.04,-.31+(s+1)*.025,.057) if flat else (s*.106,-.29,.088)
        ell('Orbital ridge', eye, (.041,.048,.027) if flat else (.028,.047,.047), bodymat if flat else iris)
        p = Vector(eye)+Vector((0,-.007,.018) if flat else (s*.018,-.005,0))
        ell('Golden iris', p, (.027,.03,.014) if flat else (.012,.031,.032), iris)
        p += Vector((0,-.004,.011) if flat else (s*.008,-.005,0))
        ell('Black pupil',p,(.016,.022,.009) if flat else (.007,.020,.021),ink)
        ell('Eye glint',p+Vector((-.005,-.006,.009)),(.005,.006,.006),pearl)
        if not flat:
            line('Operculum edge',[(s*(.112+.014*math.sin(a)),-.175+.025*math.cos(a),.16*math.cos(a)) for a in [i*math.pi/18 for i in range(19)]],.004,back)
            line('Lateral sensory line',[(s*width*profile(y)*1.008,y,.035) for y in [-.15+i*.027 for i in range(20)]],.002,scale)
    if flat:
        # Continuous, scalloped dorsal/anal fringe with individually modeled rays.
        for s in [-1,1]:
            edge = []
            for j in range(29):
                y = -.36+j*.027
                w = width*profile(y)
                tip = (s*(w+.04+.007*math.sin(j*2.4)),y,.005)
                edge.append(tip)
                line('Fringe ray',[(s*w*.93,y,.012),tip],.002,raymat)
            fin('Continuous frilled fin',[(s*width*profile(-.36),-.36,0)]+edge+[(s*width*profile(.396),.396,0)],membrane)
        rng = random.Random(23)
        for i in range(85):
            y = rng.uniform(-.30,.33); x = rng.uniform(-.9,.9)*width*profile(y)
            z = height*profile(y)*math.sqrt(max(.01,1-(x/(width*profile(y)))**2))+.002
            ell('Camouflage rosette',(x,y,z),(rng.uniform(.006,.018),rng.uniform(.007,.019),.0015),blue if i%3 else scale)
    else:
        # Raised overlapping scale arcs on both flanks; blue sea-bream flecks.
        for s in [-1,1]:
            for row in range(8):
                a = -.95+row*.27
                for col in range(13):
                    y = -.15+col*.039+(row%2)*.019
                    if y>.34: continue
                    pts=[]
                    for k in range(6):
                        b = -.5*math.pi+k*math.pi/5
                        yy = y+.017*math.cos(b)
                        aa = a+.105*math.sin(b)
                        p = profile(yy)
                        pts.append((s*(width*p*math.cos(aa)+.0015),yy,height*p*math.sin(aa)))
                    line('Overlapping scale',pts,.0014,scale)
            for i in range(17):
                y = -.09+(i%6)*.068; z = .06+(i//6)*.045
                x = width*profile(y)*math.sqrt(max(.01,1-(z/(height*profile(y)))**2))
                ell('Blue iridescent spot',(s*(x+.002),y,z),(.002,.008,.006),blue)
        dorsal=[(0,-.27,.16)]
        for j in range(16):
            y=-.24+j*.039
            base=height*profile(y)
            tip=base+(.10 if j<8 else .065)*math.sin(math.pi*(j+1)/17)
            dorsal.append((0,y,tip))
            line('Dorsal spine',[(.004,y,base*.93),(.004,y,tip)],.0025,raymat)
        dorsal.append((0,.38,.07))
        fin('Spiny dorsal silhouette',dorsal,membrane)
        fin('Anal fan',[(0,-.02,-.23),(0,.08,-.32),(0,.26,-.24),(0,.34,-.09)],membrane)
        for j in range(9):
            y=.035+j*.032
            line('Anal fin ray',[(.004,y,-height*profile(y)*.92),(.004,y,-.30+j*.021)],.002,raymat)
    for s, finname in [(-1,'FinLeft'),(1,'FinRight')]:
        pivot = marker(finname,(s*width*.75,-.12,0))
        pts=[(s*width*.72,-.17,.008),(s*(width+.14),-.04,-.065),(s*(width+.10),.11,-.07),(s*width*.8,.02,-.01)]
        parent(fin('Pectoral membrane',pts,membrane),pivot)
        for j in range(6):
            parent(line('Pectoral rays',[(s*width*.8,-.13,.004),(s*(width+.12-j*.007),-.02+j*.022,-.061)],.0018,raymat),pivot)
    tail = marker('Tail',(0,.38,0))
    pts = [(-.025,.36,0),(-.17,.65,0),(-.035,.60,0),(.035,.60,0),(.17,.65,0),(.025,.36,0)] if flat else [(0,.36,.025),(0,.65,.22),(0,.57,0),(0,.65,-.22),(0,.36,-.025)]
    parent(fin('Forked caudal fin',pts,membrane),tail)
    for j in range(13):
        v=-1+j/6
        end=(v*.16,.63,.004) if flat else (.004,.59+.05*abs(v),v*.20)
        parent(line('Caudal ray',[(.004,.38,.004),end],.002,raymat),tail)
    save(name,(0,.03,0),(1.0,-1.4,1.6) if flat else (1.7,-1.1,.65))

(OUT/'banquet_fish_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BANQUET_FISH_DONE',json.dumps(report))
