# Testing Plan – Dogfight AIsteroids

> Revision: 1.0 – 2025-06-22
>
> This document outlines **how** we will satisfy the Proposal's testing requirements (P1–P3) for the features currently implemented in the repository.  It is intended for both developers and researchers running experiments.

---

## 1  Scope & Objectives

| Proposal Ref | Requirement | Success Criteria |
|--------------|-------------|------------------|
| **P1** | Basic feature validation of F1–F3 via Unity PlayMode tests. | All automated tests green in CI; >90 % code-coverage on key gameplay scripts. |
| **P2** | Combat balance test with human play-testers. | Mean survival-time & *fairness* scores logged; balance tweaks iterated until STDDEV ≤15 %. |
| **P3** | AI believability study comparing RL vs Dummy agents. | Statistical report (t-test, α = 0.05) shows whether RL is perceived as more intelligent/fun. |

---

## 2  Test Modalities

1. **EditMode Unit Tests** – fast, isolated verification of pure C# logic (physics helpers, planners, damage math).
2. **PlayMode Integration Tests** – headless Unity scenes exercising full stacks (weapons, damage, AI behaviour) using the Unity Test Framework (UTF).
3. **Performance Tests** – frame-time, GC allocs, and pooled-object churn via the Unity Performance Testing Extension.
4. **Manual/UX Play-Testing** – structured sessions with instrumentation & post-match surveys (P2 & P3).

---

## 3  Automated Test Matrix

### 3.1  EditMode Suites  
`Assets/Tests/EditMode/`

| Target Class | Assertions |
|--------------|-----------|
| `CollisionDamageUtility` | • `KineticEnergy` closed-form vs analytical.<br/>• Symmetry of `RelativeKineticEnergy`.
| `PathPlanner` | • Returns zero desired-velocity when already at goal.<br/>• Avoidance vector pushes away from asteroids inside safety radius.
| `VelocityPilot` | • Forward vs strafe command signs under opposing errors.<br/>• Rotation target within 0–360°.
| `MissileLauncher` | • FSM transitions: Idle→Locking→Locked and cancel paths.<br/>• Cool-down enforcement of `fireRate`.

### 3.2  PlayMode Suites  
`Assets/Tests/PlayMode/`

| Test Case | Scene Stub | Metrics |
|-----------|------------|---------|
| **Laser Damage** | Empty plane with `Ship` & `LaserGun` | Target health reduction == projectile `damage`.
| **Missile Homing** | Missile & moving `ITargetable` dummy | Distance to target strictly decreasing until hit.
| **Asteroid Fragmentation** | Spawn >10 asteroids, fire projectile | Sum(fragment mass) ≈ parent mass·`massLossFactor` ±5 %.
| **Ship Shields** | Ship with full shield, apply damage | Health untouched until shield <= 0; regen after `shieldRegenDelay`.
| **Dummy AI Aim** | Dummy vs stationary player | `AIShipInput.TryFireLaser` called when LOS within tolerance.

**Helper Utilities:**
* `TestSceneBuilder` – programmatic scene composition to keep tests deterministic.
* `TimeScaleController` – accelerates PlayMode tests (e.g., 3× speed).

### 3.3  Performance Benchmarks

* **Asteroid Field Stress** – spawn to `maxAsteroids`, verify frame-time < 16 ms on reference PC.
* **Pooling Integrity** – after 10 000 projectile cycles, `SimplePool<T>.PoolSize` > 0 & no `Instantiate` spikes.

Results will be uploaded to the Unity Performance Dashboard via the CI runner.

---

## 4  Manual Play-Testing Protocols

### 4.1  Combat Balance Test (P2)

1. **Participants**: ≥5 experienced gamers.
2. **Setup**: three balance tiers (Easy/Med/Hard) configured via ScriptableObjects.
3. **Instrumentation**: in-game analytics logger writes JSON per round: `{ survivalTime, damageTaken, damageDealt }`.
4. **Survey**: Likert (1–5) *fairness* & *clarity* questions delivered via Google Forms.
5. **Analysis**: Python notebook in `analysis/` computes mean TTK, variance, and renders rain-cloud plots.

### 4.2  AI Believability Study (P3)

1. **Blind A/B**: Each participant plays 6 matches—order randomised RL vs Dummy.
2. **Metrics Captured**: win-rate, engagement time, missile count, Qualtrics intelligence/fun scale.
3. **Stat Test**: paired t-test compares win-rates; Wilcoxon signed-rank for ordinal survey data.
4. **Reporting**: publish results in `reports/p3_believability.pdf` with graphs.

---

## 5  Continuous Integration

* **GitHub Actions** workflow `.github/workflows/unity-tests.yml` will:
  1. Cache Unity Editor license.
  2. Build *test-runner* project using `-runTests -testPlatform editmode,playmode`.
  3. Upload **XML** results + **code coverage** to Codecov.
* Failing tests block PR merges via required-status check.

---

## 6  Implementation Roadmap

| Week | Milestone |
|------|-----------|
| W1 | Scaffold test assemblies; implement EditMode suites. |
| W2 | Build PlayMode scene stubs & first 5 integration tests. |
| W3 | Add performance benchmarks; wire CI. |
| W4 | Conduct pilot combat-balance session; refine survey. |
| W5 | RL agent training finishes → start believability study. |
| W6 | Final data analysis & report submission. |

---

## 7  File Tree Additions

```
Assets/
  Tests/
    EditMode/
      CollisionDamageUtilityTests.cs
      PathPlannerTests.cs
      VelocityPilotTests.cs
      MissileLauncherTests.cs
    PlayMode/
      LaserDamagePlayMode.cs
      MissileHomingPlayMode.cs
      AsteroidFragmentationPlayMode.cs
      ShieldRegenerationPlayMode.cs
      DummyAIAimPlayMode.cs
analysis/
  combat_balance.ipynb
  believability_study.ipynb
reports/
  p2_balance.pdf
  p3_believability.pdf
```

---

## 8  Risk Mitigation

* **Flaky Tests**: use deterministic random seeds and stub physics where feasible.
* **Editor-Version Drift**: lock CI to Unity *2022.3 LTS*; document upgrade procedure.
* **Participant Recruitment**: schedule during lab hours; provide fallback online survey.

---

## 9  Approval & Ownership

* **Testing Lead**: _A. Mindell_
* **CI Maintainer**: _Tech TA_
* Changes to this plan require a PR reviewed by both roles. 