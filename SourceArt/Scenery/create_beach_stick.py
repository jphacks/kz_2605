"""Blender 4.3: a weathered driftwood stick for the beach (grabbable, used to poke the turtle).

Bleached, slightly crooked shaft with grain grooves, cracks, bark remnants, knots, a broken side twig
and a splintered tip. Built along +X (about 1.1 m) and centred on the origin; Unity aligns the longest axis.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'SourceArt/Scenery'
EXPORT = ROOT/'Assets/Resources/Scenery'
NAME = 'BeachStick'
random.seed(2605)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, color, rough=.8):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=rough
    return m

# Material slot order is kept on export; Unity repaints each slot with its base colour.
WOOD, BARK, GRAIN, KNOT = range(4)
mats=[mat('Bleached driftwood',(.43,.38,.31)),mat('Bark remnant',(.17,.11,.07)),
      mat('End grain',(.58,.47,.33)),mat('Dark knot',(.10,.06,.035))]

def noise(*p):
    # Cheap smooth value noise from layered sines (deterministic, no external deps).
    x,y=p[0],p[1] if len(p)>1 else 0
    return (math.sin(x*1.7+y*2.3+1.1)*.5+math.sin(x*3.9-y*1.3+2.7)*.3+math.sin(x*7.3+y*5.1+.4)*.2)

LENGTH=1.1
RINGS=72
RADIAL=28
cracks=[(random.uniform(0,2*math.pi),random.uniform(.05,.45),random.uniform(.2,.4)) for _ in range(5)]
knots=[.22,.47,.78]

def centre(t):
    # One long gentle bow plus a small kink near the twig, like a branch that grew towards light.
    return Vector((t*LENGTH-LENGTH/2, .03*math.sin(math.pi*t)+.012*math.tanh((t-.6)*14), .012*math.sin(1.3*math.pi*t+.5)))

def bark(t,a):
    # Long strips along the grain: low frequency along the shaft, mostly on the thick end.
    return noise(t*3.2+3,a*.8)+.5*math.sin(a*2+t*4)*.4+.7*(1-t)-.62>.18

def radius(t,a):
    r=.027+(.017-.027)*t                       # thicker grip end, thinner tip
    r*=1+.10*math.exp(-((t-.05)/.05)**2)       # slight flare at the old break
    for k in knots: r*=1+.16*math.exp(-((t-k)/.025)**2)*max(0,math.cos(a-k*9))
    r*=1+.045*math.cos(7*a+3*t)+.03*noise(t*14,a*2)   # grain ridges
    for ca,c0,cl in cracks:
        if c0<t<c0+cl:
            fade=math.sin((t-c0)/cl*math.pi)
            r*=1-.20*fade*math.exp(-((math.atan2(math.sin(a-ca),math.cos(a-ca)))/.12)**2)
    if bark(t,a): r*=1.05                     # bark sits as a thin raised layer over the bare wood
    return r

verts=[]; faces=[]; face_mats=[]
def frame(t):
    tan=(centre(min(1,t+.01))-centre(max(0,t-.01))).normalized()
    n=tan.cross(Vector((0,0,1))).normalized(); b=tan.cross(n).normalized()
    return tan,n,b

for i in range(RINGS):
    t=i/(RINGS-1)
    c=centre(t); tan,n,b=frame(t)
    for j in range(RADIAL):
        a=j/RADIAL*2*math.pi
        r=radius(t,a)
        p=c+(n*math.cos(a)+b*math.sin(a))*r
        if i==RINGS-1:   # splintered break: long and short slivers along the shaft
            p+=tan*(random.uniform(0,.018)+(.05 if j%7==2 else 0)+(.03 if j%5==0 else 0))
        verts.append(p)
for i in range(RINGS-1):
    t=(i+.5)/(RINGS-1)
    for j in range(RADIAL):
        a=(j+.5)/RADIAL*2*math.pi
        q=(i*RADIAL+j,i*RADIAL+(j+1)%RADIAL,(i+1)*RADIAL+(j+1)%RADIAL,(i+1)*RADIAL+j)
        faces.append(q)
        face_mats.append(BARK if bark(t,a) else WOOD)

def cap(ring_index,depth,outward):
    t=ring_index/(RINGS-1); tan,_,_=frame(t)
    ring=[ring_index*RADIAL+j for j in range(RADIAL)]
    ctr=sum((verts[k] for k in ring),Vector())/RADIAL+tan*depth*outward
    verts.append(ctr); ci=len(verts)-1
    for j in range(RADIAL):
        a,b2=ring[j],ring[(j+1)%RADIAL]
        faces.append((ci,b2,a) if outward>0 else (ci,a,b2)); face_mats.append(GRAIN)
cap(0,.004,-1)               # old, worn cut at the grip end
cap(RINGS-1,-.01,1)          # torn heart wood sits a little inside the splinters

data=bpy.data.meshes.new(NAME); data.from_pydata([tuple(v) for v in verts],[],faces); data.update()
stick=bpy.data.objects.new(NAME,data); bpy.context.collection.objects.link(stick)
for m in mats: data.materials.append(m)
for p,mi in zip(data.polygons,face_mats): p.material_index=mi; p.use_smooth=True

def tube(name,start,direction,length,r0,r1,rings=10,radial=10):
    direction=direction.normalized()
    n=direction.cross(Vector((0,0,1))).normalized(); b=direction.cross(n)
    vs=[];fs=[];ms=[]
    for i in range(rings):
        t=i/(rings-1); c=start+direction*length*t
        for j in range(radial):
            a=j/radial*2*math.pi; r=(r0+(r1-r0)*t)*(1+.06*math.cos(5*a))
            vs.append(c+(n*math.cos(a)+b*math.sin(a))*r+(direction*random.uniform(0,.008) if i==rings-1 else Vector()))
    for i in range(rings-1):
        for j in range(radial):
            fs.append((i*radial+j,i*radial+(j+1)%radial,(i+1)*radial+(j+1)%radial,(i+1)*radial+j)); ms.append(BARK if i<3 else WOOD)
    vs.append(start+direction*length*.97); ci=len(vs)-1
    for j in range(radial): fs.append((ci,(rings-1)*radial+j,(rings-1)*radial+(j+1)%radial)); ms.append(GRAIN)
    d=bpy.data.meshes.new(name); d.from_pydata([tuple(v) for v in vs],[],fs); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o)
    for m in mats: d.materials.append(m)
    for p,mi in zip(d.polygons,ms): p.material_index=mi; p.use_smooth=True
    return o

# Broken side twig, sunk into the shaft so the join hides inside.
t=.6; tan,n,b=frame(t)
twig=tube('Twig',centre(t)+n*.008,tan*.9+n*.75+b*.25,.10,.011,.007)

parts=[stick,twig]
for k,t in enumerate(knots):
    tan,n,b=frame(t); a=t*9
    out=n*math.cos(a)+b*math.sin(a)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=centre(t)+out*radius(t,a)*.9)
    o=bpy.context.object; o.name=f'Knot{k}'
    o.scale=(.018,.018,.007); o.rotation_euler=out.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    o.data.materials.append(mats[KNOT])
    for p in o.data.polygons: p.use_smooth=True
    parts.append(o)

bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active=stick
bpy.ops.object.join()
stick=bpy.context.object; stick.name=NAME
mod=stick.modifiers.new('Soft normals','WEIGHTED_NORMAL'); mod.keep_sharp=True
bpy.ops.object.modifier_apply(modifier=mod.name)

bpy.ops.object.select_all(action='DESELECT'); stick.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(EXPORT/(NAME+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
dims=stick.dimensions
report={'vertices':len(stick.data.vertices),'triangles':sum(len(p.vertices)-2 for p in stick.data.polygons),
        'size_m':[round(d,3) for d in dims],'materials':[m.name for m in stick.data.materials]}
(OUT/'beach_stick_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

# Preview: lying on warm sand under soft daylight.
sc=bpy.context.scene
bpy.ops.mesh.primitive_plane_add(size=4,location=(0,0,-.035))
sand=bpy.context.object; sand.name='Preview sand'; sand.data.materials.append(mat('Preview sand',(.86,.76,.56),.95))
bpy.ops.object.camera_add(location=(.25,-1.25,.62))
cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.lens=40; sc.camera=cam
bpy.ops.object.light_add(type='SUN',location=(2,-2,4)); sun=bpy.context.object; sun.data.energy=2.2; sun.rotation_euler=(math.radians(40),0,math.radians(35))
bpy.ops.object.light_add(type='AREA',location=(-1.5,-1,1.2)); fill=bpy.context.object; fill.data.energy=60; fill.data.size=3
fill.rotation_euler=(Vector((0,0,0))-fill.location).to_track_quat('-Z','Y').to_euler()
sc.world.color=(.22,.27,.33); sc.view_settings.view_transform='Standard'; sc.view_settings.exposure=-.6
sc.render.engine='CYCLES'; sc.cycles.samples=32
sc.render.resolution_x=1200; sc.render.resolution_y=700; sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG'; sc.render.filepath=str(OUT/(NAME+'_Preview.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(NAME+'.blend')))
bpy.ops.render.render(write_still=True)
print('BEACH_STICK', json.dumps(report))
