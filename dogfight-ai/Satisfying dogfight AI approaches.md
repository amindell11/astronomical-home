# Committed maneuvers earn a cheap trial first

The evidence favours moving from continuous intent blending to a small vocabulary of committed, named maneuvers. Here a "beat" means one unit of behaviour with an entry condition, a flight goal handed to the model-predictive controller (MPC), a fire plan, an exit, a visible tell and a player counter. But the evidence backs that choice on precedent and mechanism, not on any measurement of fun. Nearly every documented space-combat AI (FreeSpace 2, Naev, Star Control II, Starsector, Star Citizen) ships a handful of named, time-committed patterns chosen by range-and-aspect rules and run by a steering-and-aim layer underneath. NASA's AML/Paladin line flew the same shape against human pilots for decades: pick a situation mode, forward-simulate a few trial maneuvers, commit until the next decision. Three things the evidence does not show: that an authored sequencer beats rollout scoring, utility or a learned selector choosing among the same beats; that every beat needs its own tell and counter (no vehicle-combat precedent exists, and designers scale tells with lethality); and that any of these produces a fun opponent. No study measures fun against skilled one-to-three-enemy action AI. Circling most likely beats the current pilots for a cheaper reason. With identical ships the orbit gives no turn-rate advantage. But a pilot that aims at the target's current bearing, without feeding forward how fast that bearing turns, sits outside its ±2.5° cone for the whole orbit. The pilots also lack the reversals, matched orbits, range breaks and anti-stall rules that shipped AIs use against circling. On learning, the benched PPO result plus a goal that can't be written as a reward leave only narrow slots: a selector over authored beats, beat-level imitation from labelled demonstrations, damped player-pattern prediction, or one well-specified control sub-skill. Three tests that take days would settle most of what is open: an aim-parity test against a scripted orbiter, a log of the controller's rollout costs on twitch frames, and three hand-triggered beats fought by a playtester.

*Evidence tags used below: **[measured]** = a study or playtest that reports numbers; **[demonstrated]** = it exists in shipped or open-source code, or was flown or run against people (it works as engineering, which says nothing about fun); **[designer report]** = a developer describing what they built and saw, with data unpublished; **[community]** = player perception or player-written analysis; **[inference]** = reasoning by the researchers or by this report that no test has checked. Most of this field is designer report.*

## Players reward legible, beatable pilots more than optimal ones

The measured base is small, and none of it comes from dogfights. The strongest study is Yannakakis and Hallam's predator-prey work. **30 subjects** made pairwise comparisons of five Pac-Man ghost controllers. An interest metric built on three criteria correlated with player preference at **r = 0.44 (p ≈ 10⁻⁸)**: opponents that kill the player sometimes but not always, varied behaviour between games, and spatial diversity. Both easy and near-optimal opponents scored low, and with challenge set right, enjoyment stayed high even when variety was low *[measured]* ([Yannakakis & Hallam](https://www.um.edu.mt/library/oar/bitstream/123456789/22896/1/Capturing_Player_Enjoyment_in_Computer_Games.pdf)). The same paper notes that the field's assumption, that smarter opponents satisfy more, had no evidence behind it.

