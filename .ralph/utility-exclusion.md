## Task: Add state exclusion to utility system

### Change 1: Zero-weight exclusion
- In `Sampler.Evaluate()`, skip states whose resolved weight (base × instance) is 0
- Extract weight resolution so it can be checked before full utility computation
- Commit separately

### Change 2: IsAvailable gate
- Add `virtual bool IsAvailable(Info ctx)` to `State` base class (default: true)
- Override in `Attack` and `Evade` with `ctx.Combat.HasEnemy`
- Check in `Sampler.Evaluate()` alongside weight filter
- Remove redundant `if (!ctx.Combat.HasEnemy) return 0f` from ComputeUtility methods
- Commit separately

### Checklist
- [ ] Extract weight resolution helper in Sampler
- [ ] Skip zero-weight states in Evaluate()
- [ ] Commit 1: zero-weight exclusion
- [ ] Add IsAvailable to State base class
- [ ] Override IsAvailable in Attack and Evade
- [ ] Update Sampler.Evaluate() to check IsAvailable
- [ ] Remove redundant early-return guards from ComputeUtility
- [ ] Commit 2: IsAvailable gate
- [ ] Run EditMode tests