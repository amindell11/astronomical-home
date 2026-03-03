---
name: build-feature
description: Scout feature requirements in Obsidian, plan implementation, confirm approval, and then run the agent-worktree-pr-loop skill for implementation.
---

## obsidian-scout

Use the obsidian-scout agent to research the feature request described by {task}. Follow the guidance in `.pi/prompts/design-docs.md` (which points to `D:\amind\Documents\Obsidian Vault\Astronomical\OVERVIEW.md`) as your primary context, and flag any conflicts if you encounter them. Collect: the current needs/constraints for the feature, relevant vault context, existing behavior to extend, and any unresolved dependencies. End with an explicit “Clarifying questions / questionnaire” section that lists the specific answers you still need before planning. Ask for missing data in the form of direct questions so we can identify what must be confirmed in follow-up.

## feature-planner

Using the scout summary below, craft a phased implementation plan that follows the feature-planner safety checklist (intent summary; goals; dependencies; phased work plan; risk + rollback guidance; validation strategy with targeted and broader tests that reference `results/unity-tests-agent`; and open questions/unknowns). Keep the plan aligned with existing behavior unless the user asked otherwise, note assumptions, and keep the planes small, reversible, and testable. After delivering the plan, explicitly present it to the user and ask them to review it for accuracy. End with a request such as “Please confirm when you approve this plan so I can move on to implementation.”

Scout output:
{previous}

## agent-worktree-pr-loop

Implementation step: the user-approved plan is below. Only run this step after the user explicitly confirms the plan (e.g., by replying “Plan approved; proceed with implementation”). If approval has not been received yet, ask the user to confirm and pause. Once approved, use the `agent-worktree-pr-loop` skill (`.pi/skills/agent-worktree-pr-loop/SKILL.md`) to acquire a slot, implement the plan, run the standard `./scripts/agent_worktree_pool.sh finalize agent-<n> origin/main -- -Mode Both -ScopeType Workspace` flow, and keep Unity test artifacts inside `results/unity-tests-agent`. Report the required info from the skill (slot, flow, PR status, files changed, tests, unknowns/risks) along with a brief summary of what was implemented.

Plan:
{previous}