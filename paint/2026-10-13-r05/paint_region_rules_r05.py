def region_for(name, normal, center):
    if name in ('06 Cockpit dome', '07 Side insets'):
        return 3
    if name in ('02 Upper hull', '03 Lower hull', '04 Tail booms', '05 Tail tips', '05 Tail tips.003',
                '06 Cockpit surround', '08 Nested engines') or name.startswith(('11 ', '12 ', '13 ', '14 ')):
        return 2
    if name == '01 Ram nose':
        return 2 if normal[2] < -.4 else 1
    if name.startswith('10 Tail fins'):
        return 2 if normal[2] < -.2 else 4
    if name == '02 Upper hull.001':
        return 2 if normal[2] < .32 else 1
    if name in ('05 Tail tips.002', '09 Side armor.001'):
        return 2 if normal[2] < .32 else 1
    if name == '09 Side armor':
        return 2 if normal[2] < -.25 else 0
    return 0
