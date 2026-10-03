# AI-Augmented Art Direction for Solo Devs / Small Studios — Process, Roles, Orchestration (as of Sept 2026)

Scope note: ~22 search/fetch calls. Several PC Gamer and Notebookcheck pages failed to fetch (paywall/403), so some claims rest on search-result snippets and are flagged. Much of the "how-to" web content on Nano Banana / Blender MCP / ComfyUI is vendor or SEO-blog material, not practitioner postmortems — flagged as marketing where relevant.

## Case studies (2024–2026): who shipped with substantial AI art, their pipeline, and reception; backlash triggers

### Takeaway
There is no well-documented, well-received indie *success* story built on player-visible AI art in 2024–2026; the documented record is dominated by backlash cases, and the common trigger is *visible, inconsistent, or undisclosed* AI output reaching players (placeholders left in, cutscenes that don't match the hand-made game, "plasticky" generic looks). Quantitative Steam data (2025) shows a measurable "AI stigma" penalty on reviews/sales even after controlling for comparable games.

### Cited Findings
**Backlash cases**
- *Clair Obscur: Expedition 33* (Sandfall, Dec 2025): Indie Game Awards rescinded its Game of the Year and Debut Game wins because it shipped with generative-AI texture assets (newspaper-clipping textures) that were patched out days after launch; the dev said they were placeholders that slipped through QA; IGA rules state "Games developed using generative AI are strictly ineligible for nomination." — [Yahoo/Tech](https://tech.yahoo.com/gaming/articles/indie-game-awards-strips-clair-111710724.html); [Comics Beat](https://www.comicsbeat.com/indie-game-awards-rescind-clair-obscur-expedition-33s-wins-for-gen-ai-use/)
- *Larian / Divinity* (Dec 2025 → Jan 2026): After a Bloomberg interview where Swen Vincke said Larian was "pushing hard" on genAI (exploring ideas, PowerPoints, concept art, placeholder text), fan backlash followed; in a Jan 2026 Reddit AMA Vincke said "there is not going to be any GenAI art in Divinity" and that "we've decided to refrain from using genAI tools during concept art development"; writing director Adam Smith said no genAI text either. Larian still says genAI "can help" speed of trying things out. — [Techdirt](https://www.techdirt.com/2025/12/22/larian-studios-the-latest-to-face-backlash-over-use-of-ai-to-make-games/); [GameSpot](https://www.gamespot.com/articles/larian-studios-draws-harder-line-on-ai-clarifies-stance-in-reddit-ama/1100-6537296/); [VGC](https://www.videogameschronicle.com/news/larian-backtracks-on-ai-use-for-divinity-concept-art-and-writing-but-says-generative-ai-can-help-some-areas-of-development/); [Forbes](https://www.forbes.com/sites/paultassi/2026/01/09/larian-says-it-wont-use-genai-art-or-writing-in-divinity-development/)
  - Significance for a solo dev: even *non-shipping concept art* use drew backlash for a beloved studio once publicized — the concern is reputational, not just what ships.
- *Vapor World: Over The Mind*: AI-generated cutscenes compared unfavorably to the game's own hand-crafted visuals, with character models inconsistent shot-to-shot; negative Steam/Xbox reviews followed; a hotfix removed every AI cutscene and restored the original hand-made ones; the director called it a "hasty judgment" and pledged no further AI cutscenes. — [TrueAchievements](https://www.trueachievements.com/news/vapor-world-over-the-mind-ai-cutscenes); [DualShockers](https://www.dualshockers.com/vapor-world-director-responds-launch-ai-cutscenes/); [ixbt.games, dated 2026-08-22](https://ixbt.games/en/news/2026/08/22/igroki-raskritikovali-vapor-world-za-neiroslop-geimdirektor-zaiavil-ob-otkaze-ot-ii-i-udalenii-katscen-posle-volny-kritiki.html)
  - Date conflict: a SpriteCook blog says the game had 100,000 wishlists before an "August 2025" early-access launch ([SpriteCook](https://www.spritecook.ai/blog/is-ai-art-ok-in-indie-games)); ixbt/Aroged date the cutscene removal to Aug 2026 alongside a Game Pass launch ([Aroged](https://www.aroged.com/2026/08/22/the-creators-of-vapor-world-over-the-mind-quickly-responded-to-criticism-and-made-changes/)). Possibly EA 2025 → full/Game Pass launch 2026; unverified.
- *Project Zomboid*: community backlash when fans suspected some menu/loading-screen art in an update was AI-made due to warped-object anomalies (per SpriteCook summary; not independently verified). — [SpriteCook](https://www.spritecook.ai/blog/is-ai-art-ok-in-indie-games)
- SpriteCook's summary: most backlash targets "generic, obviously-AI output with inconsistent characters and a plasticky look" (vendor blog opinion, an AI sprite tool — biased source). — [SpriteCook](https://www.spritecook.ai/blog/is-ai-art-ok-in-indie-games)

**"AI-made" as the pitch**
- *CODEX MORTIS*: marketed as "the world's first fully playable game created 100% through AI"; ChatGPT used heavily for artwork, Claude Code for custom shaders/animations; free demo Dec 2025 "ignited a firestorm"; demo reviews ~65% positive of 154; Early Access launch announced for March 26 (2026); Asmongold streamed it and called it "pretty good." Positive-reception claims come from the studio's own press releases (marketing). — [PC Gamer headline](https://www.pcgamer.com/games/roguelike/this-roguelite-claims-to-have-the-dubious-honor-of-being-the-worlds-first-fully-playable-game-created-100-percent-through-ai-in-a-milestone-for-slop-everywhere/); [Interesting Engineering](https://interestingengineering.com/ai-robotics/worlds-first-game-completely-made-with-ai); [Games Press release](https://www.gamespress.com/en-GB/CODEX-MORTIS-Announces-March-26-Early-Access-Launch---The-AI-Made-Game); [Steam page](https://store.steampowered.com/app/4084120/CODEX_MORTIS/)

**Practitioner pipeline case (older, 2023–2024)**
- *Echoes of Somewhere* (Jussi-Petteri Kemppainen, solo/volunteer veterans; blog from 2023): cyberpunk 2.5D point-and-click. Pipeline: Midjourney for backgrounds and character concepts → characters remodeled in 3D so mocap could drive animation; Zibra AI for fluid sim. — [Echoes blog: AI Character Design (2023)](https://echoesofsomewhere.com/2023/01/04/ai-character-design/); [PreMortem Games (2023)](https://premortem.games/2023/08/30/jussi-kemppainen-is-exploring-ai-assisted-game-development-with-echoes-of-somewhere/)
  - Lessons: the tools were "very bad at taking direction"; he had to "rewrite [his] story to match the AI-generated content" and used minimal art direction ("cyberpunk adventure game HDR masterpiece"-style prompts); kept story/puzzles human. Artists raised ethics concerns; he released it as freeware partly to address compensation concerns. — [Adventure Game Hotspot](https://adventuregamehotspot.com/feature/519/echoes-of-somewhere-how-a-solo-developers-game-takes-center-stage-in-the-ai-cont)
  - Note: this predates 2025 reference-image-capable models (Nano Banana Pro, MJ V7 sref/moodboards), so the "can't take direction" finding is likely partially outdated.

**Market data**
- Game Oracle analyst Ross Burton: ~10,000 paid Steam games released Jan–Oct 2025; games disclosing AI got ~53% fewer first-month reviews than comparable non-AI games (matched on timing, dev experience, backing); among titles with ≥100 reviews, avg score 84.6% (AI) vs 88.3% (non-AI); larger studios could see ~40–60% sales decline. — [PC Gamer](https://www.pcgamer.com/software/ai/data-analyst-finds-ai-stigma-on-steam-can-reduce-the-number-of-reviews-a-game-gets-by-around-53-percent-and-the-reviews-it-does-get-are-more-negative/); [Windows Central](https://www.windowscentral.com/gaming/pc-gaming/ai-game-development-stigma-study); [EGW](https://egw.news/gaming/news/35663/games-marked-created-with-ai-receive-less-attentio-EXJXWjTo7)
- Share of Steam games disclosing AI: ~1 in 5 new releases (2025–2026) — [tech-insider](https://tech-insider.org/steam-ai-disclosure-2026/); [Pikorafy](https://pikorafy.com/blog/steam-ai-games-disclosure-surge-2026); vs "roughly one in three new games" from a Sept-2026 Substack census of 53,597 store pages — [Production Alchemist](https://www.productionalchemist.com/p/steam-ai-disclosure-rules-2026-what-indie-devs-need-to-know). Sources conflict; methodology differs.
- "The overwhelming majority of AI-flagged games generate essentially no money and collect few or no user reviews" (volume is noise) — aggregated via search snippet from [SteamData Research (Medium)](https://medium.com/@research_86150/steam-in-2025-the-year-indie-games-ate-the-industry-alive-666abf04551f) — secondary/low-authority source.

### Inferences
- The consistent failure pattern is not "used AI" per se but (a) AI output that is *visibly off-style relative to the rest of the game* (Vapor World), (b) *undisclosed/placeholder leakage* (Clair Obscur), (c) *public statements* about AI in creative roles (Larian). A solo director's safest profile: AI in exploration/reference, human-authored or heavily reworked shipping assets, placeholder tracking that guarantees AI placeholders can't ship.
- For a 3D space-combat game, the path most analogous to Echoes of Somewhere (AI 2D concept → human/assisted 3D model in Blender) keeps AI out of the shipped pixels, which matters for both Steam disclosure scope and copyright (see Legal section).

### Gaps
- Found no well-sourced postmortem (Game Developer, GDC talk) from an indie that shipped *substantial player-visible AI art* and was commercially and critically successful in 2025–2026. Searches returned mostly backlash stories and vendor blogs. This absence is itself a signal.
- Could not fetch PC Gamer's "Steam Week in Review" article naming the "promising new indie" hit by backlash.
- No verified detail on Codex Mortis EA sales/reviews post-launch.

## Art-direction scaffolding for non-artists, and encoding a style so AI follows it

### Takeaway
The standard scaffolding is an art bible built from visual pillars + shape-language rules + a *rule-based* (not just hex) palette + reference touchstones; in 2025–2026 the practical way to make image models obey it is a fixed **reference-image pack** (Nano Banana Pro takes up to 14 reference images; Midjourney uses sref codes / moodboards) rather than ever-longer text prompts, with LoRAs as the heavier self-hosted option.

### Cited Findings
- Visual pillar = "a short, opinionated statement of what the game should feel like visually, usually anchored to two or three reference touchpoints"; pillars exist to settle arguments quickly. — [Nasty Rodent: Game Art Bible](https://nastyrodent.com/game-art-bible/)
- Shape language: round reads friendly, angular aggressive, square stable; a production art bible states which shape families belong to which faction and how far a silhouette can be pushed before it reads as a different design language. — [Nasty Rodent](https://nastyrodent.com/game-art-bible/)
- Palette should be rules, not a dump: which colors carry narrative weight, how material families behave under target lighting, where saturation may spike; recommended primary palette of 5–7 colors with defined roles, semantic usage, colorblind safety. — [Nasty Rodent](https://nastyrodent.com/game-art-bible/)
- Art bible's purpose: keep art consistent across contributors so it doesn't drift from original intent. — [GameDev.net forum thread](https://gamedev.net/forums/topic/644435-what-is-an-art-bible-and-any-tips-to-make-one-better/) (older, pre-2020)
- Free templates exist: [DVNC Art Bible Template (itch.io)](https://dvnc.itch.io/art-bible-template); someone has also published an "art-bible" agent skill file — [skills.lc art-bible SKILL.md](https://skills.lc/kjuhwa/skills-hub/kjuhwa-skills-hub-skills-design-art-bible-skill-md) (unvetted).
- **Nano Banana Pro**: up to 14 reference images; guidance is to upload a "brand bible" (palette swatches, style examples, turnarounds) as few-shot context; "consistency is not achieved by longer prompts—it's achieved by how you use reference images." — [MetaFluxTech (Medium)](https://metafluxtech.medium.com/10-pro-nano-banana-tips-for-high-quality-asset-production-93c25b7385b2); [selfielab reference-sheet tutorial](https://selfielabstudio.com/blog/nano-banana-pro-reference-sheet-tutorials-for-consistent-characters-20260226). Claims like "93% character consistency per ZDNET" and "95% with 8–14 angle sheets" come from SEO blogs — treat as marketing, unverified. — [techyheaven](https://techyheaven.com/nano-banana-pro-character-consistency/)
- For game assets with Nano Banana Pro: define angles, lighting, palette, edge rules; batch similar objects; audit alignment; iterate with micro-edits (vendor blog). — [Sider.ai isometric guide](https://sider.ai/blog/ai-image/nano-banana-pro-isometric-game-asset-generation-guide)
- **Midjourney**: V7 introduced new sref and moodboard algorithms with higher precision than V6 and improved personalization profiles; V7 is compatible with V6.1 sref codes. — [Midjourney update: V7 default](https://updates.midjourney.com/v7-is-now-the-default-model/). Moodboards = user-selected image sets forming a custom style, broader than a single sref. — [Midjourney docs: Moodboards](https://docs.midjourney.com/hc/en-us/articles/39193335040013-Moodboards); [Personalization](https://docs.midjourney.com/hc/en-us/articles/32433330574221-Personalization). A V8/V8.1 exists by 2026 per third-party posts — [PixMind](https://www.pixmind.io/posts/midjourney-v8-1-moodboards-style-reference).
- Consistency practice: vary the content text per image, keep the moodboard/master style references fixed. — [Chase Jarvis](https://chasejarvis.com/blog/how-to-control-midjourney-style-references-image-references-and-moodboards/)

### Inferences
- A workable encoding stack for this project: (1) a written art bible (pillars, shape rules per faction/ship class, palette-with-roles, value/lighting rules, a "never" list); (2) a versioned **canonical reference pack** (≤14 images: palette card, 2–3 hero touchstones, silhouette sheet, a material/lighting sample, and — crucially — renders of *already-approved in-engine assets* so the pack evolves toward the actual game); (3) a stable prompt preamble generated from the bible by the LLM. Feeding approved in-engine renders back in is the direct counter to the Vapor World "doesn't match the game" failure.
- For a space-combat game, silhouette readability at distance and faction color-coding are gameplay-functional, so shape/palette rules double as design rules — worth putting in the bible as testable constraints (e.g., "readable as a silhouette at 64px").

### Gaps
- No rigorous comparison found of reference-pack vs LoRA vs sref for 3D-game concept consistency; evidence is anecdotal/vendor.
- Did not find Google's own official Nano Banana Pro reference-image documentation in this pass (only third-party); another researcher may cover tool specifics.

## Decision workflow: divergent→convergent loops, contact sheets, critique rubrics, avoiding slot-machine generation

### Takeaway
Practitioner guidance converges on "use AI outputs as inspiration, not product": broad exploration, human curation, then paintover/kitbash/3D block-out as the convergent step; but I found little rigorous, sourced material on decision-fatigue controls or critique rubrics specific to AI art — this is mostly a gap to fill with process design.

### Cited Findings
- Use AI outputs as inspiration, paint over, add your own flourishes; "sheer quantity doesn't equal quality. Human curation is essential." — [Wayline: Indie Dev's Guide to AI Art](https://www.wayline.io/blog/indie-dev-ai-art-guide)
- Blender used for kitbash, quick 3D block-outs, and lighting setups that then get painted over; composite best AI parts in Blender using the concept as lighting/texturing guide. — [Wayline](https://www.wayline.io/blog/indie-dev-ai-art-guide); [Tripo3D blog (vendor)](https://www.tripo3d.ai/blog/explore/using-concept-art-to-drive-ai-3d-outputs)
- AI often produces "visually striking images that are impossible to build in 3D" (unsupported structures, nonsensical proportions). — [MindStudio](https://www.mindstudio.ai/blog/ai-image-video-gaming-concept-art-trailers) (vendor blog, via search snippet)
- Indie benefit is "reducing the time needed to explore ideas and assemble a usable first-pass asset library, while leaving room for engine-side testing and manual polish" — not eliminating steps. — [Tripo3D indie pipeline (vendor)](https://www.tripo3d.ai/blog/explore/ai-3d-model-generator-for-indie-game-teams-pipeline)
- Echoes of Somewhere's inverse failure: with minimal direction, the dev ended up adapting his story *to* the outputs — the model steered the director. — [Adventure Game Hotspot](https://adventuregamehotspot.com/feature/519/echoes-of-somewhere-how-a-solo-developers-game-takes-center-stage-in-the-ai-cont)

### Inferences (process design, not sourced findings)
- Anti-slot-machine controls a solo director can adopt: fix a **generation budget per decision** (e.g., one contact sheet of 12–16, max two rounds) before any pick; require every generation to answer a written question ("which of three silhouette families reads as 'heavy interceptor'?") rather than "make it cool"; pick by elimination against the bible's pillars, not by favorite.
- Divergent→convergent ladder: thumbnails/silhouettes (grayscale, value only) → pick 2–3 → color/material variants of the pick → 3D block-out in Blender → in-engine render under game lighting/camera → only then detail. Judging in-engine at gameplay distance catches "impossible to build" and "off-style vs the game" before sunk cost.
- Going deep without losing global consistency: every deep-dive asset's final render gets added to the canonical reference pack and re-checked side-by-side with the rest of the approved set (a "lineup" contact sheet of all ships at the same scale/lighting). The user's existing contact-sheet preference fits this directly.
- A critique rubric the LLM can apply consistently: silhouette readability, pillar adherence (per pillar, pass/fail + note), palette-rule compliance, buildability in 3D, consistency with approved lineup, and "AI tells" (warped details, mushy greebles, text artifacts). Human makes the final call; LLM pre-filters and explains.

### Gaps
- No sourced GDC talk or postmortem found with an explicit decision-fatigue protocol or quantitative critique rubric for AI art direction.
- No sourced evidence on how many variants per round is optimal.

## LLM as creative director/orchestrator: prompts, vision critique, style-guide upkeep, driving image APIs and Blender/Unity via MCP

### Takeaway
The tooling exists and is maturing fast (Blender MCP, official Comfy MCP, many ComfyUI MCP servers, some with explicit build-run-look-critique loops), but practitioner reports consistently frame the agent as a **fast junior technical artist**, good at repetitive, measurable, inspectable work and weak at precise spatial judgment and production-final quality — so the human stays the director at explicit checkpoints.

### Cited Findings
- GDC 2026 State of the Industry (2,300+ respondents): 36% personally use genAI; top uses research/brainstorming 81%, daily tasks 47%, code 47%, prototyping 35%; tools: ChatGPT 74%, Gemini 37%, Copilot 22%. — [GDC 2026 SOTI article](https://gdconf.com/article/gdc-2026-state-of-the-game-industry-reveals-impact-of-layoffs-generative-ai-and-more/)
- **Blender MCP** (ahujasid; community plugin controlling Blender from any LLM) — [GitHub](https://github.com/ahujasid/blender-mcp).
  - Limits reported: Claude's spatial understanding is imprecise; exact positioning needs several rounds; production game assets "still need significant manual work"; break complex requests into small steps; `execute_blender_code` runs arbitrary Python that can modify/delete scene data; asset downloads block Blender's main thread; one MCP server instance at a time. — [MindStudio: Claude + Blender MCP](https://www.mindstudio.ai/blog/claude-blender-mcp-real-world-performance) (vendor blog but practitioner-toned)
  - Good fits: "repetitive, measurable, and easy to inspect" — low-poly props, modular environment pieces, scene cleanup, naming, material changes, collisions, export automation, fast visual prototypes; "treat the coding agent as a fast junior technical artist." — [best-games.io Codex/Claude Code + Blender MCP guide](https://best-games.io/blog/codex-claude-code-blender-mcp-game-development-guide)
- **ComfyUI + MCP**: official Comfy MCP for driving ComfyUI from agents — [Comfy docs](https://docs.comfy.org/agent-tools/mcp); [comfy.org/mcp](https://comfy.org/mcp). Community servers: artokun/comfyui-mcp (claims 178 tools, authors/edits graphs node by node, works with Claude/ChatGPT/Gemini/Ollama) — [GitHub](https://github.com/artokun/comfyui-mcp); a loop-aware server enforcing "build-run-look-critique-fix" until output matches the brief (per search snippet; repo not verified); joenorton lightweight server for iterative refinement — [GitHub](https://github.com/joenorton/comfyui-mcp-server); a server originally built for Godot asset generation with character/item/environment templates — [Glama listing](https://glama.ai/mcp/servers/@PurlieuStudios/comfyui-mcp). Feature claims are self-reported by repo authors.
- The repo this research serves already has a Unity MCP (`unityMCP`) exposing `generate_image`/`generate_model`/`manage_texture` etc. (observed in this session's tool list) — i.e., image/model generation can be driven from inside the Unity-connected agent.
- CODEX MORTIS (ChatGPT for art, Claude Code for shaders) is a public example of an LLM-driven pipeline shipped end-to-end, with polarized reception — [Interesting Engineering](https://interestingengineering.com/ai-robotics/worlds-first-game-completely-made-with-ai)

### Inferences
- Role split that matches the evidence: **Director (human)** — owns pillars, picks from contact sheets, approves at gates. **Art-director agent (LLM)** — maintains the bible and reference pack in git, writes prompts from the bible, runs vision critique against the rubric, assembles contact sheets, files decisions to the tracker. **Production agents** — image API (Nano Banana) for 2D exploration; Blender MCP for block-outs, cleanup, naming, export; Unity MCP for import + in-engine lineup renders.
- Human-in-the-loop checkpoints worth hard-coding: (1) brief approval, (2) pick from exploration sheet, (3) block-out approval in-engine, (4) final asset approval in lineup. Agents iterate freely *between* gates but can't cross one — mirrors the user's existing PR-gate discipline.
- LLM self-critique loops (build-run-look-critique-fix) are useful as pre-filters, but since vision-LLM taste isn't the director's taste, the loop's stop condition should be "meets rubric" → hand to human, not "LLM thinks it's good."

### Gaps
- No independent evaluation found of vision-LLM critique accuracy for art-direction adherence.
- Figma MCP relevance to 3D game art not researched (likely low for this project).
- "GPT Astra" identification left to the other researcher per assignment.

## Asset management & provenance

### Takeaway
Little dedicated 2025–2026 practitioner literature exists; the actionable pieces are (a) keep generation metadata (prompt, refs, model/version, seed) with each output, (b) Nano Banana outputs carry an invisible SynthID watermark, so AI provenance is detectable regardless of your records, and (c) your own records are what lets you demonstrate human authorship and answer disclosure questions accurately.

### Cited Findings
- All images created/edited with Gemini 2.5 Flash Image (Nano Banana) include an invisible SynthID watermark (plus a visible mark in some app contexts); SynthID is identification, not rights management. — [aifreeapi Nano Banana Pro watermark guide](https://www.aifreeapi.com/en/posts/nano-banana-pro-watermark-commercial-use); [zenn.dev Gemini API guide](https://zenn.dev/sora_biz/articles/gemini-api-image-generation-guide?locale=en) (third-party summaries)
- Nano Banana Pro workflows cite seed control + style reference images to keep batch outputs unified (vendor blog). — [apiyi blog](https://help.apiyi.com/nano-banana-pro-game-assets-generation-en.html)
- USCO: protection extends to human "selection, coordination, or arrangement" and human modifications of AI material — so records of what the human did are the evidence base. — [USCO Part 2 report](https://copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-2-Copyrightability-Report.pdf)

### Inferences
- Sidecar-per-output convention (e.g., `ship_interceptor_v03_gen012.png` + `.json` with prompt, reference-pack version hash, model name/version, seed, date, tool, parent image, decision status) committed via git-LFS; decisions (why this pick) go to the GitHub issue for that asset, matching the repo's "issue carries the why" rule.
- Mark every AI-derived file with a status flag (`exploration` / `placeholder` / `approved-human-reworked`) and add a pre-build check that fails if any `placeholder`-flagged AI asset is referenced by a shipping scene — the direct lesson from Clair Obscur.

### Gaps
- No sourced industry-standard naming/versioning scheme for AI concept art found; no sourced guidance specific to git-LFS for AI art (beyond general LFS practice).
- C2PA/content-credentials adoption in game pipelines not researched.

## Legal / platform: Steam disclosure, US copyright, model terms, player perception

### Takeaway
Steam (clarified Jan 2026) requires disclosure only for AI content that ships and is consumed by players (plus live-generated content), exempting dev-efficiency tools; concept art that never ships is widely read as not requiring disclosure. US copyright protects only human-authored expression — prompts alone don't suffice (USCO Jan 2025) and the human-authorship rule stands after SCOTUS denied cert in *Thaler* (Mar 2026). Players are strongly negative on genAI for art specifically.

### Cited Findings
**Steam**
- Steamworks content survey definitions: Pre-Generated = "Any kind of content that ships with your game and is consumed by players that is created with the help of AI tools during development"; Live-Generated = "Any kind of content created with the help of AI tools while the game is running"; "efficiency gains through the use of these tools is not the focus of this section"; dev must ensure the game is consistent with marketing materials. — [Steamworks docs: Content Survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey)
- Valve clarified the policy on Jan 16, 2026, narrowing disclosure to player-facing content and exempting internal dev tools such as AI coding assistants (per third-party guide; PC Gamer headline confirms the "consumed by players" framing). — [StraySpark guide](https://www.strayspark.studio/blog/steam-ai-disclosure-rules-2026-indie-developer-guide); [PC Gamer headline](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/)
- Interpretation: "Generate concept art you then paint over by hand, you disclose nothing, because the concept art never ships"; for live-generated content, "You describe your guardrails." — [Production Alchemist (Sept 2026 Substack)](https://www.productionalchemist.com/p/steam-ai-disclosure-rules-2026-what-indie-devs-need-to-know). Caveat: interpretation, not Valve text; the Steamworks page does not explicitly name concept art. Marketing/store art that *is* AI-generated is plausibly "consumed by players" — not explicitly resolved in the doc fetched.
- Epic, PlayStation, Xbox, Nintendo have no equivalent disclosure rule (per tech-insider). — [tech-insider](https://tech-insider.org/steam-ai-disclosure-2026/)
- History: Valve reversed its effective ban and introduced disclosure in Jan 2024 (older). — [AlternativeTo](https://alternativeto.net/news/2024/1/valve-reverses-ai-content-ban-on-steam-and-introduces-new-updated-guidelines)

**US copyright**
- USCO Part 2 (Jan 29, 2025): existing law suffices; AI used to *assist* doesn't bar protection; wholly AI-generated works aren't protected; "prompting an AI model does not alone provide sufficient control"; protection for human selection/coordination/arrangement and human modifications; no sui generis right recommended. — [USCO AI hub](https://copyright.gov/ai/); [Part 2 PDF](https://copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-2-Copyrightability-Report.pdf); [Mintz](https://www.mintz.com/insights-center/viewpoints/54731/2025-02-07-us-copyright-office-publishes-second-part-report-ai)
- SCOTUS denied cert in *Thaler v. Perlmutter* on March 2, 2026, leaving the D.C. Circuit's human-authorship requirement intact; how much human contribution is enough remains unanswered. — [Mayer Brown](https://www.mayerbrown.com/en/insights/publications/2026/03/supreme-court-denies-review-in-ai-authorship-case); [Holland & Knight](https://www.hklaw.com/en/insights/publications/2026/03/the-final-word-supreme-court-refuses-to-hear-case-on-ai-authorship)

**Model terms**
- Google's terms: Google does not claim ownership of generated output; commercial use permitted; SynthID doesn't restrict use. (Third-party summaries — confirm against Google's Gemini API Additional Terms directly.) — [aifreeapi](https://www.aifreeapi.com/en/posts/nano-banana-pro-watermark-commercial-use); [glbgpt](https://www.glbgpt.com/hub/can-i-use-gemini-ai-images/)

**Player & developer perception**
- Quantic Foundry (Oct–Dec 2025, n=1,799): 85% negative on AI in games, 63% picked the most negative option; most negative on genAI for artwork, music, narrative, dialogue; more open to dynamic difficulty; younger and female/non-binary gamers more negative. Nick Yee: such skew is "rare" in their research. — [Quantic Foundry](https://quanticfoundry.com/2025/12/18/gen-ai/)
- GDC 2026 SOTI: 52% of devs say genAI harms the industry (up from 30% and 18% in prior two years); 7% positive (down from 13%); visual/technical artists 64% unfavorable. — [GDC](https://gdconf.com/article/gdc-2026-state-of-the-game-industry-reveals-impact-of-layoffs-generative-ai-and-more/); [Game Developer](https://www.gamedeveloper.com/business/one-third-of-game-workers-use-generative-ai-but-half-think-it-s-bad-for-the-industry); [80.lv](https://80.lv/articles/gdc-survey-over-50-of-game-devs-say-generative-ai-harms-industry)
- Awards: Indie Game Awards bans genAI-developed games from nomination (Clair Obscur precedent). — [Comics Beat](https://www.comicsbeat.com/indie-game-awards-rescind-clair-obscur-expedition-33s-wins-for-gen-ai-use/)

### Inferences
- The legally and reputationally strongest profile for this project is the same one: AI for exploration/reference and agentic tooling (Blender automation, cleanup, export), shipped assets substantially human-modeled/reworked in Blender. This (a) likely keeps most content outside Steam's "pre-generated, consumed by players" disclosure scope, (b) maximizes copyrightable human expression, (c) avoids the review/sales stigma measured by Game Oracle.
- If any AI pixels do ship (e.g., textures, skybox/nebula art, UI illustrations), disclose honestly — the documented backlash hits hardest on concealment and contradiction (Clair Obscur's earlier assurances).
- Note for this repo: it already has AI-derived skybox/nebula work in recent commits; whether those pixels ship determines disclosure — worth tracking per-asset provenance now.

### Gaps
- Did not retrieve Valve's exact Jan 2026 announcement text (PC Gamer/Notebookcheck fetches failed); the Jan 16 date comes from a third-party guide.
- Did not verify Google's current Gemini API / Nano Banana Pro terms first-hand (only third-party summaries, some from watermark-remover sites — low reliability).
- Non-US copyright (EU, UK CDPA s.9(3) computer-generated works) not researched.
