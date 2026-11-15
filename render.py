"""Render the planet sketch offscreen from the same fragment body the tuner page runs.

    python render.py still <planet> <look> <time> <out.png> [--size 1280x720] [--set knob=value ...]
    python render.py sheet <out.png> [--time 40]
    python render.py strip <planet> <look> <out.png> [--seconds 8] [--frames 5]
"""
import argparse
import json
import sys
from pathlib import Path

import moderngl
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = Path(__file__).parent
REPO_TEXTURES = Path("D:/amind/git/astronomical-home/src/Asteroids3D/Assets/Visuals/Locales/Sky/Planets")
SETTINGS = json.loads((HERE / "settings.json").read_text(encoding="utf-8"))

BACKDROPS = {"VerdantGiant": str(HERE.parent / "refs" / "Locale_6-engine-v6.png"),
             "BlueCrescent": str(HERE.parent / "refs" / "Locale_7-engine-v6.png")}

VERT = """#version 330 core
in vec2 position;
void main() { gl_Position = vec4(position, 0.0, 1.0); }
"""


class Renderer:
    def __init__(self):
        self.ctx = moderngl.create_standalone_context()
        body = (HERE / "planet_body.glsl").read_text()
        self.program = self.ctx.program(vertex_shader=VERT, fragment_shader="#version 330 core\n" + body)
        quad = np.array([-1, -1, 3, -1, -1, 3], dtype="f4")
        self.vao = self.ctx.vertex_array(self.program, [(self.ctx.buffer(quad.tobytes()), "2f", "position")])
        self.textures = {}

    def texture(self, name):
        if name not in self.textures:
            image = ImageOps.flip(Image.open(REPO_TEXTURES / f"{name}.png").convert("RGB"))
            tex = self.ctx.texture(image.size, 3, image.tobytes())
            tex.build_mipmaps()
            tex.filter = (moderngl.LINEAR_MIPMAP_LINEAR, moderngl.LINEAR)
            tex.anisotropy = 8.0
            tex.repeat_x = True
            tex.repeat_y = False
            self.textures[name] = tex
        return self.textures[name]

    def backdrop(self, path):
        if path not in self.textures:
            image = ImageOps.flip(Image.open(path).convert("RGB"))
            tex = self.ctx.texture(image.size, 3, image.tobytes())
            tex.filter = (moderngl.LINEAR, moderngl.LINEAR)
            self.textures[path] = tex
        return self.textures[path]

    def render(self, planet_id, knobs, time, size=(1280, 720), backdrop=None, camera=(0.0, 0.0), tint=None):
        planet = dict(SETTINGS["planets"][planet_id])
        if tint:
            planet["tint"] = [tint] * 3
        planet["center"] = [planet["center"][0] - camera[0], planet["center"][1] - camera[1], planet["center"][2]]
        fbo = self.ctx.simple_framebuffer(size)
        fbo.use()
        self.texture(planet["texture"]).use(0)
        if backdrop:
            self.backdrop(backdrop).use(1)
        values = {
            "uMain": 0, "uBackdrop": 1, "uUseBackdrop": 1.0 if backdrop else 0.0, "uRes": size, "uTime": time,
            "uCenter": planet["center"], "uRadius": planet["radius"], "uLight": planet["light"],
            "uTint": planet["tint"], "uAtmo": planet["atmo"], "uSky": planet["sky"],
            "uTilt": planet["tilt"], "uLongitude": planet["longitude"],
            "uRotSpeed": planet["rotSpeed"] * knobs["rotScale"],
            "uJet": knobs["jet"], "uJetBands": knobs["jetBands"], "uEddy": knobs["eddy"],
            "uEddyScale": knobs["eddyScale"], "uFlowCycle": knobs["flowCycle"],
            "uDeck": knobs["deck"], "uDeckCover": knobs["deckCover"], "uDeckSpeed": knobs["deckSpeed"], "uDeckShadow": knobs["deckShadow"],
            "uRimPower": knobs["rimPower"], "uRimIntensity": knobs["rimIntensity"],
            "uHaloWidth": knobs["haloWidth"], "uHaloIntensity": knobs["haloIntensity"],
            "uHaloFalloff": knobs["haloFalloff"], "uNightGlow": knobs["nightGlow"],
        }
        for name, value in values.items():
            if name in self.program:
                self.program[name].value = tuple(value) if isinstance(value, (list, tuple)) else value
        self.vao.render()
        image = Image.frombytes("RGB", size, fbo.read(components=3))
        fbo.release()
        return ImageOps.flip(image)


