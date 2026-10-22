# Growing spike and oblique mine blast

Implementation commit `7116b1a69f61c45035a3a61aee341b53e6f8e96d` grows the upright spike by extending both
complete outer sections while the central burst keeps its shape. It reaches full
length at 180 ms and fades with the existing 400 ms core. The disc keeps its
800 ms sweep. The missile's approved animation stays unchanged.

The perspective comparison shows the circular version, a compressed oblique disc,
and the oblique disc with a faint circular range ripple. The user selected the
range ripple. The bright disc is 28% as tall as wide; the circular ripple has
28% opacity. The camera remains overhead and the mine's hit area remains circular.

The Unity capture contains 179 frames at 50 fps, lasting 3.58 seconds. Both effects
replay twice through the game camera. The 20 selected graphics-enabled PlayMode
tests passed with no failures or skips. The recording verifies appearance and
pooled replay; it does not exercise missile flight or audio.

Drawing playback uses the shared texture renderer and shows large and game-scale
views. Extraction and browser checks passed. Earlier stages remain for comparison.
