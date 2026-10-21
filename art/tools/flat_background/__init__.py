bl_info = {
    "name": "Flat Background",
    "author": "Astronomical",
    "version": (2, 0, 0),
    "blender": (5, 1, 0),
    "location": "3D View > Sidebar > Flat Background",
    "description": "Author reproducible starless, repeating flat cloud backgrounds with Cycles",
    "category": "Render",
}

from .flat_panel import register, unregister
