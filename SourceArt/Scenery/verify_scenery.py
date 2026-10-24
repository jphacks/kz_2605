"""Reimport exported Blender scenery and validate game-facing structure."""
import bpy, json, math
from pathlib import Path

root=Path(__file__).resolve().parents[2]
report={}
for name in ['ReefButterfly','ReefClownfish','ReefTang','FishingBoat']:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Resources/Scenery'/(name+'.fbx')))
    objects=list(bpy.context.scene.objects)
    meshes=[o for o in objects if o.type=='MESH']
    assert len(meshes)==(1 if name=='FishingBoat' else 2),(name,'Unexpected mesh count')
    assert all(math.isfinite(n) for o in meshes for v in o.data.vertices for n in v.co)
    assert all(o.data.materials for o in meshes),(name,'Missing materials')
    if name!='FishingBoat':
        assert any(o.name=='Nose' for o in objects),(name,'Missing forward marker')
        tail=next(o for o in objects if o.name=='Tail')
        assert any(o.parent==tail for o in meshes),(name,'Tail hierarchy lost')
    report[name]={'fbx_reimport':True,'meshes':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes)}
(root/'SourceArt/Scenery/fbx_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SCENERY_VALIDATION_OK',json.dumps(report))
