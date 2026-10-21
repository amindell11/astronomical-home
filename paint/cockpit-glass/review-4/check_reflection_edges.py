import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

path = Path(sys.argv[1])
pixels = np.asarray(Image.open(path))
height, packed_width = pixels.shape[:2]
width = packed_width // 2
green = pixels[:, :width, 1].astype(float)
edges = []
for row in green[int(height * .35):int(height * .55)]:
    i = np.flatnonzero(row[:width // 2] > 79)[-1]
    edges.append(i + (row[i] - 79) / (row[i] - row[i + 1]))
steps = np.diff(edges) * 512 / width
stationary = np.abs(steps) < .01
longest = max(len(run) for run in ''.join('1' if item else '0' for item in stationary).split('0'))
report = {'texture_size': [packed_width, height], 'maximum_edge_step_at_512': float(np.abs(steps).max()), 'longest_stationary_run_at_512': longest * 512 / height}
print(json.dumps(report, indent=2))
assert report['maximum_edge_step_at_512'] < .6, 'The broad reflection outline has a staircase jump.'
assert report['longest_stationary_run_at_512'] < 12, 'The diagonal reflection outline has a long square step.'
