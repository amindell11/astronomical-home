# Ram-friction benchmark (parked)

Not wired into anything. `RamFrictionBenchmarkPlayModeTests.cs` sits outside `Assets/`, so it does
not compile — restoring it means copying it back under `Assets/Scripts/Editor/Tests/PlayMode/`.

Parked here rather than deleted because the eval/capture redesign absorbs it: policy-vs-policy
evaluation needs the second-checkpoint import slot this bench was hand-rolling. See
`doc/Feature_Plans/RL_Infra_Paydown_Pass.md` §PR-3. The JSON files are the runs it produced
2026-07-24/25, kept as the baseline those results were read against.
