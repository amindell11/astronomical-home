"""One-off source edit for console.blend (issue 1476), third step. Saves in place.
The selector pointer takes the legends' outside rim, so ink no longer covers its ivory."""
import bpy

settings = bpy.context.view_layer.freestyle_settings
pointer = bpy.data.objects['08 | Selector pointer']
hardware = settings.linesets['Outer ink contour'].collection
rim = settings.linesets['Lit legend rim'].collection
hardware.objects.unlink(pointer)
rim.objects.link(pointer)
bpy.ops.wm.save_mainfile()
print('FIX_DONE')
