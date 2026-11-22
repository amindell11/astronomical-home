"""Apply the supplied game projection and return its Blender/Unity parameters."""
import math


VIEWS = [('16x10', 1920, 1200), ('3x2', 1620, 1080), ('4x3', 1440, 1080),
         ('16x9', 1920, 1080), ('21x9', 2560, 1080), ('32x9', 3840, 1080)]


def game_projection(scene, camera, width, height):
    aspect = width / height
    reference = 16 / 9
    tangent = math.tan(math.radians(34.11393) / 2) * max(1, reference / aspect)
    scale = 1.01 if aspect < reference else 1
    m12 = -(1 - aspect / reference) * .36 if aspect < reference else 0
    effective_tangent = tangent / scale
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1
    camera.data.sensor_width, camera.data.sensor_height = 36, 24
    camera.data.sensor_fit = 'AUTO' if aspect == reference else 'HORIZONTAL'
    camera.data.lens = 18 / (aspect * effective_tangent)
    camera.data.shift_x = 0
    camera.data.shift_y = m12 / (2 * aspect)
    return {'resolution': [width, height], 'aspect': aspect,
            'vertical_fov_degrees': math.degrees(2 * math.atan(tangent)),
            'effective_vertical_fov_degrees': math.degrees(2 * math.atan(effective_tangent)),
            'horizontal_fov_degrees': math.degrees(2 * math.atan(aspect * effective_tangent)),
            'diagonal_scale': scale, 'm12': m12, 'shift_y': camera.data.shift_y,
            'lens_mm': camera.data.lens, 'sensor_fit': camera.data.sensor_fit}
