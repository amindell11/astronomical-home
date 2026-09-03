# Test artifacts & conventions

> STATUS: living — branch-triggered reference for running Unity tests; pointed at from `AGENTS.md`. Suite guide: `TESTING.md`.

- Standardize Unity test artifacts to `results/unity-tests-agent` (pass an
  explicit `outDir`/`-OutDir`).
- Unity access is coordinated per project through `scripts/unity_access.ps1`:
  runs on different worktree projects overlap, and only Unity startup
  serializes through a machine-wide boot lane. Prefer batch tests; they drive
  the whole protocol automatically. Use a tracked interactive editor only when
  batch mode cannot verify the behavior, and close/release it immediately
  afterward. An untracked main-worktree editor is user-owned: ask the user to
  close it, never terminate it. Inspect owners, the boot lane, and the FIFO
  queue with `./scripts/unity_access.ps1 -Action Status`.
- For PlayMode tests, prefer inheriting from
  `Tests.PlayMode.Common.PlayModeWorldFixture` when it makes sense (ensures
  GamePlane/test-arena setup and cleanup).
- Dev-loop iteration can route a scoped run into a resident editor your work
  stream already holds: add `-Routed` to `unity_test_agent.ps1` (attach-only —
  it never boots; start the editor via unity-access rung 4 first). Same
  artifact contract, marked `transport: "routed"`; the merge gate refuses
  routed summaries as proof, so gate runs stay cold. It refuses any selection
  the pipeline's include-only filter can't reproduce exactly (notably
  unfiltered PlayMode under `-ExcludeCategory RequiresGraphics`) — run those
  cold. Details: `TESTING.md` § Routed runs.
- See `TESTING.md` for the test suite guide. Every fixture is tagged with one
  **domain** category (`Sectors`, `Weapons`, `MPC`, …) plus optional
  `Smoke`/`Slow`; run a feature slice with `-TestCategory <Domain>` instead of
  the whole suite. Give new fixtures exactly one domain tag.
- **Performance/benchmark sweeps run SOLO** — a parallel Unity on the machine
  contaminates timing metrics (solve-ms in particular).
- **Never leave eval or benchmark artifacts only in a pool slot** — slot
  recycling destroys them; copy anything you will cite out of the worktree.
- **A test whose subject is an artifact asserts CONTENT, not existence.** "The
  file appeared" is nearly uncorrelated with the producer working — #374's
  capture lane passed while writing 1,195 uniformly blank PNGs. Assert something
  only a working producer could satisfy (non-uniform pixels, a row count, a
  parsed field), and prefer a discriminating on-vs-off pair. Same bar for any
  "proven" claim: name the assertion that would fail if it were false.
