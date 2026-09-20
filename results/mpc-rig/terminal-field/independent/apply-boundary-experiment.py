from pathlib import Path
import shutil

root = Path('D:/amind/git/agent-2/src/Asteroids3D/Assets/Scripts')
backup = Path(__file__).parent / 'revision-boundary-extension'
def replace(rel, old, new):
    path = root / rel
    text = path.read_text(encoding='utf-8')
    if old not in text:
        raise ValueError(f'Missing replacement in {rel}: {old}')
    copy = backup / (path.name + '.before-extension')
    if not copy.exists():
        shutil.copyfile(path, copy)
    path.write_text(text.replace(old, new), encoding='utf-8')

view = 'AI/Navigation/MPC/TerminalField/TerminalFieldView.cs'
replace(view, '[ReadOnly] public NativeArray<float> emptyDistances;', '[ReadOnly] public NativeArray<float> emptyDistances;\n        [ReadOnly] public NativeArray<byte> occupied;')
replace(view, '''            if (!math.isfinite(distance))
                return maxFiniteDistance + spacing * (resolution - 1) * math.sqrt(2f);
            if (goalRadius > 0f) return math.max(0f, distance - emptyDistances[index]);''', '''            if (math.isfinite(distance)) return ReachableExcess(index, distance);
            var best = maxFiniteDistance + spacing * (resolution - 1) * math.sqrt(2f);
            if (occupied[index] == 0) return best;
            var x = index % resolution;
            var y = index / resolution;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;
                var next = nx + ny * resolution;
                if (!math.isfinite(distances[next])) continue;
                var edge = spacing * (dx != 0 && dy != 0 ? math.sqrt(2f) : 1f);
                best = math.min(best, ReachableExcess(next, distances[next]) + edge);
            }
            return best;
        }

        private float ReachableExcess(int index, float distance)
        {
            if (goalRadius > 0f) return math.max(0f, distance - emptyDistances[index]);''')
owner = 'AI/Navigation/MPC/TerminalField/TerminalField.cs'
replace(owner, 'new TerminalFieldView { distances = distances, emptyDistances = emptyDistances,', 'new TerminalFieldView { distances = distances, emptyDistances = emptyDistances, occupied = occupied,')
replace(owner, 'emptyDistances = emptyDistances, valid = true,', 'emptyDistances = emptyDistances, occupied = occupied, valid = true,')
solver = 'AI/Navigation/MPC/BurstSolver.cs'
replace(solver, 'private NativeArray<float> emptyTerminalDistances;', 'private NativeArray<float> emptyTerminalDistances;\n        private NativeArray<byte> emptyTerminalOccupied;')
replace(solver, 'emptyDistances = emptyTerminalDistances };', 'emptyDistances = emptyTerminalDistances, occupied = emptyTerminalOccupied };')
replace(solver, 'emptyTerminalDistances = new NativeArray<float>(0, Allocator.Persistent);', 'emptyTerminalDistances = new NativeArray<float>(0, Allocator.Persistent);\n            emptyTerminalOccupied = new NativeArray<byte>(0, Allocator.Persistent);')
replace(solver, 'emptyTerminalDistances.Dispose();', 'emptyTerminalDistances.Dispose();\n            emptyTerminalOccupied.Dispose();')
replace('Editor/Tests/EditMode/TerminalField/TerminalFieldTests.cs', 'emptyDistances = emptyDistances, valid = true,', 'emptyDistances = emptyDistances, occupied = Occupied, valid = true,')
replace('Editor/Tests/EditMode/TerminalField/TerminalFieldGoalRegionTests.cs', 'emptyDistances = EmptyDistances, valid = true,', 'emptyDistances = EmptyDistances, occupied = occupied, valid = true,')
print('Applied boundary extension; originals preserved.')
