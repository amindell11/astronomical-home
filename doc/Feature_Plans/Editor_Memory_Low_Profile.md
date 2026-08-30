# Editor-memory low-profile launch brief

## Scope

Add verified, launch-scoped editor-profile selection to the coordinator's direct
editor launch. The default is `LowMemory`; `HighFidelity` is an explicit opt-in.

## Non-goals

- Change tracked Quality Settings or any build profile.
- Change shipped High Fidelity visuals.
- Extend generic `RunBatch` child contracts.
- Alter Unity CLI or test-transport behavior.
- Tune the measured contents of the low-memory profile.

## Decision brief

- `EditorProfile` is a semantic coordinator input: `LowMemory` maps to the
  existing `Performant` quality tier and `HighFidelity` maps to `High Fidelity`.
  Existing tiers stay the source of their configuration.
- `StartEditor` defaults to `LowMemory` and accepts an explicit profile. Its
  Python client keeps that default; no launch adapter owns the mapping.
- The coordinator passes the requested profile and a unique receipt location in
  child-process environment variables. An editor-side `InitializeOnLoad` module
  applies the selected tier and atomically writes the observed profile receipt.
  Environment inheritance keeps the seam independent of Unity CLI transport.
- The coordinator attaches only after the receipt reports the requested and
  observed profile. A missing, malformed, or mismatched receipt is an earliest
  deterministic launch failure: terminate the spawned editor and release its
  ownership and boot leases.
- Prove the mapping/receipt contract with EditMode tests and coordinator launch
  behavior with PowerShell tests. The slice is headless; no graphics-required
  test is introduced.

## Vocabulary

- **editor profile** — a coordinator-selected, launch-scoped choice of an
  existing Unity quality tier.
- **profile receipt** — the bootstrap's atomic record of requested and observed
  profile values, consumed by the coordinator before editor handoff.
