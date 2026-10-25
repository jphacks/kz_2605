"""Blender 4.3: give the aged hand meshes the same finger bones as the normal hands.

old_hand/old_handL.fbx and old_handR.fbx are static meshes (no armature) with the same size and pose as
hand/Resources/Hands/Hand_L.fbx and Hand_R.fbx. This copies the normal hand's armature and transfers its skin
weights onto the aged mesh (nearest surface), so HandVisual can curl the aged fingers the same way.
Output: hand/Resources/Hands/HandOld_L.fbx and HandOld_R.fbx (the original files are left untouched).

    & 'C:\\Program Files\\Blender Foundation\\Blender 4.3\\blender.exe' --background --python SourceArt/Characters/rig_old_hands.py
"""
import bpy, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'Assets/_Project/Art/Models'
OUT = MODELS / 'hand/Resources/Hands'
report = {}

for side in ['L', 'R']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT / f'Hand_{side}.fbx'))
    armature = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    young = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    bpy.ops.import_scene.fbx(filepath=str(MODELS / f'old_hand/old_hand{side}.fbx'))
    old = next(o for o in bpy.context.scene.objects if o.type == 'MESH' and o != young)

    for g in young.vertex_groups: old.vertex_groups.new(name=g.name)
    mod = old.modifiers.new('Copy hand weights', 'DATA_TRANSFER')
    mod.object = young
    mod.use_vert_data = True
    mod.data_types_verts = {'VGROUP_WEIGHTS'}
    mod.vert_mapping = 'POLYINTERP_NEAREST'
    mod.layers_vgroup_select_src = 'ALL'
    mod.layers_vgroup_select_dst = 'NAME'
    bpy.context.view_layer.objects.active = old
    bpy.ops.object.datalayout_transfer(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)

    world = old.matrix_world.copy()
    old.parent = armature
    old.matrix_world = world
    arm_mod = old.modifiers.new('Armature', 'ARMATURE')
    arm_mod.object = armature
    unweighted = sum(1 for v in old.data.vertices if not any(g.weight > 0 for g in v.groups))

    bpy.data.objects.remove(young, do_unlink=True)
    old.name = f'HandOld.{side}'
    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True); old.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / f'HandOld_{side}.fbx'), use_selection=True,
                             object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False)
    report[side] = {'vertices': len(old.data.vertices), 'unweighted': unweighted,
                    'groups': len(old.vertex_groups), 'bones': len(armature.data.bones)}

print('OLD_HANDS', json.dumps(report))
