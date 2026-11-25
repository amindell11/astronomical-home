# Design vault

The game's design lives here as an Obsidian vault (open this folder in
Obsidian). Notes use Obsidian syntax — `[[wikilinks]]`, `[[Note|alias]]`,
`![[image]]` embeds — and agents read them as raw files; nothing here needs
GitHub to render it. Rules for what belongs here versus on an issue or PR:
`doc/agents/design-docs.md` → Where design lives.

Read before proposing or implementing anything design-facing:

- [OVERVIEW](OVERVIEW.md) — vision, core pillars, and the entry point into
  everything below.
- Design/Gameplay — `Combat`, `Combat Depth`, `Flight`, `Weapons`, `Ships`:
  the skill-ceiling pillar. `Loot and Rewards` (+ subfolder), `Rogue-like`,
  `Gameplay loop`, `Keystones`: the run structure and reward pillar.
  `LLM driven AI dialogue`: the third pillar.
- Design/World — `World`, `Sectors`, `Points of Interest`, `Encounters`,
  `Home Base`: where a run happens.
- Design/Story — `Story`, `Themes and Motivation`.
- Art/Style — the visual target.
- Inspiration/ — reference games and talks; context, never spec.

Images sit beside the notes (root and `assets/`) and are tracked in LFS.
