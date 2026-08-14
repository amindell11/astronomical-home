# MPC terminal learned cost

> STATUS: shelved 2026-08-13 — preserved on `archive/rl-value`; not queued to land on `main`

This experimental branch evaluates an executed-return model as an MPC terminal
cost. The branch remains a resume point, not production code.

## Preserved work

- Decision-boundary transition capture: #364, PR #373.
- Terminal-candidate value-state and inference seam: #366, PR #354.
- Executed-return value baseline and artifact: #365, PR #383.

## Unbuilt work

- Synchronous shadow scoring: #367.
- Candidate-sensitivity and authority-readiness evaluation: #368.
- Bounded learned-value behavioral A/B: #369.

The six issues are closed and absent from the project board while this arc is
shelved. Resuming the arc means restoring `archive/rl-value` as a working
branch, reopening the needed issues, and adding only the active frontier back
to the project.
