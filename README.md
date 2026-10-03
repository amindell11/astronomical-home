# research

An orphan branch for research reports and their working notes: one folder per topic. It shares no history with `main` and never merges into it. Issues link here by commit URL, so a link keeps pointing at the text the issue was ruled on.

Each file is a byte-exact copy of what sat untracked in the primary tree on 2026-10-03, recovered after that tree was wiped. Paths inside a file (`reports/…`, `research_notes/…`) refer to the old untracked layout. The tables below map them to their folders here.

## dogfight-ai/

Research for the committed-maneuver AI pilot arc, [#871](https://github.com/amindell11/astronomical-home/issues/871).

| File | What it is |
| --- | --- |
| [Satisfying dogfight AI approaches.md](dogfight-ai/Satisfying%20dogfight%20AI%20approaches.md) | The report (was `reports/`) |
| [notes/architecture_paradigms_viability.md](dogfight-ai/notes/architecture_paradigms_viability.md) | Decision-layer architectures over an MPC; viability of named committed maneuvers |
| [notes/decoupled_flight_tactics.md](dogfight-ai/notes/decoupled_flight_tactics.md) | Decoupled-facing flight tactics and counters to circle-strafing |
| [notes/perceived_intelligence_and_readability.md](dogfight-ai/notes/perceived_intelligence_and_readability.md) | Perceived intelligence, readability and fairness in combat AI |
| [notes/shipped_space_combat_ai.md](dogfight-ai/notes/shipped_space_combat_ai.md) | Pilot AI in shipped space- and flight-combat games |

`notes/` was `research_notes/Satisfying dogfight AI approaches/`.

## art-pipeline/

Research and reviews behind the asset architecture arc, [#868](https://github.com/amindell11/astronomical-home/issues/868).

| File | What it is |
| --- | --- |
| [AI game art pipeline.md](art-pipeline/AI%20game%20art%20pipeline.md) | The report, 2026-09-22 (was `reports/`) |
| [notes/concept_to_3d_blender.md](art-pipeline/notes/concept_to_3d_blender.md) | 2D concept to game-ready 3D asset (Blender to Unity) |
| [notes/director_workflows_case_studies.md](art-pipeline/notes/director_workflows_case_studies.md) | AI-assisted art direction: process, roles, case studies |
| [notes/image_generation_models.md](art-pipeline/notes/image_generation_models.md) | Image generation and editing models for concept art |
| [notes/ui_hud_workflows.md](art-pipeline/notes/ui_hud_workflows.md) | AI-assisted UI and HUD workflows in Unity |
| [notes/vfx_textures_environments.md](art-pipeline/notes/vfx_textures_environments.md) | VFX, shaders, materials and space environments |
| [codex-visual-work-audit-2026-10-02.md](art-pipeline/codex-visual-work-audit-2026-10-02.md) | Organization and hygiene audit of the illustrated-ship work |
| [art-pipeline-retrospective-2026-10-03.md](art-pipeline/art-pipeline-retrospective-2026-10-03.md) | Stage-by-stage retrospective of how Vanguard, Crimson and Valis were made |

`notes/` was `research_notes/AI game art pipeline/`.

## test-infra/

A static review of the test infrastructure, 2026-09-29/30: two independent graders against one rubric. Nothing was run. Its findings fed [#824](https://github.com/amindell11/astronomical-home/issues/824), [#825](https://github.com/amindell11/astronomical-home/issues/825), [#826](https://github.com/amindell11/astronomical-home/issues/826) and the deferred strictness work in [#827](https://github.com/amindell11/astronomical-home/issues/827).

| File | What it is |
| --- | --- |
| [test-infra-rubric.md](test-infra/test-infra-rubric.md) | The grading rubric |
| [test-infra-grade-opus.md](test-infra/test-infra-grade-opus.md) | Grade, cut list and add list from the Opus reviewer |
| [test-infra-grade-gpt.md](test-infra/test-infra-grade-gpt.md) | Grade, cut list and add list from the GPT reviewer |
| [review/packet.md](test-infra/review/packet.md) | The packet both reviewers were given |
| [review/issue-index.txt](test-infra/review/issue-index.txt) | The issue index handed to the reviewers |

All of it was `reports/`; `review/` was `reports/test-infra-review/`.
