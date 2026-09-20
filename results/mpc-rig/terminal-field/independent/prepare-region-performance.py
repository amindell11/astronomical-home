from pathlib import Path
import shutil

path = Path('D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/TerminalField/TerminalFieldPerformanceTests.cs')
backup = Path(__file__).parent / 'TerminalFieldPerformanceTests-before-region-matrix.cs'
text = path.read_text(encoding='utf-8')
changes = [
    ('density,resolution,obstacles,allocationMs', 'density,resolution,goalRadius,obstacles,allocationMs'),
    ('MeasureSolverLoop(settings, dynamics, field, outDir, density);', 'foreach (var goalRadius in new[] { 0f, 18f })\n                        MeasureSolverLoop(settings, dynamics, field, outDir, density, goalRadius);'),
    ('foreach (var resolution in new[] { 32, 48, 64 })', 'foreach (var resolution in new[] { 32, 48, 64 })\n                    foreach (var goalRadius in new[] { 0f, 18f })'),
    ('goal = new float2(0f, 90f), clearance = clearance,', 'goal = new float2(0f, 90f), goalRadius = goalRadius, clearance = clearance,'),
    ('{density},{resolution},{count},{allocationMs}', '{density},{resolution},{goalRadius},{count},{allocationMs}'),
    ('string outDir, float density)', 'string outDir, float density, float goalRadius)'),
    ('armed = true, referent = 1, weight = 1f', 'armed = true, referent = 1, weight = 1f, setpoint = goalRadius'),
    ('solver-loop-d{density}-', 'solver-loop-d{density}-r{goalRadius}-'),
]
for old, new in changes:
    if old not in text:
        raise ValueError(f'Missing replacement: {old}')
    text = text.replace(old, new)
if not backup.exists():
    shutil.copyfile(path, backup)
path.write_text(text, encoding='utf-8')
print('Extended benchmark to point goals and 18m regions; no Unity launched.')
