# Test infrastructure rubric

Grades this repo's test infrastructure and proposes improvements. The goal is
**maximum proven behavior per line of test code**: green must mean green, and
every test must earn its lines.

## Scope — two layers

- **Unity suite** — EditMode/PlayMode under `src/Asteroids3D/Assets/Tests` and
  `Assets/Scripts/Editor/Tests` (~28.5k lines C#); domain categories; shared
  fixtures (`PlayModeWorldFixture`, `AsyncAssert`, `ShipTestFactory`,
  `Tests.Common`).
- **Harness** — `scripts/unity_test_agent.ps1`, `scripts/unity_access*.ps1`,
  `scripts/unity_test_scope_lib.ps1` + `unity_test_scopes.json`, the script
  suite `scripts/tests/` (~4.7k lines), the merge gate in
  `scripts/agent_worktree_pool.sh`, `.github/workflows/headless-suite.yml`.

Score each criterion per layer where it applies. Ground truth for conventions:
`TESTING.md`, `doc/agents/testing.md`, `AGENTS.md` (fix ladder, dependency
rule #6).

## Criteria (score 0–4, weight in brackets)

### 1. Verdict integrity [×3]
Can any path report success without having proven anything?
- 0 — a known path maps "ran nothing" / "unknown exit" / "crashed" to pass.
- 2 — the main runner distinguishes them; side paths (routed runs, script
  suite, CI workflow, scope resolution) do not.
- 4 — every entry point distinguishes pass / fail / infra_error / ran-zero,
  each distinction covered by a test.
- Probe hands-on in a pooled slot: empty filter, misspelled category, missing
  tests dir, Unity crash before XML, null/unknown exit code, a test that ends
  early (the #752 shape).
- History: #612, #635, #752, #374 (blank PNGs passing), #582.

### 2. Assertion strength [×3]
Would each test fail if the thing it covers broke?
- 0 — "no exception", existence-only, or tautological (`solveMs > 0`).
- 4 — observable effects, content over existence, on/off pairs where it fits.
- Method: sample ~10 tests across domains and argue a mutation for each —
  which production line could break while this stays green?

### 3. Determinism & load tolerance [×2]
- 0 — raw wall-clock bounds, bare `WaitForSeconds`, `Time.time` in tested logic.
- 4 — injected time (`Tick(dt)`), conditional waits, wall-clock budgets split
  from work budgets.
- Inventory every timing assertion; judge whether it survives three concurrent
  Unity boots. History: #542, #651, #762.

### 4. Risk coverage [×2]
Coverage by risk, not lines.
- Does each domain category test its highest-blast-radius behavior?
- Do the last ~20 `fix(...)` commits each carry a regression test?
- Which production seams have zero coverage?
Score by gaps weighted by blast radius.

### 5. Economy — minimal code for maximal coverage [×2]
Every test must name the distinct failure mode it catches; a test that cannot
is a cut candidate. Hunt for:
- **Self-satisfying tests** — assert what the test itself arranged (stub
  returns X, assert X); test NUnit/Unity/C# rather than our code; restate a
  constant or serialized value; re-implement the production algorithm to
  compute the expected value.
- **Pinned transient behavior (change detectors)** — exact tuning numbers,
  log/message strings, internal call counts or ordering, private structure,
  magic intermediate values; anything that breaks on a behavior-neutral
  refactor. Empirical signal: `git log` co-change — test files edited in the
  same commit as `refactor`/non-behavior production changes.
- **Redundancy** — several tests exercising one path with no distinct failure
  mode; the same assertion at EditMode and PlayMode; near-duplicates that
  collapse into one `[TestCase]`-parameterized test.
- **Dead weight** — tests of retired features; `[Ignore]` without a ticket or
  with a closed ticket; production seams used only by tests that no longer
  need them; helpers/fixtures used once or larger than the tests they serve;
  harness workarounds whose trigger is gone (the #656 shape).
- **Leverage gaps** — places where one test at a better seam would replace N
  shallow ones.
- 0 — widespread change detectors / self-satisfying tests; helpers dwarf tests.
- 4 — every sampled test names a distinct failure mode; no dead weight found.
Tension with #2/#4 is expected: a cut must name the failure mode it loses
("none" = safe cut); an addition must name the risk it buys. Cuts follow
`TESTING.md` → verify-before-delete.

### 6. Feedback-loop cost [×2]
- Edit → verdict time for a typical scoped change; boot vs exec share.
- Merge-gate phase budgets vs observed (#611, #639).
- Does scope/category selection pick the right slice? Does script-suite
  selection cover the scripts a diff touched?

### 7. Isolation [×2]
- State leakage between tests (statics, surviving scene objects, singletons).
- Cross-worktree / concurrent-editor interference.
- Everything that touches Unity goes through the access coordinator
  (`AGENTS.md` dependency rule #6) — flag any bypass.

### 8. Diagnosability [×1]
Does the first artifact of a failure say why? Messages naming the violated
invariant; JSON summary distinguishing verdict classes; logs not stranded in a
pool slot.

### 9. Test-code health [×1]
Reflection where a seam belongs; duplicated stubs (third copy = finding);
exactly one domain tag per fixture; naming per `TESTING.md`.

### 10. Doc–reality drift [×1]
Run 3–4 documented commands from `TESTING.md` / `doc/agents/testing.md`
verbatim; record every divergence.

## Evidence rules

- Every score cites evidence: `file:line`, or a command plus its output.
- Every finding names a **concrete failing scenario** (inputs → wrong verdict,
  or edit → spurious red). Without one it is an observation and does not move
  the score.
- Every suggestion names the root cause and its fix-ladder rung; present narrow
  and structural together when they diverge.
- Before suggesting anything, check closed issues
  (`gh issue list --state closed --search <term>`, or the `design-lookup`
  agent); don't re-propose what was tried or rejected — cite it instead.

## Run constraints

- **Report only — no code changes, no issues filed, no PRs.**
- Allowed to run: the script suite (`scripts/tests/`), and 2–3 scoped cold
  Unity runs via `unity_test_agent.ps1` under the unity-access protocol.
  Nothing perf-sensitive (perf sweeps run solo).
- Hands-on verdict-integrity probes (criterion 1) run in a pooled worktree
  slot, never the primary tree. Release the slot afterward.
- Copy any artifact you cite out of the slot before releasing it.

## Output

Write `reports/test-infra-grade.md` with:
1. **Scorecard** — table: criterion × layer, score, weight, one-line evidence.
   Weighted total.
2. **Findings** — ranked by severity, each with scenario + evidence.
3. **Cut list** — test/helper/harness code to delete or collapse: location,
   lines saved (est.), failure mode lost (should be "none" or named), confidence.
4. **Add list** — missing coverage: risk bought, rough size, suggested seam.
5. **Net ledger** — estimated lines removed vs added.
6. **Issue-ready suggestions** — each with title, rung, why, and prior-art
   issue links; the user picks which to file.
