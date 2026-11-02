import bpy
import bmesh
from mathutils import Vector
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = str(PROJECT / 'Assets/Props/PuertasIsoplanas/puertas_isoplanas.glb')
OUTPUT = str(PROJECT / 'Assets/Props/PuertasIsoplanas/isoplana_hoja_01.glb')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=SOURCE)

# The download is a showroom containing three doors, labels and a floor.
# Keep the first door's panels, backing, trim and hardware by their location.
for obj in list(bpy.data.objects):
    if obj.type != 'MESH':
        continue
    mesh_name = obj.data.name
    if mesh_name not in {'Object_0', 'Object_6', 'Object_9', 'Object_10'}:
        bpy.data.objects.remove(obj, do_unlink=True)
        continue
    mesh = obj.data
    editable = bmesh.new()
    editable.from_mesh(mesh)
    discard = []
    for face in editable.faces:
        center = face.calc_center_median()
        if not (2050 < center.x < 3350 and -2450 < center.y < -40 and 3800 < center.z < 4200):
            discard.append(face)
    bmesh.ops.delete(editable, geom=discard, context='FACES')
    if not editable.faces:
        editable.free()
        bpy.data.objects.remove(obj, do_unlink=True)
        continue
    for vertex in editable.verts:
        x, y, z = vertex.co
        height = (y + 2386) * 0.001
        if mesh_name == 'Object_9':
            height += 0.9
        vertex.co = Vector(((x - 2700) * 0.001, (z - 4000) * 0.001, height))
    editable.to_mesh(mesh)
    editable.free()
    mesh.update()
    obj.parent = None
    obj.matrix_world.identity()
    print(f"ISOPLANA {mesh_name}: faces={len(mesh.polygons)} bounds={[(min(v.co[i] for v in mesh.vertices), max(v.co[i] for v in mesh.vertices)) for i in range(3)]}")

bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=OUTPUT, export_format='GLB', use_selection=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(PROJECT / 'Assets/Props/PuertaDeMadera/puerta_de_madera.glb'))
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        for vertex in obj.data.vertices:
            x, y, z = vertex.co
            vertex.co = Vector(((x - 45.19475) * 0.01, (z - 10.81769) * 0.01, (y + 221.9983) * 0.01))
        obj.parent = None
        obj.matrix_world.identity()
        print(f"WOOD LEAF {obj.name}: {[(min(v.co[i] for v in obj.data.vertices), max(v.co[i] for v in obj.data.vertices)) for i in range(3)]}")
        for side in (-1, 1):
            candidates = [v.co for v in obj.data.vertices if v.co.x * side > 0.25 and 0.7 < v.co.z < 1.5]
            print(f"WOOD HANDLE SIDE {side}: vertices={len(candidates)} depth={[(min(p.y for p in candidates), max(p.y for p in candidates))] if candidates else []}")

bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        obj.select_set(True)
bpy.ops.export_scene.gltf(
    filepath=str(PROJECT / 'Assets/Props/PuertaDeMadera/puerta_de_madera_hoja.glb'),
    export_format='GLB', use_selection=True)