def look_knobs(look_id, overrides=()):
    knobs = {k: v for k, v in SETTINGS["looks"][look_id].items() if k not in ("label", "note")}
    for item in overrides:
        key, value = item.split("=")
        knobs[key] = float(value)
    return knobs


def font(size):
    for name in ("segoeui.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def label(image, text, sub=None):
    draw = ImageDraw.Draw(image)
    draw.text((16, 12), text, font=font(26), fill=(240, 240, 235), stroke_width=3, stroke_fill=(0, 0, 0))
    if sub:
        draw.text((16, 46), sub, font=font(17), fill=(200, 205, 210), stroke_width=3, stroke_fill=(0, 0, 0))
    return image


def sheet(renderer, out, time, size=(960, 540)):
    looks = list(SETTINGS["looks"])
    planets = list(SETTINGS["planets"])
    gap = 6
    canvas = Image.new("RGB", (len(looks) * (size[0] + gap) - gap, len(planets) * (size[1] + gap) - gap), (0, 0, 0))
    for row, planet in enumerate(planets):
        for col, look in enumerate(looks):
            image = renderer.render(planet, look_knobs(look), time, size, backdrop=BACKDROPS[planet])
            label(image, SETTINGS["looks"][look]["label"], SETTINGS["planets"][planet]["label"])
            canvas.paste(image, (col * (size[0] + gap), row * (size[1] + gap)))
    canvas.save(out)


def strip(renderer, planet, look, out, seconds, frames, start, size=(640, 360), overrides=()):
    gap = 4
    canvas = Image.new("RGB", (frames * (size[0] + gap) - gap, size[1]), (0, 0, 0))
    for index in range(frames):
        time = start + seconds * index / (frames - 1)
        image = renderer.render(planet, look_knobs(look, overrides), time, size, backdrop=BACKDROPS[planet])
        label(image, f"t = {time - start:.0f}s")
        canvas.paste(image, (index * (size[0] + gap), 0))
    canvas.save(out)


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    still = sub.add_parser("still")
    still.add_argument("planet"); still.add_argument("look"); still.add_argument("time", type=float)
    still.add_argument("out"); still.add_argument("--size", default="1280x720")
    still.add_argument("--set", nargs="*", default=[])
    sheet_p = sub.add_parser("sheet")
    sheet_p.add_argument("out"); sheet_p.add_argument("--time", type=float, default=40)
    strip_p = sub.add_parser("strip")
    strip_p.add_argument("planet"); strip_p.add_argument("look"); strip_p.add_argument("out")
    strip_p.add_argument("--seconds", type=float, default=8); strip_p.add_argument("--frames", type=int, default=5)
    strip_p.add_argument("--start", type=float, default=40); strip_p.add_argument("--set", nargs="*", default=[])
    args = parser.parse_args()
    renderer = Renderer()
    if args.command == "still":
        size = tuple(int(v) for v in args.size.split("x"))
        renderer.render(args.planet, look_knobs(args.look, args.set), args.time, size).save(args.out)
    elif args.command == "sheet":
        sheet(renderer, args.out, args.time)
    else:
        strip(renderer, args.planet, args.look, args.out, args.seconds, args.frames, args.start, overrides=args.set)


if __name__ == "__main__":
    sys.exit(main())
