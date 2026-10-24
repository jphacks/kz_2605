import bpy
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'SourceArt/Turtle/SeaTurtle.blend'))
rig = bpy.data.objects['SeaTurtle']
mesh = bpy.data.objects['SeaTurtle_Mesh']
assert len(rig.data.bones) == 8
assert all(v.groups for v in mesh.data.vertices), 'Unweighted vertices'
report = {'vertices': len(mesh.data.vertices), 'polygons': len(mesh.data.polygons), 'bones': len(rig.data.bones), 'actions': {}}
for name, end in [('Swim',61),('Float',121)]:
    rig.animation_data.action = bpy.data.actions[name]
    samples = []
    for frame in [1, (end-1)//4+1, end]:
        bpy.context.scene.frame_set(frame)
        samples.append([tuple(x for row in b.matrix for x in row) for b in rig.pose.bones])
    assert max(abs(a-b) for ma,mb in zip(samples[0],samples[2]) for a,b in zip(ma,mb)) < 1e-5
    assert max(abs(a-b) for ma,mb in zip(samples[0],samples[1]) for a,b in zip(ma,mb)) > .01
    report['actions'][name] = {'frames':end, 'seconds':(end-1)/30, 'loop_checked':True}
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for action in list(bpy.data.actions): bpy.data.actions.remove(action)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Models/Turtle/SeaTurtle.fbx'))
report['fbx_actions'] = [a.name for a in bpy.data.actions]
assert any('Swim' in a.name for a in bpy.data.actions)
assert any('Float' in a.name for a in bpy.data.actions)
assert any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
(root/'SourceArt/Turtle/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VALIDATION_OK',json.dumps(report))
