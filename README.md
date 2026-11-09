# Smaller needle flecks with variation

The owner flew the first pale-gold needle fleck on 2026-11-09 and requested a shorter, smaller tracer, followed by slight length and general variation. This revision is implemented in `682fe0532566dc2fdfbaff46a4b092c6fa50c161`.

The six authored variants span 0.60–0.75 world units, compared with the first trial's 1.1. They differ slightly in width, angle, detached fleck, and brightness. All use the selected pale-gold palette. Each shot keeps one variant throughout its flight, using the existing projectile phase selection with playback stopped. Current projectile speed remains 60 units per second.

The source verification checks six distinct 256×512 cells, transparent cell edges, and byte-identical impact textures. The imported atlas matches the source export. Five existing projectile tests passed, covering hits, headless presentation, distance fade, and pool reuse. A second reviewer found no actionable issues in the eight-file diff. No runtime C# changes were required.

The game-camera capture uses real Rippers through GameHost and ObserverCam. Frames advance simulation time by 0.02 seconds; MP4 playback is 50 fps and the GIF keeps every second frame at 25 fps. This avoids making slow PNG acquisition look like slow projectile movement.

The first trial and its proof remain at evidence commit `505fc3066d91e4a12963619d54a4ca485ac5077b`. The owner requested this revision after that trial; these captures do not replace ordinary flight feedback.
