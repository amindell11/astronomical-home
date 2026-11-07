# Turbo and ripper integration

Work in D:/amind/git/agent-4, lease hit-effects-turbo-ripper. User rulings and frozen brief are on issue792 comments6704746548 and latest combined implementation brief. User has accepted shape and requested warm ripper, verified turbo palette, narrower turbo fan. Continue to concrete combined flight; no extra art approval needed for these specified revisions.

Implement the recommended minimum plan from your previous reply: art/vfx/turbo-ripper native generator folder, editor TurboRipperBaker, two impact prefab variants based on LaserImpact and own ripper visual child. Reuse PaintedBurst and one-frame CrackleFlipbook; no runtime class/shader changes. Remove standard baker's explicit null turbo wiring. Keep sourcehash and standard rendering unchanged. Preserve physics, weapon stats and audio.

Actual color proof succeeded: results/capture/turbo-color-proof.json and framefolder20261107-090215-865-turbo-current-color. 18 shots,336 visible changedpixels. Turbo palette is vivid magenta/pink and pale core. Spread0.75 across travel, same length/timing340ms and opacity60→280ms. Ripper warmamber/yellow/cream compactsolid slug+shorttail and sparseimpact180ms, initial lengths0.7 and0.85 worldunits respectively.

Careful: native study simulates Unity opacity already. Do not bake that opacity AND leave same particle fade, which would double-fade. Turbo should retain exactly one copy of the accepted60→280ms fade. Ripper similarly needs matching study behavior, no compounded fade. Preserve contact-centered atlas and negative-localY effectfan; projectilehead toward positiveY. Keep scale change on visual child only, compensate existing root scale without touching collider or stats. Existing shader maincolor alpha must continue LaserVisual distancefade.

Tests: extend LaserHitVfxPlayModeTests to exercise actual turbo/ripper contacts, orientation, expiration/reuse, headlesssuppression, identity distinct fromstandard; ripper child flightdistancefade/reset assertion. Do not mirror every authoredvalue with unit tests. Meaningful gameplay cases only. Update README regeneration contract and include source/settings underart, rejectedimagegen stays evidenceonly. No unrelated cleanup, guards, new abstractions, or speculative fixes.

Verify command: ./scripts/agent_worktree_pool.sh compile-check agent-4. Root owns Unity runs, baker/import, capture and tests unless you explicitly coordinate with root before running. You may write a public batch bakeentry with args for root. Do not invoke Unity directly. No commits/pushes until root reviews. Existing capture scratch file stays outsideAssets and will be archived separately.

Report STATUS: DONE | BLOCKED, CHANGED:, VERIFIED:, NOTICED:, bakecommand and export manifest paths. Every shader property must exist in the current shader source. Read live sources, no guessed names. You can implement now; agent4 Unity is closed and released. Cache cleanup was auto-rejected, do not retry or bypass it.
