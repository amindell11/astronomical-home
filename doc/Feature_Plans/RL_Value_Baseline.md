# RL executed-return value baseline

Issue: #365

This transient brief freezes the single-PR design for turning executed combat
transitions into an inspectable, reproducible combat-state value model. The PR
deletes this brief when the implementation and evidence are complete.

## Scope

- Consume `rl-transition-v1` JSONL emitted by the existing decision-boundary
  recorder.
- Validate episode structure, construct discounted task and shaping returns,
  split without seed or episode leakage, train one small feed-forward model,
  compare it with constant and linear baselines, and export a versioned ONNX
  artifact with durable audit evidence.
- Collect one bounded real dataset from the canonical non-smoke scripted-roster
  configuration: 500,000 total trainer steps, six workers, two arenas per
  worker, transition recording enabled.
- Commit the compact model contract needed by downstream issues; stage the raw
  dataset and full audit output outside the recyclable worktree.

## Non-goals

- No obstacle features in v1.
- No MPC candidate encoding, Sentis integration, shadow scoring, latency gate,
  learned-value authority, behavioral A/B, reward changes, transition-schema
  changes, or labels for hypothetical candidates.
- No fixed-policy collection lane, full 3.5M-step collection, hyperparameter
  sweep, or tuning after held-out results are visible.

## Locked decisions

### Input and target

- Input schema `rl-value-combat-v1` is the ordered 28-float `state.combat`
  vector. Actions and obstacle tokens are validated but excluded.
- The task target is the discounted return of `dense + timeCost + outcome` at
  gamma 0.99. Higher output means better expected return.
- `shapingEnvelope` and `shapingBorder` returns are computed and retained as
  separate analysis fields; neither enters the task target or model input.
- Terminal episodes bootstrap from zero. Truncated episodes are censored: they
  remain visible in audit output but produce no supervised labels.
- A label belongs only to an executed row's current state. `nextState` proves
  continuity and never receives a label by itself.

### Integrity and split

- Malformed schemas, duplicate episode identities, non-finite values,
  non-contiguous decisions, inconsistent provenance, multiple/floating end
  markers, or broken adjacent-state continuity fail at ingestion and identify
  the source file, episode, and decision.
- Split by `runSeed`, never by transition. Deterministically hash the 12 seeds
  with a versioned salt into eight training, two validation, and two held-out
  seeds; record the actual membership in the manifest.
- Refuse training unless every assigned seed contributes at least ten terminal
  episodes. Report terminal and truncated counts per seed before the gate.

### Model and baselines

- Train one deterministic CPU MLP: 28 -> 64 -> 64 -> 1 with ReLU, MSE, Adam
  at 1e-3, batch size 1024, at most 200 epochs, and patience 20 on validation
  RMSE. Save the best validation epoch. Do not sweep or retune.
- Fit input and target mean/std on the training split only. Bake input
  normalization and target denormalization into ONNX so its public interface is
  raw `[batch, 28]` `combat_state` to original-scale `[batch, 1]`
  `value_return`.
- The constant baseline predicts the training-target mean. The linear baseline
  is NumPy ordinary least squares with an intercept; report rank and condition.
- Training and primary metrics are transition-weighted because deployment
  queries decision states. Also report episode-macro and seed-macro metrics.
- Results are report-only. Export remains valid when the neural model loses to
  either baseline, and the loss must be prominent in the metrics.

### Calibration and inspectability

- Held-out calibration uses ten equal-count prediction bins plus the global
  fit `observed = intercept + slope * predicted`. Each bin records support,
  prediction range, mean prediction, mean observation, residual, MAE, and
  RMSE. Constant predictions carry an explicit undefined-calibration status.
- The result directory contains `value.onnx`, `manifest.json`, `metrics.json`,
  `baselines.json`, `training_history.jsonl`, `episode_audit.jsonl`,
  `heldout_predictions.jsonl`, and `verification.json`.
- Row-level prediction output carries provenance, task and both shaping
  returns, neural/constant/linear predictions, and residuals so aggregates can
  be reconstructed by inspection.
- The manifest records source paths and SHA-256 hashes, collection command and
  config hash, source Git commit, package versions, feature names/order,
  normalization, target definition, split membership, architecture, random
  seeds, best epoch, and hashes of every produced file.
- Preserve the raw transitions and full result directory under the primary
  tree's untracked `results/`. Commit the ONNX, manifest, metrics, and baseline
  summary beside the existing AI models for the downstream chain.

### Verification and failure policy

- Unit tests cover hand-computed terminal and truncated episodes, ingestion
  failures, split determinism/leakage, train-only normalization, metric
  weighting, calibration, baselines, and a tiny end-to-end export.
- `onnx.checker` and reference inference must match PyTorch at batch sizes one
  and 128 within maximum absolute error 1e-5; write the evidence to
  `verification.json`. Python/Sentis agreement belongs to #367.
- Data and artifact contract violations fail loudly. Inadequate real data stops
  after writing the audit; it never triggers an automatic additional run.
- The 500k collection is a consequential base-port 5006 launch and requires a
  fresh explicit approval immediately before it starts.

## Repository shape

One deep Python module owns parsing, validation, return construction, splitting,
training, evaluation, and export. A thin CLI supplies paths and invokes that
interface. Use the repository's existing NumPy, PyTorch, ONNX, and stdlib
`unittest` stack. No new glossary vocabulary or runtime Unity code is expected.
