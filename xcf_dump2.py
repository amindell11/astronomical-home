import json, os
from gi.repository import Gimp, Gio
D = os.environ.get("ICON_DIR", r"C:\Users\amind\AppData\Local\Temp\claude\882050741e06a72d\0f6d02b4-a1ea-43bd-9c3c-1b1e7470b41a\scratchpad\icon")
img = Gimp.file_load(Gimp.RunMode.NONINTERACTIVE, Gio.File.new_for_path(os.path.join(D, "icon_ship_edit.xcf")))
out = os.path.join(D, "owner2")
os.makedirs(out, exist_ok=True)
meta = {"w": img.get_width(), "h": img.get_height(), "layers": []}
for i, layer in enumerate(img.get_layers()):
    ok, x, y = layer.get_offsets()
    name = layer.get_name()
    fn = os.path.join(out, "%02d.png" % i)
    tmp = Gimp.Image.new(layer.get_width(), layer.get_height(), Gimp.ImageBaseType.RGB)
    nl = Gimp.Layer.new_from_drawable(layer, tmp)
    tmp.insert_layer(nl, None, 0)
    nl.set_offsets(0, 0)
    nl.set_visible(True)
    Gimp.file_save(Gimp.RunMode.NONINTERACTIVE, tmp, Gio.File.new_for_path(fn), None)
    tmp.delete()
    meta["layers"].append({"i": i, "name": name, "x": x, "y": y, "w": layer.get_width(), "h": layer.get_height(),
                           "visible": layer.get_visible(), "opacity": layer.get_opacity(), "mode": str(layer.get_mode())})
flat = img.duplicate()
flat.merge_visible_layers(Gimp.MergeType.CLIP_TO_IMAGE)
Gimp.file_save(Gimp.RunMode.NONINTERACTIVE, flat, Gio.File.new_for_path(os.path.join(out, "composite.png")), None)
json.dump(meta, open(os.path.join(out, "layers.json"), "w"), indent=1)
Gimp.Procedure  # keep import used
