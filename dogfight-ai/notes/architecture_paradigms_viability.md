# Tactical decision-layer architectures for a dogfight AI over an existing MPC, and whether "named committed maneuvers" are viable

Scope note: these notes cover the decision layer above a working sampling-based MPC (128 rollouts, ~1.7 s horizon, 50 Hz) that tracks a weighted "intent sentence" set ~5 Hz by a swappable brain. The game is 2.5D top-down, decoupled facing and velocity, 1 player vs 1–3 enemies flying the same ship in asteroid fields. Each finding is labeled as either a demonstrated result or a claim. Inferences are the researcher's own and are kept separate from the findings.

---

## Q1. Survey of authored architectures (FSM/HFSM, behavior trees, utility/IAUS, GOAP/HTN, scripted maneuver libraries, rule-based systems): shipped vehicle/ship-combat use, developer reports, authoring cost, debuggability, failure modes

### Takeaway
Every authored architecture has shipped, and each has a known failure mode. FSMs get hard to extend late in development, behavior trees loop between stateless behaviors, utility systems oscillate and are "more art than science" to tune, and planners make plans nobody predicted. Space-combat games that have documented their pilot AI (FreeSpace 2, Star Citizen, Starsector) all converge on the same shape: a small set of named combat modes or maneuvers, with personality expressed as parameters or as access to different maneuvers. Nobody has published a head-to-head comparison of architectures for vehicle combat.

