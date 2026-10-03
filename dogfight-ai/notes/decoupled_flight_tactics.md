# Decoupled-Facing Flight: Skilled Piloting, Dogfight Tactics, and Counters to Circle-Strafing

Scope note for the report writer: web evidence on this topic is thin and mostly community-sourced (forum posts, player wikis, one long player-written design proposal). Primary developer design statements were hard to retrieve. Citations marked "(search snippet; page not fetched)" come from search-result text whose exact page attribution could not be verified; treat them as weaker than fetched pages. Everything under "Inferences" is reasoning (often first-principles kinematics), not sourced fact, and is labelled with an evidence grade where it matters. The developer's flight model is: one plane; independent thrust, strafe and yaw; linear drag; speed cap; short boost on cooldown; forward-fixed weapons with narrow cones; identical ships for player and AI.

## 1. Named maneuvers and tactics skilled players use in decoupled / assist-off flight

### Takeaway
Across Star Citizen and Elite Dangerous, play at a high level decays into two documented states — circle-strafing ("turreting while strafing") and jousting (head-on passes) — plus the flip-and-fire-while-drifting move that decoupled flight uniquely enables (Elite FA-off flips, "reverski", flying backwards while shooting). Classic tail-chase turn fighting largely disappears when ships can rotate fast and strafe hard. Game-specific guides for SubSpace/Continuum, Cosmic Rift, Altitude, Star Control, Reassembly and Cosmoteer could not be retrieved, so the strongest evidence is from the two 3D sims, and some of it does not carry into a single plane.

