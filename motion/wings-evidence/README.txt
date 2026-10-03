Valis wing motion evidence for issue #850.
Unity Game View capture: idle, forward thrust, idle, reverse thrust, idle; 0.5-second transitions.
Controlled visual demonstration: the ship is held at the origin while the production movement command drives the visual rig and thrusters. This does not establish gameplay or handling changes.
The separate Blender GIF is the approved pre-implementation reference.
Independent verification: all 14880 baked surface and contour vertices match the user forward transforms, maximum error 6.343066E-07 Unity units.
Quality review: positions, normals, tangents, UVs, index buffer, submeshes, and eight material references preserved byte-for-byte. Collider, mounts, gameplay component fields unchanged.
Scoped routed PlayMode checks: 12/12 passed; corrected capture 1/1 passed. These are not full-suite merge proof.