### Cited Findings
**General comparison (Game AI Pro survey chapter)**
- FSMs: adding states early is trivial, but "if you're nearing the end of development and your FSM is already complicated with 10, 20, or 30 existing states, then fitting your new state into the existing structure can be extremely difficult and error-prone." HFSMs add "history states", which the authors explicitly describe as adding hysteresis that a flat FSM lacks. — [Dawe et al., "Behavior Selection Algorithms: An Overview", Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
- Behavior trees: "Since behaviors themselves are stateless, care must be taken when creating behaviors that appear to apply memory." Their example is a citizen who flees until out of danger, at which point the higher-priority behavior takes him back into combat, so he "continually loop[s] between two behaviors". The tree is re-evaluated from the root on every tick. — [Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
- Utility systems: they adapt well, but are "often somewhat unpredictable". They are "somewhat challenging to tune… The trick is to juggle all the models to encourage the most reasonable behaviors to shine… This is often more art than science." If a design needs specific behaviors at specific moments, "you must make a point to override the utility calculations with more scripted actions." — [Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
- GOAP was pioneered for F.E.A.R. (2005) and later used in Just Cause 2 and Deus Ex: Human Revolution. HTN was used in Killzone 2 and Transformers: Fall of Cybertron. HTN's authored network "removes the NPC's ability to build plans that the designer might not have thought of", which the chapter calls "either a strength or a weakness." The chapter also calls GOAP "significantly easier to design than one based around hierarchical task networks." — [Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
- Planners in general: "it is difficult to predict all of the situations where a planner can break down. This, understandably, causes distaste for planners among many [designers]." — [Hilburn, "Simulating Behavior Trees", Game AI Pro ch.8](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter08_Simulating_Behavior_Trees.pdf)

**Utility AI in shipped games: Infinite Axis Utility System (IAUS) and dual utility**
- Guild Wars 2: Heart of Thorns used an AI "modeled on the Infinite Axis Utility System (Mark 2013)". Each action has a "decision score evaluator" (DSE). A DSE's considerations are inputs normalized to [0,1], each remapped through a response curve, and the results are multiplied together. The highest-scoring DSE wins. — [Mike Lewis, "Choosing Effective Utility-Based Considerations", Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- In Heart of Thorns, runtime and cooldown considerations were "commonplace" for controlling timing. A cooldown "is useful for avoiding strobing between two otherwise competing decisions". A runtime curve stops an evade from being chosen back-to-back, which avoids "edge cases where the character constantly evades and becomes impossible to hit". "The vast majority of the response curves used in the game are chosen from a small palette of preset curves." — [Lewis, Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- A key admission: when two decisions score similarly they "may oscillate or ping-pong". The standard fixes are a "commitment bonus" for the last decision, weights, and runtime/cooldown curves. All of them "do not eliminate the possibility of two decisions oscillating — they simply shift where the scores will land when the oscillation happens… scores will continue to compete in harder and harder to recognize ways." The suggested fix is to add another consideration. — [Lewis, Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- A "relative direction" consideration was added to charge attacks so the AI would not charge targets behind itself, which avoided "ugly animation snaps and directional flip-flopping". — [Lewis, Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- Kevin Dill's dual-utility reasoning (shipped in Zoo Tycoon 2) gives each option a rank and a weight. The reasoner picks the highest rank category, then picks within it by weight-based random. He warns that pure weight-based random "can easily make your AI look stupid" because low-weight options still get picked. In Zoo Tycoon 2, ranks were coordinated by "a simple FSM". — [Dill, "Dual-Utility Reasoning", Game AI Pro 2 ch.3](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter03_Dual-Utility_Reasoning.pdf)

**Behavior trees in a vehicle-combat shooter**
- Halo 2 (GDC 2005, Damian Isla): a prioritized behavior tree/DAG with a maximum depth of four. It uses "impulses", which are zero-duration triggers such as "Self-preserve on damage impulse" or "Player vehicle entry impulse". An impulse can redirect to another behavior without appearing in the behavior stack. — [Folleher seminar slides summarizing Isla 2005](https://tams.informatik.uni-hamburg.de/lectures/2014ws/seminar/ir/presentations/2014-10-27_pascal_folleher-behavior_trees.pdf); secondary source.
- Halo 1 (GDC 2002) design goals included "Transparent thought process" and "Racial personality". Under intelligibility: "Discarded: Hidden States → Inform the Player" through language, posture, gesture and focus of attention. Randomness was "Discarded" in favor of reactive, analog reactions. Under "Things to Avoid" the talk lists "Subtlety" and "Looking Broken". The budget was 20–25 actors and 2–4 vehicles at about 15% of Xbox CPU. — [Butcher & Griesemer, "The Illusion of Intelligence" (GDC 2002 slides)](https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf)

**Space-combat games with documented pilot AI**
- FreeSpace 2 (open-sourced as fs2open) structures fighter AI as modes plus chase submodes: SM_ATTACK, SM_EVADE_SQUIGGLE, SM_EVADE_BRAKE, SM_EVADE, SM_SUPER_ATTACK, SM_AVOID, SM_GET_BEHIND, SM_GET_AWAY, SM_EVADE_WEAPON, SM_FLY_AWAY, SM_ATTACK_FOREVER, and later AIS_CHASE_GLIDEATTACK and AIS_CHASE_CIRCLESTRAFE. AIM_GET_BEHIND is commented "This mode is not actually implemented." Per-difficulty ai_profiles include ai_accuracy, ai_evasion, ai_courage, ai_patience, ai_predict_position_delay, ai_max_aim_update_delay, ai_glide_attack_percent, ai_circle_strafe_percent, ai_random_sidethrust_percent, ai_stalemate_time_thresh, ai_stalemate_dist_thresh and ai_get_away_chance. — [fs2open code/ai/ai.h](https://raw.githubusercontent.com/scp-fs2open/fs2open.github.com/master/code/ai/ai.h)
- Starsector (2D top-down ship combat with independent facing): one shared ship AI, varied by officer personality (Timid, Cautious, Steady, Aggressive, Reckless). Personalities change engagement range (Timid fights at longest weapon range; Aggressive "strongly attempts to stay at range of its shortest non-missile weapon"), the flux threshold for backing off, and whether other enemy ships are considered (Reckless ignores them). Outnumbered ships act more cautiously, except Reckless. Documented failure modes include "flux-locking" (refusing to drop shields at high flux) and escorts jostling each other into bad positions. — [Starsector wiki: AI Behaviour](https://starsector.wiki.gg/wiki/AI_Behaviour) (community wiki)
- Starsector's 2011 dev blog described earlier personality names (cowardly, cautious, steady, aggressive, suicidal, fearless). For example, "A cautious captain is also quite good at harassment, as he won't force the issue and can keep an enemy tied down in a game of cat and mouse." — [Fractal Softworks blog, "Captain Personalities, Fleet Control Update" (2011)](https://fractalsoftworks.com/2011/08/03/captain-personalities-fleet-control-update). The full page returned HTTP 402; this content is from the search snippet only.
- Star Citizen (Nov–Dec 2020 monthly report): "aggressive, cowardly, reckless, resentful, and show-off behaviors were added to open pilots up to different maneuvers based on their skill". A "fire discipline" skill value decides when pilots use lasers or ballistics. A "defend target" bug left NPCs unaware their protectee was dead, which "led to erratic flight behavior". — [RSI Monthly Report Nov–Dec 2020 (via star-citizen.wiki API)](https://api.star-citizen.wiki/comm-links/17950)

**Rule-based expert systems**
- TacAir-Soar is "an intelligent, rule-based system that generates believable humanlike behavior". It does "real-time hierarchical execution of complex goals and plans". In the Synthetic Theater of War 1997, a 48-hour exercise, it flew all U.S. fixed-wing aircraft. — [Jones et al., "Automated Intelligent Pilots for Combat Flight Simulation", AI Magazine 20(1), 1999](https://ojs.aaai.org/aimagazine/index.php/aimagazine/article/view/1438)

### Inferences
- The developer's deleted "utility-scored state machine" matches the failure Lewis documents: scores ping-pong, and commitment bonuses only move the oscillation elsewhere. A "knob farm" verdict is what the literature predicts when utility is used for mutually exclusive, temporally extended choices without hard commitment.
- None of the documented space-combat AIs is a continuous blend. FreeSpace, Star Citizen and Starsector all use a discrete vocabulary (named submodes or maneuvers), and personality appears as parameter differences or as access to different maneuvers. That is weak but consistent precedent for the "named maneuver" shape. It is weak because none of these games has published playtest evidence that the shape caused readability.
- FreeSpace's ai_stalemate_time_thresh / ai_stalemate_dist_thresh suggests that "gets in range and sits there" was a known failure in that genre, handled with an explicit stalemate breaker.
- GOAP and HTN fit problems with world-state preconditions (doors, cover, items). A 1v1–3 dogfight has almost no symbolic world state, so a planner's search adds authoring cost with little to search over. This is inference: no vehicle-combat GOAP/HTN postmortem was found either way.

### Gaps
- No public postmortem or head-to-head comparison of FSM vs BT vs utility for ship or vehicle dogfighting was found.
- The FreeSpace 2 submode-transition logic (timers, randomness) was not retrieved: the hard-light.net wiki returned 403, and the aicode.cpp source was not read.
- No source found on how Starsector's AI is structured internally (it is closed source; only the modding API and community descriptions are public).
- No documented GOAP or HTN use for vehicle or flying combat AI was found.
- TacAir-Soar rule counts and development effort were not retrieved (the PDF was not accessible).
- Star Citizen's later Alpha 3.23 "move sets… strafe runs, new orbits… break-aways" appeared in search snippets, but the specific report was not verified.

---

## Q2. Maneuver libraries and maneuver selection in air-combat simulation (AML, trial maneuvers, game-theoretic/ADP, Falcon/DCS, TacAir-Soar, ALPHA); forward simulation vs hand-written transitions

### Takeaway
The longest-running lineage of automated dogfight opponents flown against real human pilots works the same way: NASA's AML (1975 onward), then CLAWS, then Paladin at NASA Langley. At fixed decision intervals the system generates a situation-dependent set of trial maneuvers, forward-predicts each one against an extrapolated opponent, scores the predicted outcomes with weights chosen by a discrete situation mode, and commits to the best until the next interval. That is the closest precedent to "authored situation layer + forward-simulated candidates + low-level controller". Its documented weak points are tuning the weight vectors and the lack of a single performance metric. Commercial sims (DCS, Falcon BMS) don't publish their BFM logic, and their criticisms are mostly about AI cheating on the flight model.

### Cited Findings
- AML (Burgin, Fogel, Phelps; NASA CR-2582, 1975) simulates one-on-one close-in combat. A real-time version "permits human pilots to fly air-to-air combat against the adaptive maneuvering logic (AML) in Langley Differential Maneuvering Simulator (DMS)." — [NTRS 19750022744](https://ntrs.nasa.gov/citations/19750022744)
- AML was revised in 1986 (NASA CR-3985) to fix bugs found "over the years" and to replace the equations of motion, which eliminated an "over-the-top problem" in near-vertical loops. — [NTRS 19880002266](https://ntrs.nasa.gov/citations/19880002266). Burgin & Sidor later published "Rule-based air combat simulation" (NASA CR-4160, 1988) as an improved AML. — [NTRS 19890018022](https://ntrs.nasa.gov/citations/19890018022)
- Paladin (NASA Langley, Chappell/McManus/Goodrich) was built from a baseline AML and works as follows (all from the paper):
  - "As in AML, Paladin models a combat engagement as a series of discrete decisions."
  - At each decision point it extrapolates the opponent, "generates a situationally dependent set of trial maneuvers", predicts a future engagement state for each, and passes those states through scoring functions. The scores are "weighted, based on the mode of operation", and the winning maneuver "is then used to direct the aircraft until the next decision interval."
  - There are six modes of operation, each with "a unique vector of scoring weights and a unique decision interval" (e.g., Evasive 0.25 s, Ground Avoidance 0.125 s, Neutral 1.0 s). The opponent look-ahead is 4× the decision cycle.
  - The candidate set is small and depends on the situation. Over-the-top reversal generates a single maneuver, high-speed turning generates oblique turns, and several situation families are capped at ten trial maneuvers.
  - The opponent is predicted by fitting a quadratic to its last three positions and assuming the same aircraft.
  - Extrapolation error caused the wrong trial maneuver in 6.8% of decisions, but the effects were "both infrequent and relatively small in resulting loss of capability."
  - Tuning used batches of 32 sets of initial conditions, expanded to 320, but "no single metric has been developed that can completely measure the performance of an aircraft in the engagement."
  - Its predecessor CLAWS was flown against experienced pilots in the DMS, and "the pilots' comments and suggestions are then the basis for changing" the system.
  - Conclusion: without situation-dependent mode selection Paladin "would be forced to either sacrifice its real-time execution or assume an unrealistic tactical mind-set."
  - Source: [Chappell, McManus, Goodrich, "Trial Maneuver Generation and Selection in the Paladin Tactical Decision Generation System" (NTRS 19930013899)](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)
- A NASA report summary says the Moffett Field AML effort focused on improving guidance laws "regardless of required execution time", while the Dryden effort aimed at "a robust decision logic, guaranteed to work in real time." — [NTRS 19890018022 (search summary)](https://ntrs.nasa.gov/api/citations/19890018022/downloads/19890018022.pdf)
- The trial-maneuver scheme is still active research, e.g., "level-by-level elimination to select the best trial maneuver." — [BUAA, "Study of trial maneuver scheme in autonomous air combat decision"](https://research.buaa.edu.cn/en/publications/study-of-trial-maneuver-scheme-in-autonomous-air-combat-decision-/)
- McGrew et al. (MIT, JGCD 2010) used approximate dynamic programming with a "rollout based policy extraction" for online use. Their 2D model has level flight and fixed velocity. They report good performance "without explicit coding of air combat tactics", attributing the success to "extensive feature development, reward shaping and trajectory sampling." — [MIT DSpace record](https://dspace.mit.edu/handle/1721.1/67298?show=full)
- Psibernetix ALPHA (genetic fuzzy tree, 2016) was assessed by retired USAF Col. Gene Lee as "the most aggressive, responsive, dynamic and credible AI I've seen to date", running on Raspberry-Pi-class compute. This is a claim from a press summary, not a controlled comparison. — [DSIAC article](https://dsiac.dtic.mil/articles/genetic-fuzzy-tree-ai-beats-tactical-experts-in-combat-simulations)
- DCS World changelog: AI will "appropriately use lead, pure, and lag intercept geometry" and "use different defensive manoeuvres such as… notch and reversal/extensions based on Skill Level"; a "new and improved AI BFM for jets" was added. — [DCS 2.7.14 changelog](https://www.digitalcombatsimulator.com/en/news/changelog/release/2.7.14.24228/)
- Before the 2021 flight-model and 2022 AI-BFM updates, Ace-level DCS AI "consistently out climbing and out turning F-15Cs", with the ability to "pull unbelievable Gs and use a thrust to weight ratio human players simply cannot match." After the updates, lower skill levels became "genuinely good more often than not." — [Skyward FM, "DCS World: Oops, All Aces"](https://www.skywardfm.com/post/dcs-world-oops-all-aces) (enthusiast blog)
- Falcon BMS: community reports describe dogfight AI that is "very good" with strong energy management at Ace level, and wingmen who need micromanaging. No primary documentation of the maneuver logic was found. — [Falcon BMS forum](https://forum.falcon-bms.com/post/17242)

### Inferences
- The trial-maneuver structure is demonstrated (decades of use against real pilots in NASA simulators), not a hypothetical. The structure is: discrete mode → mode-specific weight vector and decision interval → small set of candidate maneuvers → forward prediction → score → commit for one interval.
- Compared with hand-written transition rules, forward simulation replaces "if X then do Y" with "which of these few candidates scores best when simulated?". That cuts the number of transitions to author: each candidate needs a generator and the scoring is shared.
- But forward simulation moves the authoring burden into weight vectors per mode, which is exactly the knob-farm risk. Paladin's own authors say no single metric captured performance, and they tuned against hundreds of initial conditions.
- Paladin's decision intervals (0.125–1.0 s, depending on mode) are themselves a form of commitment. The candidate is held until the next interval rather than re-picked every frame.
- The DCS criticism (AI using flight performance humans can't reach) is the relevant fairness lesson for a game where enemies "fly the same ship". Readability and fairness both suffer if the AI's execution exceeds the player's controls. Inference: an MPC with perfect knowledge of its own dynamics may "fly" more precisely than any player, so tells and deliberate limits matter.

### Gaps
- Falcon 4.0 / BMS BFM logic: the source leaked in 2000, but no public technical write-up of its maneuver library or skill-level parameters was found.
- DCS AI internals are proprietary. Only changelog-level descriptions exist.
- No primary text of Burgin's AML maneuver-selection math was retrieved; the volume-2 PDFs were not parsed. The trial-maneuver description above comes from Paladin, which states it follows AML's discrete-decision approach.
- No published comparison of AML or Paladin against a purely rule-based (transition-table) opponent with human pilots was found. The search result's claim of "significant performance gains" over AML was not verified in the retrieved text.

---

## Q3. Learned approaches (ADT/ACE, GT Sophy, OpenAI Five, AlphaStar, RL in shipped games, indie ML-Agents): what made them work, and indie-scale cost

### Takeaway
The learned agents that beat humans had four things in common: a crisp scoring rule (damage or kills, race position, win/loss), very large simulation budgets (billions of steps or 1,000+ consoles), and teams who reshaped rewards to remove unwanted behavior (unsportsmanlike ramming, reckless head-on shots). Even then the agents showed behaviors judged unrealistic or unfair. RL in shipped commercial enemy AI remains rare. The main shipped case (GT Sophy) needed Sony-scale infrastructure, and other shipped uses cover locomotion rather than tactics. Indie ML-Agents reports show modest success with reward-exploit and "stopped engaging" failures. This matches the developer's PPO experience.

### Cited Findings
- AlphaDogfight Trials (Aug 2020): Heron Systems' agent beat a USAF F-16 pilot 5–0 in simulation. Press reports say it trained on "over 4 billion simulations", about "12 years of experience". These figures are press-level claims. — [Unite.AI](https://www.unite.ai/ai-controlled-jet-fighter-defeats-human-pilot-in-simulated-combat/); [Defense News](https://www.defensenews.com/artificial-intelligence/2020/08/21/ai-algorithm-defeats-human-fighter-pilot-in-simulated-dogfight/)
- Criticisms (Navy F/A-18 squadron commander): the AI won partly through forward-quarter (head-on) gun shots that are "normally prohibited in training", perfect information, no rules of engagement, and indifference to G. It "would most likely never bleed airspeed or altitude excessively." — [The War Zone](https://www.twz.com/35947/navy-f-a-18-squadron-commanders-take-on-ai-repeatedly-beating-real-pilot-in-dogfight). DARPA's Justin Mock said the AI will "take shots that we would never take in our training environments." — [Defense News (search summary)](https://www.defensenews.com/artificial-intelligence/2020/08/21/ai-algorithm-defeats-human-fighter-pilot-in-simulated-dogfight/)
- Lockheed Martin's PHANG-MAN (2nd place) is a two-level hierarchy:
  - A selector at 10 Hz picks among three 50 Hz low-level policies: Control Zone, Aggressive Shooter (side and head-on shots) and Conservative Shooter. All are trained with SAC.
  - Rewards shaped with "domain knowledge from retired fighter pilots" (track angle, adverse angle, distance, hard-deck height, closure).
  - It beat the human instructor 5W–0L and lost to Heron, partly because its shots were from farther away.
  - Failure mode: inflating health ×10 in training taught it to prioritize "future positioning over immediate scoring" and "give away near victories."
  - The selector's win rate was at least that of its best sub-policy against any opponent.
  - Source: [Pope et al., arXiv 2105.00990](https://ar5iv.labs.arxiv.org/html/2105.00990)
- ACE: in September 2023 the X-62A VISTA, flown by AI agents, dogfought a manned F-16 (announced April 2024). Officials did not say who won; the stated goal was demonstrating safe testing. — [The Aviationist](https://theaviationist.com/2024/04/18/ai-flew-x-62-vista-during-dogfight/); [DARPA news](https://www.darpa.mil/news/2024/ace-ai-aerospace)
- GT Sophy:
  - Trained with Sony's DART system across "more than 1,000 PlayStation 4 consoles". The reward balanced "track progress, collision avoidance, steering smoothness, and racing etiquette."
  - A week before the July 2021 exhibition, testers found "GT Sophy was crashing into opponents on purpose" and blocking aggressively. The team retrained in one week.
  - Shipped in GT7 as "Race Together" in Feb 2023 (4 circuits). GT Sophy 2.0 followed in Nov 2023 (340+ cars, 9 tracks) and 3.0 as paid DLC in Dec 2025.
  - Deployment required "automated tests running across more than 1,000 PlayStations in parallel".
  - Sources: [Sony AI, "GT Sophy five years on"](https://ai.sony/blog/gran-turismo-sophy-five-years-on-from-nature-cover-to-open-frontier); [Sony AI training blog](https://ai.sony/blog/training-the-worlds-fastest-gran-turismo-racer)
- OpenAI Five trained on 256 GPUs and 128,000 CPU cores, playing about 180 years of games per day via self-play. A "team spirit" hyperparameter blended individual and team reward. — [The Register](https://www.theregister.co.uk/2018/06/25/openai_dota_2/); [Wikipedia: OpenAI Five](https://en.wikipedia.org/wiki/OpenAI_Five)
- AlphaStar's APM caps (600 per 5 s … 300 per 60 s) still allowed superhumanly fast, accurate bursts at crucial moments. Critics argued its Stalker micro was something "no human player in the world could do." — [LessWrong/GreaterWrong discussion](https://www.greaterwrong.com/posts/f3iXyQurcpwJfZTE9/alphastar-mastering-the-real-time-strategy-game-starcraft-ii/comment/Nc8eSoQ6rGXyX6Ktu); [Alex Irpan, "An Analysis of AlphaStar"](https://www.alexirpan.com/2019/02/22/alphastar.html)
- Song et al. (Science Robotics 2023) found that RL beat optimal control in drone racing because it "optimizes a better objective". Optimal control's "explicit intermediate representation, such as a trajectory… limits the range of behaviors." The policy reached superhuman control "within minutes of training on a standard workstation". This is a narrow single-agent control task, not tactics. — [arXiv 2310.10943](https://arxiv.org/abs/2310.10943v2)
- Why RL is rare in shipped enemy AI: commentators cite the need for designer control and predictability, and that RL optimizes winning rather than fun. These are commentary-level claims. — [Microsoft Research, "Designer-centered reinforcement learning"](https://www.microsoft.com/en-us/research/blog/designer-centered-reinforcement-learning/); [Game Developer, "Good AI is predictable"](https://www.gamedeveloper.com/design/good-ai-is-predictable)
- ARC Raiders (Embark, Oct 2025) uses RL for locomotion and some reactive behaviors of legged machine enemies. Embark presented this at GDC 2026 ("Learning to Move: Physics-Based Enemy Locomotion"). This is locomotion, not tactical decision-making. — [GDC 2026 session listing](https://schedule.gdconf.com/session/learning-to-move-physics-based-enemy-locomotion-in-arc-raiders/917319); [ARC-RL paper (alphaXiv)](https://www.alphaxiv.org/abs/2605.19503v2)
- Indie ML-Agents (Riposte!):
  - The developer abandoned FSM opponents that were "obnoxiously good in terms of control and accuracy, while at the same time boringly simple at strategy and tactics", estimating over a month per hand-made opponent.
  - ELO self-play produced the best agents. Intermediate rewards created exploits (one agent learned to hide in a corner).
  - One run's agents "stopped engaging" entirely.
  - Final verdict: "moderately successful".
  - Source: [Game Developer devlog](https://gamedeveloper.com/design/devlog-2---using-machine-learning-to-create-ai-opponents)

### Inferences
- Every success above had a scoring rule that doubles as a well-defined reward (damage or kills, lap position, win/loss), plus huge sample budgets. The developer's stated root problem ("the target behavior was never well defined") goes to the heart of this: the target for these pilots is "fun, readable, distinct", judged by playtest, and that is not a reward function. The GT Sophy etiquette crisis and Lockheed's "give away near victories" artifact show that even with a crisp win condition, reward shaping toward desirable style is iterative and expensive.
- The AlphaDogfight and AlphaStar criticisms (head-on snaps, superhuman micro) are the learned-agent versions of "can't read it, can't counter it". These failure modes run directly against this game's readability requirement.
- Song et al. cuts both ways. It suggests an MPC tracking an intent sentence is capped by intent quality, which supports putting effort into the intent-setting layer. It also suggests RL is cheap for narrow, well-defined control sub-skills.
- At indie scale (one developer, one machine), self-play leagues and 1,000-console rollouts are out of reach. The cheapest learned option that remains is narrow: a small discrete choice over authored options, or a single well-defined sub-skill.

### Gaps
- No primary Heron Systems technical paper was retrieved (training details are press-level).
- No published indie postmortem of ML-Agents for ship or vehicle dogfight enemies was found.
- No shipped commercial game was found that uses RL for tactical enemy decision-making in combat (as opposed to racing or locomotion), other than research demos.

---

## Q4. Hybrid approaches (hierarchical RL with authored options, RL for a sub-skill, imitation/behavior cloning, Drivatar, Killer Instinct Shadows, GAIL): can a solo developer teach pilots by flying demonstrations?

### Takeaway
The most successful hybrid pattern is a learned or rule-based selector over a small set of skills with clear entry and exit. It appears in the options framework (formally) and in Lockheed's PHANG-MAN (empirically). Imitation from demonstrations works at large data scales (Counter-Strike: 95 hours), or when expert-designed features reduce the problem to choosing among parsed patterns (Killer Instinct Shadows: 3 dojo sessions minimum, up to 40 matches per opponent, which its developer still called insufficient). Raw-control behavior cloning from one developer's flying is very unlikely to be enough. Imitation at the level of "which beat to use, when" is more plausible but undemonstrated for this genre.

### Cited Findings
- Options framework: an option is a triple ⟨I, π, β⟩ of an initiation set, an intra-option policy and a termination condition. A set of options over an MDP forms a semi-MDP. — [Sutton, Precup, Singh, "Between MDPs and semi-MDPs", AIJ 1999](https://www.ece.uvic.ca/~bctill/papers/learning/Sutton_etal_1999.pdf)
- Sequential composition of feedback controllers ("funnels"): a "palette of pre-existing feedback controllers" partitions state space so that "entry into any cell guarantees passage to successively 'lower' cells until the goal cell is achieved". The technique comes with formal stability guarantees for the switching. — [Burridge, Rizzi, Koditschek, IJRR 18 (1999)](https://kodlab.seas.upenn.edu/sequential-composition-of-dynamically-dexterous-robot-behaviors/)
- PHANG-MAN's learned selector over three specialized policies did at least as well as its best sub-policy, and "generate[d] unique and effective strategies by combining complementary behaviors". The two-level structure let the selector train "without the added complexity associated with non-stationary policies." — [Pope et al., arXiv 2105.00990](https://arxiv.org/pdf/2105.00990)
- Killer Instinct Shadows (Iron Galaxy):
  - Uses case-based reasoning. Matches are parsed into "400–700 patterns" per match. Decisions use nearest-neighbor retrieval over "over 40 metrics" of state similarity, sometimes picking lower-ranked options "to trick its opponent."
  - Baseline competence needs three dojo sessions. The system stores up to 40 matches (up to 28,000 patterns) per match-up, but "even the 40-match limit per opponent can still prove insufficient."
  - The parsing and similarity metrics required "expert knowledge" from an ex-tournament player.
  - "Everything your shadow does in a fight is something you've actually done before."
  - Sources: [Game Developer, "The Killer Groove: The Shadow AI of Killer Instinct"](https://gamedeveloper.com/programming/the-killer-groove-the-shadow-ai-of-killer-instinct); [GamesRadar](https://www.gamesradar.com/future-fighting-game-ai)
- Forza Drivatar was developed with Microsoft Research Cambridge. Early versions are described as a Bayesian neural network over player data. Later versions are described as cloud-trained from all players' data; Forza Motorsport's Update 20 added multi-line racing. Players have long complained about aggressive Drivatar behavior. — [Wikipedia: Forza](https://en.wikipedia.org/wiki/Forza) (secondary; its "reinforcement learning paradigm" claim for FM5+ is unverified); [Forza.net, Update 20 Drivatar changes](https://forza.net/news/forza-motorsport-drivatar-changes); [Forza forums thread on Drivatar behavior](https://forums.forza.net/t/is-poor-drivatar-behavior-cumulative-to-create-even-more-bad-drivatar-behavior-serious-question/32094)
- Counter-Strike behavior cloning: 5.5M frames (about 95 hours) of scraped human play plus smaller clean expert sets produced an agent that "matches the performance of the medium difficulty built-in AI" with a humanlike style. — [Pearce & Zhu, arXiv 2104.04258](https://arxiv.org/pdf/2104.04258)
- ML-Agents docs: GAIL is "generally the preferred approach, especially if you have few (<10) episodes of demonstrations". Behavior cloning "tends to work best when there exists demonstrations for nearly all of the states that the agent can experience, or in conjunction with GAIL and/or an extrinsic reward". On Pyramids, 6 demonstration episodes cut training steps by more than 4×. — [ML-Agents docs: Training with Imitation Learning (GitHub mirror)](https://github.com/beyretb/ml-agents/blob/master/docs/Training-Imitation-Learning.md)

### Inferences
- A "beat" with entry condition, flight goal and exit maps one-to-one onto an option ⟨I, π, β⟩, with the MPC tracking the beat's intent sentence as π. It also maps onto a funnel in sequential composition. The theory exists; what's untested is whether hand-authored I and β are good enough at game speed.
- The PHANG-MAN result supports one specific hybrid: authored or trained skills plus a selector. The developer could later swap a learned selector over beats for the authored sequencer. That would shrink the RL action space from a continuous intent sentence to a discrete choice at ~1–2 Hz, and would keep outputs readable because every action is a named beat. This would still need a reward, so it doesn't solve the "undefined target" problem. It only shrinks it.
- Demonstration teaching by the developer:
  - Raw-control BC needs coverage of "nearly all states" (ML-Agents docs) or tens of hours (CS:GO). One player flying alone is unlikely to produce that coverage, and compounding error would surface as exactly the "twitch" already complained about.
  - Imitation at the beat level is more plausible: label which beat the developer was "doing", then fit a nearest-neighbor or decision-tree selector over a few authored features (Killer Instinct-style). It would need minutes to hours of labeled play, not tens of hours, but it inherits Killer Instinct's dependence on expert-designed features.
  - A "personality from demonstration" variant (record the developer flying "aggressive" vs "cautious") is an untested idea.

### Gaps
- No published demonstration-learning results for 2D/2.5D ship dogfighting were found.
- Killer Instinct's data requirements are per-match-up for a fighting game. Translating them to continuous-space dogfights is speculative.
- No primary Microsoft Research Drivatar paper was retrieved.

---

## Q5. Planning and search over the existing MPC: tactical cost terms, prediction, opponent-aware/game-theoretic MPC, rollouts over maneuver choices; why mixing tactical objectives into one cost oscillates

### Takeaway
Three independent bodies of evidence explain why summing tactical terms with obstacle avoidance in one cost produces hesitation and flip-flop:
- Steering-behavior blending: weights "only move the problem", and prioritization makes movement "single minded".
- Utility score oscillation: the bonus fixes only shift it.
- Sampling-based MPC (MPPI-family): importance-weighted averaging of incompatible rollouts causes "hesitation or even collision".

The structures that avoid this choose a discrete mode first and optimize within it. Examples: Paladin's mode-specific weights, Kinect Star Wars' simulate-then-select behavior tree, context steering's "interest/danger" maps, and mode-seeking or clustered MPPI. Game-theoretic MPC exists and beats plain MPC in two-drone racing, but at small scale and with heavy computation.

### Cited Findings
- Context steering (F1 2010/2011, Codemasters):
  - Averaged steering behaviors can cancel to near zero ("the entity hardly moves").
  - Adding weights means "we've only succeeded in moving the problem, at the cost of a new weighting parameter."
  - Prioritization makes movement near obstacles "very single minded, and not very expressive."
  - "Adding prioritization or weighting… translates to louder shouting rather than more nuanced debate."
  - In F1 2010 the avoid behavior "decomposed into an old-school sequence of if/else blocks… a maintenance nightmare." Replacing steering behaviors with context maps in F1 2011 "shrunk [the codebase] by 4000 lines" while improving avoidance and overtaking.
  - Hysteresis came from blending last frame's context map with the current one, "a kind of global hysteresis that requires no support from the behaviors."
  - Source: [Andrew Fray, "Context Steering", Game AI Pro 2 ch.18](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter18_Context_Steering_Behavior-Driven_Steering_at_the_Macro_Scale.pdf)
- MPPI "suffers from averaging-induced failure in cluttered environments, where the importance-weighted update averages incompatible rollouts and leads to hesitation or even collision when an obstacle lies directly ahead". The proposed fixes cluster feasible rollouts by avoidance mode and pick one. — [Clustering-embedded MPPI, arXiv 2508.21364](https://arxiv.org/html/2508.21364v1)
- MPPI "struggles with multimodality of the optimal distributions, such as those involving non-convex constraints for obstacle avoidance… especially challenging for fast maneuvering vehicles", and the proposed fix is a "mode-seeking" solution. — [SVG-MPPI, arXiv 2309.11040](https://arxiv.org/pdf/2309.11040)
- Lewis's utility oscillation finding (Q1): commitment bonuses "simply shift where the scores will land when the oscillation happens." — [Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- Simulate-then-select in a shipped game (Kinect Star Wars Jedi AI): each behavior-tree node gets a simulate() step. Selectors "simulate each of their child behaviors… fed through the heuristic function to generate a score," then pick the best. "The behavior tree structure allows designers to have full control over what the AI can do, while the planner mechanism handles determining what the AI should do." The heuristic classifies outcomes coarsely (Deadly/Hurtful/Safe/Urgent). — [Hilburn, Game AI Pro ch.8](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter08_Simulating_Behavior_Trees.pdf)
- Scripts with choice points plus look-ahead search: "Fully scripted game AI systems are usually predictable and… susceptible to poor decision-making when facing unexpected opponent actions." Exposing choice points to search keeps "control over the range of possible AI behaviors" while evaluating consequences. Players who discover predictable patterns "quickly learn to exploit it." — [Barriga, Stanescu, Buro, Game AI Pro 3 ch.14](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter14_Combining_Scripted_Behavior_with_Game_Tree_Search_for_Stronger_More_Robust_Game_AI.pdf)
- Game-theoretic receding-horizon planning (two-drone racing): iterative best response with a sensitivity term approximates a Nash equilibrium each step and "significantly outperforms" an MPC racing algorithm in simulation. Physical experiments ran at speeds up to 1.25 m/s. — [Spica et al., arXiv 1801.02302](https://arxiv.org/pdf/1801.02302)
- Paladin (Q2) is the air-combat version of mode-then-optimize: mode selects the weight vector and interval, candidates are forward-simulated and scored. — [NTRS 19930013899](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)
- Song et al.: an intermediate trajectory representation "limits the range of behaviors that can be expressed." — [arXiv 2310.10943](https://arxiv.org/abs/2310.10943v2)

### Inferences
- The developer's history of "tactical cost terms fighting obstacle avoidance inside the solver" and of pilots that "twitch and fly timidly" matches the MPPI averaging failure and the steering-blend cancellation. If the solver averages rollouts weighted by a cost that contains both "go there to shoot" and "stay out of line of fire / away from rocks", bimodal good options (left vs right of an asteroid; attack vs disengage) average into hesitant in-between motion. This is inference, but it is a falsifiable diagnosis: log the rollout cost distribution and check for bimodality on frames where the ship twitches.
- The structural fix that recurs across domains is: pick one coherent intent (a mode or beat) discretely, then let the continuous optimizer pursue only that intent, with hard safety terms. Per-mode weights in Paladin, per-beat intent sentences in the hypothesis, and per-mode clustering in CE-MPPI all have this shape.
- Using the MPC's own rollouts to evaluate candidate beats (run each candidate intent sentence for 1.7 s against a predicted player, score the outcome) is a cheap version of Paladin and Kinect Star Wars. It reuses existing infrastructure, but it multiplies solver cost by the number of candidates. Running it at 1–2 Hz rather than 5 Hz may keep this affordable for three enemies. Inference; not measured.
- Full game-theoretic MPC (iterated best response) is demonstrated only at small scale and would add significant compute and complexity. Its main benefit, anticipating that the opponent reacts, may be approximated more cheaply by scoring candidates against two or three player-response hypotheses (see Q6).

### Gaps
- The classic Koren & Borenstein (1991) potential-field oscillation paper was not retrieved. The steering and MPPI sources cover the same phenomenon.
- No source was found on game-theoretic MPC deployed in a shipped game.
- No source was found that quantifies the compute cost of per-candidate MPC rollouts at game rates. This must be measured in-engine.

---

## Q6. Opponent prediction and anticipation: cheap techniques that have shipped

### Takeaway
Cheap prediction has a long record. Constant-maneuver extrapolation from the last few samples, assuming the opponent flies the same craft, was good enough for Paladin (wrong maneuver 6.8% of the time, with small losses). N-grams over discretized player actions predict so well in fighting games that they must be toned down. Case-based pattern matching ships in Killer Instinct. No shipped source was found for intent classification in vehicle combat.

### Cited Findings
- Paladin estimated the opponent's velocity and load factor from a quadratic fit to the last three positions, "assuming that the opponent's aircraft is aerodynamically similar", and extrapolated "assuming no change in the opponent's current maneuver". Extrapolation errors caused wrong choices in 6.823% of decisions but "do not greatly affect" overall capability. Earlier research found realistic input noise had "negligible impact." — [NTRS 19930013899](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)
- N-grams expose a predictor to a history of player actions, and "for certain types of games (such as Mortal Kombat-style fighting games) they can work so well that they have to be toned down." The window length is N−1, and higher orders need more data. — [Vasquez, "Implementing N-Grams for Player Prediction…", Game AI Pro ch.48](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter48_Implementing_N-Grams_for_Player_Prediction_Proceedural_Generation_and_Stylized_AI.pdf)
- Killer Instinct Shadows parse raw inputs into higher-level patterns (zoning, combos, counters) and retrieve by similarity over 40+ hand-chosen metrics. This makes it a shipped form of pattern-based anticipation, built on expert-designed features. — [Game Developer](https://gamedeveloper.com/programming/the-killer-groove-the-shadow-ai-of-killer-instinct)
- Human reaction time for AI design purposes is "somewhere between 0.2 and 0.4 seconds, possibly longer depending on context." — [Game AI Pro 2 ch.5, "Agent Reaction Time"](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter05_Agent_Reaction_Time_How_Fast_Should_An_AI_React.pdf)
- Opponent-aware planning in racing approximates how much the adversary "will yield". — [Spica et al.](https://arxiv.org/pdf/1801.02302)

### Inferences
- Because enemies fly the same ship as the player, the MPC's own dynamics model is a valid predictor of the player's reachable set. That is the same symmetry assumption Paladin used. A cheap anticipation model is to extrapolate the current thrust/strafe/yaw input as constant, plus two or three alternative hypotheses (hard break left, hard break right, boost) bounded by control limits, and score candidate beats against all of them (worst or expected case).
- Pattern prediction (n-grams over player beats: "boosts when shot at", "breaks left after overshoot") would give "anticipation" that players can feel and exploit. That is good for readability and counterplay, but it needs a player-action discretizer, which is the same feature-engineering burden as Killer Instinct.
- The gap between the AI's prediction quality and the player's reaction time (0.2–0.4 s) bounds how far ahead a "tell" must lead its committed action to be counterable.

### Gaps
- No shipped vehicle-combat or space-shooter example of player-intent classification was found.
- No quantitative data was found on n-gram accuracy for continuous-motion games.

---

## Q7. Commitment and decision cadence: commitment vs continuous re-evaluation, hysteresis, plan stickiness, interrupts; links to perceived purposefulness and jitter

### Takeaway
The game-AI literature treats oscillation as a selection-layer problem. Its standard tools (commitment bonus, cooldown, runtime curve, global hysteresis, history states) reduce but do not eliminate it (Lewis). Systems that fly well against humans commit by construction: Paladin holds each maneuver for a mode-dependent 0.125–1.0 s, and PHANG-MAN's selector decides at 10 Hz over 50 Hz skills. Readability research from Halo and robotics says players perceive intent only when it is communicated. "Subtlety" reads as broken. Legible motion can differ from merely predictable motion.

### Cited Findings
- Commitment bonus, weights and runtime/cooldown "do not eliminate… oscillating — they simply shift where the scores will land." — [Lewis, Game AI Pro 3 ch.13](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf)
- Context steering's frame-to-frame map blending is "global hysteresis that requires no support from the behaviors", whereas per-behavior hysteresis "adds state and complexity to behaviors." — [Fray, Game AI Pro 2 ch.18](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter18_Context_Steering_Behavior-Driven_Steering_at_the_Macro_Scale.pdf)
- HFSM history states add "additional hysteresis that isn't present in an FSM." — [Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
- Paladin's decision intervals differ per mode (Evasive 0.25 s, Ground Avoidance 0.125 s, Neutral 1.0 s). The chosen maneuver "is then used to direct the aircraft until the next decision interval." — [NTRS 19930013899](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)
- PHANG-MAN selector at 10 Hz over 50 Hz low-level policies. — [arXiv 2105.00990](https://ar5iv.labs.arxiv.org/html/2105.00990)
- Halo 2 "impulses" are zero-duration interrupts that redirect behavior, such as self-preserve on damage. — [Folleher slides on Isla 2005](https://tams.informatik.uni-hamburg.de/lectures/2014ws/seminar/ir/presentations/2014-10-27_pascal_folleher-behavior_trees.pdf)
- Halo 1 included "Transparent thought process" and "Inform the Player" (hidden states were discarded). "Subtlety" and "Looking Broken" were on its avoid list. In playtests with weak enemies, 8% of players rated the AI "Very Intelligent"; with tough enemies, 43% did ("Smarter = Tougher, Tougher = Smarter"). — [Butcher & Griesemer GDC 2002 slides](https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf)
- "Predictability and legibility are fundamentally different and often contradictory properties of motion." Legible motion conveys intent, and predictable motion matches expectation. — [Dragan, Lee, Srinivasa, HRI 2013](https://www.ri.cmu.edu/publications/legibility-and-predictability-of-robot-motion)
- Deliberate-miss systems: shots without a "token" deliberately miss. Misses must be hidden so players aren't "distracted by the fact that the AI is being generous and missing shots on purpose", while still conveying urgency. — [Ocio Barriales, Game AI Pro 3 ch.33](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter33_Using_Your_Combat_AI_Accuracy_to_Balance_Difficulty.pdf)
- Dual-utility weight-based random "can easily make your AI look stupid." — [Dill, Game AI Pro 2 ch.3](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter03_Dual-Utility_Reasoning.pdf)

### Inferences
- The complaints "twitches" and "cannot hold aim" fit a selection cadence (5 Hz re-picking of intent) with no structural commitment. That is the regime Lewis says can only be patched, not fixed, by bonuses.
- Commitment by construction (a beat runs until its exit condition or an interrupt) changes the question from "how much bonus prevents flip-flop?" to "what are the interrupts?". Halo 2's impulses and Paladin's short Evasive and Ground-Avoidance intervals show the standard answer: a small set of high-priority interrupts (incoming fire, collision imminent) and long commitment otherwise.
- Dragan's result implies that a beat's "tell" may need deliberately exaggerated, slightly suboptimal motion (a wind-up yaw, a visible boost charge) for players to read it. An MPC that tracks the goal optimally will tend to produce predictable but illegible motion unless the tell is an explicit term or an authored pre-phase.
- Halo's "tougher = smarter" warns that playtest perception of intelligence is confounded by toughness. Playtest protocols comparing brains should hold enemy durability fixed.

### Gaps
- No controlled study was found linking a specific commitment duration to perceived purposefulness in action games. The cadence figures above are engineering choices, not perception results.

---

## Q8. The "named committed maneuvers (beats) over an MPC" hypothesis: precedents, arguments for and against, what must be true, cheapest falsifying experiment, and the strongest alternatives with costs

### Takeaway
The hypothesis is viable, and it has the best-documented precedent of any option considered. The pattern of discrete maneuver vocabulary, situation-gated selection, commitment for an interval and low-level execution goes back to NASA's AML and Paladin (flown against human pilots for decades). It shows up as named submodes in FreeSpace 2, named maneuvers plus personality traits in Star Citizen, and a skill selector in Lockheed's PHANG-MAN. Robotics formalizes it as options and funnels. The novel and unproven parts are the visible tell and player counter per beat, the joint flight-and-fire plans, and whether an authored sequencer avoids becoming the deleted utility state machine under a new name. The literature points to two specific risks: transition-rule combinatorics with predictability (scripts get exploited), and beats whose goals the MPC cannot execute among asteroids. A cheap test can falsify it in days by hand-triggering a few beats.

### Cited Findings (precedents)
- Discrete maneuvers selected per decision interval and executed until the next interval, with situation modes, flown against human pilots: AML, CLAWS and Paladin. — [NTRS 19750022744](https://ntrs.nasa.gov/citations/19750022744); [NTRS 19930013899](https://ntrs.nasa.gov/api/citations/19930013899/downloads/19930013899.pdf)
- Named fighter submodes (attack, evade-squiggle, evade-brake, get-behind, get-away, glide attack, circle strafe), with per-profile percentages and a stalemate breaker: FreeSpace 2. — [fs2open ai.h](https://raw.githubusercontent.com/scp-fs2open/fs2open.github.com/master/code/ai/ai.h)
- Personalities "open pilots up to different maneuvers based on their skill": Star Citizen. — [RSI Monthly Report Nov–Dec 2020](https://api.star-citizen.wiki/comm-links/17950)
- One AI, distinct personalities via engagement range and back-off thresholds: Starsector. — [Starsector wiki](https://starsector.wiki.gg/wiki/AI_Behaviour)
- A selector over specialized skills beat or matched each skill alone: PHANG-MAN. — [arXiv 2105.00990](https://ar5iv.labs.arxiv.org/html/2105.00990)
- The formal analogues are options ⟨I, π, β⟩ and sequential composition of controllers with entry regions. — [Sutton et al. 1999](https://www.ece.uvic.ca/~bctill/papers/learning/Sutton_etal_1999.pdf); [Burridge et al. 1999](https://kodlab.seas.upenn.edu/sequential-composition-of-dynamically-dexterous-robot-behaviors/)
- An authored vocabulary chosen by forward simulation shipped in Kinect Star Wars. — [Game AI Pro ch.8](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter08_Simulating_Behavior_Trees.pdf)
- Readability through communicated intent comes from Halo. — [GDC 2002 slides](https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf)
- Evidence against, or risks:
  - Fully scripted AI is predictable and gets exploited, and it decides poorly against unexpected opponent actions. — [Game AI Pro 3 ch.14](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter14_Combining_Scripted_Behavior_with_Game_Tree_Search_for_Stronger_More_Robust_Game_AI.pdf)
  - FSMs with 10–30 states are hard to extend. — [Game AI Pro ch.4](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter04_Behavior_Selection_Algorithms.pdf)
  - Riposte!'s FSM opponents were "obnoxiously good in… control and accuracy, while… boringly simple at strategy." — [Game Developer devlog](https://gamedeveloper.com/design/devlog-2---using-machine-learning-to-create-ai-opponents)
  - FreeSpace's own header marks AIM_GET_BEHIND "not actually implemented", a sign that named-maneuver vocabularies accumulate dead or partial entries. — [fs2open ai.h](https://raw.githubusercontent.com/scp-fs2open/fs2open.github.com/master/code/ai/ai.h)

### Inferences: arguments for
1. **Commitment by construction.** A beat runs until it exits or an interrupt fires. That replaces the bonus and hysteresis patching Lewis says cannot fully work, and it directly addresses chatter, twitch and "can't hold aim".
2. **One coherent intent per beat.** Each beat issues a single intent sentence (e.g., "lead-pursuit gun run": aim at predicted intercept, position on the target's rear quarter, line-of-fire term low, obstacle term at full). This keeps tactics from fighting obstacle avoidance in the solver. It is the "mode first, then optimize" structure that Paladin, context steering and CE-MPPI converge on. It also avoids the MPPI averaging failure, because the cost no longer contains competing tactical modes.
3. **Joint flight-and-fire planning becomes authorable.** A railgun beat ("charge while drifting to a firing lane, snap-aim and fire, then break") or a grenade beat ("drag the pursuer over the drop point, release on overshoot") encodes the coupling of maneuver and weapon that a separate gunner cannot plan. The unsolved sticking point gets a concrete home.
4. **Readability and counterplay are first-class.** The tell and counter fields force each beat to be legible (Dragan) and communicated (Halo's "inform the player"). This targets the stated requirement head-on. No other architecture surveyed makes this explicit.
5. **Distinct personalities are cheap.** Per pilot: a beat subset, beat parameters (engagement range, commit duration, aggression thresholds), and sequencer biases. The Starsector, Star Citizen and FreeSpace ai_profiles precedent shows this scales from data rather than code.
6. **Debuggability.** The live state is one beat name, its entry reason, time in beat and its exit predicate. That is simpler to overlay and log than a field of scores. Each beat can be tested in isolation with scripted scenarios, as Paladin's knowledge sources were "developed and tested independently".
7. **Fits solo scale and keeps the MPC.** No training infrastructure is needed, the MPC investment is preserved, and a learned selector can be slotted in later (PHANG-MAN shape) without changing the beats.

### Inferences: arguments against
1. **It may be the deleted system renamed.** The old system bundled goal modes and solver weight overrides into scored states. If beats are selected by scores every 200 ms, without hard commitment, and still carry tactical terms that override obstacle avoidance, the same chatter and knob-farm failure will return. The hypothesis is only distinct if (a) commitment and exits are structural, (b) obstacle safety is never traded off by a beat, and (c) transitions are few and explicit.
2. **Combinatorial transitions and predictability.** N beats with hand-written entry rules grows the FSM problem, and fixed rules become patterns players learn and exploit (Game AI Pro 3 ch.14, Killer Instinct "exposing flaws"). Some predictability is the goal here (readable, counterable), but too much becomes boring. Variety would need either randomization among valid beats, which Halo discarded and Dill warns can look stupid, or rollout-based selection.
3. **Commitment vs responsiveness.** A committed beat that ignores a changed situation looks dumb (Halo's "Looking Broken"). Interrupt design is where the difficulty concentrates.
4. **MPC execution risk.** In dense asteroid fields, a beat's goal may be infeasible within a 1.7 s horizon. The obstacle term then dominates, the beat is invisible, and the tell lies. Beats need feasibility-aware entry conditions, e.g., checking the MPC's own rollouts before committing.
5. **Anticipation isn't solved by beats.** Entry conditions and fire plans need a player predictor (Q6). Without one, beats will commit on stale geometry.
6. **Multi-enemy coordination isn't solved by beats.** "Ignore each other" needs a separate layer that rations who attacks and how. The shipped precedent is the Amalur "grid capacity / attack capacity" token system. — [Dawe, "Beyond the Kung-Fu Circle", Game AI Pro ch.28](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf)
7. **No direct evidence of fun.** None of the precedents published playtest evidence that named maneuvers with tells produced "fun to fight". AML and Paladin optimized combat effectiveness, not entertainment.

### Inferences: what would have to be true
- The MPC can visibly execute each beat's intent sentence in typical asteroid density, so that beat differences show on screen.
- A vocabulary of roughly 6–10 beats covers most situations the player creates. This range is a guess; FreeSpace's fighter submode list is about a dozen.
- Entry and exit predicates can be cheap, robust geometry tests (range, aspect, closure, line of fire, cooldowns), optionally backed by 1–2 Hz rollout checks.
- Tells lead the committed action by more than ~0.2–0.4 s of human reaction time plus decision time, and are visually distinct at gameplay zoom.
- Players can name what an enemy is doing and find counters in playtest, with durability held constant (Halo's confound).

### Inferences: cheapest falsifying experiment ("Wizard-of-Oz beats")
- Implement 3 beats with no automated sequencer:
  - lead-pursuit gun run (commit until overshoot or 2 s);
  - break-and-extend (boost away perpendicular, re-engage);
  - railgun charge-and-snap (charge with visible tell, hold a firing lane, fire, break).
- Bind them to debug hotkeys on one enemy. The developer, or a second person, triggers beats live while a playtester fights. Also log beat, time-in-beat, aim-on-target fraction and abort reasons.
- Falsifiers:
  - (a) Beats look the same as the current brain, or are swallowed by obstacle avoidance. That means an execution problem, and no sequencer can fix it.
  - (b) Playtesters cannot tell beats apart or predict them after a few encounters. The readability premise fails.
  - (c) Hand-sequenced beats are not more fun or purposeful than the current brain. The vocabulary premise fails.
- If it passes, add the simplest sequencer (priority list + minimum duration + 2–3 interrupts) and repeat with 2 enemies plus an attack-token rule.
- Cost estimate: a few days on top of the existing MPC. It isolates execution and readability from sequencing logic, which is where the old system's failure was hard to attribute.

### Inferences: strongest alternatives for this developer, with costs

**A. Rollout-scored maneuver selection (the Paladin, Kinect Star Wars and trial-maneuver lineage)**
- Shape: the same beat vocabulary, but instead of hand-written transitions, every 0.5–1 s the AI simulates each valid beat's intent sentence with the existing MPC against 1–3 predicted player responses. It scores outcomes with a per-personality weight vector (damage dealt, damage risk, energy/position, obstacle margin) and commits to the winner.
- Cost: a forward model (exists), a player predictor (Q6), a scoring function per personality, and N× solver cost at low rate.
- Risk: the scoring weights become a knob farm. Paladin needed 320-condition sweeps and still lacked a single metric.
- This is less an alternative than the most natural sequencer for the beats hypothesis. It is the documented answer to "scripted AI is predictable and decides poorly against unexpected opponent actions" (Game AI Pro 3 ch.14).

**B. Data-driven utility (IAUS) or a behavior tree with interrupts selecting the same beats**
- Shape: the conventional industry choice. Utility in the Guild Wars 2 style (DSEs with preset response curves, runtime and cooldown) or a Halo 2-style prioritized tree with impulses. Both pick among committed beats rather than continuous intent weights.
- Cost: low engineering effort, but sustained tuning.
- Risk: utility oscillation per Lewis, which matters less if beats commit structurally. A behavior tree is predictable and easy to debug, but transitions are hand-authored.
- This differs from the deleted system only if the beats themselves enforce commitment and never override obstacle safety.

**C. A learned or case-based selector over authored beats (deferred)**
- Shape: either PHANG-MAN-style RL choosing among beats at 1–2 Hz, or Killer Instinct-style nearest-neighbor over the developer's labeled demonstrations of "which beat when".
- Cost: training or labeling infrastructure plus a reward or feature definition. It is cheaper than the PPO intent-sentence attempt because the action space is a handful of named beats and outputs stay readable.
- Risk: the "undefined target" problem shrinks but does not disappear. Both need the beat vocabulary first, so they are an upgrade path rather than a competitor.

### Gaps
- No precedent was found that combines all six beat fields (entry, flight goal, fire plan, exit, tell, counter). The tell and counter fields in particular have no documented vehicle-combat precedent.
- No playtest data comparing named-maneuver AI with blended or utility AI for perceived intelligence or fun was found.
- MPC compute headroom for rollout-scored selection with 3 enemies is unknown and must be measured.
- Whether the specific prior failure (tactical cost terms fighting obstacle avoidance) came from MPPI-style averaging is a hypothesis. It can be checked by logging the rollout cost distribution, but that was not verified here.
