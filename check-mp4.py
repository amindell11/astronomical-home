import csv
import json
import re
import subprocess
from pathlib import Path
import imageio_ffmpeg

root = Path('results/canopy-events')
rows = list(csv.DictReader((root / 'frames.csv').open(encoding='utf-8')))
times = [float(row['unscaled_seconds']) for row in rows]
expected = [t - times[0] for t in times]
expected.append(expected[-1] + times[-1] - times[-2])
result = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-i', str(root / 'canopy-events.mp4'),
    '-vf', 'showinfo', '-fps_mode', 'passthrough', '-f', 'null', '-'], capture_output=True, text=True)
assert result.returncode == 0, result.stderr[-2000:]
decoded = [float(value) for value in re.findall(r'\bn:\s*\d+\s+pts:\s*-?\d+\s+pts_time:([\d.]+)', result.stderr)]
assert len(decoded) == len(expected), (len(decoded), len(expected))
error = max(abs(a - b) for a, b in zip(decoded, expected))
assert error < .001, error
report = {'source_frames': len(rows), 'decoded_frames': len(decoded), 'terminal_repeat': True,
    'expected_end_seconds': expected[-1], 'decoded_end_seconds': decoded[-1],
    'maximum_timestamp_error_seconds': error, 'verdict': 'passed',
    'shared_verifier': 'Rejected FFmpeg progress-clock duration. Known issue #1185; actual presentation timestamps verified independently.'}
(root / 'mp4-verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report))
