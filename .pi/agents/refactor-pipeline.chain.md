---
name: refactor-pipeline
description: Scout → plan → execute refactor pipeline for targeted goals or broad package reviews.
---

## refactor-code-scout

Analyze this refactor request: {task}.
- Classify as targeted refactor vs broad package review.
- This is a code refactor scout step: do NOT use `obsidian-scout`/Obsidian vault notes unless explicitly requested by the user.
- Use embedded principles in your agent prompt (do not fetch historical plan docs unless explicitly requested).
- Produce a concise evidence-backed handoff.

## refactor-planner

Using the scout output below, create a phased refactor plan with risk controls and test strategy.

Scout output:
{previous}

## unity-worker

Execute the refactor plan below with minimal, safe edits.
Run targeted validation first, then broader checks as needed.
Keep Unity test artifacts under results/unity-tests-agent.
Stop when done or blocked, and summarize outcomes/risks.

Plan:
{previous}