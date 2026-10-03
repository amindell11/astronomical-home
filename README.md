<h1 align="center">Astronomical</h1>

<p align="center">
  <em>Bold, hand-drawn spacecraft against luminous, layered space.</em><br>
  A 2.5D top-down roguelike space shooter, built in Unity 6.
</p>

<p align="center">
  <img src="doc/design/Pasted%20image%2020260923020110.png" alt="Combat art-direction mockup: two hand-drawn fighters trade fire beside an asteroid over a blue-violet nebula, with a minimal HUD and radar" width="100%">
</p>

<p align="center"><sub>Art-direction mockup (combat): the visual target, not an in-game capture.</sub></p>

---

You are a clone who wakes in a home base with no idea why you were made. Your
creator is gone; your AI companion's memory was wiped too. All you have is one
clue. Each run you assemble a ship, fly out across the galaxy's sectors chasing
leads and artifacts, and try to find your creator and ask them one question:
*why?* When you die, you respawn as a new clone, with your memories restored
from the last backup.

## Pillars

| Pillar | What it means |
|---|---|
| **High skill-ceiling combat** | Newtonian flight, few but dangerous enemies, and fights where fleeing is often the right call. Enemies fly the same ships and controls you do. |
| **Loot and rewards** | Power grows within a run (parts, weapons, upgrades) and across runs (permanent unlocks), in the spirit of Hades and FTL. |
| **LLM-driven dialogue** | Characters talk through an LLM with configurable traits and tools it can trigger (attack, flee, give items), so a conversation can change the game state. |

## A run

1. **Home base.** Respawn, talk to your companion and crew, craft from salvaged parts, and pick a contract for the run.
2. **Hangar.** Build a ship from parts found on earlier runs; the combination sets its stats and abilities.
3. **Sectors.** Jump between 3–5 star systems on a non-linear map, in any order. Difficulty is suggested, not enforced.
4. **Encounters.** Each sector is an open, explorable space with 6–8 objective-driven encounters: skirmishes, elites, escorts, hacks, hazard runs, salvage, and narrative choices.
5. **Key and extraction.** A sector is won by acquiring its key (the intel or artifact the story needs) and then *extracting* through a jump gate, often under pursuit. Getting out matters more than killing everything.

Target run length is about 30–45 minutes.

## Ships

<p align="center">
  <img src="doc/design/Pasted%20image%2020260923023842.png" alt="Hangar mockup: a white-and-orange hand-drawn ship on a landing pad, with a minimal loadout panel for engine, shield, railgun and missiles" width="49%">
  <img src="doc/design/Pasted%20image%2020260923023903.png" alt="Hangar mockup: a white-and-violet fighter posed against a swirling green planet, with the same loadout panel" width="49%">
</p>

<p align="center"><sub>Art-direction mockups: the hangar, and ship inspection between sectors.</sub></p>

A ship is a **chassis with typed slots filled by modules**. The chassis (the
ship prefab) owns hull, mass, and slot layout; modules own everything else.
Upgrading means swapping a whole module, not stacking bonuses.

| Slot | Fills with | Status |
|---|---|---|
| Engine | thrust, speed, handling, boost | built |
| Shield | capacity, regen delay and rate | built |
| Weapon ×2 | laser, missiles, … | built; two fixed mounts for now |
| Utility | consumables, mines, countermeasures | designed |
| Keystone | a signature ability on cooldown (blink, overdrive, gravity well, …) | designed |
| Passive | a standing perk | designed |

Enemies are built from the same chassis and modules, so every new part also
widens the enemy roster.

## Flight and combat

- **WASD** thrusts, **right mouse** aims the nose, strafing rolls the ship, and a
  forward **boost** runs on a cooldown. Weapons fire along your facing, so
  aiming means managing inertia.
- **Shield and hull:** the shield regenerates after a break in damage; the hull never does.
- **Lasers** build heat and lock out when overheated. **Missiles** need line of
  sight and a held angle to lock on. Asteroids block shots, eat missiles, and
  hurt on impact (glancing blows less than head-on ones).
- The design goal is **no dominant strategy.** Brawling costs you shield regen
  and heat, kiting costs scarce missiles, and fleeing costs damage. Each style
  has a counter.

## Art direction

Hand-drawn lineart with simplified, dimensional shading, influenced by Hades'
sculptural shadows and vivid light. Localized bloom on impacts, engines, and
shields. A flat, minimal HUD with thin off-white strokes over dark translucent
navy that lets space show through. It all has to hold up in 2.5D: ships roll
and asteroids spin without losing the drawn look.

## Under the hood

- **Engine:** Unity 6, a single `GameCore` assembly.
- **Enemy AI:** a reinforcement-learning policy picks velocity, fire, and
  boost commands. A model-predictive-control (MPC) layer turns those commands
  into flyable thrust and handles intercept aim and obstacle avoidance.
  Tactics like attacking, fleeing, orbiting, and kiting come from training,
  not authored rules.
- **Training:** Unity ML-Agents with PPO self-play, driven from `training/rl/`.

## Repository layout

- `src/Asteroids3D/`: the Unity project.
- `training/rl/`: Python ML-Agents training harness (configs, runners, Unity-access coordination). See [`training/rl/README.md`](training/rl/README.md).
- `doc/design/`: the game design vault (an Obsidian vault). Start at [`OVERVIEW.md`](doc/design/OVERVIEW.md).
- `doc/`: also holds agent law (`doc/agents/`), the [glossary](doc/Glossary.md), and diagnosis recipes. Design briefs and records live on GitHub Issues (`design-record` label); code is the source of truth.
- `art/`: WIP Blender sources, asset archives, and art-pipeline tools, kept outside `Assets/` so Unity never imports them.
- `scripts/`: agent tooling (worktree pool, Unity test runner, Unity access coordinator).

Design images and art sources are stored in Git LFS. Run `git lfs pull` after
cloning to fetch them.

## Key docs

- [`doc/design/`](doc/design/README.md): design vault index covering gameplay, world, story, and art.
- [`TESTING.md`](TESTING.md): test suite guide and runner usage.
- [`AGENTS.md`](AGENTS.md): agent workflow rules (fix ladder, worktree/PR loop, tracker conventions). `CLAUDE.md` imports it.