Humanness is a separate axis from skill. In the 2012 BotPrize, two bots scored **52% humanness** while real human players averaged about **40–41%** *[measured]* ([Wikipedia](https://en.wikipedia.org/wiki/Computer_game_bot_Turing_test); [UT Austin](https://news.utexas.edu/2012/09/26/artificially-intelligent-game-bots-pass-the-turing-test-on-turings-centenary/)). Humanlike noise does not by itself read as competence.

Two newer experiments point toward legibility. In a 2×2 study on a top-down stealth prototype, contextual dialogue let simple guard behaviour match complex behaviour in enjoyment *[measured; sample size not visible]* ([Al Enezi & Verbrugge 2023](https://ojs.aaai.org/index.php/AIIDE/article/view/27512)). In a military shooter, coherent and consistent NPC design shifted ratings of perceived intelligence and believability *[measured; participant count not visible]* ([Poivet et al. 2025](https://arxiv.org/abs/2512.07388v1)).

The most-quoted industry number is the weakest. Bungie's Halo playtests moved "very intelligent" ratings from **8% to 43%** when enemies got tougher. But the slides give no sample size or method, and the tough condition was also rated about right on difficulty **92% vs 52%** of the time *[measured; method undocumented]* ([Halo GDC 2002](https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf)). What survives the confound is narrower than "tougher reads as smarter". An enemy must live long enough to show several decisions, and any playtest comparing two brains must hold durability fixed.

### Designers agree on showing intent

Designer reports fill the gap, and they agree with each other more than any single study can confirm. Bungie set three goals: make the AI intelligible, interactive and unpredictable. It dropped hidden states and told the player what the AI was doing through posture, focus of attention and dialogue. It dropped a complete world model in favour of perception the player can fool, and it named subtlety and looking broken as things to avoid *[designer report]* ([Halo GDC 2002](https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf)).

Jeff Orkin's account of F.E.A.R. goes further. Reviewers praised the squad tactics, but those were mostly scripted two- and three-line dialogues announcing state. His summary: "If the AI didn't say it, it didn't happen." One dying soldier's shout for reinforcements got the AI credited with a behaviour nobody wrote *[designer report]* ([Orkin, Game AI Pro 2](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter02_Combat_Dialogue_in_FEAR_The_Illusion_of_Communication.pdf)). Steve Rabin's "head look" (glancing at two enemies before committing to one) signals deliberation that never happened *[designer report]* ([Rabin, Game AI Pro 3](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter01_The_Illusion_of_Intelligence.pdf)).

The ship translation is direct but untested *[inference]*. Facing is decoupled from velocity, so the nose is the head: a pilot that swings its nose toward a second threat before committing performs Rabin's trick with no animation. Radio callouts like "guns hot, pulling out" are the genre's native form of Orkin's dialogue. They also turn a pilot that looks timid into one that looks cautious.

### Fairness depends on how damage arrives

In designer accounts, fairness depends on how damage arrives, not on how competent the enemy is. Several studios ration it explicitly:

| Game | Rationing rule | Source |
|---|---|---|
| DOOM (2016) | Each attack type sits behind a limited token pool that scales with difficulty | [Game Developer](https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-) |
| Spider-Man | One attacker token is passed around; off-screen ranged fire is slowed so the warning cue has time to show | [Game Developer](https://www.gamedeveloper.com/programming/designing-ai-to-do-anything-a-spider-can-in-i-marvel-s-spider-man-i-) |
| Kingdoms of Amalur | A slot grid caps how many creatures engage; flanking emerged as a side effect | [Dawe](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf) |
| The Last of Us | One NPC shooting at a time was judged enough; the rest flank or take cover | [McIntosh](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter34_Human_Enemy_AI_in_The_Last_of_Us.pdf) |
| FreeSpace 2 | Attackers and missile locks on the player are capped per difficulty level | [ai_profiles.cpp](https://github.com/scp-fs2open/fs2open.github.com/blob/master/code/ai/ai_profiles.cpp) |

These are *[designer report]*, except FreeSpace 2, which is *[demonstrated in code]*. The known cost is enemies visibly waiting their turn. Sergio Ocio Barriales's fix lets everyone shoot but only the token holder hit, with deliberate misses placed where the player sees them *[designer report]* ([Ocio Barriales](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter33_Using_Your_Combat_AI_Accuracy_to_Balance_Difficulty.pdf)).

### Telegraphs belong on lethal commitments

Telegraphing has the same designer consensus, with even less measurement behind it. Mike Stout argues for a short announcing delay before each attack, layered across animation, sound, effects and voice. Monster Hunter starts with easy tells and escalates. Both are designer report ([Stout](https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing); [Vice](https://vice.com/en/article/the-developers-of-monster-hunter-explain-what-its-like-to-build-monsters)). The negative evidence is player perception. Elden Ring players accuse enemies of input reading when they react on the first frame of a heal. They also resent delayed attacks, which turn reacting into memorising timing *[community]* ([Steam](https://steamcommunity.com/app/1245620/discussions/0/3316358999125136735)).

No controlled study isolates what tells do. No source supports a tell on every action, either. Halo wanted unpredictability alongside intelligibility. Bullet-hell design treats aimed streams as their own telegraph ([Boghog](https://shmups.wiki/library/Boghog's_bullet_hell_shmup_101)). The Last of Us deliberately lets one shooter interrupt any animation to fire, because earlier rushing players had barely been hit *[designer report]* ([McIntosh](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter34_Human_Enemy_AI_in_The_Last_of_Us.pdf)). The best-supported reading is that **tells scale with lethality** *[inference]*. Big commitments (a charged railgun release, a missile launch, a boost attack) get explicit tells and recovery windows. Low-damage pressure and ordinary movement stay readable through the projectiles and the motion themselves.

### Handicaps should look human

Designers rank handicaps that look like human limits above ones that break the shared rules. Rabin's reaction times come from cognitive psychology: about **0.2 s** for simple reaction and **0.4 s** for recognition ([Rabin, Agent Reaction Time](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter05_Agent_Reaction_Time_How_Fast_Should_An_AI_React.pdf)). FreeSpace 2 phases its lead in over about two seconds on target, and its aim error starts up to five times larger right after acquisition *[demonstrated in code]* ([aicode.cpp](https://github.com/scp-fs2open/fs2open.github.com/blob/master/code/ai/aicode.cpp)). Racing designers lower an AI driver's skill rather than give its identical car more speed *[designer report]* ([Melder](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter42_A_Rubber-Banding_System_for_Gameplay_and_Race_Management.pdf)).

The counter-examples are vivid:

- **Elite Dangerous 2.1** paired a steeper skill curve with engineered NPC weapons. Players called the NPCs overpowered. Frontier removed the stat upgrades but kept the behaviour ([PC Gamer](https://pcgamer.com/elite-dangerous-patch-stops-ai-developing-wmds)).
- **DCS** Ace-level AI out-climbed and out-turned F-15Cs with performance no human could reach, until flight-model updates *[community]* ([Skyward FM](https://www.skywardfm.com/post/dcs-world-oops-all-aces)).
- **Fighting games** use n-gram models (statistics over a player's recent move sequence) to predict players. These work so well they have to be deliberately weakened ([Vasquez](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter48_Implementing_N-Grams_for_Player_Prediction_Proceedural_Generation_and_Stylized_AI.pdf)).

When enemies fly the player's own ship, that shared ship is an asset. Stat cheats and perception cheats spend it.

### Personality needs only a few differences

Distinct personalities need surprisingly little. Pac-Man's ghosts differ mainly by one targeting rule each, plus a colour. Blinky targets Pac-Man's tile, Pinky aims four tiles ahead, and Clyde retreats once within eight tiles *[demonstrated, reverse-engineered]* ([Pac-Man Dossier](https://www.gamedeveloper.com/design/the-pac-man-dossier)). Starsector's officer personalities are bundles of engagement range and back-off thresholds *[community documentation]* ([Starsector wiki](https://starsector.wiki.gg/wiki/AI_Behaviour)). Racing AI keeps skill and style as separate axes ([Tomlinson & Melder](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter38_An_Architecture_Overview_for_AI_in_Racing_Games.pdf)). No source says how many rules must differ before two same-hull pilots read as different people.

| Lever | Strongest support | Grade |
|---|---|---|
| Middle win rate (beats you sometimes) | Yannakakis & Hallam, 30 subjects | Measured (Pac-Man) |
| Behaviour variety across encounters | Yannakakis & Hallam | Measured (Pac-Man) |
| Announced intent (dialogue) | Al Enezi & Verbrugge; Orkin | Measured (stealth) + designer report |
| Coherent, consistent behaviour | Poivet et al. | Measured (sample unknown) |
| Enemies live long enough to show decisions | Halo survey | Measured, confounded |
| Tells and punish windows on lethal attacks | Stout, Monster Hunter, fighting-game frame data | Designer report / lore |
| Rationed damage arrival (tokens) | DOOM, Spider-Man, Amalur, TLOU, FS2 | Designer report |
| Human-like handicaps over stat cheats | FS2 code, Melder, Elite 2.1, DCS | Designer report + community |
| Visual-only tells (no dialogue) | Nothing found | Gap |

## Shipped space-combat AIs converge on small, committed vocabularies

### FreeSpace 2

FreeSpace 2 is the best-documented case, because its source is open. Its fighter AI is a two-level state machine. The outer level is a mission mode (chase, evade, guard). Inside chase sit about eight submodes in the retail game: attack, super-attack, evade-squiggle, evade-brake, get-behind, get-away, continuous-turn and avoid. Ordered if/else rules pick among them, using whether I point at him, whether he points at me, range, timers and dice rolls *[demonstrated in code]* ([ai.h](https://github.com/scp-fs2open/fs2open.github.com/blob/master/code/ai/ai.h); [aicode.cpp](https://github.com/scp-fs2open/fs2open.github.com/blob/master/code/ai/aicode.cpp)).

Every submode has an explicit exit:

- evade-squiggle ends after 5 s or at 300 m;
- evade-brake ends when aim is re-acquired or after 4 s;
- get-away runs 2–5 s or out to 200–500 m;
- circle-strafe breaks off after 8 s.

Two rules target exactly the stalls this game shows. After **6 s** without an attack submode, the AI is forced into super-attack. And the code comments say get-away exists to stop endless circling. A later community-added stalemate detector, off by default, rolls against a patience value to break deadlocks.

Skill lives mostly in numbers: accuracy, evasion, courage, patience, turn-time scale, fire delay. Difficulty is openly asymmetric. At Very Easy the AI turns **3×** slower and fires **4×** less often, and the player takes **25%** damage ([ai_profiles.tbl](https://github.com/scp-fs2open/fs2open.github.com/blob/master/code/def_files/ai_profiles.tbl)). Reviewers praised the game's capable AI wingmen ([Wikipedia](https://en.wikipedia.org/wiki/Descent:_FreeSpace_%E2%80%93_The_Great_War)), but that praise doesn't say which mechanism earned it.

The costs are visible too. The selector is a chain of hand-tuned cutoffs (0.2/0.5/0.8/0.9 dot products, 50–500 m ranges, 2–8 s timers) carrying 1997 comments and decades of patches. The header still marks one mode as never implemented.

### Open-source top-down games

The open-source top-down games reach the same shape, plus a stance chosen from the matchup:

- **Naev** picks a pattern from a comparison. It dogfights if its relative HP×DPS is at least 0.25, kites when the target is chasing it, and harasses from range when the target is busy elsewhere. Skill gates fancy moves like zigzag evasion ([Naev util.lua](https://github.com/naev/naev/blob/main/dat/ai/core/attack/util.lua)).
- **Star Control II**'s melee AI chooses between pursuing and enticing from range, thrust and mass comparisons. It expresses difficulty purely as aim error (0, 20 or 40 pixels) plus which maneuvers are available. Each ship can override the generic routine. The Spathi fires its rear missile only when the enemy is behind it, a near-exact analogue of rear-dropped grenades ([UQM cyborg.c](https://github.com/Serosis/UQM-MegaMod/blob/master/src/uqm/cyborg.c); [spathi.c](https://github.com/Serosis/UQM-MegaMod/blob/master/src/uqm/ships/spathi/spathi.c)).
- **Starsector** personalities are ranges plus thresholds on flux, Starsector's shared heat budget and the counterpart of this game's laser heat. Ships back off around **80–85% flux** and won't re-engage until roughly **10–15%** *[community documentation]* ([Starsector wiki](https://starsector.wiki.gg/wiki/AI_Behaviour)).
- **Reassembly** exposes the same idea as faction flags such as ALWAYS_KITE, DODGES and BAD_AIM ([Reassembly docs](https://www.anisopteragames.com/docs/)).
- **Endless Sky**, the most widely played of these, shows the failure mode. Within 0.75× its shortest weapon range, a ship only rotates to aim and stops thrusting: "get in range and sit there", in production code. It also needs a 10% speed margin so ships don't chase each other in loops *[demonstrated in code]* ([Endless Sky AI.cpp](https://github.com/endless-sky/endless-sky/blob/master/source/AI.cpp)).

### Commercial and indie reports

Commercial developers describe the same vocabulary in prose *[designer report]*. Star Citizen's reports name its strafer and jouster behaviours. They describe a "punishidle" change that makes pilots maneuver instead of statically circling a target, and better tactic commitment, added after an investigation years into development ([SC Aug 2025](https://api.star-citizen.wiki/comm-links/20753)). An earlier report says personality behaviours unlock different maneuvers depending on pilot skill ([SC Nov–Dec 2020](https://api.star-citizen.wiki/comm-links/17950)). The 3.23 patch drew new move sets from experienced dogfighters' data. As far as the reports say, that data fed authoring, not training ([DSOGaming](https://dsogaming.com/?p=179491)).

The closest analogue to this developer is Squadron: Mercenaries, a 2024 indie game. Its original demo devolved into space jousting until it added named maneuvers taken from Wing Commander's design notes. A decision tree runs those data-driven maneuvers, and each can be restricted by pilot skill ([devlog](https://ibgoge.itch.io/wing-mercenaries/devlog/718838/recapturing-the-golden-era-of-dogfighting-behind-the-scenes-of-squadron-mercenaries-space-combat-ai)).

### The shared shape, and what is missing

Across these games, the recurring architecture has six parts:

- target selection that spreads attackers;
- a handful of named, time-committed patterns chosen by aspect, range and matchup, with some randomness;
- a steering-and-aim layer underneath;
- per-pilot numbers plus personality flags;
- aim error that shrinks with time on target;
- explicit anti-stall rules.

Three absences matter just as much. First, **no shipped space or flight-combat game was found using a learned policy for pilot tactics**. The shipped machine-learning cases are racing (GT Sophy) and locomotion under behaviour-tree tactics (ARC Raiders) ([GDC schedule](https://schedule.gdconf.com/session/learning-to-move-physics-based-enemy-locomotion-in-arc-raiders/917319)). Second, coordination is always implicit: attacker caps, target-assignment penalties, call-for-help flags. None of the open-source top-down AIs flies explicit pincers. Third, **none of these games published playtest evidence that the named-maneuver shape caused its readability**. The precedent is convergence, not proof.

Two operational lessons carry over. Fans rebuilding X-Wing couldn't reproduce its AI exactly, because the original's behaviour depended on framerate and platform ([Digital Trends](https://www.digitaltrends.com/gaming/star-wars-fans-recreate-1993s-xwing/)), so maneuver timers and dice belong on fixed-step simulation time. And The Last of Us's flank routes swung wildly from frame to frame until it switched to a stable cost shape built around a smoothed "combat vector" ([McIntosh](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter34_Human_Enemy_AI_in_The_Last_of_Us.pdf)). That is a shipped description of a twitch and its cure.

## Circling wins because the bearing outruns the aim, not because orbiting is unbeatable

### What skilled players actually do

Skilled decoupled flight, as players describe it, collapses into a few states. In Star Citizen and Elite Dangerous, high-level fights decay into circle-strafing and jousting. A long player-written Star Citizen proposal says turn fighting has all but vanished, because rotation and braking thrust stop anyone holding a positional advantage *[community]* ([SC flight model proposal](https://sites.google.com/view/starcitizenflightmodelproposal)). Experienced Star Citizen pilots sum up the skill in three habits: aim constantly, keep moving, and change direction unpredictably ([SC Base forum](https://forums.starcitizenbase.com/topic/22607-core-problem-with-6dof-what-do-you-think/)).

Elite pilots manage throttle to stay in the best turning band and fly in reverse to hold position. They also switch flight assist off to flip and fire while drifting backwards, though that last pattern rests on search snippets ([Elite Steam discussion](https://steamcommunity.com/app/359320/discussions/0/3182216552765314429)). Everspace's developers recommended strafing in a continuous spiral against fighters ([Xbox Wire](https://news.xbox.com/en-us/2016/11/22/everspace-developer-tips-strategies/)). Top-down evidence is thin. SubSpace, Starblast and Cosmic Rift guides couldn't be retrieved, so most of this comes from 3D sims, and some of it doesn't carry over to a single plane.

### Why circling dominates

Why circling dominates is documented as mechanism, not data. Circle-strafing works best close in. It punishes opponents whose turning or weapon tracking is slow, and it loses most of its edge against hitscan or high-rate-of-fire weapons ([Wikipedia: Strafing](https://en.wikipedia.org/wiki/Strafing_(video_games))). The Star Citizen proposal traces nose-on circling to three causes: fast rotation, side thrusters giving about **75% of main-engine thrust**, and strong braking. If both ships can always point at each other, they will ([SC flight model proposal](https://sites.google.com/view/starcitizenflightmodelproposal)).

Designers who fought it moved the dominant style rather than adding variety. Elite players say yaw is deliberately slow to stop ships acting as turrets. That is a player claim; no Frontier statement was found ([Elite Steam](https://steamcommunity.com/app/359320/discussions/0/1638668751270096628)). Star Citizen's Master Modes capped combat speed ([Master Modes guide](https://api.star-citizen.wiki/comm-links/20053)). Afterwards some players reported strafing crippled, with jousting and interceptors dominant, but the reports conflict and come from search snippets *[community]* ([Spectrum](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/continuing-the-mm-trainwreck)). No game has telemetry on orbit-versus-joust outcomes.

### The kinematics point at aim

The kinematics explain why circling beats the current pilots, and they point at a cheaper cause than missing tactics *[inference, first principles; to be checked against the code]*. When ship A orbits ship B at range r with sideways speed v, the line between them rotates at ω = v/r. It is the same line for both ships. With identical ships, orbiting gives no turn advantage the AI can't match. What it demands is tracking.

At 10 u/s and 15 u, the bearing turns about **38°/s**; at 8 u, about **72°/s**. A 5° laser cone allows ±2.5° of error. An aim controller that chases the target's *current* bearing settles at a steady error of roughly ω divided by its gain. So staying inside the cone at 38°/s needs an effective lag under **0.07 s**. A human aims ahead by eye. A pilot that doesn't add the bearing's rotation rate to its aim command (line-of-sight-rate feedforward) sits outside its own cone for the whole orbit.

That also reconciles two symptoms that look contradictory: the pilots can't hold aim, yet they kill fast when a shot connects. Aim is perfect while the bearing is still and lags when it rotates. FreeSpace 2's settle model, where error starts large and shrinks with time on target, addresses both.

Two other mechanics can tilt the orbit:

- **Projectile inheritance.** If projectiles don't inherit the shooter's velocity, an orbiter firing at a slow target needs no lead, while the target must lead by about asin(v / projectile speed). That is a real asymmetry, worth checking for the lasers, charge lasers and autocannon. The railgun is hitscan, so it ignores lead.
- **Strafe-to-forward thrust ratio.** Curving at radius r needs centre-pointing acceleration v²/r, which with the nose pointed inward is forward thrust. Holding speed against drag k needs continuous strafe thrust of about k·v. So the tightest orbit at speed v is roughly v² / a_forward. If strafe is nearly as strong as forward thrust, orbiting is free.

### The full set of counters in a plane

In a plane, an orbited pilot can only move radially or tangentially. That makes the counter set short, and the researchers argue it is complete *[inference]*:

- **Track** at the line-of-sight rate plus lead. The orbit becomes a symmetric shootout.
- **Match** the orbit by co-rotating. Relative sideways speed collapses and the fight becomes a damage race (Star Citizen players call it the "death circle").
- **Reverse** strafe direction, so the opponent's lead flips sign (the flat-scissors analogue).
- **Break range outward** past the 20 u laser range. The orbiter has to chase, which sets up railgun, missiles or grenades.
- **Collapse range** into autocannon distance with a boost.
- **Fight next to a rock** that sits inside the orbiter's circle.
- **Use guided and area weapons**, which ignore sideways speed.

With two enemies, a pincer at 90–180° apart defeats circling structurally, because no single sideways direction lowers both lines of sight. That is the flat version of the Thach weave. Its bait-and-hook geometry let underpowered fighters spoil or reverse every attack in testing ([Wikipedia: Thach Weave](https://en.wikipedia.org/wiki/Thach_Weave)).

### What transfers from real air combat

Real air-combat theory transfers for the geometry of velocity, but not the geometry of pointing ([Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)). Lead, pure and lag pursuit (aiming ahead of, at, or behind the target) still describe the velocity vector. But a decoupled pilot can fly lag pursuit with its velocity, so it never overshoots, while holding lead with its nose. No aircraft can do that *[inference]*.

Several aircraft concepts change meaning:

- **Overshoot** becomes a crossing-shot window rather than a role reversal, because the overshooter can flip.
- **A break turn** becomes a perpendicular strafe plus boost that ruins the attacker's lead.
- **One-circle and two-circle fights** become co-rotating and counter-rotating orbits.
- **Extension** (accelerating away) works only when the opponent's boost is on cooldown, since top speed is capped and equal.
- **Reading commitments** (a charge glow, a spent boost, heat near the cap) transfers fully, as the basis of initiative.

Yo-yos, altitude as stored energy, corner speed, and turn-radius differences between airframes don't transfer.

### Candidate maneuvers, by evidence grade

| Maneuver | Beats | Beaten by | Evidence grade |
|---|---|---|---|
| Orbit / circle-strafe | Lagging aim, slow projectiles, stationary turreting | Proper tracking, matched orbit, reversal, range break, rock inside orbit, hitscan | Strong (widely documented at high skill) |
| Joust / head-on pass | Opponents with less burst damage | Lateral-offset pass; refusing the merge | Strong |
| Flip-and-fire while drifting | A pursuer who thinks the tail is safe | Lateral offset; drag bleeding off the drift | Strong in Elite; moderate for top-down arcade |
| Strafe reversal / jink | Weapons that need lead or telegraph a charge | Fast-tracking hitscan; area denial | Moderate |
| Matched-velocity brawl | An orbiter's tracking advantage | The better aim or bigger burst | Moderate |
| Extend and reset | Short-range weapons | A pursuer with boost ready; missiles; railgun | Moderate; needs boost asymmetry |
| Stand-off charged railgun | Orbiters at range | Jinking during the charge; breaking line of sight behind a rock | Grounded guess |
| Drag pursuer over grenades | Pure-pursuit followers | Offset or lag pursuit | Grounded guess |
| Slip behind a rock | Missile locks and charged shots | Pre-positioning; flanking | Grounded guess |
| Bait-and-hook (2v1) | A target-fixated pursuer | Disengaging; watching for the hook | Strong in air combat; guess for games |

This is the researchers' catalogue on their own grading scale ([SC proposal](https://sites.google.com/view/starcitizenflightmodelproposal); [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers); [Wikipedia: Thach Weave](https://en.wikipedia.org/wiki/Thach_Weave)). The design consequence is uneven support. Only orbit, joust and flip-and-fire are well documented in high-skill play. The weapon-specific beats this game most wants (stand-off charged shot, grenade drag, rock slip) are plausible, but nobody has documented them as high-skill tactics. Elite's advice to sit close behind an NPC works only because Elite limits yaw. With identical yaw rates, being behind is worth exactly as long as the target takes to flip.

Suppose orbiting still dominates once the AI tracks properly. The researchers list flight-model levers, least invasive first *[inference]*:

1. strafe acceleration below forward acceleration;
2. a yaw cap set between the laser-range and autocannon-range line-of-sight rates;
3. projectiles that inherit shooter velocity;
4. speed-dependent yaw;
5. heat or boost costs on sustained strafing.

The evidence doesn't say whether any of these will be needed.

## Beats settle the vocabulary question; the sequencer is a separate bet

The beats proposal bundles three decisions, and the evidence for each differs sharply:

1. **The unit of behaviour.** Discrete committed beats, continuous blending (the deleted utility-scored state machine), or end-to-end learning (the benched PPO).
2. **The selector.** How the AI picks among units: authored rules, utility scoring, a behaviour tree, forward simulation, or a learned policy.
3. **The beat contract.** Which of the six fields every beat must carry.

Utility, behaviour trees, forward-simulated selection and learned selectors are mostly not rivals to beats. They are candidate selectors over beats. The real rivals to the beat vocabulary are continuous blending and end-to-end learning, and at this scale the evidence runs against both.

### The precedent for discrete, committed units

Discrete, committed units have the strongest precedent of any option. NASA's AML, from 1975, let human pilots fly close-in combat against it in Langley's Differential Maneuvering Simulator *[demonstrated]* ([NTRS 1975](https://ntrs.nasa.gov/citations/19750022744)). Its successor, Paladin, treats an engagement as a series of discrete decisions *[demonstrated]* ([Chappell et al., NTRS](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)). At each decision point it generates up to about ten trial maneuvers suited to the situation. It forward-predicts each one against an opponent extrapolated from a quadratic fit to its last three positions, assumed to fly the same aircraft. It scores those predictions with weights chosen by one of six modes, each with its own decision interval (**Evasive 0.25 s, Ground Avoidance 0.125 s, Neutral 1.0 s**). It commits to the winner until the next interval. Extrapolation error picked the wrong maneuver in **6.8%** of decisions, at small cost. The authors tuned against 32, then 320, starting conditions, and still admitted that no single metric captured performance. For the predecessor, CLAWS, pilots' comments drove the changes.

Lockheed's PHANG-MAN, runner-up in DARPA's AlphaDogfight, put a 10 Hz selector over three 50 Hz trained skills. The selector did at least as well as its best single skill, and it beat a human instructor 5–0 in simulation *[demonstrated in simulation]* ([Pope et al.](https://ar5iv.labs.arxiv.org/html/2105.00990)). Robotics formalises the same pattern. In reinforcement learning, an "option" is a start condition, a policy and a termination condition ([Sutton et al. 1999](https://www.ece.uvic.ca/~bctill/papers/learning/Sutton_etal_1999.pdf)). In control, sequenced feedback controllers with entry regions switch between each other with stability guarantees ([Burridge et al. 1999](https://kodlab.seas.upenn.edu/sequential-composition-of-dynamically-dexterous-robot-behaviors/)). A beat maps one-to-one onto an option, with the MPC as its policy *[inference]*.

### Why blending flip-flops

The evidence on why blending flip-flops is equally consistent, and it comes from three separate fields:

- **Utility AI (Guild Wars 2).** Mike Lewis reports that near-tied decisions ping-pong. Commitment bonuses, cooldowns and runtime curves only move where the oscillation happens; they never remove it *[designer report]* ([Lewis, Game AI Pro 3](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)).
- **Steering behaviours (F1 games).** Andrew Fray at Codemasters found that blended steering behaviours cancel out, that adding weights just relocates the problem, and that prioritising makes motion one-track. Replacing them shrank the codebase by **4,000 lines** *[designer report]* ([Fray, Game AI Pro 2](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter18_Context_Steering_Behavior-Driven_Steering_at_the_Macro_Scale.pdf)).
- **Sampling-based MPC (MPPI).** Averaging incompatible rollouts, such as swerve left and swerve right, produces hesitation and sometimes collision *[demonstrated in robotics papers]* ([CE-MPPI](https://arxiv.org/html/2508.21364v1); [SVG-MPPI](https://arxiv.org/pdf/2309.11040)).

The recurring fix is to choose one coherent mode discretely, then optimise within it under hard safety terms. Paladin's per-mode weights, Fray's context maps, clustered MPPI and The Last of Us's stable combat vector all share that shape. It is a hypothesis, not a finding, that the developer's twitching, timid pilots and the deleted utility state machine failed this way. It is cheap to test: log the controller's rollout costs on twitch frames and look for two clusters.

### The case for beats

The case for beats, at its strongest, has seven parts:

1. **Commitment comes by construction.** A beat runs until it exits or an interrupt fires. That replaces the bonus-and-hysteresis patching Lewis says can't work, and it turns the design question into "what are the interrupts?". Halo 2's zero-duration "impulses", such as self-preservation on damage, are one answer *[designer report, secondary]* ([Folleher on Isla](https://tams.informatik.uni-hamburg.de/lectures/2014ws/seminar/ir/presentations/2014-10-27_pascal_folleher-behavior_trees.pdf)).
2. **One intent per beat.** Each beat hands the controller a single intent sentence (the weighted goal set the brain gives the MPC), so tactics stop fighting obstacle avoidance inside the solver.
3. **Flight and fire are planned together.** A sequence like "charge while drifting into a firing lane, release, break" becomes one authorable unit, which no separate gunner can plan.
4. **Readability and counterplay are explicit fields.** No other surveyed architecture makes them first-class.
5. **Personalities are cheap.** Each pilot is a subset of beats plus parameters.
6. **Debugging is simple.** The live state is a beat name, its entry reason, its time in beat and its exit predicate.
7. **It fits a solo developer.** It keeps the MPC investment, needs no training infrastructure, and leaves room for a learned selector later.

### The case against beats

The case against is just as serious:

1. **The deleted system could come back under a new name.** Beats only differ from it if commitment and exits are structural, no beat can trade away obstacle safety, and transitions stay few and explicit.
2. **Hand-written transitions grow combinatorially.** A survey chapter warns that an FSM with 10 to 30 states becomes very hard and error-prone to extend ([Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)).
3. **Fully scripted AI is predictable and decides badly when opponents surprise it** ([Barriga, Stanescu & Buro](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter14_Combining_Scripted_Behavior_with_Game_Tree_Search_for_Stronger_More_Robust_Game_AI.pdf)). The indie Riposte! abandoned hand-made FSM opponents that were precise at control but simplistic at tactics, and cost over a month each ([Game Developer](https://gamedeveloper.com/design/devlog-2---using-machine-learning-to-create-ai-opponents)).
4. **Commitment fights responsiveness.** A beat that ignores a changed situation looks broken, so the difficulty moves into designing interrupts.
5. **A beat can be infeasible.** In a dense field, a beat's goal may be out of reach within the 1.7 s horizon. Obstacle avoidance then takes over, the beat becomes invisible, and its tell lies *[inference]*.
6. **Beats don't solve anticipation or coordination.** Each needs its own layer.
7. **Optimal motion is not legible motion.** Robotics research finds that legible motion (which reveals intent) and predictable motion (which matches expectation) are distinct properties that often conflict ([Dragan et al., HRI 2013](https://www.ri.cmu.edu/publications/legibility-and-predictability-of-robot-motion)). An optimally tracking MPC will produce predictable but illegible motion, unless the tell is an explicit cost term or an authored wind-up phase *[inference]*.
8. **The novel fields have no precedent.** The tell and counter fields have none in vehicle combat, and none of the precedents measured fun.

### How the options compare

| Option | Role | Best precedent (grade) | Solo cost | Characteristic failure |
|---|---|---|---|---|
| Continuous intent blending | Unit (deleted) | F1 2010 steering; MPPI | Sunk | Oscillation, hesitation (Lewis, Fray, MPPI) |
| End-to-end RL | Unit (benched) | AlphaDogfight, GT Sophy (demonstrated, huge budgets) | Very high | Unreadable winning tactics; reward exploits |
| Authored sequencer over beats | Selector | FS2, Naev, UQM (code); Squadron: Mercenaries (designer report) | Low at first, grows with transitions | Threshold soup; exploitable patterns |
| Utility over beats | Selector | Guild Wars 2: Heart of Thorns (designer report) | Low to build, ongoing tuning | Ping-pong at score ties, smaller once beats commit structurally |
| Behaviour tree with interrupts over beats | Selector | Halo 2 (designer report, secondary) | Low | Re-evaluation loops; hand-written transitions |
| Forward-simulated beat selection | Selector | AML/Paladin (flown against humans); Kinect Star Wars (shipped) ([Hilburn](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter08_Simulating_Behavior_Trees.pdf)) | Moderate: predictor, scoring, N× solver load | Per-mode weights become a knob farm; no single metric |
| Learned selector over beats | Selector | PHANG-MAN (demonstrated in simulation) | High: needs a reward | The reward is still undefined |
| Imitation of beat choices | Selector | Killer Instinct Shadows (shipped, fighting game) | Moderate: labelling plus features | Depends on expert-designed features |
| Game-theoretic MPC | Unit + selector | Two-drone racing (small-scale simulation) ([Spica et al.](https://arxiv.org/pdf/1801.02302)) | High compute | No game precedent |
| GOAP / HTN planning | Selector | F.E.A.R., Killzone 2 (not vehicles) | High authoring | A dogfight has little symbolic state to plan over *[inference]* |

Forward-simulated selection is less a competitor to beats than their most natural selector. Every 0.5–1 s, it would run each valid beat's intent sentence through the existing MPC for 1.7 s against one to three predicted player responses, score the outcomes, and commit to the winner. That is Paladin rebuilt on the developer's own solver, and it directly answers the critique that scripted AI is predictable. Its costs are a scoring function per personality, with exactly the tuning risk Paladin's authors admitted, and a solver load multiplied by the number of candidates. Nobody has measured that load for three enemies. Nobody has compared any of these selectors head-to-head for vehicle combat.

### Learning fits narrow slots under authored beats

The learned agents that beat humans shared three traits. They had a crisp scoring rule, enormous simulation budgets, and repeated reward reshaping to remove unwanted behaviour. Even then they did things people judged unfair *[demonstrated]*:

- **AlphaDogfight.** Heron Systems' agent beat an F-16 pilot 5–0 after a press-reported four billion-plus simulations. It won partly with head-on gun shots that training rules normally forbid, using perfect information ([Defense News](https://www.defensenews.com/artificial-intelligence/2020/08/21/ai-algorithm-defeats-human-fighter-pilot-in-simulated-dogfight/); [The War Zone](https://www.twz.com/35947/navy-f-a-18-squadron-commanders-take-on-ai-repeatedly-beating-real-pilot-in-dogfight)).
- **GT Sophy** trained on more than **1,000 PlayStation 4s**. A week before its July 2021 exhibition it was deliberately ramming opponents, and the team retrained it in a week. Shipping it took automated tests across more than 1,000 consoles ([Sony AI](https://ai.sony/blog/gran-turismo-sophy-five-years-on-from-nature-cover-to-open-frontier)).
- **AlphaStar**'s action caps still allowed superhuman bursts at crucial moments ([Irpan](https://www.alexirpan.com/2019/02/22/alphastar.html)).
- **PHANG-MAN** learned to forgo near-certain wins after its health was inflated tenfold during training ([Pope et al.](https://ar5iv.labs.arxiv.org/html/2105.00990)).
- **Riposte!**, the one indie report, was only a qualified success. One agent learned to hide in a corner, and one training run produced agents that stopped fighting entirely ([Game Developer](https://gamedeveloper.com/design/devlog-2---using-machine-learning-to-create-ai-opponents)).

The developer's target is pilots that are fun, readable and distinct, judged by playtest. That is not a reward function. The evidence implies the PPO attempt hit this gap rather than a tuning shortfall *[inference]*.

Learning still has four narrower places to help:

- **A learned selector over authored beats** (the PHANG-MAN shape). It cuts the action space from a continuous intent sentence to a handful of named choices at 1–2 Hz. Outputs stay readable because every action is a beat. It shrinks the undefined-reward problem without solving it, and it needs the beat vocabulary first.
- **Imitating beat choices.** The developer flies, labels which beat they were doing, and a nearest-neighbour or tree selector learns which beat to pick when, from a few authored features. Killer Instinct's Shadows ship a case-based version of this. They need at least three practice sessions, store up to 40 matches per opponent (which the developer still called insufficient), and rely on similarity metrics designed by an ex-tournament player ([Game Developer](https://gamedeveloper.com/programming/the-killer-groove-the-shadow-ai-of-killer-instinct)).
- **Damped prediction of player habits,** such as "breaks left after an overshoot". This is the cheapest learning that has shipped in combat AI, and it has to be deliberately weakened ([Vasquez](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter48_Implementing_N-Grams_for_Player_Prediction_Proceedural_Generation_and_Stylized_AI.pdf)).
- **One crisp control sub-skill.** RL beat optimal control in drone racing after minutes of training on a workstation, because it optimised a better objective ([Song et al.](https://arxiv.org/abs/2310.10943v2)). That fits a narrow task like threading a gap at speed, not tactics *[inference]*.

The evidence argues against cloning the developer's raw stick inputs. Behaviour cloning works best when demonstrations cover almost every state the agent will meet ([ML-Agents docs](https://github.com/beyretb/ml-agents/blob/master/docs/Training-Imitation-Learning.md)). Counter-Strike cloning needed about **95 hours** of play to match a medium bot ([Pearce & Zhu](https://arxiv.org/pdf/2104.04258)). With thin coverage, compounding error would show up as the same twitch the developer already sees *[inference]*. Building the beat vocabulary forecloses none of the options above. It is the prerequisite for every learning option still open.

## The evidence settles seven decisions and leaves the sequencer open

| Decision | Verdict | Basis |
|---|---|---|
| Fix aim first: line-of-sight-rate feedforward plus lead, then FreeSpace-style settling error and a 0.2–0.4 s reaction delay | **Supported**; cheap and independent of architecture | Kinematics *[inference]*, FS2 code, Rabin; it also removes a confound from every later test |
| Move from continuous blending to discrete committed units | **Supported** by convergent precedent and mechanism; **not** by fun data | FS2, Naev, UQM, Star Citizen, Paladin; Lewis, Fray, MPPI |
| Keep obstacle safety a hard term that no beat can override | **Supported** | MPPI averaging; context steering |
| Anti-stall rules (forced re-engage, stalemate breaker, get-away from circling) | **Supported** as shipped practice | FS2's 6 s rule and get-away; Star Citizen's punishidle |
| Ration lethal damage (one kill-shot holder) and give the others visible jobs | **Supported** by designer report; space precedent limited to FS2's attacker caps | DOOM, TLOU, Amalur, Ocio Barriales, FS2 |
| Tells on lethal commitments (railgun charge, missile lock, boost flare) | **Supported** by designer consensus, unmeasured | Stout, Monster Hunter, frame data |
| Radio callouts announcing state | **Supported** (measured in another genre, plus designer report) | Al Enezi & Verbrugge; Orkin |
| A tell and a counter for *every* beat | **Not supported**; the evidence favours tells scaled to lethality | No precedent; Halo, TLOU, bullet-hell practice |
| Authored sequencer vs forward simulation vs utility vs behaviour tree | **Open** | No head-to-head comparison exists |
| Beats will make pilots fun to fight | **Open** | No study of skilled 1v1–1v3 action AI |
| A vocabulary of 6–10 beats | **Open**; a guess from FS2's roughly a dozen submodes | — |
| Flight-model changes (strafe ratio, yaw cap, projectile inheritance) | **Premature** until the aim-parity test | Kinematics |
| Revive end-to-end RL, or clone raw demonstrations | **Not supported** at solo scale | Budgets, reward exploits, coverage requirements |
| Feints (fake tells) for ace pilots | **Not supported**, and risky | No source endorses them; Elden Ring complaints |

The cheapest experiments should run in an order that keeps their results interpretable. Since the developer judges by playtest, each pass signal is something a player sees, with logs used only for diagnosis.

1. **Aim parity** (hours to a day). A scripted bot or the developer orbits one enemy at laser range. Compare the share of time the target sits inside the 5° cone before and after adding feedforward and lead, and check whether projectiles inherit shooter velocity. If the orbit becomes a visibly even shootout, the flight model is fine and circling was an AI defect. If good tracking still loses, the flight-model levers become live.
2. **Rollout bimodality** (hours). On frames where a pilot twitches, dump the 128 rollout costs and look for two clusters. Two clusters confirm the averaging diagnosis and strengthen the one-intent-per-beat argument. One cluster sends the search elsewhere.
3. **Wizard-of-Oz beats** (a few days). Run this after tests 1 and 2, so improved aim doesn't take credit. Build three beats (lead-pursuit gun run, break-and-extend, railgun charge-and-snap) with no sequencer. Bind them to hotkeys and trigger them live while a playtester fights one enemy, with durability held fixed. The beats premise is falsified if the beats look like the current brain or get swallowed by obstacle avoidance (an execution problem no sequencer can fix). It is also falsified if testers can't tell the beats apart or predict them after a few encounters, or if hand-sequenced beats aren't more purposeful than the current brain.
4. **Callout A/B** (a day). Run the same behaviour with and without radio lines, to replicate the dialogue finding in this genre.
5. **Minimal sequencer, then two enemies** (days). Use a priority list, minimum durations, two or three interrupts and FreeSpace's anti-stall rules, then add a kill-shot token.
6. **Selector bake-off** (a week, only if test 5 is promising). Pit hand rules against forward-simulated selection at 1–2 Hz over the same beats. Measure solver headroom for three enemies first, and keep playtests blind to which selector is running.

## Conclusion

The evidence reframes the decision. Whether to use committed, named maneuvers is mostly settled. Every documented space-combat AI and the longest-flown dogfight logic use discrete committed units, and three separate fields explain why continuous blending flip-flops. The real risk sits in two places the literature never tested. The first is the per-beat tell-and-counter contract. It is this game's genuine novelty and has zero precedent; its defensible form is tells scaled to lethality, with motion itself as the tell for movement beats. The second is the selector. An authored sequencer is one reasonable bet among several, and forward-simulated selection on the existing MPC has the deepest flown precedent. The reported symptoms (twitching, lost aim, losing to circling, sitting in range) look more like control and commitment defects than missing tactics. Fixing them first is cheap, and it makes any later architecture comparison readable instead of confounded.

On learning, the evidence is clearer than the decision to bench RL suggests. It failed not because dogfighting is special, but because "fun to fight" is not a reward. Every learning option that remains therefore sits on top of an authored beat vocabulary: a selector, a matcher trained on labelled demonstrations, a damped habit predictor, or a crisp control sub-skill. That makes the beats worth building even if the authored sequencer above them is later replaced.
