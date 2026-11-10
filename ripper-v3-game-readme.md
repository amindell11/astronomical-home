# Ripper v3 in-game capture

Recorded from commit cb20e0cf55261f3c6414478289014496dda25c3f through Unity's native Game View. The scene uses the real GameHost, ObserverCam, player ship, Rippers weapon, and RipperSlug projectile with the IllustratedBlue background. Firing is scripted in an empty sector.

The latest atlas makes the trail much dimmer and the compact head slightly brighter. Gameplay speed remains 60 units per second. The capture contains 14 firing events and 101 frames at 1280 by 720. Simulation advances 0.02 seconds per frame. The MP4 plays at 50 FPS; the GIF retains every second frame at 25 FPS, preserving normal game speed. Capture wall-clock timestamps are slower because PNG acquisition pauses rendering.

The producer completed all frames. Its post-capture warm-color assertion failed with 4 qualifying pixels against a threshold greater than 5. This run is not a passing test. The assertion chooses the frame with the most brightened pixels, then checks the warm pixels in that frame. The footage was visually inspected; no threshold or game implementation was changed to obtain it. Earlier proof JSON files were not copied because the failed assertion prevented this run from replacing them.

The owner's primary Unity Editor remained open, PID 30920. The separate coordinated capture process exited and released its lease. This evidence does not claim the full merge test suite passed.
