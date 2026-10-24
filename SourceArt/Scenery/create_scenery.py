"""Blender 4.3: editable reef fish and a planked wooden fishing boat."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'SourceArt/Scenery'
EXPORT = ROOT/'Assets/Resources/Scenery'
OUT.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
report = {}

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def mat(name, color, rough=.5):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=rough
    return m

def finish(obj,name,m):
    obj.name=name
    obj.data.materials.append(m)
    for p in obj.data.polygons: p.use_smooth=True
    return obj

def ell(name,loc,scale,m):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=loc)
    o=bpy.context.object; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,m)

def mesh(name,vertices,faces,m):
    data=bpy.data.meshes.new(name); data.from_pydata(vertices,[],faces); data.update()
    o=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(o)
    return finish(o,name,m)

def line(name,points,r,m):
    c=bpy.data.curves.new(name,'CURVE'); c.dimensions='3D'; c.bevel_depth=r; c.bevel_resolution=1
    s=c.splines.new('POLY'); s.points.add(len(points)-1)
    for p,v in zip(s.points,points): p.co=(*v,1)
    o=bpy.data.objects.new(name,c); bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object,name,m)

def box(name,loc,scale,m):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,name,m)
    mod=o.modifiers.new('Worn wooden edges','BEVEL'); mod.width=.008; mod.segments=2
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def fin(name,points,m):
    center=tuple(sum(p[i] for p in points)/len(points) for i in range(3))
    o=mesh(name,[center]+points,[(0,j+1,(j+1)%len(points)+1) for j in range(len(points))],m)
    mod=o.modifiers.new('Fin membrane','SOLIDIFY'); mod.thickness=.006
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def studio(name,target,scale):
    sc=bpy.context.scene
    bpy.ops.object.camera_add(location=(scale*.9,-scale*1.3,scale*.8))
    c=bpy.context.object; c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
    c.data.type='ORTHO'; c.data.ortho_scale=scale; sc.camera=c
    for loc,power in [((2,-3,5),450),((-3,-1,2),240),((1,4,3),400)]:
        bpy.ops.object.light_add(type='AREA',location=loc)
        l=bpy.context.object; l.data.energy=power; l.data.size=4
        l.rotation_euler=(Vector(target)-l.location).to_track_quat('-Z','Y').to_euler()
    sc.render.engine='CYCLES'; sc.cycles.samples=24
    sc.render.resolution_x=1100; sc.render.resolution_y=850; sc.render.resolution_percentage=100
    sc.render.film_transparent=True; sc.world.color=(.16,.16,.16)
    sc.render.image_settings.file_format='PNG'; sc.render.filepath=str(OUT/(name+'_Preview.png'))

def save(name,target,scale):
    # Batch the body and tail independently, preserving every material slot.
    groups={}
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH': groups.setdefault(obj.parent,[]).append(obj)
    for parent,items in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in items: obj.select_set(True)
        bpy.context.view_layer.objects.active=items[0]
        bpy.ops.object.join()
        bpy.context.object.name='Tail mesh' if parent else name+' mesh'
    objects=[o for o in bpy.context.scene.objects if o.type in {'MESH','EMPTY'}]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    report[name]={'vertices':sum(len(o.data.vertices) for o in objects if o.type=='MESH'),'meshes':sum(o.type=='MESH' for o in objects)}
    studio(name,target,scale)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(name+'.blend')))
    bpy.ops.render.render(write_still=True)

black=mat('Reef ink',(.015,.025,.035),.27)
white=mat('Pearl white',(.94,.95,.85),.34)
gold=mat('Fin gold',(.95,.60,.065),.4)
colors=[mat('Butterfly yellow',(.96,.71,.08)),mat('Clownfish orange',(.95,.24,.035)),mat('Tang ocean blue',(.035,.24,.72))]
for kind,name in enumerate(['ReefButterfly','ReefClownfish','ReefTang']):
    clear(); main=colors[kind]
    width=.105 if kind!=1 else .12
    height=.25 if kind!=1 else .18
    verts=[]; faces=[]; longitudinal=32; radial=24
    for j in range(longitudinal+1):
        y=-.45+j*.90/longitudinal
        profile=math.sin(math.pi*j/longitudinal)**.7
        for k in range(radial):
            a=2*math.pi*k/radial
            verts.append((width*profile*math.cos(a),y,height*profile*math.sin(a)))
    for j in range(longitudinal):
        for k in range(radial): faces.append((j*radial+k,j*radial+(k+1)%radial,(j+1)*radial+(k+1)%radial,(j+1)*radial+k))
    body=mesh('Sculpted body',verts,faces,main)
    body.data.materials.append(black); body.data.materials.append(white)
    body.data.update()
    for p in body.data.polygons:
        y=sum(body.data.vertices[v].co.y for v in p.vertices)/len(p.vertices)
        if kind==0 and (abs(y+.24)<.045 or abs(y-.12)<.025): p.material_index=1
        if kind==1:
            d=min(abs(y+.25),abs(y-.02),abs(y-.28))
            p.material_index=2 if d<.032 else 1 if d<.045 else 0
        if kind==2 and -.05<y<.27 and p.center.z>.025: p.material_index=1
    # Head points to -Y; the Nose marker lets Unity align each asset reliably.
    nose=bpy.data.objects.new('Nose',None); bpy.context.collection.objects.link(nose); nose.location=(0,-.47,0)
    ell('Mouth',(0,-.443,-.025),(.035,.024,.025),gold if kind==0 else main)
    for s in [-1,1]:
        ell('Eye surround',(s*.073,-.297,.065),(.031,.049,.05),gold if kind!=1 else white)
        ell('Eye',(s*.093,-.308,.068),(.018,.033,.034),black)
        ell('Catchlight',(s*.106,-.32,.082),(.006,.010,.010),white)
        line('Gill cover',[(s*(.09+.012*math.sin(a)), -.19+.025*math.cos(a),.13*math.cos(a)) for a in [i*math.pi/16 for i in range(17)]],.003,black)
        fin('Pectoral fin',[(s*.085,-.12,-.035),(s*.25,.045,-.13),(s*.21,.15,-.09),(s*.10,.08,-.025)],gold)
        for r in range(4):
            line('Pectoral rays',[(s*.09,-.10,-.04),(s*(.18+r*.015),.03+r*.025,-.10)],.0017,white)
    fin('Dorsal fin',[(0,-.28,.12),(0,-.15,height+.10),(0,.04,height+.12),(0,.30,.13),(0,.36,.04)],gold if kind!=2 else main)
    fin('Anal fin',[(0,-.05,-height*.75),(0,.10,-height-.06),(0,.32,-.14),(0,.37,-.025)],gold)
    for j in range(9):
        y=-.15+j*.052
        top=height+.10 if y<=.04 else (height+.10)*(1-(y-.04)/.26)+.13*((y-.04)/.26)
        base=height*math.sqrt(max(.05,1-(y/.45)**2))*.92
        line('Dorsal fin rays',[(0,y,base),(0,y,top-.012)],.002,white if kind==0 else black)
    pivot=bpy.data.objects.new('Tail',None); bpy.context.collection.objects.link(pivot); pivot.location=(0,.38,0)
    tail=fin('Fan tail',[(0,.36,.025),(0,.64,.21),(0,.60,0),(0,.64,-.21),(0,.36,-.025)],gold if kind!=1 else main)
    tail.parent=pivot; tail.matrix_parent_inverse=pivot.matrix_world.inverted()
    for j in range(9):
        ray=line('Tail ray',[(0,.38,0),(0,.62,-.18+j*.045)],.0025,white if kind==1 else black)
        ray.parent=pivot; ray.matrix_parent_inverse=pivot.matrix_world.inverted()
    save(name,(0,0,0),1.5)

clear()
woods=[mat('Cedar plank %d'%i,(.28+i*.025,.13+i*.014,.055+i*.007),.76) for i in range(5)]
dark=mat('Tar seams',(.075,.045,.024),.9)
rope=mat('Hemp rope',(.52,.40,.22),.9)
iron=mat('Iron nails',(.09,.105,.10),.5)

def hull(y,a,inside=0):
    q=max(0,1-(y/1.72)**2)
    w=max(.004,.78*math.sqrt(q)-inside)
    return (w*math.sin(a),y,.07+.55*(1-math.cos(a))+.20*(abs(y)/1.72)**4+inside)

# Separate curved planks, with actual thickness and dark seams between boards.
for board in range(12):
    a0=-1.39+board*2.78/12+.008; a1=-1.39+(board+1)*2.78/12-.008
    v=[]; f=[]
    for j in range(33):
        y=-1.72+j*3.44/32
        for a in [a0,(a0+a1)/2,a1]: v.append(hull(y,a))
    for j in range(32):
        for k in range(2):
            n=j*3+k; f.append((n,n+3,n+4,n+1))
    o=mesh('Curved cedar hull plank %02d'%board,v,f,woods[board%5])
    m=o.modifiers.new('Solid timber','SOLIDIFY'); m.thickness=.025
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=m.name)
    # Fine grain follows the long axis of each board.
    for d in [-.035,.03]: line('Wood grain',[hull(-1.55+j*3.1/24,(a0+a1)/2+d,.002) for j in range(25)],.0012,dark)
for side in [-1,1]:
    line('Heavy gunwale',[hull(-1.72+j*3.44/40,side*1.39,.012) for j in range(41)],.035,woods[3])
    for j in range(13):
        p=hull(-1.53+j*3.06/12,side*1.26,-.009)
        ell('Forged nail',p,(.009,.009,.009),iron)
for j in range(9):
    y=-1.36+j*.34
    line('Interior rib',[hull(y,-1.35+k*2.7/20,.032) for k in range(21)],.024,woods[0])
for x in [-.24,-.12,0,.12,.24]: box('Floor board',(x,0,.13),(.11,2.55,.045),woods[2])
for y in [-.85,0,.82]:
    w=1.44*math.sqrt(1-(y/1.72)**2)
    box('Thwart seat',(0,y,.47),(w,.24,.055),woods[3])
    for s in [-1,1]: box('Seat brace',(s*w*.37,y,.30),(.05,.10,.31),woods[0])
line('Keel',[(0,-1.72,.20),(0,-1.5,.10),(0,0,.045),(0,1.5,.10),(0,1.72,.25)],.035,dark)
for y in [-1.70,1.70]: box('Stem post',(0,y,.46),(.09,.09,.55),woods[0])
for side in [-1,1]:
    line('Oar shaft',[(side*.38,-1.05,.55),(side*.90,1.1,.63)],.023,woods[3])
    blade=ell('Oar blade',(side*.95,1.30,.635),(.105,.35,.025),woods[2]); blade.rotation_euler.z=-side*.22
    line('Rowlock',[(side*.73,-.3,.57),(side*.76,-.3,.64),(side*.80,-.3,.57)],.012,iron)
for j in range(5):
    r=.12+j*.015
    line('Coiled bow rope',[(r*math.cos(a),-1.17+r*math.sin(a),.39+j*.008) for a in [k*2*math.pi/48 for k in range(49)]],.009,rope)
save('FishingBoat',(0,0,.25),4.5)
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SCENERY_DONE',json.dumps(report))
