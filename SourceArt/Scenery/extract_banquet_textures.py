"""Extract packed source textures without modifying the supplied Blender file."""
import bpy
from pathlib import Path

root = Path(__file__).resolve().parents[2]
target = root / 'Assets/_Project/Art/Models/enkaijou/Textures'
target.mkdir(parents=True, exist_ok=True)
for image in bpy.data.images:
    if image.packed_file:
        path = target / (image.name + '.png')
        path.write_bytes(bytes(image.packed_file.data))
        print('EXTRACTED', path.name)
