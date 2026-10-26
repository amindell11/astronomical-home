#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/capture/assemble.py
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
python - "$SCRIPT_DIR/../capture/assemble.py" "$TMP" <<'PY'
import csv
import importlib.util
import json
import shutil
import subprocess
import sys
from pathlib import Path
from PIL import Image
import imageio_ffmpeg

script, scratch = map(Path, sys.argv[1:])
spec = importlib.util.spec_from_file_location('assemble', script)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
base = scratch / 'valid'
base.mkdir()
times = [0, .015, .030, .065, .080, .115]
colors = [(30 + i * 30, 20 + i * 20, 210 - i * 25) for i in range(len(times))]
rows = []
for index, (time, color) in enumerate(zip(times, colors), 1):
    name = f'f_{index:05d}.png'
    Image.new('RGB', (16, 16), color).save(base / name)
    rows.append([index, name, time, 4.5, index + 50])
manifest = {'width': 16, 'height': 16, 'frameCount': len(rows), 'completed': True, 'frameMetadata': 'frames.csv'}
(base / 'manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
def metadata(folder, data):
    with (folder / 'frames.csv').open('w', encoding='utf-8', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['frame_id', 'filename', 'unscaled_seconds', 'sim_seconds', 'render_id'])
        writer.writerows(data)
metadata(base, rows)
paths = sorted(map(str, base.glob('f_*.png')))
intervals = module.frame_intervals(str(base), paths, manifest)
assert abs(sum(intervals) - .150) < 1e-9, intervals
for format in ('gif', 'mp4'):
    subprocess.run([sys.executable, str(script), str(base), '--format', format, '--keep-frames'], check=True)
with Image.open(base.with_suffix('.gif')) as gif:
    duration = 0
    for i in range(gif.n_frames):
        gif.seek(i)
        duration += gif.info['duration']
    assert duration == sum(module.gif_frame_ms(value) for value in intervals)
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
decoded = subprocess.run([ffmpeg, '-v', 'error', '-i', str(base.with_suffix('.mp4')),
                          '-fps_mode', 'passthrough', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], capture_output=True, check=True).stdout
assert len(decoded) == (len(rows) + 1) * 16 * 16 * 3
for i, expected in enumerate(colors + [colors[-1]]):
    actual = decoded[i * 16 * 16 * 3:i * 16 * 16 * 3 + 3]
    assert max(abs(a - b) for a, b in zip(actual, expected)) < 8, (i, actual, expected)

cases = {
    'missing-row': rows[:-1],
    'extra-row': rows + [rows[-1]],
    'wrong-filename': [[*row[:1], 'wrong.png', *row[2:]] if i == 0 else row for i, row in enumerate(rows)],
    'wrong-id': [[2, *rows[0][1:]], *rows[1:]],
    'duplicate-time': [rows[0], [*rows[1][:2], 0, *rows[1][3:]], *rows[2:]],
    'nonfinite': [[*rows[0][:3], 'nan', rows[0][4]], *rows[1:]],
}
for name, data in cases.items():
    folder = scratch / name
    shutil.copytree(base, folder)
    metadata(folder, data)
    # Speed overrides never relax source identity or completion.
    run = subprocess.run([sys.executable, str(script), str(folder), '--format', 'gif', '--fps', '10'], capture_output=True, text=True)
    assert run.returncode and 'invalid capture contract' in run.stderr, (name, run.stderr)
    assert len(list(folder.glob('*.png'))) == len(rows)
    assert not folder.with_suffix('.gif').exists()
for name in ('extra-png', 'incomplete'):
    folder = scratch / name
    shutil.copytree(base, folder)
    if name == 'extra-png':
        shutil.copyfile(folder / 'f_00001.png', folder / 'unexpected.png')
    else:
        (folder / 'manifest.json').write_text(json.dumps({**manifest, 'completed': False}), encoding='utf-8')
    run = subprocess.run([sys.executable, str(script), str(folder), '--format', 'gif'], capture_output=True, text=True)
    assert run.returncode and 'invalid capture contract' in run.stderr, (name, run.stderr)
    assert folder.exists()
print('PASS: exact frame identity, paused simulation, variable playback, and fail-before-cleanup contract')
PY
