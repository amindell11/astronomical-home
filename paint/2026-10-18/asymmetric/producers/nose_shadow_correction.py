"""Use the canonical painter to isolate a local owner edit; never change its baseline."""
import hashlib
import shutil
import sys
from pathlib import Path

root = Path('D:/amind/git/agent-4')
sys.path.insert(0, str(root / 'art/tools/ship'))
import ship_contract
import ship_paint

source = root / 'art/ships/vanguard/Vanguard.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
out = root / 'results/vanguard-redo/asymmetric/nose-cleanup'
out.mkdir(exist_ok=True)
layers = out / 'layers'
shutil.copytree(root / 'art/ships/vanguard/layers', layers)
ship_contract.LAYERS_DIR = str(layers)
build = ship_paint.build_material
corrected = 0


def without_nose_sweep(part, cap, region, flags, paint, *args):
    global corrected
    result = build(part, cap, region, flags, paint, *args)
    if part.name == 'MVP Cockpit.001' and cap is not None and set(cap['indices']) == {68, 97, 225, 252}:
        graph = result[1][0]
        candidates = [node for node in graph.tree.nodes
                      if node.bl_idname == 'ShaderNodeMath' and node.operation == 'MULTIPLY'
                      and abs(node.inputs[1].default_value - paint['shadow_depth']) < 1e-6
                      and node.inputs[0].is_linked
                      and node.inputs[0].links[0].from_node.bl_idname == 'ShaderNodeMath'
                      and node.inputs[0].links[0].from_node.operation == 'LESS_THAN']
        assert len(candidates) == 1, len(candidates)
        candidates[0].inputs[1].default_value = 0
        corrected += 1
    return result


ship_paint.build_material = without_nose_sweep
ship_paint.main(['--ship', str(root / 'art/ships/vanguard/ship.json'), '--out', str(out / 'paint'),
                 '--unity', str(root / 'src/Asteroids3D/Assets/Visuals/Ships/Vanguard')])
assert corrected == 1, corrected
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
print('NOSE_SHADOW_CORRECTION_PASS')