### Cited Findings
**Star Citizen (decoupled / coupled 6DOF)**
- In 1v1, fights decay into circle-strafing and jousting. Circle-strafing is the most basic maneuver and also the most powerful. Pure "turreting" (sitting still and pointing) is called the least-skilled style, and strafing adds moderate skill — [SC Flight Model Proposal (player-authored, 2016–2018)](https://sites.google.com/view/starcitizenflightmodelproposal)
- "Boom and zoom" exists but devolves into jousting because high rotation rates let ships stay nose-to-nose instead of flying curved attack paths. Turn fighting is described as "virtually non-existent" because retro thrust and rotation rates stop anyone gaining a positional advantage behind an enemy — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- Experienced SC pilots summarize skilled dogfighting as three principles: aim constantly, never stop moving, and keep your direction unpredictable. They say circle-strafing combined with angle fighting stays intense when G-force limits apply — [Star Citizen Base forum, "Core problem with 6DOF"](https://forums.starcitizenbase.com/topic/22607-core-problem-with-6dof-what-do-you-think/)
- The same thread's original poster complains that 6DOF fights become "turrets in space": pointing straight at an opponent causes overshoot, and planning ahead wastes thrust cancelling unwanted velocity — [Star Citizen Base forum](https://forums.starcitizenbase.com/topic/22607-core-problem-with-6dof-what-do-you-think/)
- Under Master Modes (Alpha 3.23), players report that strafing is penalized by an artificial slowdown, which leaves jousting and hit-and-run as the main ways to get guns on target. Jousting "typically becomes a death spiral/circle" won by whoever has more or bigger guns, and interceptors dominate because speed makes them best at jousting and hit-and-run — [Spectrum thread "continuing the mm trainwreck"](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/continuing-the-mm-trainwreck); [Spectrum thread "a game of punishment"](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/star-citizen-a-game-of-punishment-and-why-you-don-); [Lemmy discussion](https://feddit.it/post/8029745) (search snippets; pages not fetched)

**Elite Dangerous (Flight Assist on/off)**
- Players turn FA off to flip the ship without changing their direction of travel, which brings guns to bear almost instantly. One described pattern: boost past, switch FA off, rotate back, and shoot the enemy while flying backwards at full speed, so the enemy "can't fly around" — Elite Steam discussions ([1](https://steamcommunity.com/app/359320/discussions/0/1743355067101072302), [2](https://steamcommunity.com/app/359320/discussions/0/1692662484249565747)); [Frontier forum "Flight Assist Off"](https://forums.frontier.co.uk/threads/flight-assist-off.396897) (search snippets; pages not fetched)
- "Reverski": players describe putting the throttle into reverse during an FA-off turn so speed builds while the ship keeps moving backwards. Exact community definitions vary, so treat this description as approximate — same Elite discussions as above (search snippets; pages not fetched)
- Turn rate is highest when speed sits in the throttle "blue zone", so skilled pilots manage throttle to stay there while turning — [Elite Steam discussion](https://steamcommunity.com/app/359320/discussions/0/3182216552765314429)
- Specific player techniques: use vertical (up) thrusters to tighten the turn and stay close above the target; down-thrust while pitching turns faster but opens range; boost plus FA-off sharply increases rotation; jousting means "boost past them head to head close" and then out-pitch them; fly in reverse when you are slower than the target to hold relative position. And: starting close behind an NPC, under 700 m, the NPC "will never manage to face you" while you stay close — [Elite Steam discussion](https://steamcommunity.com/app/359320/discussions/0/3182216552765314429)
- Fixed weapons are described as needing the most skill. Turrets track in nearly every direction (blind only in their mounting plane), and Frontier mainly balances gimballed and turreted weapons through damage output — [Frontier forum "Fixed, Gimbals, and Turrets"](https://forums.frontier.co.uk/threads/fixed-gimbals-and-turrets-a-different-way.250870) and Elite Steam discussions (search snippets; pages not fetched)

**Starsector (top-down, planar, facing separate from motion via strafe)**
- Player controls include strafing while keeping the ship pointed at the mouse (SHIFT-A/D), braking (C) and reversing (S). In small ships, "face-to" mode is the easiest way to keep the nose on target while moving — [Starsector wiki: Piloting](https://starsector.wiki.gg/wiki/Piloting)
- Flux (one shared heat budget for weapons fire and shield damage) is the core resource. When flux is high, take kinetic hits on armour rather than shields, and vent between engagements — [Starsector wiki: Piloting](https://starsector.wiki.gg/wiki/Piloting)
- Players put most weapons on autofire so the mouse only governs shields — [Starsector wiki: Piloting](https://starsector.wiki.gg/wiki/Piloting)
- In close frigate fights, players zigzag backwards to disengage and reset. "Anvil" (a sturdy ship that tanks the front) plus "hammer" (a ship that hits from the side or rear) is a named combined-arms pattern — [Starsector community summaries](https://youtubesummary.com/summary/fAbkYAES43M); [r/starsector combat tips](https://reddit.birdcat.cafe/r/starsector/comments/clj5tp/tips_for_combat/evvxa64/?context=3) (search snippets; pages not fetched)

**SubSpace/Continuum (2D Newtonian arena)**
- Flight is inertial, with a top speed. The afterburner drains energy, and when it stops the ship returns to normal top speed. Energy is a single pool for both health and ammo, so careless firing invites quick death. Ships collide inelastically with walls and asteroids but take no damage from them. Eight ship classes. Squads, leagues and duel arenas exist — [Wikipedia: SubSpace](https://en.wikipedia.org/wiki/SubSpace_(video_game))

### Inferences
- **Evidence-graded maneuver catalog for the developer's planar model** (S = strong, widely documented at high skill; M = moderate, documented but in a different flight model or thinly; G = grounded guess from kinematics or fiction):

| Maneuver | When used | Beats | Beaten by | Flight-model dependencies | Grade |
|---|---|---|---|---|---|
| **Orbit / circle-strafe** (strafe tangentially, forward thrust toward the centre for curvature, nose on target) | Inside weapon range, against an opponent who can't keep the nose on a rotating line of sight | Opponents whose aim lags, slow-flight projectiles, stationary "turrets" | Hitscan, wide-cone or area weapons; an opponent matching the orbit (it becomes a DPS race); reversals; leaving range; terrain inside the orbit | Strafe thrust strong relative to forward thrust, high yaw rate, finite projectile speed, drag (sets the orbit's sustaining thrust) | S |
| **Joust / head-on pass** | Neutral merge; high-burst short-range weapons (autocannon) | Opponents with less burst or shield | A lateral-offset pass (strafe so you cross outside their cone while keeping yours on); an opponent who refuses the merge and orbits | Burst damage, shield regen, boost (passing speed), weapon range | S |
| **Flip-and-fire while drifting** (reverse flight, the BSG Viper move) | Being pursued; after a boost-past | A pursuer who assumes the tail position is safe | A pursuer holding lateral offset (high line-of-sight rate) or dropping out of range; drag bleeding the drifter's speed so the gap closes | Facing decoupled from velocity; low drag (long drift); fast yaw | S (Elite), M in planar arcade |
| **Strafe reversal / jink** | Opponent's lead solution is settled; against a charge weapon you can see charging | Lead-dependent and charge-telegraphed weapons | Hitscan with fast tracking; area denial | Lateral acceleration (strafe thrust ÷ mass), boost | M (SC "directional unpredictability") |
| **Matched-velocity brawl** (counter-orbit in the same rotational direction) | You out-DPS or out-shield the orbiter | The orbiter's tracking advantage disappears | The better aimer or bigger alpha wins | Equal top speed | M (SC "death circle") |
| **Cut inside / collapse range** | You have the autocannon or more burst | Long-range or charge weapons | Collision risk; the line-of-sight rate spikes for both ships | Boost impulse, collision rules | G |
| **Extend and reset** | Shields low, boost available, opponent mid-cooldown | Short-range weapons | A pursuer who still has boost; missiles; railgun | Boost cooldown asymmetry; with an equal speed cap, extension only works on cooldown asymmetry or a wrong-facing opponent | M (BFM; Starsector zigzag backwards) |
| **Stand-off charged shot** | Range beyond laser range against a predictable path | Orbiters at range (hitscan ignores lead) | Jinking during the visible charge; using terrain to break line of sight; closing to inside the cone's tracking limit | Charge time, cone width, line-of-sight rate at range | G/M |
| **Drag pursuer over dropped grenades** | Opponent in a tight tail chase on your exact track | A pure-pursuit follower | Lag or offset pursuit; staying out of the drop zone | Grenade arming time and spread, pursuer's path discipline | G |
| **Slip behind a rock** | Breaking a missile lock or a charged shot; resetting shields | Line-of-sight-dependent weapons, locks | An opponent who pre-positions or flanks; grenades lobbed around | Line-of-sight rules for locks, rock density | G (fiction plus SubSpace walls) |
| **Bait-and-hook (Thach weave)** in 2v1 | Two ships against one | A pursuer fixated on one target | A pursuer who disengages or watches the hook | Mutual support, sensor awareness | S (air combat), G (games) |

- The developer's first-guess list holds up broadly. Pursuit, head-on pass, drift-and-shoot and break are well supported. Extend-and-reset depends on boost asymmetry. Stand-off charged shot, grenade-drag and rock-slip are plausible but not documented as high-skill game tactics in the sources found. The evidence-backed additions are **strafe reversal/jink**, **matched-velocity brawl**, **lateral-offset joust** and **bait-and-hook**.
- Elite's "stay close behind under 700 m" works because Elite deliberately limits yaw/pitch, so close range raises the line-of-sight rate beyond the target's turn rate. In the developer's game, where identical ships yaw at the same rate, "behind" is worth only as much as the target's delay in flipping.

### Gaps
- No retrievable SubSpace/Continuum strategy guide (Trench Wars or SSCU wikis): bullet leading, rushing, running and wall bouncing are unverified here.
- No sources retrieved for Cosmic Rift, Armada, Altitude, Luftrausers, Star Control Melee, Reassembly, Cosmoteer, Kerbal or Children of a Dead Earth player tactics. Author recollection (unverified): Star Control and Luftrausers couple thrust to facing, so they are not decoupled models.
- No authoritative definition of Elite's "reverski" (Elite Wiki or a well-known PvP guide) was fetched. The Frontier BFM/CFM thread returned HTTP 403.
- The Starblast.io "Dueling" wiki (2D inertial shooter, directly relevant) returned HTTP 402.

## 2. Why circle-strafing and orbiting dominate, what decides orbit vs joust, the counters, and what designers changed

### Takeaway
Circle-strafing dominates when ships can keep their nose on target at all times — fast rotation, strong lateral thrust, strong braking — and weapons have flight time, so the faster-moving target is the harder one to hit. Hitscan, high fire rate and area weapons blunt it. With identical ships, the orbit carries no inherent turn-rate advantage, because the line-of-sight rotation rate is the same for both ships. An AI that loses to orbiting is almost certainly failing to track that rate, or failing to lead, rather than being out-performed. Designers have fought nose-on circling by slowing yaw (Elite), penalizing strafe and capping speed (SC Master Modes), and proposing speed-dependent turn rates with weaker lateral and retro thrust.

### Cited Findings
- Circle-strafing means moving around an opponent in a circle while facing them. It works best at close range, where the circler's apparent motion is much larger than a stationary enemy's. It hurts opponents with limited turning or slow-tracking weapons most, and its advantage "largely disappears" against hitscan or high-rate-of-fire weapons — [Wikipedia: Strafing (video games)](https://en.wikipedia.org/wiki/Strafing_(video_games))
- Root cause per the SC proposal: if two ships can both point at each other, they will. That comes from high rotation rates, side thrusters with roughly 16× too much thrust for their size (the Hornet's side thrusters are said to give 75% of main-engine thrust at about 25% of the size), and strong retro thrust that lets pilots "slam on brakes". The result is a guns-versus-shields slugfest. Gimbals, close-range projectile speeds and alpha-strike-favouring time-to-kill make it worse — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- Proposed fixes in the SC proposal: lower coupled-mode rotation rates as speed rises (example: 30°/s at 100 m/s, 15°/s at 200 m/s); size thrust to visible thrusters (manoeuvring thrusters at about 1/16 of main); weaker retro thrust; a decoupled "Direct Mode" where the pilot commands rotational force, not rate, so aim has momentum; realistic lateral G limits (about 3 g temporary, 1 g sustained lateral); a heat-limited afterburner. The author argues flight-model changes fail unless gunnery also gets harder (slower gimbal slew, larger damage per shot, no lead indicator beyond 100–200 m) — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- CIG's official Master Modes guide frames SCM mode as bringing ships closer in combat, letting light fighters evade heavy attacks with agility, and capping every ship's maximum speed "to ensure the optimal balance between forward thrust and maneuverability". NAV mode trades shields, weapons and countermeasures for speed. The guide never mentions circle-strafing, jousting or merges directly — [Master Modes Guide (CIG comm-link via star-citizen.wiki)](https://api.star-citizen.wiki/comm-links/20053)
- Community reports on Master Modes conflict. One view: the controlled speeds prevent overshooting and jousting. Another: strafing is crippled by forced slowdown, so jousting and hit-and-run dominate and interceptors are favoured — [Spectrum "continuing the mm trainwreck"](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/continuing-the-mm-trainwreck); [Spectrum "things to know about SCM and QCM"](https://robertsspaceindustries.com/spectrum/community/SC/forum/3/thread/things-to-know-about-scm-and-qcm-that-some-seem-to) (search snippets; pages not fetched; the two claims conflict)
- Elite players say yaw is intentionally slow on every ship to stop ships being mobile turrets, and that turning is best inside the blue-zone speed band — Elite Steam discussions ([1](https://steamcommunity.com/app/359320/discussions/0/1638668751270096628), [2](https://steamcommunity.com/app/359320/discussions/0/3182216552765314429)) (the yaw-intent claim is a search snippet; no Frontier developer statement found)
- Experienced SC pilots' counter to circling is constant aim, constant motion and unpredictable direction changes — [Star Citizen Base forum](https://forums.starcitizenbase.com/topic/22607-core-problem-with-6dof-what-do-you-think/)

### Inferences
- **Kinematics of the orbit (first principles; developer should verify against code).** Ship A orbits a near-stationary ship B at range r with tangential speed v, nose on B.
  - **The line-of-sight rate is symmetric.** The line A–B rotates at ω = v_rel,⊥ / r, and it is the same line for both ships. Any ship with fixed forward guns needs yaw ≥ ω to keep the target in its cone. With identical ships, neither has a yaw-rate edge. Example numbers, illustrative only: v = 10 u/s at r = 15 u gives ω ≈ 38°/s; at r = 8 u, ω ≈ 72°/s.
  - **A 5° cone is unforgiving to a lagging controller.** It allows ±2.5° of aim error. A proportional or PD aim controller chasing the *current* bearing of a target whose bearing moves at constant ω settles at a steady error of about ω divided by its gain. At ω ≈ 38°/s, staying inside 2.5° needs an effective lag under about 0.07 s. A human aims ahead by eye; an AI without line-of-sight-rate feedforward will sit outside its own cone for the whole orbit. **This is the most likely reason the orbit beats the current AI**, and it is a programmer-side fix (feedforward ω plus lead), not a new maneuver.
  - **Projectile inheritance decides who must lead.** If projectiles do *not* inherit the shooter's velocity, the orbiter firing at a stationary target needs no lead, while the target must lead by about asin(v / v_proj). That is a real asymmetry in the orbiter's favour. If projectiles *do* inherit velocity, both ships face the same lead problem. Check which applies to the lasers, charge lasers and autocannon. The railgun is hitscan, so it ignores lead entirely, and per Wikipedia hitscan blunts circle-strafing.
  - **What it costs to sustain the orbit.** Curving at radius r needs a centre-pointing acceleration v²/r. With the nose pointed inward, that is *forward* thrust. Linear drag k means holding tangential speed v needs continuous strafe thrust of about k·v. So the tightest orbit at speed v is r_min ≈ v² / a_forward. If strafe acceleration is close to forward acceleration, orbiting costs nothing; if strafe is much weaker, orbits must be slow or wide. **Strafe-to-forward thrust ratio is the main design knob** (the SC proposal's central claim).
- **What decides orbit vs joust:** (1) the ratio of yaw rate to line-of-sight rate at weapon range — high yaw favours orbiting; (2) the strafe-to-forward thrust ratio — high favours orbiting; (3) projectile speed and inheritance — slow, non-inherited projectiles favour the orbiter, hitscan neutralizes it; (4) cone width — wide cones favour whoever is being circled; (5) time to kill relative to the time the orbit takes to set up — short TTK favours alpha strikes and jousting; (6) drag — high linear drag makes drift and orbit speed cost continuous thrust, which favours shorter engagements.
- **Counters a same-ship pilot (or AI) can use**, ordered by how directly they answer an orbit:
  1. **Track properly.** Rotate at the line-of-sight rate plus lead. This turns the orbit into a symmetric shootout.
  2. **Match the orbit.** Strafe in the same rotational direction, which cuts relative transverse velocity and the line-of-sight rate. The fight becomes a DPS and shield race (SC's "death circle"). Good when ahead on shields or holding burst.
  3. **Reverse.** Strafe the opposite way or jink. The line-of-sight rate jumps and the opponent's lead flips sign; the better tracker benefits (the flat-scissors analog).
  4. **Break range outward.** Get beyond 20 u laser range. The orbiter must chase, its velocity now points at you, and it loses the orbit. That sets up the railgun, missiles, or grenades behind you.
  5. **Collapse range.** Boost inward to autocannon range. The line-of-sight rate spikes for both ships; it forces a pass.
  6. **Use terrain.** A rock inside the orbiter's circle blocks shots and forces it to re-path.
  7. **Use area or guided weapons.** Missiles and grenades don't care about tangential speed the way forward-fixed lasers do.
- **In a single plane, orbits are harder to escape than in 3D.** In 3D an orbited ship can break out of plane (SC spiral or corkscrew). In 2.5D it can only go radial or tangential, so orbit geometry matters more. The counters above (reverse, match, radial break, terrain) are the complete set.

### Gaps
- No Frontier or CIG developer post was found that explicitly names circle-strafing or nose-on circling as the problem being solved. The SC Master Modes guide is silent on it, and the Elite slow-yaw intent comes only from players.
- No quantitative study (telemetry, win rates) of orbit vs joust outcomes in any game was found.
- Whether SC's speed-dependent rotation (proposed in 2016 and said to be planned as of CitizenCon 2018) shipped in that form was not verified.

## 3. What transfers from real BFM and energy-maneuverability theory to decoupled planar flight

### Takeaway
The *geometry of velocity* transfers: pursuit curves, overshoot, one-circle vs two-circle turn flows, extension, mutual support, OODA. The *geometry of pointing* does not, because decoupled ships aim independently of where they are going. Altitude/energy trades (yo-yos, rolling scissors, lift-vector use) do not transfer to a gravity-free plane. Energy-maneuverability shrinks to "kinetic energy plus boost cooldown", and with a hard speed cap and linear drag, speed is cheap to regain and does not store an advantage the way altitude does. No rigorous written "space BFM" analysis was found.

### Cited Findings
- **Pursuit curves:** lead pursuit (nose ahead of the target; fast closure, overshoot risk; used for guns), pure pursuit (nose on the target; missile lock), lag pursuit (nose behind the target; holds energy and avoids overshoot) — [Wikipedia: Basic fighter maneuvers](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)
- **The merge** is a close, neutral pass. A **one-circle fight** (both turning the same way on one circle) is won by the smaller turn radius. A **two-circle fight** (turning opposite ways) is won by the higher turn rate — [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)
- **Overshoot:** a wingline overshoot (the attacker passes the defender's 3–9 line) reverses the roles. The defender's **break turn** across the attacker's path increases angle-off and creates high crossing speeds that are hard to shoot. **Flat scissors** are repeated turn reversals in the horizontal plane to deny a guns solution, favouring the more manoeuvrable aircraft. **Rolling scissors** are vertical. **Extension** is unloading to accelerate away, best after an overshoot but not recommended against a higher-energy opponent — [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)
- **High and low yo-yo** trade airspeed for altitude, out of plane, to fix overshoot or cut a corner. They depend on a vertical dimension and gravity — [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)
- **Energy-maneuverability:** specific excess power governs sustained manoeuvring. Corner speed is the minimum speed that reaches the maximum sustained g-load. Energy fighters avoid turning and use climbs and dives; angles fighters exploit break turns. The **control point** sits about one turn radius behind the defender — [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)
- In space there is no lift or gravity, so manoeuvres built around keeping lift are largely redundant — [SFF Chronicles / Gizmodo search results on space-battle physics](https://gizmodo.com/5426453/the-physics-of-space-battles) (search snippet; page not fetched)
- Newtonian physics brings about half a dozen momentum effects that make combat more complex than players intuitively expect. That complexity is one reason arcade flight models dominated through the 2000s — [Elite Wiki (Oolite): Newtonian modelling](https://wiki.alioth.net/index.php/Newtonian_modelling) (search snippet; fetch failed)

### Inferences
- **Transfers (with translation):**
  - **Pursuit curves apply to the velocity vector, not the nose.** A decoupled pilot can fly *lag pursuit with the velocity* (no overshoot, holding the control point) while holding *lead with the nose* (guns on). That is impossible in an aircraft and is the main new capability.
  - **Overshoot** still exists, because velocity can't be cancelled instantly. But the overshooter can flip and keep shooting, so an overshoot is far less punishing than in an aircraft. A defender who forces an overshoot gains a *crossing-shot window* (high transverse velocity for the passing ship), not a role reversal.
  - **Break.** The planar analog is a sudden strafe plus boost perpendicular to the attacker's line of sight. It spikes the transverse velocity and invalidates the attacker's lead. The boost cooldown makes it a committed, readable resource.
  - **Flat scissors maps onto strafe reversals.** Repeated tangential reversals keep the opponent's lead wrong; the side with better lateral acceleration and tracking wins.
  - **One-circle vs two-circle flows map onto co-rotating vs counter-rotating orbits.** Co-rotating lowers relative transverse velocity (an easy shooting brawl). Counter-rotating raises it (a tracking contest).
  - **Extension** works only if the extender has more acceleration available (boost off cooldown versus on), since top speed is capped and equal.
  - **OODA / initiative:** whoever reads the opponent's commitment (charge glow, boost spent, heat near cap) and moves first wins exchanges. This transfers fully and is the natural basis for AI "readiness" logic.
- **Does not transfer:**
  - Yo-yos, rolling scissors and lift-vector roll-to-pitch (Elite's habit of rolling to put the target on the faster pitch axis) are 3D-only.
  - Altitude as stored energy does not exist.
  - Corner speed only matters if the developer adds a speed-dependent turn rate.
  - "Turn-radius vs turn-rate" ship differences don't exist between identical ships.
  - Six-o'clock "control zone" dominance is much weaker, because the defender can flip.
- **E-M in this model:** with linear drag and a speed cap, "energy" is mostly the boost charge plus current speed. Since drag bleeds speed without thrust, drift-and-shoot is short-lived (time constant about 1/k). The boost cooldown is the true scarce energy.

### Gaps
- Robert Shaw's *Fighter Combat: Tactics and Maneuvering* was not retrievable in summary form beyond the Wikipedia BFM article, which draws heavily on it.
- No written "space BFM" doctrine from a game community (for example an Elite PvP wing guide, or an SC org flight-school document) was retrieved.

## 4. Tactics tied to weapon types (charge, hitscan, short-range burst, guided missiles, rear-dropped mines)

### Takeaway
Documented evidence is sparse and mostly comes from the circle-strafing literature. Hitscan and high rate of fire negate circle-strafing. Alpha-strike, high-burst weapons push fights toward jousting. Easy missile countermeasures make missiles irrelevant to manoeuvre. Shared resource pools for heat, energy or flux (SubSpace energy, Starsector flux) make firing a risk decision. No good sources were found on charge-weapon timing or rear-dropped mine tactics in games.

### Cited Findings
- Hitscan or high-rate-of-fire weapons remove most of circle-strafing's evasive benefit. Scoped or sighted weapons that slow the user are especially vulnerable to circle-strafers — [Wikipedia: Strafing (video games)](https://en.wikipedia.org/wiki/Strafing_(video_games))
- In SC, time to kill rewards alpha strikes (all damage at once) over sustained fire. Gimbals give continuous aim assistance. Missiles are easily defeated with flares and chaff without any evasive flying, so long-range missile combat is suppressed — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- In the SC proposal, jousting is decided by who has the biggest or most guns — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal); echoed in Master Modes reports — [Spectrum](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/continuing-the-mm-trainwreck) (search snippet)
- SubSpace's single energy pool (health and ammo) means reckless firing risks fast defeat — [Wikipedia: SubSpace](https://en.wikipedia.org/wiki/SubSpace_(video_game))
- Starsector's flux couples weapon fire to shield capacity. At high flux, players take hits on armour and vent between engagements — [Starsector wiki: Piloting](https://starsector.wiki.gg/wiki/Piloting)
- Elite players match damage type to layer (lasers for shields, ballistics for hull) and target subsystems such as power plants — [Elite Steam discussion](https://steamcommunity.com/app/359320/discussions/0/3182216552765314429)
- In BFM, pure pursuit is used for missile lock with caged seekers, and lead pursuit for guns — [Wikipedia: BFM](https://en.wikipedia.org/wiki/Basic_fighter_maneuvers)

### Inferences (grade G unless noted)
- **Charged railgun (2° cone, long range, hitscan).** The natural counter to an orbit at range, since hitscan ignores lead (supported by the Wikipedia strafing claim). The charge is a telegraph. A skilled target jinks or breaks line of sight when it sees the charge, and a skilled shooter charges *before* the target enters the cone. Readable as: "charge while predicting where the line of sight will be when charge completes, release when the cone aligns." A 2° cone at long range requires a low line-of-sight rate, so the railgun works best against targets moving radially (toward or away), extending targets, or targets on predictable paths, and worst against close orbiters.
- **Autocannon (short range, burst).** The joust and pass weapon, matching the developer's preference. It also works as a counter-orbit tool after collapsing range. Its effectiveness depends on passing time inside range: higher closure means fewer rounds.
- **Lasers with heat.** Heat caps sustained orbit fire. An orbiter running hot must disengage, which creates an exploitable window (the SubSpace/Starsector "resource pressure" pattern).
- **Lock-on missiles.** In the developer's game their value depends on the evasion model. If they are dodged only by maneuver and line-of-sight breaks (not free countermeasures), they create the "make the target turn" pressure the SC proposal says is missing. Planar evasion options: turn perpendicular to the missile's approach ("beam" it) to maximise its turn demand, time a boost jink late, or put a rock between you. Out-of-plane missile defeats are 3D-only.
- **Rear-dropped grenades.** They punish pure-pursuit followers on your exact track. They can also be laid as a field during an orbit or as a choke behind a rock. The competent counter is offset or lag pursuit, which an AI should do by default.
- **Charge lasers.** They combine telegraph and burst; best used at a merge, timed to finish charging at minimum range.

### Gaps
- No game-specific sources on charge-weapon timing (charge-before-cone, charge feints) in any space game.
- No sources on rear-dropped mine or grenade tactics in space shooters (SubSpace mines, Star Control mines, etc.).
- No sources on maneuver-based missile evasion in planar games.

## 5. Use of terrain: asteroids and obstacles

### Takeaway
Almost nothing citable was found on documented high-skill terrain play in space games. SubSpace confirms walls and asteroids as inelastic, damage-free colliders. The rest is fiction (Obi-Wan vs Jango) and inference.

### Cited Findings
- In SubSpace, ships collide inelastically with walls and asteroids but take no damage. Maps have gates that open and close — [Wikipedia: SubSpace](https://en.wikipedia.org/wiki/SubSpace_(video_game))
- One commenter on Newtonian space games notes that space battles are mostly "parked" affairs, which may suit deep space but not asteroid fields or areas near planets — [Orbital Dogfight devlog and related search results](https://mogacreative.itch.io/orbital-dogfight/devlog/85299/orbital-dogfight-log-00091) (search snippet; page not fetched; attribution uncertain)

### Inferences (grade G)
- Rocks are the planar substitute for altitude and the third dimension: they create the asymmetric positions that identical ships otherwise lack.
- Concrete uses:
  - Break the line of sight for a charging railgun or missile lock.
  - Peek-shoot: hold behind a rock, strafe out, fire, strafe back. This exploits the opponent's need to keep its cone pointed at a corner.
  - Make orbits collide: an orbiter's circle around you intersects rocks, so standing near a rock is a direct counter to circling.
  - Drag a pursuer through a gap so the follower must slow or collide. This depends on the developer's collision damage, which SubSpace sets to none.
  - Hide and ambush.
- AI requirement: line-of-sight checks for firing and for "slip behind rock" goals, plus a "near cover" utility term.

### Gaps
- No documented terrain-play guides (asteroid field PvP in Elite, SC, SubSpace or Starblast) were found.
- No design posts on obstacle density versus dogfight quality were found.

## 6. Fights against multiple opponents and wingman tactics

### Takeaway
The best-documented cooperative pattern is the air-combat Thach weave (bait-and-hook), which beat a more manoeuvrable opponent through geometry alone. Starsector players use anvil-and-hammer (one ship tanks the front, another flanks). Game-specific wing doctrine for decoupled fighters was not found.

### Cited Findings
- **Thach weave:** two or more planes weave on regularly intersecting paths. An attacker who focuses on one plane (the bait) is drawn across the wingman's guns (the hook). The attacker must choose between exposing itself and breaking off. In testing, Thach's fighters, despite a power handicap, either ruined the attack or got into a firing position every time. It grew out of the two-plane element of the finger-four formation — [Wikipedia: Thach Weave](https://en.wikipedia.org/wiki/Thach_Weave)
- **Starsector anvil and hammer:** a sturdy "anvil" absorbs frontal fire while a "hammer" hits from the side or rear. Players use commands and positioning so allies tank while they flank — [Starsector community summaries](https://youtubesummary.com/summary/fAbkYAES43M) (search snippet; page not fetched)
- Carriers in Starsector can toggle fighters between engaging and regrouping — [Starsector wiki: Piloting](https://starsector.wiki.gg/wiki/Piloting)

### Inferences (grade G unless noted)
- **Why groups beat a lone orbiter in the developer's game.** An orbiter's escape options are radial or tangential, but against two shooters at different bearings no single tangential direction lowers both lines of sight. A 2-ship **pincer or crossfire** (positions about 90–180° apart around the target) defeats circle-strafing structurally.
- **Alternating passes:** ship 1 jousts while ship 2 holds back with the railgun charged, firing as the target turns to track ship 1. That makes the target's tracking commitment the trigger.
- **Drag-and-bag:** a "bait" ship extends, drawing the pursuer, while a second ship waits off-axis or behind a rock. This is the Thach weave plus terrain.
- Coordination token (sector bus): "I am bait / you are hook" roles fit the repo's "signals cause" wiring rule (AGENTS.md).

### Gaps
- No sources found for "drag and bag", "bracket" or "sandwich" in game contexts or public air-combat primers (only the Thach weave was fetched).
- No game-community wing doctrine for Elite, SC or SubSpace was found.

## 7. What makes a decoupled-flight duel deep rather than degenerate

### Takeaway
The clearest analysis found (the SC proposal) argues a duel degenerates when one geometry — nose-on circling — is always available and always best. Depth comes from making facing cost something (rotation limits, weaker lateral thrust, aim momentum), making gunnery harder for the stationary or turreting style, and offering distinct archetypes that beat each other. Designers' real changes (Elite slow yaw; SC Master Modes strafe penalty and speed caps) each moved the meta rather than adding variety, and the community reports a new dominant pattern (jousting and interceptors) afterwards.

### Cited Findings
- The SC proposal argues for four styles that counter each other — strafe fighter, turn fighter, boom-and-zoomer and jack-of-all-trades — made possible by different strafe-to-main thrust ratios and speed-dependent rotation. It explicitly aims to add combat variety, not replace one style with another — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- The same proposal says flight-model changes fail unless gunnery changes too: easy aiming keeps strafing easier than turn fighting — [SC Flight Model Proposal](https://sites.google.com/view/starcitizenflightmodelproposal)
- Circle-strafing, turreting and FPS-style gameplay are described as the obvious result of SC's flight model in a min-maxing competitive environment — [SC flight model debate, search results](https://sites.google.com/view/starcitizenflightmodelproposal) (search snippet; attribution likely the proposal)
- Master Modes (3.23) split SCM and NAV and capped SCM speed for balance. Community reports say it suppressed strafing and promoted jousting and interceptors — [Master Modes Guide](https://api.star-citizen.wiki/comm-links/20053); [Massively OP coverage](https://massivelyop.com/?p=521491); [Spectrum](https://robertsspaceindustries.com/spectrum/community/SC/forum/4/thread/continuing-the-mm-trainwreck) (latter two are search snippets)
- Elite players say slow yaw stops turret-style play — [Elite Steam discussions](https://steamcommunity.com/app/359320/discussions/0/1638668751270096628) (search snippet)
- Absolute Territory pairs a Newtonian model with a flight-assist computer and a "Slide" mode that keeps velocity while turning, an example of the assist/decoupled toggle used to make Newtonian flight approachable — [vgdb: Absolute Territory](https://www.vgdb.co/games/absolute-territory) (search snippet)
- Hitscan neutralizes circle-strafing — [Wikipedia: Strafing (video games)](https://en.wikipedia.org/wiki/Strafing_(video_games))

### Inferences
- **Rock-paper-scissors available in the developer's model** (grade G, built from the kinematics in §2):
  - Orbit beats turret-and-joust if the target's aim lags.
  - Matched orbit plus burst beats orbit.
  - Radial break plus railgun beats orbit at range.
  - Collapse plus autocannon beats the railgun stand-off.
  - Jink or line-of-sight break beats the railgun charge.
  - Grenades beat close pursuit.
  - Offset pursuit beats grenades.
  - Rock-hugging beats orbit.
  - Pincer beats any lone counter.
- **Levers if orbiting stays dominant even after the AI tracks properly**, ordered from least to most invasive:
  1. AI parity: aim feedforward and lead.
  2. Strafe acceleration below forward acceleration (the SC proposal's central lever).
  3. A yaw-rate cap tuned so that ω_max is below the line-of-sight rate of a full-speed orbit at autocannon range but above it at laser range.
  4. Projectiles inherit shooter velocity, removing the orbiter's free no-lead advantage.
  5. Speed-dependent yaw (the SC proposal; Elite's blue zone).
  6. Heat or boost costs on sustained strafing.
- **Depth needs readable commitments:** charge glow, boost-cooldown state and heat level let both humans and AI do OODA-style reads. Weapons with no commitment and no telegraph (gimbals, hitscan with no charge) flatten the decision space (the SC proposal's gimbal critique).

### Gaps
- No GDC talk or Game Developer article on Newtonian dogfight dominant strategies was found in this pass.
- No designer post-mortem with data on how a flight-model change shifted the meta was found.

## 8. Fictional references: BSG Viper and Star Wars choreography

### Takeaway
Star Wars dogfights were choreographed from WWII and Korean War air-combat footage, so they depict *aircraft* BFM (coupled facing and velocity, banking turns, tail chases). Little of that is physically meaningful in decoupled flight except tail-chase pressure and terrain hiding. BSG's Viper flip-and-shoot-backwards is the iconic decoupled move, but no published technical breakdown of its choreography was found beyond Gary Hutzel's VFX leadership and the use of detailed animatics.

### Cited Findings
- George Lucas studied over 25 hours of WWII dogfight footage and newsreels and cut them into the film as placeholder animatics. Producer Gary Kurtz listed *The Dam Busters*, *Tora! Tora! Tora!*, *The Battle of Britain*, *Jet Pilot*, *The Bridges at Toko-Ri*, *633 Squadron* and about forty-five other films as sources — [Smithsonian Air & Space](https://www.smithsonianmag.com/air-space-magazine/air-battles-became-star-wars-1-180975832/); [Military Times](https://www.militarytimes.com/off-duty/military-culture/2024/05/04/may-the-4th-be-with-you-how-world-war-ii-influenced-star-wars); [/Film](https://www.slashfilm.com/557980/dam-busters-influenced-star-wars/)
- BSG's VFX supervisor Gary Hutzel worked with Zoic Studios on detailed animatics to design sequences, and called one battle over eight minutes long the longest continuous battle sequence done for television — [Below the Line / mande.net on BSG crafts](https://mande.net/btl/crafts/battlestar-gallactica); [Below the Line](https://www.btlnews.com/?p=1844) (search snippets; pages not fetched)

### Inferences (grade G; fiction described from the works themselves, not sourced breakdowns)
- **BSG Starbuck flip** (rotate 180° while the velocity carries on, firing at pursuers): this is exactly "flip-and-fire while drifting", which Elite players do with FA off. In the developer's model it works only while drag hasn't bled the speed, and it ends with the ship closing on the pursuer. That is good for an autocannon merge, bad if outnumbered.
- **Revenge of the Sith opening** (dense capital-ship battlefield, buzz droids, tail chases through structure): mostly aircraft-style pursuit. The transferable idea is pursuit through obstacles, where the follower must path-match.
- **Obi-Wan vs Jango (Attack of the Clones):** seismic charges are a rear-dropped, delayed area weapon against a close pursuer (the developer's grenades). Obi-Wan clinging to an asteroid to drop off the sensors is "slip behind a rock" plus breaking the lock. Both are physically meaningful in a decoupled planar model.
- **Physically meaningless in decoupled planar flight:** banking to turn, the "can't shake him" tail chase where the pursued ship cannot simply turn and fire, and wingover reversals.

### Gaps
- No published choreography breakdown (VFX interview, Zoic or ILM making-of) explaining specific Viper maneuvers or Ron Moore's "naturalistic" space-combat rules was retrieved.
- No breakdown of the Revenge of the Sith opening battle or the Attack of the Clones asteroid chase choreography was retrieved.
