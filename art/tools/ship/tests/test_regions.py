import copy
from pathlib import Path
import sys
import unittest
from types import SimpleNamespace

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ship_regions as regions


def mark(shape, **changes):
    return {"parts": ["Hull"], "region": "Accent", "mirror_x": False,
            "where": {}, "shape": shape, "opacity": 1, **changes}


class AuthoredRegionTests(unittest.TestCase):
    def test_independent_sides_detects_shared_uvs_in_center_crossing_faces(self):
        def panel(xs, offset=0):
            coords = [(offset, 0), (offset + .4, 0), (offset + .4, .4), (offset, .4)]
            uv = SimpleNamespace(uv=[SimpleNamespace(vector=p) for p in coords])
            triangles = [SimpleNamespace(loops=t, vertices=t, polygon_index=0)
                         for t in ((0, 1, 2), (0, 2, 3))]
            mesh = SimpleNamespace(uv_layers={"PaintUV": uv}, loop_triangles=triangles,
                                   calc_loop_triangles=lambda: None)
            return SimpleNamespace(mesh=mesh, co=np.array([(x, y, 0) for x, y in zip(xs, (0, 0, 1, 1))]))

        center = panel([-1, 1, 1, -1])
        regions.require_independent_sides({"Center": center}, 64, "PaintUV")
        mirrored = panel([1, -1, -1, 1])
        with self.assertRaisesRegex(ValueError, "independent sides"):
            regions.require_independent_sides({"Center": center, "Mirror": mirrored}, 64, "PaintUV")
        left, right = panel([-2, -1, -1, -2]), panel([1, 2, 2, 1], .5)
        regions.require_independent_sides({"Left": left, "Right": right}, 64, "PaintUV")
        with self.assertRaisesRegex(ValueError, "independent sides"):
            regions.require_independent_sides({"Left": left, "Right": panel([1, 2, 2, 1])}, 64, "PaintUV")
        regions.require_independent_sides({"Left": left, "Same side": left}, 64, "PaintUV")

    def test_shapes_keep_signed_and_mirrored_placement(self):
        points = np.array([[-.5, 0, 0], [.5, 0, 0], [.5, .5, 0], [1.5, 0, 0]])
        rectangle = {"kind": "polygon", "points": [[.2, -.2], [.8, -.2], [.8, .2], [.2, .2]]}
        self.assertEqual(regions.coverage(mark(rectangle), points, 1).tolist(), [False, True, False, False])
        self.assertEqual(regions.coverage(mark(rectangle, mirror_x=True), points, 1).tolist(), [True, True, False, False])
        circle = {"kind": "circle", "center": [.5, 0], "radius": .2}
        self.assertEqual(regions.coverage(mark(circle, where={"nz": [.28, None]}), points, 0).sum(), 0)
        path = {"kind": "path", "points": [[0, 0], [1, 0]], "width": .1, "closed": False}
        self.assertEqual(regions.coverage(mark(path), points, 1).tolist(), [False, True, False, False])
        box = mark({"kind": "box"}, where={"x": [.2, .8], "y": [-.1, .1]})
        self.assertEqual(regions.coverage(box, points, 1).tolist(), [False, True, False, False])

    def test_invalid_authoring_data_is_rejected(self):
        valid = {"schema_version": 1, "marks": [mark({"kind": "polygon", "points": [[0, 0], [1, 0], [0, 1]]})]}
        regions.parse(valid)
        changes = [lambda v: v.update(schema_version=True),
                   lambda v: v["marks"][0].update(opacity=float("nan")),
                   lambda v: v["marks"][0].update(parts=["Hull", "Hull"]),
                   lambda v: v["marks"][0].update(where={"x": [1, 0]}),
                   lambda v: v["marks"][0]["shape"].update(points=[[0, 0], [1, 0], [2, 0]]),
                   lambda v: v["marks"][0]["shape"].update(points=[[0, 0], [0, 0], [1, 1]])]
        for change in changes:
            value = copy.deepcopy(valid)
            change(value)
            with self.assertRaises(ValueError):
                regions.parse(value)

    def test_packing_keeps_alpha_and_palette_changes_only_recompose_colour(self):
        names = ["Accent", "Frame", "Ink", "Primary", "Signal"]
        masks = {regions.PREFIX + name: np.zeros((2, 2), np.uint8) for name in names}
        masks["Region_Primary"][0, 0] = 255
        masks["Region_Accent"][1, 1] = 255
        packed = regions.pack(masks, names)
        self.assertEqual(len(packed), 2)
        self.assertEqual(packed[0][0, 0].tolist(), [0, 0, 0, 255])
        colours = {name: [0, 0, 0, 1] for name in names}
        colours["Primary"], colours["Accent"] = [1, 1, 1, 1], [1, .2, 0, 1]
        first = regions.compose(masks, names, colours)
        colours["Accent"] = [.2, 0, 1, 1]
        second = regions.compose(masks, names, colours)
        np.testing.assert_allclose(first[0, 0], second[0, 0])
        np.testing.assert_allclose(second[1, 1], [.2, 0, 1])
        self.assertEqual(packed[0][1, 1].tolist(), [255, 0, 0, 0])


if __name__ == "__main__":
    unittest.main()
