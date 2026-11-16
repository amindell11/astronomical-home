import importlib.util
from pathlib import Path
here = Path(__file__).parent
spec = importlib.util.spec_from_file_location("k6", here.parent / "k6" / "make_prompts.py")
src = (here.parent / "k6" / "make_prompts.py").read_text(encoding="utf-8")
BASE = src.split('BASE = """', 1)[1].split('"""', 1)[0]
V = {
 "sliver": """Concept: PINCH SHUT, SPLIT OPEN, with a destination mark. The arrival point is marked from the very start of the wind-up.
Wind-up: at the departure point two tall angular streaks close in on the ship from its left and right like closing doors, squeezing it. At the same moment, at the arrival point ahead, a single thin vertical sliver of the same red streak appears in empty space, exactly where the ship will appear: the mark.
Vanish: the departure point has pinched down to a thin vertical sliver; the ship is gone. The mark sliver at the arrival point is still there.
Arrive: the mark sliver splits open sideways, two tall angular streaks swinging outward left and right, revealing the ship between them.
After: the ship flies on; the two streaks at the arrival point fall away to each side and fade; the departure sliver has faded.""",
 "outline": """Concept: PINCH SHUT, SPLIT OPEN, with a destination mark.
Wind-up: at the departure point two tall angular streaks close in on the ship from its left and right like closing doors, squeezing it. At the same moment, at the arrival point ahead, a faint hollow outline of the ship's silhouette drawn in a few broad red streaks appears: the mark showing where it will land.
Vanish: the departure point has pinched down to a thin vertical sliver; the ship is gone. The outline mark is still at the arrival point.
Arrive: the ship appears inside the outline, which splits open sideways as two tall angular streaks swinging outward left and right.
After: the ship flies on; the two streaks fall away to each side and fade.""",
 "doors": """Concept: PINCH SHUT, SPLIT OPEN, with a destination mark.
Wind-up: at the departure point two tall angular streaks close in on the ship from its left and right like closing doors, squeezing it. At the same moment, at the arrival point ahead, a matching pair of tall angular streaks stands already CLOSED together into a tall pointed vertical lens shape, as tall as the ship: the closed door waiting where the ship will land.
Vanish: the departure doors have pinched shut to a thin sliver; the ship is gone. The closed lens still stands at the arrival point.
Arrive: the closed lens at the arrival point swings open sideways, its two streaks swinging outward left and right, revealing the ship between them.
After: the ship flies on; the opened streaks fall away to each side and fade.""",
}
for k, body in V.items():
    d = here / f"run_{k}"; d.mkdir(exist_ok=True)
    (d / "prompt.txt").write_text(BASE + body, encoding="utf-8")
print("ok")
