"""One-off source edit for console.blend (issue 1476), second step. Saves in place.
The chevrons become lit legends with a thin ink rim drawn outside them, not ink drawn over them."""
import bpy

D = bpy.data.objects
scene = bpy.context.scene
settings = bpy.context.view_layer.freestyle_settings
chevrons = [D['08 | Previous inlaid chevron'], D['08 | Next inlaid chevron']]
outer = settings.linesets['Outer ink contour']
hardware = outer.collection
if any(lines.collection != hardware for lines in settings.linesets):
    raise RuntimeError('The ink line sets no longer share one collection')
rim_collection = bpy.data.collections.new('29 - Lit legend ink')
scene.collection.children.link(rim_collection)
for chevron in chevrons:
    chevron.data.bevel_depth = .016
    hardware.objects.unlink(chevron)
    rim_collection.objects.link(chevron)
rim = settings.linesets.new('Lit legend rim')
rim.select_by_visibility = True
rim.visibility = 'VISIBLE'
rim.select_by_edge_types = True
rim.select_silhouette = True
for flag in ('select_border', 'select_crease', 'select_contour', 'select_external_contour',
             'select_material_boundary', 'select_edge_mark', 'select_ridge_valley', 'select_suggestive_contour'):
    setattr(rim, flag, False)
rim.select_by_collection = True
rim.collection = rim_collection
rim.collection_negation = 'INCLUSIVE'
rim.select_by_image_border = outer.select_by_image_border
style = outer.linestyle.copy()
style.name = 'Lit legend rim'
style.thickness = outer.linestyle.thickness * .6
style.thickness_position = 'OUTSIDE'
rim.linestyle = style
print('RIM_THICKNESS=%.3f' % style.thickness)
bpy.ops.wm.save_mainfile()
print('FIX_DONE')
