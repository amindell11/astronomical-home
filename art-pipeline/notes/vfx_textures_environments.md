# AI-augmented VFX, shaders, textures/materials and space environments (Unity URP, as of Sept 2026)

Scope note: 21 tool calls (searches + fetches). Practitioner evidence is thin in several sub-areas (especially AI-video-to-flipbook and LLM-authored Shader Graph); those gaps are called out explicitly. "Inference" bullets are my reasoning, not sourced fact.

## VFX: VFX Graph vs Shuriken in URP, and can LLM agents author VFX reliably? (incl. flipbooks from EmberGen / AI video / Blender)

### Takeaway
For a desktop Windows URP space shooter, both particle systems are viable; Shuriken (CPU, module-based, fully scriptable via C#) is the easier target for an AI agent today, and the best published practitioner result (Coplay, Jan 2026) got "decent but rough" effects only after adding recipe templates, generated textures, and a screenshot feedback loop. Flipbook texture quality — not particle wiring — is the biggest visual lever, and the proven sources are EmberGen, Blender sims, and CC0 packs; AI-video-to-flipbook has no solid practitioner evidence yet.

### Cited Findings
**VFX Graph vs Shuriken**
- Shuriken is CPU-simulated and lacks VFX Graph's raw particle throughput; the performance comparison "isn't a simple win for one over the other." Commonly cited rough scale: Shuriken ~10K particles vs VFX Graph ~10M on GPU, with VFX Graph limited to newer versions/platforms (aggregator-style summary, older) — [Unity Discussions: Updated take on VFX Graph vs Shuriken](https://discussions.unity.com/t/updated-take-on-vfx-graph-vs-shuriken-performance/908264); [Real Time VFX: Unity VFX Graph and Shuriken](https://realtimevfx.com/t/unity-vfx-graph-and-shuriken/15033)
- Community consensus in that thread (2023): Shuriken appropriate for ~100–200 projectiles scenario, watch CPU cost; VFX Graph on Android needs Vulkan; Unity staff confirmed VFX Graph instancing shipped in 2022.2. The thread has no hard benchmarks for small-effect overhead, lights or collisions — [Unity Discussions](https://discussions.unity.com/t/updated-take-on-vfx-graph-vs-shuriken-performance/908264)
- Shuriken "is not currently being actively worked on" but is the only particle tool fully supporting mobile (older thread, ~2021) — [Unity Discussions: VFX Graph on non-compute mobile and the future of Shuriken](https://discussions.unity.com/t/vfx-graph-on-non-compute-mobile-urp-and-the-future-of-shuriken/837645)
- VFX Graph integrates with Shader Graph for custom particle shaders; enabling "Support VFX Graph" on a Shader Graph doesn't affect runtime performance but lengthens shader compile — [Unity docs: Working with Shader Graph in VFX Graph 17.0](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/sg-working-with.html)

**LLM/agent authoring of VFX (practitioner result)**
- Coplay (maker of the Unity MCP ecosystem, blog dated 22 Jan 2026) built AI VFX generation on **particle systems (Shuriken), deliberately deferring Shader Graph**. Three techniques: (1) a recipe library the model retrieves via tool calls instead of stuffing context; (2) AI texture generation for masks, noise, sprites with specified resolution/transparency; (3) a visual feedback loop — simulate ~3 s, capture the frame with maximum particle density, render to texture for the model to evaluate — [Coplay: We Taught AI to Build VFX in Unity. It Kinda Worked.](https://coplay.dev/blog/ai-vfx)
- Failure modes without constraints: "The AI often picks the wrong shaders and builds effects with messy, inconsistent structure"; spatial reasoning, aesthetics, motion and timing are hard; static images are insufficient feedback for motion — [Coplay](https://coplay.dev/blog/ai-vfx)
- Their conclusion: "the AI didn't magically become a VFX artist" — results "still rough, still simple, but real progress" (campfire, snow, magical explosion demos) — [Coplay](https://coplay.dev/blog/ai-vfx)

**Flipbooks / sprite sheets**
- Unity published free CC0 VFX image sequences/flipbooks (older, pre-2025) — raw frame sequences or pre-assembled sheets; explosion flipbook budgets are rare in industry — [Unity Blog: Free VFX image sequences and flipbooks](https://blog.unity.com/engine-platform/free-vfx-image-sequences-flipbooks)
- Blender-to-flipbook fireball tutorials (simulate, render, pack) exist and are a mainstream path — [YouTube: Make a Fireball Flipbook Texture in Blender](https://www.youtube.com/watch?v=wFywnH-t_PI); [Real Time VFX: Create Flipbook Textures (Tutorial)](https://realtimevfx.com/t/create-flipbook-textures-tutorial/28873); [VFX Apprentice: What are flipbooks](https://www.vfxapprentice.com/blog/what-are-flipbooks-in-games)
- EmberGen exports flipbooks/sprite sheets plus "Motion Vectors, 6 Point Lighting, Normal Maps, Depth Maps" in EXR/PNG/TGA, and VDB; claims use in 200+ studios. No AI features mentioned on the product page — [JangaFX EmberGen](https://jangafx.com/software/embergen)
- EmberGen pricing: indie perpetual $299.99, or indie subscription $19.99/mo (earning < $1M/yr) converting to perpetual after 18 months (CG Channel, 2023/2025 reporting) — [CG Channel: EmberGen 1.0](https://www.cgchannel.com/2023/03/jangafx-releases-embergen-1-0/); [CG Channel: EmberGen 2.0 features](https://www.cgchannel.com/2025/01/check-out-the-new-features-due-in-embergen-2-0/)
- EmberGen 2.0 (sparse sims, retiming, cache/resume, path tracer, USD, macOS) was announced Jan 2025 as due "2025 or 2026"; I could not confirm it has shipped as of Sept 2026 — [CG Channel](https://www.cgchannel.com/2025/01/check-out-the-new-features-due-in-embergen-2-0/)
- Third-party Unity flipbook shaders for EmberGen output (motion-vector blending, custom lighting with normals) exist on Asset Store/Fab — [Fab: Flipbook Shader for EmberGen](https://www.fab.com/listings/01e6c45d-cdf7-422b-b67f-4508bb183bd2)
- Some itch.io VFX asset packs now advertise "No generative AI was used" as a selling point — [search summary of itch.io explosion packs](https://aklingon.itch.io/explosions-vfx) (signal of community stance, weakly sourced)
- Stock VFX on black backgrounds are composited with Add/Screen blend (luminance as pseudo-alpha) — the same trick that would apply to AI-generated fire/explosion video — [Envato Elements smoke element](https://elements.envato.com/smoke-vfx-video-element-isolated-on-black-backgrou-VT7LNPN); [mycreativefx 2026 VFX guide](https://mycreativefx.com/blog/398-vfx-download-the-complete-2026-guide-to-free-vfx-for-every-creator)

### Inferences
- For a coding-agent-driven solo dev: Shuriken is the lower-risk authoring target because every module is reachable via C# `ParticleSystem` APIs and serializes into prefabs the agent can inspect; VFX Graph assets are graph files the agent must edit indirectly. For a space shooter on desktop, VFX Graph earns its keep only for large counts (debris fields, dense spark showers, warp streaks).
- Replicate Coplay's three levers locally: (a) write a small "VFX recipe" doc per effect class (muzzle flash, impact sparks, explosion, shield hit, engine trail) fixing shader choice, blend mode, lifetimes and texture slots; (b) keep a curated flipbook library; (c) require the agent to capture multi-frame contact sheets (not single stills) for review — this repo's `game-capture` skill already produces clips/stills.
- AI video → flipbook (Veo/Kling/Runway): plausible pipeline is generate on pure black → extract frames → luminance-to-alpha → pack with a flipbook tool; expected problems are non-looping timing, camera motion, temporal flicker, and no motion vectors/normals. Blender Mantaflow or EmberGen give controllable, loopable, lit-able output; AI video is best treated as reference/timing inspiration.

### Gaps
- No practitioner write-up found (Reddit, realtimevfx, 80.lv) demonstrating AI video models turned into shipped game flipbooks; searches returned only stock/asset listings.
- No 2025–2026 benchmark of VFX Graph vs Shuriken for many small short-lived effects in URP desktop.
- Could not confirm EmberGen 2.0 release status or current (2026) pricing page values.
- Coplay post does not name the underlying LLM.

## Shaders: LLM-written HLSL / Shader Graph for shields, engine glow, hologram, dissolve, fresnel — reliability and review

### Takeaway
LLMs can write hand-coded URP HLSL for these classic effects, but routinely produce compile errors and URP-specific mistakes (wrong includes, missing LightMode tags, type mismatches), so the loop must be compile → console read → screenshot. Programmatic Shader Graph authoring via MCP is currently unreliable: `.shadergraph` is a multi-JSON format and at least one Unity MCP's Shader Graph tools were reported writing invalid assets; Coplay's own MCP has Shader Graph support only as an open feature request (July 2026).

### Cited Findings
- A developer's ChatGPT-written Unity HLSL shader needed manual fixes for compile errors such as "cannot implicitly convert from 'const float2' to 'float4'" (older, ~2023) — [Better Programming: How I Wrote an HLSL Shader for Unity With ChatGPT](https://betterprogramming.pub/how-i-wrote-a-hlsl-shader-for-unity-with-chatgpt-e8db0bce6ec2)
- Common URP hand-written shader failures: missing include files (e.g. LitInput.hlsl), missing/misspelled LightMode tags (UniversalForward, ShadowCaster, DepthOnly, DepthNormals) causing invisible or broken rendering; shaders that render in editor but not in build (stripping/variants) — [Unity forum: URP Lit Shader error](https://forum.unity.com/threads/urp-lit-shader-error.1145912/); [Bugnet: Fix URP shader not rendering in build](https://bugnet.io/blog/fix-unity-urp-shader-not-rendering-build)
- Authoritative references for correct URP shader-code structure (useful as grounding docs to feed the agent): [Cyanilux: Writing Shader Code in URP (v2)](https://www.cyanilux.com/tutorials/urp-shader-code/); [NedMakesGames: Writing URP shaders with code](https://nedmakesgames.medium.com/writing-unity-urp-shaders-with-code-part-1-the-graphics-pipeline-and-you-798cbc941cea)
- AnkleBreaker Unity MCP issue #18: Shader Graph tools (create, disconnect, add_node) corrupt/produce invalid `.shadergraph` files — every template writes an identical 809-byte file with no UniversalTarget, no SubTarget, no BlockNodes, empty properties, and an output node id that doesn't exist — [GitHub: AnkleBreaker-Studio/unity-mcp-plugin #18](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/issues/18)
- Shader Graph files are stored as multiple JSON objects in one file (serialization via `JsonObject`), which complicates programmatic generation — [Unity Shader Graph API: JsonObject](https://docs.unity3d.com/Packages/com.unity.shadergraph@10.2/api/UnityEditor.ShaderGraph.Serialization.JsonObject.html)
- CoplayDev/unity-mcp issue #1255 (8 July 2026) requests Shader Graph access via MCP, labeled "New tool (substantial)", open, no maintainer response visible — i.e., no first-class Shader Graph node editing in that MCP as of then — [GitHub: CoplayDev/unity-mcp #1255](https://github.com/CoplayDev/unity-mcp/issues/1255)
- Coplay's VFX work also explicitly deferred Shader Graph ("next step") — [Coplay blog](https://coplay.dev/blog/ai-vfx)
- Unity AI Assistant (Unity 6.2+, beta Aug 2025) uses third-party LLMs (reported: OpenAI GPT and Meta Llama series) to answer questions and generate code snippets — [CG Channel: Unity rolls out Unity AI in Unity 6.2](https://www.cgchannel.com/2025/08/unity-rolls-out-unity-ai-in-unity-6-2/); [completeaitraining summary](https://completeaitraining.com/news/unity-62-launches-unity-ai-suite-with-generative-tools-new/)

### Inferences
- Prefer hand-written HLSL `.shader` files (text, diffable, reviewable in PRs) over agent-generated Shader Graphs for this project. Shields (fresnel + scrolling noise + hit-ripple from impact point array), engine glow (additive HDR emissive, fresnel), hologram (scanlines + fresnel + flicker), dissolve (noise threshold + emissive edge) are all short, well-documented shaders that LLMs have seen many times — good fit.
- Review practice: (1) ground the agent on one known-good URP unlit template in-repo and require every new shader derive from it; (2) compile check via console read after each edit; (3) visual check via capture at a fixed camera; (4) check SRP Batcher compatibility (CBUFFER UnityPerMaterial) and pass tags; (5) test in a player build, not just editor, because of variant stripping.
- If Shader Graph is preferred for tweakability, a hybrid works: human builds the graph skeleton once; the agent only adjusts exposed material properties via material tools — which MCP material/texture tools handle safely.
- HDR emissive values > 1 on shields/engines only read as glow if URP Bloom is on and the camera uses HDR; this belongs to look-dev (below).

### Gaps
- No systematic 2025–2026 benchmark of LLM shader-compile success rates or Claude-specific shader results found.
- Whether Unity AI Assistant can author/edit Shader Graph nodes directly was not confirmed.
- AnkleBreaker issue date and fix status not captured.

## Textures/materials: AI PBR generators, tiling, normal/roughness derivation, asteroid surfaces, sci-fi trim sheets

### Takeaway
Many tools now produce seamless PBR sets from text or a photo, but licenses and quality vary: Unity's own in-editor Material Generator (beta, credits, prototyping-oriented), Substance 3D Sampler (Image to Material + Make It Tile — the established pro path), and open models like Ubisoft CHORD (ComfyUI, research-only license — not usable commercially). Most "best AI texture" listicles are vendor marketing. For asteroids, AI-derived tileable rock + procedural triplanar/detail blending is the realistic path; trim sheets remain a hand-authored discipline with little AI-specific evidence.

### Cited Findings
- Unity AI Material Generator (blog 14 May 2026): generates base color, normal, height, metallic, smoothness/roughness, emission, occlusion and mask maps; "realistic, tileable surface materials", pattern references recommended for repeating surfaces; choice of realistic vs stylised models; requires Unity 6.0+, Unity Cloud link, consumes AI credits; open beta, positioned for prototyping; URP support not explicitly stated in article — [Unity: Material Generator](https://unity.com/blog/unity-ai-material-generator)
- Unity AI Generators suite (Unity 6.2, Aug 2025, beta): Texture2D, Material, Terrain Layer, Sprite, Sound, Animation generators; sprite models are LoRAs from Scenario and Layer on Stable Diffusion / Flux bases — [CG Channel](https://www.cgchannel.com/2025/08/unity-rolls-out-unity-ai-in-unity-6-2/); [Unity: Sprite Generator](https://unity.com/blog/unity-ai-sprite-generator); [Unity Learn: Material and Texture Generators](https://learn.unity.com/course/prototype-a-scene-with-unity-ai/tutorial/use-material-and-texture-generators-to-make-the-environment?version=6.2); [package mirror](https://github.com/needle-mirror/com.unity.ai.generators)
- Ubisoft CHORD (open-sourced 9 Dec 2025): ComfyUI nodes; stage 1 seamless tileable texture from text/reference, stage 2 single image → Base Color, Normal, Height, Roughness, Metalness; optimal at 1024; the 2K/4K upscaling stage is **not** open-sourced; **research-only license, commercial use not permitted** — [Comfy blog: Ubisoft open-sources CHORD](https://blog.comfy.org/p/ubisoft-open-sources-the-chord-model)
- Substance 3D Sampler: "Image to Material" infers PBR channels; "Make It Tile" makes textures tileable; a user report notes Image to Material can introduce seams into an already-seamless input (older) — [Toolify: Midjourney + Substance Sampler workflow](https://www.toolify.ai/ai-news/create-seamless-pbr-textures-with-mid-journey-ai-and-substance-sampler-802667); [Adobe Community: Image to material introduces seams](https://community.adobe.com/t5/substance-3d-sampler-discussions/image-to-material-introduces-seams-in-a-seamless-texture/m-p/13834457/highlight/true)
- Vendor claims (marketing, not practitioner evidence): 3D AI Studio self-ranks as "best AI texture and PBR generator in 2026"; Scenario, Sorceress, CraftPBR, AITextured all claim text/photo → full seamless PBR in minutes — [3D AI Studio blog](https://www.3daistudio.com/blog/best-ai-texture-and-pbr-generators-2026); [Scenario](https://www.scenario.com/features/generate-textures); [CraftPBR guide](https://craftpbr.com/guides/create-pbr-materials-with-ai); [AITextured](https://aitextured.com/)
- Shader Graph can itself be used as a procedural texture creation tool (bake noise/patterns) — [Medium: ShaderGraph as procedural texture tool](https://medium.com/@omid3098/using-unity-s-shadergraph-as-a-procedural-texture-creation-tool-54fc5836534e)

### Inferences
- Normal/roughness "derived" from a single AI albedo image is an estimate (baked lighting in the albedo leaks into normals); for hero ships, Blender baking from geometry beats AI inference. For asteroids, which are seen at speed and range, AI-generated tileable rock + triplanar mapping + a macro noise variation layer is sufficient and hides tiling.
- License triage matters for a shippable game: avoid research-only models (CHORD) for final assets; Poly Haven (CC0, not AI) and Material Maker (open-source, procedural node-based, not AI) remain safe free baselines.
- Trim sheets for hard-surface sci-fi: AI can generate panel/greeble reference, but the sheet layout must match UV strips — best done in Blender/Substance by hand or by an agent-scripted Blender bake. No evidence of AI tools producing correct trim-sheet layouts.

### Gaps
- No sources fetched on Polycam, Meshy texturing, Adobe Firefly-in-Substance, or Material Maker specifics for 2026.
- Unity AI credit pricing not disclosed in fetched article.
- No practitioner comparison of AI PBR quality vs Poly Haven/Substance for asteroid rock found.

## Space environments: AI vs procedural skyboxes, nebula volumetrics, HDR vs LDR, seams/poles

### Takeaway
Blockade Labs Skybox AI is the leading dedicated tool (paid tiers $24–$140/mo, EXR/HDR/cubemap export, HDRI only on $60+ tier) but reviewers still flag seam/horizon/coherence problems; general image models (Nano Banana 2 etc.) can produce 2:1 equirects but seams at the wrap edge and pinwheel stretching at the poles are the classic failure modes. The user's existing procedural Blender HDR pipeline already avoids both and is true HDR — AI is better used for reference/art-direction or as a nebula texture layer than as a replacement.

### Cited Findings
- Skybox AI exports EXR, HDR and cubemap at 1K–16K depending on plan; API documents exports — [Blockade Labs API: Skybox Exports](https://api-documentation.blockadelabs.com/api/skybox-exports.html); [Blockade Labs](https://www.blockadelabs.com/)
- Skybox AI 2026 plans: Free (5 credits, preview only, no export); Essential $24/mo (100 credits, 8K); Standard $60/mo (300 credits, 8K, projects, HDRI, mesh); Business $140/mo (500 credits, 16K, API); all paid plans include commercial licensing; reviewer: "Panoramic generations can still contain seam, horizon, scale or object-coherence problems that become obvious in a headset" — [The Rundown: Skybox AI (2026)](https://www.therundown.ai/tools/skybox-ai)
- Blockade markets 32-bit HDRI / 16K EXR for virtual production and has Unity Asset Store and Blender add-on integrations — [Blockade Labs: Film & VFX](https://www.blockadelabs.com/industries/skybox-ai-for-film-&-vfx); [Unity Asset Store listing](https://assetstore.unity.com/packages/tools/generative-ai/skybox-ai-generator-by-blockade-labs-subscription-274237); [Skybox AI for Blender](https://www.blockadelabs.com/integrations/skybox-ai-for-blender); Model 3 launch (2024, older) — [CG Channel](https://www.cgchannel.com/2024/04/blockade-labs-launches-skybox-ai-2/)
- Nano Banana 2 workflows turn a photo into a 2:1 equirectangular wrap for skyboxes (third-party workflow marketing) — [Floyo: Nano Banana 2 image to 360 panorama](https://www.floyo.ai/workflows/nano-banana-2-for-image-to-360-panor-i64lt6mmfhhb)
- Equirect failure modes: left/right edge mismatch shows as a vertical stripe, most often when AI adds noise/vignetting at borders; top/bottom rows collapse to poles so detail stretches into "pinwheels"; downsizing causes radial pole artifacts; 2K fine for flat-screen preview, 4K for VR — [panoramagenerator: skybox formats](https://panoramagenerator.com/blog/skybox-formats-explained); [Onix: 360 equirect panoramas in games](https://onix-systems.medium.com/how-to-use-360-equirectangular-panoramas-for-greater-realism-in-games-55fadb0547da); [Oreate: equirect to seamless cubemap](https://discover.oreateai.com/discover/converting-equirectangular-panoramas-into-seamless-cubemap-skyboxes)
- Procedural alternatives: Spacescape (free, open-source, stars + nebulas, still on GitHub) — [GitHub: FrozenStormInteractive/Spacescape](https://github.com/FrozenStormInteractive/Spacescape); wwwtyro space-3d web generator (free, seeded) — [Unity Discussions](https://discussions.unity.com/t/space-skybox-generator-neat-resource-i-found/810216); "Procedural Space Skybox" 3.0 renders stars/nebulas as VFX Graph particles behind scene, runtime 4K — [Unity Asset Store](https://assetstore.unity.com/packages/vfx/shaders/procedural-space-skybox-295596); Farland Skies Nebula One single-pass skybox shader (older) — [Unity forum](https://forum.unity.com/threads/released-farland-skies-nebula-one-procedural-skybox.442617/)

### Inferences
- HDR vs LDR: diffusion/image models output 8-bit sRGB; "HDR" exports from AI tools are likely inverse-tonemapped estimates, not physically meaningful radiance. For a space game this matters because only true >1.0 values let stars and nebula cores trigger URP bloom selectively. Keep the Blender procedural HDR as the base; if using an AI image for nebula color/shape, import it as a separate layer multiplied by an HDR intensity in the skybox shader.
- Poles/seams: space backdrops are more forgiving than landscapes (no horizon), but pinwheels at poles are still visible when players roll the camera freely in 6DOF combat. Cubemap generation (or procedural generation directly on the sphere, as Blender does) avoids it; if an AI equirect is used, inpaint the poles and wrap seam (offset by half width, inpaint, offset back).
- Volumetric nebulae in-play (flying through gas) are a different technique from the skybox: layered soft billboard particles or a raymarched volume shader; no AI shortcut found.

### Gaps
- Could not verify whether Blockade's HDR is true scene-referred HDR or LDR-derived.
- No practitioner reports specifically on AI-generated *space* skyboxes (vs landscapes) quality found.
- No sources on URP-specific nebula volumetrics fetched.

## Look development & consistency: color scripts, post-processing (bloom, tonemapping, LUTs), AI-generated LUTs/reference, paint-over loop

### Takeaway
The established game method is screenshot → grade in an image editor → export LUT → apply in engine post-processing; AI fits naturally at the "reference frame / paint-over" step (generating target frames from real screenshots), with the human or agent then implementing changes in shaders, materials and URP volumes. Evidence for AI-generated LUTs specifically in games is essentially absent; built-in URP color grading is sufficient for most indie projects.

### Cited Findings
- Indie LUT workflow: take game screenshots, adjust colors/curves in an image editor, export back as a LUT the engine applies to the scene — [IndieDB: LUT color correction devlog #24](https://www.indiedb.com/features/lut-color-correction-in-video-games-devlog-24); Unreal's documented equivalent — [Epic: Using LUTs for color grading](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-lookup-tables-luts-for-color-grading?application_version=4.27)
- LUT authoring under ACES tonemapping has pitfalls (grading must happen in the right color space relative to the tonemapper) — [ACESCentral: Creating an ACES LUT for game engine](https://community.acescentral.com/t/creating-an-aces-lut-for-color-grading-in-a-game-engine/2945)
- Built-in color-grading tools suffice for most projects; LUTs are useful when matching a specific reference — [gamineai: Color grading for game cinematics 2026](https://www.gamineai.com/blog/color-grading-and-post-processing-for-game-cinematics-2026)
- AI grading tools (fylm.ai, DaVinci Resolve AI features, Resolve 21) are film/video-oriented, can export LUTs — [fylm.ai](https://fylm.ai/); [Pixflow: AI color grading tools 2026](https://pixflow.net/blog/ai-color-grading-tools/)
- Indie paint-over practice: AI used to generate lighting/shading over flat colors, then manual paint-over and adjustment ("speed up, not replace") — [itch.io devlog: Pencil, Krita & AI](https://orevyn.itch.io/locked-up-with-them/devlog/938426/inside-our-art-process-pencil-krita-ai)
- Coplay's VFX system shows the agent-side analogue: capture a rendered frame and feed it back for evaluation, and notes static frames miss motion problems — [Coplay](https://coplay.dev/blog/ai-vfx)

### Inferences
- A practical paint-over → implement loop for this project: (1) capture a fixed "look-dev camera" frame set (combat, explosion, shield hit, engine trail against nebula); (2) ask an image model (e.g., Nano Banana / GPT image) to repaint the frame toward a written style brief; (3) the director picks a target; (4) the coding agent translates the delta into concrete parameters (bloom threshold/intensity, tonemapper, color adjustments, emissive intensities, particle color gradients); (5) recapture and diff side-by-side. Keep the style brief + approved reference frames in-repo as the consistency anchor (color script: e.g., cool blue/teal nebula base, warm orange for explosions/engines, a reserved hue per faction/shield).
- LUT generation from a paint-over is possible without AI: grade the neutral screenshot to match the approved paint-over in an editor (or Resolve's color-match), then export a 32-cube LUT into URP's Color Lookup volume override — the AI supplies the target, not the LUT math.
- Consistency levers that matter most in URP: HDR camera + one tonemapper (ACES or Neutral) chosen early; bloom tuned once and emissives authored in HDR relative to it; a shared palette ScriptableObject/material property set so the agent reuses colors rather than inventing them per effect.

### Gaps
- No evidence found of AI tools producing game-ready LUTs directly from reference images, or of practitioners using image-model paint-overs for real-time space/VFX look-dev specifically.
- No URP-specific 2026 post-processing guidance for space games was fetched.
