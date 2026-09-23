"""Windowed Blender regression for grouped colors, independent randomizers, locks and Undo."""
import argparse
import colorsys
import json
from pathlib import Path
import sys
import traceback

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import skybox
from skybox import skybox_panel as panel

parser = argparse.ArgumentParser()
parser.add_argument('--out', required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index('--') + 1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)
skybox.register()
area = next(a for a in bpy.context.screen.areas if a.type == 'VIEW_3D')
region = next(r for r in area.regions if r.type == 'WINDOW')
bpy.context.preferences.view.show_splash = False


def run():
    try:
        with bpy.context.temp_override(area=area, region=region):
            s = panel.get_settings(bpy.context.scene)
            s.output_name = 'keep-name'
            s.unity_project = 'D:/keep-project'
            before = panel.to_preset(s)
            for key in panel.NEBULA_FIELDS + panel.COLOR_FIELDS:
                setattr(s, 'lock_' + key, True)
            bpy.ops.skybox.randomize(target='NEBULA')
            assert panel.to_preset(s) == before, 'Nebula locks failed'
            for action in ('BASE', 'RANDOM', 'SEED'):
                s.palette_scheme = 'ICE'
                bpy.ops.skybox.palette(action=action)
                assert panel.to_preset(s) == before, 'Palette generation changed a locked color'
            s.lock_scale = False
            s.lock_palette2 = False
            bpy.ops.skybox.randomize(target='NEBULA')
            after = panel.to_preset(s)
            assert after['nebula']['scale'] != before['nebula']['scale']
            assert after['nebula']['palette'][2] != before['nebula']['palette'][2]
            after['nebula']['scale'] = before['nebula']['scale']
            after['nebula']['palette'][2] = before['nebula']['palette'][2]
            assert after == before, 'Nebula randomization escaped its unlocked fields'
            for key in ('tiny_brightness', 'anchor_brightness'):
                setattr(s, 'lock_' + key, True)
            for star in s.anchors:
                for key in panel.STAR_FIELDS:
                    setattr(star, 'lock_' + key, True)
            before = panel.to_preset(s)
            bpy.ops.skybox.randomize(target='STARS')
            assert panel.to_preset(s) == before, 'Star locks failed'
            star = s.anchors[2]
            star.lock_longitude = False
            star.lock_strength = False
            lat = star.latitude
            bpy.ops.skybox.randomize(target='STARS')
            after = panel.to_preset(s)
            assert abs(star.latitude - lat) < 1e-6, 'Locked latitude moved'
            assert after['anchors'][2]['direction'] != before['anchors'][2]['direction']
            assert after['anchors'][2]['strength'] != before['anchors'][2]['strength']
            after['anchors'][2] = before['anchors'][2]
            assert after == before, 'Star randomization escaped its unlocked fields'
            assert (s.output_name, s.unity_project, s.draft_width) == ('keep-name', 'D:/keep-project', '1024')
            for channel in ('HUE', 'SATURATION', 'VALUE'):
                for i in range(4):
                    setattr(s, f'palette{i}', (0.12, 0.24, 0.4))
                before_colors = [list(getattr(s, key)) for key in panel.COLOR_FIELDS]
                bpy.ops.skybox.color_adjust(channel=channel)
                for i in range(4):
                    color = list(getattr(s, f'palette{i}'))
                    assert color != before_colors[i], 'Group edit incorrectly respected a lock'
                    h, sat, val = colorsys.rgb_to_hsv(*color)
                    h0, sat0, val0 = colorsys.rgb_to_hsv(*before_colors[i])
                    expected = {'HUE': (h0 + 1/36, sat0, val0), 'SATURATION': (h0, sat0+.05, val0), 'VALUE': (h0, sat0, val0*1.1)}[channel]
                    assert max(abs(a-b) for a,b in zip((h,sat,val),expected)) < 1e-6
                bpy.ops.skybox.color_adjust(channel=channel, decrease=True)
                assert max(abs(a-b) for key, old in zip(panel.COLOR_FIELDS, before_colors) for a,b in zip(getattr(s,key),old)) < 1e-6
            for operator, kwargs in ((bpy.ops.skybox.color_adjust, {'channel': 'HUE'}),
                                     (bpy.ops.skybox.randomize, {'target': 'NEBULA'}),
                                     (bpy.ops.skybox.randomize, {'target': 'STARS'})):
                s = panel.get_settings(bpy.context.scene)
                before = panel.to_preset(s)
                bpy.ops.ed.undo_push(message='Before sky adjustment')
                operator('EXEC_DEFAULT', True, **kwargs)
                assert panel.to_preset(s) != before
                bpy.ops.ed.undo()
                s = panel.get_settings(bpy.context.scene)
                assert panel.to_preset(s) == before, 'Undo did not restore sky settings'
            bpy.ops.object.mode_set(mode='EDIT')
            assert not bpy.ops.skybox.color_adjust.poll()
            assert not bpy.ops.skybox.randomize.poll()
            bpy.ops.object.mode_set(mode='OBJECT')
            area.spaces.active.show_region_ui = True
            bpy.ops.wm.save_as_mainfile(filepath=str(out / 'locks.blend'))
            assert s.lock_palette0 and s.anchors[0].lock_longitude
            assert not s.lock_scale and not s.anchors[2].lock_longitude
        (out / 'result.json').write_text(json.dumps({'success': True}))
    except Exception:
        (out / 'result.json').write_text(json.dumps({'success': False, 'error': traceback.format_exc()}))
    bpy.app.timers.register(lambda: bpy.ops.wm.quit_blender() and None, first_interval=0.2)


bpy.app.timers.register(run, first_interval=1)
