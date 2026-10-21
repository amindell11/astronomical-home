"""Assemble the browser captures into a quarter-speed comparison GIF."""
from pathlib import Path
from PIL import Image

root = Path(__file__).parent
frames = [Image.open(p).convert("RGB") for p in sorted((root / "frames").glob("*.png"))]
sample = Image.new("RGB", (frames[0].width, frames[0].height * 4))
for index, frame in enumerate((frames[0], frames[11], frames[17], frames[27])):
    sample.paste(frame, (0, frame.height * index))
palette = sample.quantize(colors=256)
indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
indexed[0].save(root / "motion-quarter-speed.gif", save_all=True, append_images=indexed[1:], duration=80, loop=0, optimize=True)
print(f"Saved {len(indexed)} frames at quarter speed.")
