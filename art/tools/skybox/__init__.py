bl_info = {
    "name": "HDR Space Skybox",
    "author": "Astronomical",
    "version": (1, 2, 0),
    "blender": (5, 1, 0),
    "location": "3D View > Sidebar > Skybox",
    "description": "Author reproducible HDR nebula skies with Cycles",
    "category": "Render",
}

from .skybox_panel import register, unregister
