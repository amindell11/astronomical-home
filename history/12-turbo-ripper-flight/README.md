# Turbolaser and ripper flight evidence

The owner selected the compact ripper slug with a short tracer, then requested warm yellow color and tighter turbolaser impact arms. The implementation brief is on [the hit-effects issue](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6704847365). The prior stage records the actual magenta turbo projectile in the game.

## What the captures show

`ripper-in-game.png`, `ripper-in-game.gif` and `ripper-in-game.mp4` show the shipped Rippers weapon firing its new RipperSlug through the actual GameHost and authored camera/postprocessing. The proof counted 14 shots, 124 brightened pixels at the strongest sampled projectile and 49 warm-colored pixels. The camera was fixed to keep the shot line visible. These clips show projectile appearance and repeated firing, not a combat encounter.

The four `*-impact.png` files show actual projectile collisions with Crimson and a spawned asteroid, sampled at 50 ms. Each `*-without.png` is the identical scene with only the impact renderer hidden. Each `*-300ms.png` verifies that the impact has faded. The probe checks contact, orientation and the correct material. These stills use a controlled camera and light, not the full game's postprocessing.

## Verification

The final combined run passed all 10 cases with no skips: five production behavior tests, four temporary render cases and the full-game ripper recording. `graphics-summary.json` and `graphics-tests.xml` preserve the results. Production checks cover contact/orientation, distinct impacts, pool expiry/reuse, headless presentation suppression and the ripper child's distance fade/reset.

`ripper-capture-manifest.json` and `ripper-frames.csv` preserve capture identity and timings. The clip assembly uses 50 fps to match the locked 0.02-second simulation steps. MP4 and GIF are checked after encoding. The screenshot is frame 89, named by `ripper-flight-proof.json`.

## Capture correction

The initial run passed eight of nine checks. The temporary ripper-on-ship still probe waited an extra rendered frame before looking for an active impact. The instrumented rerun observed the actual effect at contact, then the same instance inactive 464 ms later; its pool return delay is 230 ms. The effect worked and had already returned to the pool before that lookup.

The corrected probe samples synchronously inside the Hit callback, after the existing hit-effect subscriber. This removes the asynchronous boundary before still sampling. No runtime code or effect duration changed. The original failed probe and results are in `capture-diagnosis/`. The diagnostic source and XML are preserved here for the finding; the probe is excluded from production Assets.

## Build and review

One combined quality review found no correctness or scope issues. Its only edit split a script-contract comment. The temporary player builder and launcher are included for reproducibility and do not land in production.

The Windows player build succeeded with zero errors. It was built from the tested working tree before the production commit, so its embedded identity records base `828d62d52811e70b40ac2911e818bff320a57f44` with `dirty: true`. The unchanged production payload was then committed as `36fb0b01ce8ec8498556a0246c9b674ba02ae480`. Generated build settings, bank copies and temporary helpers were excluded from that commit.

The player launched and exposed its Astronomical window. The owner pressed Escape during computer control, so UI automation stopped before inspecting the hangar. The game captures above provide the visual verification; no further automated player input was sent.

## Merge

The owner instructed merge on 2026-11-07. [The combined pass](https://github.com/amindell11/astronomical-home/pull/1387) landed as `7c1a44d2ddcc7f16986a08057e5473b3f6035bcf`. Its integrated landing tree passed 1,570 tests with zero failures and two skips, plus the final ReSharper check with zero blocking findings. The merge-suite XML, summary and final static-check receipt are retained beside this note.

Primary-tree sync refused because the owner's checkout remains on `fly/owner-flights`. No owner branch or edits were changed.
