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
- See `TESTING.md` for the test suite guide. Every fixture is tagged with one
  **domain** category (`Sectors`, `Weapons`, `MPC`, …) plus optional
  `Smoke`/`Slow`; run a feature slice with `-TestCategory <Domain>` instead of
  the whole suite. Give new fixtures exactly one domain tag.
