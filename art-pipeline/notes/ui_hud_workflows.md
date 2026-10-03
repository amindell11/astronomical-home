# AI-Augmented Workflows for Game UI / HUD in Unity (as of Sept 2026)

Scope: concept -> design system -> implemented, legible in-game UI for a solo dev on a Unity URP 3D space-combat game, driven through Claude Code + a Unity MCP server. Research pass date: 2026-09-22. ~20 search/fetch calls; several practitioner pages (Medium, echoesofsomewhere) returned HTTP 403 and could not be read, so the practitioner evidence base is thinner than the vendor/tutorial base. Marketing vs practitioner is flagged per item.

## 1. Concepting UI with image models (Nano Banana / Gemini image, GPT Image, Midjourney) and converting mockups into real layouts

### Takeaway
Image models are useful for *direction-finding* (mood, palette, density, framing of HUD elements) and Nano Banana Pro (Gemini 3 era) has largely fixed the "gibberish text" problem, but every source that discusses production treats the output as a reference to be **rebuilt by hand/agent** in a real layout tool or engine, not traced or sliced. No source found describes a reliable automated "image mockup -> implementable layout" converter for game UI.

### Cited Findings
- Midjourney v6's default model was reported to no longer produce convincing UX/UI or website mockups (a 2023-24 era observation; older info) — [Pedro Sostre, Medium](https://psostre.medium.com/midjourney-v6-removes-the-ability-to-create-ux-ui-and-website-designs-12823d1c1d91)
- Midjourney outputs only raster images, not vectors, so re-creating them in Figma "takes time" (older, LogRocket tutorial) — [LogRocket](https://blog.logrocket.com/ux-design/using-midjourney-generate-ui-designs/)
- Tutorial advice: prompt with UI/UX vocabulary (e.g., "HUD", "panel", "button"), avoid art words like "beautiful", "render", "painting" to get functional-looking layouts — [LogRocket](https://blog.logrocket.com/ux-design/using-midjourney-generate-ui-designs/)
- Indie devlog (Echoes of Somewhere, Aug 2023; older) reported that Midjourney-generated UI pages "did not amount to anything usable in the game" as implementation — surfaced via search snippet; full page returned 403 — [Echoes of Somewhere](https://echoesofsomewhere.com/2023/08/28/ai-inspired-ui-design/)
- Nano Banana Pro (Raw.Studio, Jan 30 2026): strengths are legible text rendering ("buttons, labels, and headings feel usable rather than placeholder noise"), realistic spatial relationships, 4K output; weaknesses: no grasp of design systems or cross-screen consistency, no deep understanding of accessibility or technical constraints, grid/alignment "suggestive rather than pixel-perfect" — [Raw.Studio](https://raw.studio/blog/ui-design-with-nano-banana-pro)
- Raw.Studio's recommended workflow: generate many variations -> compare side-by-side -> import the best concepts into Figma (or similar) -> **manually rebuild using a proper design system** -> apply accessibility/technical constraints -> hand off — [Raw.Studio](https://raw.studio/blog/ui-design-with-nano-banana-pro)
- Vendor/prompt-site claim: Nano Banana Pro can render "logically consistent" HUDs/post-match screens with correctly labelled stats (marketing-tier source) — [Higgsfield](https://higgsfield.ai/blog/Nano-Banana-Pro-Expert-Use-Cases)
- Prompt guide advice for game assets: don't let the model generate text unless you are specifically making buttons, since in-engine text is rendered dynamically — [Nano Banana Lab](https://nanoprompts.org/lab/2025/game-asset-design-guide)
- Dedicated "game UI mockup generator" products exist (NightCafe, visualizee.ai, others) — these are wrappers over general image models; no practitioner validation found — [NightCafe](https://creator.nightcafe.studio/tools/game-ui-mockup-generator); [visualizee.ai](https://visualizee.ai/blog/game-ui-design-ai-generator)

### Inferences
- The practical role of an image model for this project is a *mood board / art-direction probe*: generate 10-30 HUD screenshots over actual in-game backgrounds (feed a real game capture as the input image to Nano Banana for edit-style generation), pick a direction, then extract **decisions** (palette hex values, line weight, corner treatment, density, where elements sit) rather than pixels.
- The "convert" step that actually works is: human (art director) annotates the chosen mockup -> writes/approves a small style spec (section 6) -> agent authors UXML/USS or uGUI prefabs against that spec -> screenshot in-engine -> compare to mockup -> iterate. The mockup is a target image, not a source asset.
- Mockup text legibility (now good) is misleading: game UI legibility depends on in-engine font rendering, scale at 1080p/1440p, and motion over a busy background — none of which a static mockup tests.

### Gaps
- No 2025-2026 practitioner post was found (readable) that measured how much of an AI HUD mockup survived into a shipped game. r/gamedev / r/Unity3D threads did not surface in search results.
- GPT Image (gpt-image-1 / successors) and Midjourney v7 specifics for UI were not researched in this pass.

## 2. Design tools: Figma (AI, Figma Make, MCP), Google Stitch, Galileo, Uizard, Penpot — and Figma -> Unity pipelines

### Takeaway
Figma remains the pivot: Stitch (Google, Gemini 3, free in Labs) is a fast first-draft generator whose output is web/app-oriented and exports to Figma; Figma's official MCP server lets Claude Code read frames, variables and components (and write to canvas). For getting into Unity, there are several Figma -> UI Toolkit (UXML/USS) converters, open-source and paid; their quality depends on the Figma file using Auto Layout, which maps to UI Toolkit's flexbox.

### Cited Findings
- Google Stitch: as of June 2026 runs on Gemini 3, free in Labs preview with monthly caps, generates five screens at once, exports HTML/CSS, Tailwind, Vue, Angular, Flutter, SwiftUI, and "Paste to Figma" — [nocode.mba review](https://www.nocode.mba/articles/google-stitch-review); [SFAI Labs, March 2026 update](https://sfailabs.com/guides/google-stitch-vs-figma)
- Consensus in reviews: Stitch handles "the first 80%" (ideation/drafting); Figma the last 20% (design systems, polish, handoff) — [vibecoding.app](https://vibecoding.app/blog/google-stitch-review); [nocode.mba](https://www.nocode.mba/articles/google-stitch-review)
- Figma MCP server gives Claude Code structured access to components, variables, layout data, FigJam, Make resources; supports generating code from selected frames and writing native content back to the canvas — [Figma Help Center](https://help.figma.com/hc/en-us/articles/39888612464151-Claude-Code-and-Figma-Set-up-the-MCP-server); [Builder.io](https://www.builder.io/blog/claude-code-figma-mcp-server)
- The Dev Mode MCP passes variables, components, styles, screenshots, text, SVGs and layer names; its stock code examples are React/Tailwind (i.e., web-oriented, not Unity) — [Clauder Navi](https://www.clauder-navi.com/en/claude-to-figma); [Builder.io](https://www.builder.io/blog/claude-code-figma-mcp-server)
- **FigmaToUnity (TrackMan, open source)**: Unity plugin importing entire Figma pages into UI Toolkit UXML/USS — [GitHub](https://github.com/TrackMan/Unity.Package.FigmaToUnity); [Unity Discussions announcement](https://discussions.unity.com/t/figmatounity-is-now-available-convert-figma-documents-to-ui-toolkit-open-source/932378)
- **UnityUI-Transformer (open source)**: translates Figma Auto-Layout flex direction, padding, gap and bounds into .uxml/.uss — [GitHub](https://github.com/argentium0/UnityUI-Transformer)
- **D.A. Assets "Figma to UI Toolkit Converter"** (paid Asset Store extension to "Figma Converter for Unity"): UXML, USS and component templates "in one click" (vendor claim) — [Asset Store](https://assetstore.unity.com/packages/tools/utilities/figma-to-ui-toolkit-converter-272042); [D.A. Assets](https://da-assets.com/uitk-converter)
- Figma Community plugins export to Unity prefabs (uGUI) via JSON: "Figma UI Exporter for Unity", "Unity UI Exporter", "Unity Importer" — [Figma plugin 1](https://www.figma.com/community/plugin/1608740543377934247/figma-ui-exporter-for-unity); [plugin 2](https://www.figma.com/community/plugin/1569658015023703627/unity-ui-exporter); [plugin 3](https://www.figma.com/community/plugin/1047282855279327962/unity-importer)

### Inferences
- For a solo dev using Claude Code, the Figma step is optional. Its value is (a) a place to hand-tweak layout visually as art director and (b) Figma Variables as a token source the agent can read via MCP. If the dev won't use Figma interactively, going mockup -> style spec -> agent-authored USS directly is fewer seams.
- Stitch is tuned for app/web screens; for menus (settings, loadout, hangar) it is plausibly useful as a layout draft; for an over-the-world combat HUD it is a poor fit (no concept of 3D target brackets, reticles, lead indicators).
- Converter output will only be as clean as the Figma structure (Auto Layout, named layers, components). An AI-generated Figma file (from Stitch) will probably need restructuring before conversion.

### Gaps
- Galileo AI, Uizard, and Penpot were not researched with sources in this pass. (Unverified recollection: Google acquired Galileo AI in 2025 and it fed into Stitch — confirm before citing.)
- No evidence found of game studios/solo devs specifically using Stitch or Figma Make for *game* UI; all Stitch/Make material is app/web-focused.
- No independent quality comparison of the Figma->UXML converters.

## 3. Unity-side: UI Toolkit vs uGUI (2025-2026), LLM agents authoring UXML/USS, Unity AI (Muse successor)

### Takeaway
Split verdict in 2026: UI Toolkit is the stronger choice for menus/data-heavy screens (and is now Unity's pick for world-space too, with custom shaders, vector images and better scaling in Unity 6), while uGUI still wins for Animator/Timeline-driven, gameplay-coupled HUD feedback. UXML/USS is HTML/CSS-like text, which makes it the more agent-friendly format; Unity itself shipped an official Claude Code plugin (Sept 9 2026) with a dedicated `/ui-uitk` skill. Unity Muse is retired; Unity AI generators (Unity 6.2+) produce sprites/textures usable as UI placeholders, not layouts.

### Cited Findings
- Unity docs position uGUI as recommended for runtime UI with UI Toolkit as the alternative (per migration/comparison docs) — [Unity Manual: Migrate from uGUI to UI Toolkit](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Transitioning-From-UGUI.html); summarized by [Angry Shark Studio, 2025](https://medium.com/@studio.angry.shark/unity-ui-toolkit-vs-ugui-2025-developer-guide-8407312c91ed)
- H. Idris (8 Sep 2026): both systems now support world-space UI, and UI Toolkit is Unity's listed pick for it; UI Toolkit supports "custom shaders, materials, vector graphics and textureless elements"; uGUI owns keyframed animation via Animation Clips/Animator/Timeline, while UI Toolkit motion is "transitions and code, which is fine for menu polish and awkward for authored sequences"; Unity 6 brought "jobified mesh generation, parallelised text generation and much faster event dispatch"; for gameplay HUDs with Animator-driven feedback and world anchoring, uGUI is recommended — [h-idris.com](https://h-idris.com/blog/unity-ugui-vs-ui-toolkit.html)
- Rule of thumb from 2025 guide: uGUI for animated game HUD, UI Toolkit for menus and data-heavy screens; UI Toolkit lacks in-scene authoring, serialized events, and Animation/Timeline integration — [Angry Shark Studio](https://medium.com/@studio.angry.shark/unity-ui-toolkit-vs-ugui-2025-developer-guide-8407312c91ed)
- UI Toolkit cost scales more predictably (retained visual tree); uGUI performance degrades non-linearly with canvas rebuilds — [Darko Unity](https://darkounity.com/blog/i-researched-ui-toolkit-so-you-dont-have-to)
- Unity's official Claude Code plugin (Sept 9 2026): 29 skills; `/ui` router "detects which UI system a project uses before any UI code is written"; `/ui-uitk` "UI Toolkit expert for Unity 6.0+: UXML and USS files, flex layouts, UIDocument"; `/ui-ugui`, `/ui-imgui`; `/optimize-text-mesh-pro`; `/unity-cli` drives a live Editor. No UI verification/before-after evidence in the post — [Unity blog](https://unity.com/blog/unity-plugin-for-claude-code)
- Unity's App UI package also documents a Claude Code plugin — [App UI docs 2.2](https://docs.unity3d.com/Packages/com.unity.dt.app-ui@2.2/manual/claude-plugin.html)
- Community Claude Code skills for UI Toolkit (UXML/USS, flex, runtime binding, custom elements, PanelSettings) exist on skill marketplaces — [LobeHub](https://lobehub.com/skills/dev-gom-claude-code-marketplace-unity-uitoolkit); [mcpmarket](https://mcpmarket.com/tools/skills/unity-ui-toolkit-designer)
- Unity Muse retired Oct 1 2025, replaced by Unity AI in Unity 6.2+; sprite generation uses third-party models (Scenario and Layer LoRAs on Stable Diffusion / Flux) — [CG Channel, Aug 2025](https://www.cgchannel.com/2025/08/unity-rolls-out-unity-ai-in-unity-6-2/)
- Unity "AI UI Generator" workflow (Unity blog, May 13 2026, open beta, Unity 6+): Sprite Generator (icons, UI graphics) + Texture Generator (panel fills, backdrops) + AI Assistant to wire functionality into uGUI Canvas or UI Toolkit document; generated assets carry AI-generated metadata for later replacement; users responsible for usage rights/store declarations. No 9-slice or UXML generation mentioned — [Unity blog](https://unity.com/blog/unity-ai-ui-generator)

### Inferences
- For this project: menus (main, pause, settings, hangar/loadout) -> UI Toolkit, agent-authored UXML/USS with a shared token stylesheet. Screen-space combat HUD -> either works; UI Toolkit is more agent-editable (text files diff cleanly), uGUI is better if HUD feedback will be keyframed. Target brackets/lead indicators that track 3D objects are usually a screen-space overlay positioned from code each frame, which UI Toolkit handles fine (or custom meshes/shaders).
- Agent reliability evidence is indirect: the existence of Unity's own `/ui-uitk` skill plus MCP/CLI editor control means an agent can author UXML/USS *and* screenshot the Game view to check it. The environment here already exposes `mcp__unityMCP__manage_ui` and `mcp__unityMCP__generate_image` tools (observed in this session's tool list, not a web source). The verification loop (author -> capture -> compare to mockup) is the thing that makes it reliable, not the authoring alone.
- Unity AI Sprite Generator is placeholder-grade by Unity's own framing (metadata "for later replacement").

### Gaps
- No quantitative/practitioner report found on how often LLM-authored UXML/USS is correct on first compile or visually matches intent.
- Whether UI Toolkit world-space is production-stable in the user's exact Unity version was not checked.
- TextCore (UI Toolkit) vs TextMeshPro (uGUI) font/SDF pipeline differences not researched.

## 4. Icons and vector assets: Recraft, icon-family consistency, SVG in Unity, 9-slice, atlases

### Takeaway
Recraft (V4 / V4.1, 2026) is the only mainstream image model producing native SVG, and is the default choice for icon families; consistency comes from explicit shared constraints (stroke weight, grid, corner radius, palette) plus Recraft's style-locking, not from the model automatically. Unity 6.3+ has a built-in Vector Graphics module that imports SVG directly as UI Toolkit Vector Images; the package is only needed for Sprite/uGUI SVGImage.

### Cited Findings
- Recraft claims to be the only image-gen model with native vector output (real SVG paths/layers) — vendor/partner claim — [Replicate blog, Recraft V4](https://replicate.com/blog/recraft-v4)
- Icon-set advice: name shared attributes ("consistent line weight", "same corner radius", "uniform grid"); model understands 24x24 / 16x16 grid conventions; brand-style features lock palette, illustration style and line weights across generations — [MindStudio, V4 Vector](https://www.mindstudio.ai/blog/what-is-recraft-v4-vector-generate-svg-logos-icons-ai); [MindStudio, V4.1 brand design](https://www.mindstudio.ai/blog/recraft-v4-1-brand-design-logos-svg-assets)
- Z.Tools (Feb 17 2026) — an opinion piece, no quantitative tests: photoreal, complex gradients, soft shadows and heavy texture "turn into awkward vector files"; treat decorative texture "as a liability"; use style controls and palettes to keep a set coherent — [Z.Tools](https://z.tools/blog/recraft-v4-vector-image)
- Recraft has a dedicated icon generator page — [Recraft](https://www.recraft.ai/generate/icons)
- Unity 6.3+: built-in Vector Graphics module imports SVG for UI Toolkit Vector Images and Texture2D; the `com.unity.vectorgraphics` package is only required for Sprite Editor support and uGUI `SVGImage`; projects migrate automatically on upgrade to 6.3 — [Unity Manual 6000.4: Work with vector graphics](https://docs.unity3d.com/6000.4/Documentation/Manual/ui-systems/work-with-vector-graphics.html); [needle-mirror package README](https://github.com/needle-mirror/com.unity.vectorgraphics); [Vector Graphics 3.0 docs](https://docs.unity3d.com/Packages/com.unity.vectorgraphics@3.0/manual/index.html)
- Third-party "SVG Renderer for UI Toolkit" asset exists (released 2025) — [Unity Discussions](https://discussions.unity.com/t/svg-renderer-for-ui-toolkit-released/1646540)

### Inferences
- Sci-fi HUD icons are usually flat, single-color line glyphs — the case vector generation handles best (no gradients/texture). Generating the whole family in one prompt/one SVG sheet (as Recraft demos do) likely yields more consistency than one-at-a-time.
- An agent can post-process SVGs deterministically (normalize viewBox to 24x24, force `stroke-width`, strip fills, set `currentColor`) — this enforces consistency better than prompting and fits a Claude Code workflow.
- Keeping icons single-color lets USS/tint drive state colors from tokens (friendly/hostile/neutral), rather than baking color into assets.

### Gaps
- 9-slice panel generation and sprite-atlas workflows with AI were not researched with sources (no citations gathered). Standard practice (Sprite Editor borders / UI Toolkit `-unity-slice-*` USS properties; Sprite Atlas v2) is known but uncited here.
- No independent benchmark of Recraft SVG cleanliness (path counts, anchor bloat) found; the Z.Tools piece explicitly contained none.
- Recraft pricing/licensing for commercial game use not checked.

## 5. Sci-fi HUD design principles for a non-designer (diegetic vs non-diegetic, readability over space, color/type, references, FUI resources)

### Takeaway
The recurring rules: readability beats decoration; bold sans-serif for critical info; plates/outlines/shadows only where needed because over-decoration "kills readability faster than its absence"; diegetic UI adds immersion but costs readability under pressure. Reference games cluster into diegetic-cockpit (Elite Dangerous, Star Citizen) and clean-overlay (Everspace 2). HUDS+GUIS and Game UI Database are the standard reference libraries.

### Cited Findings
- Elite Dangerous uses cockpit-mounted holographic panels (side panels pop up when the pilot looks left/right); losing the canopy removes most of the HUD, including crosshair and ammo — [Wikipedia: Elite Dangerous](https://en.wikipedia.org/wiki/Elite_Dangerous); [TV Tropes: Diegetic Interface](https://tvtropes.org/pmwiki/pmwiki.php/Main/DiegeticInterface)
- Diegetic HUDs deepen immersion but "require extra care to stay readable under pressure" — [Sunstrike Studios HUD guide](https://sunstrikestudios.com/en/blog/HUD_design_in_games/)
- Diegetic-UI guide ordering: blend with the environment, then ensure readability, make interactions natural, balance info load; recommends bold sans-serif fonts (blog-tier source, Apr 2026) — [yamii](https://www.yamii.shop/2026/04/04/diegetic-ui-guide/)
- Game fonts must be readable at speed, on any background, under dynamic lighting; use outlines, drop shadows and background plates only where truly necessary; avoid decorative fonts for critical info — [Designing Readable Typography for Game UI](https://salivity.github.io/game-development/article/designing-readable-typography-for-game-ui)
- Everspace 2's HUD is described as clean and minimal, making lead-time for shots clear; Rockfish tested the HUD in both 1st- and 3rd-person views — [Unreal Engine developer interview](https://www.unrealengine.com/en-US/developer-interviews/everspace-2-delivers-a-handcrafted-universe-brimming-with-space-combat); [Everspace Kickstarter update 20 (older, first game)](https://www.kickstarter.com/projects/rockfishgames/everspace/posts/1515268)
- Player feedback threads on Everspace 2 HUD/UI customization exist (useful for real complaints) — [Steam suggestions](https://steamcommunity.com/app/1128920/discussions/5/3731826842448018569/?l=english)
- FUI definition: film/game artists adapted radar, cockpit, medical and industrial readouts into readable dramatic props; its visual language is shorthand for "high-tech" — [HUDS+GUIS: FUI](https://www.hudsandguis.com/fui-media); [Sarah Kay Miller, Domo UX](https://medium.com/domo-ux/designing-a-functional-futuristic-user-interface-c27d617ce8cc)
- FUI/HUD trend page describes the aesthetic (dark, neon accents, terminal type, glitch motifs) and offers an AI prompt for the style — [daisyUI trends](https://trends.daisyui.com/trend/fui-hud/)
- HUDS+GUIS (Jono Yuen) curates FUI from film, TV, games and concept work — [HUDS+GUIS](https://www.hudsandguis.com/fui-media)
- Homeworld 2 improved on Homeworld 1's UI with a hybrid bottom-bar/widget interface (older) — [treeform, Strategy Game Battle UI](https://medium.com/@treeform/strategy-game-battle-ui-3b313ffd3769)

### Inferences
- Film FUI is designed to be *looked at*, game HUD to be *read in 200 ms during combat*; copying FUI density is the main trap. Borrow FUI's accents (thin rules, corner brackets, mono numerals) for frames and menus; keep combat-critical readouts sparse.
- Space backgrounds are high-contrast (black void + bright stars/nebula/explosions); a thin, low-alpha, single-hue HUD can vanish against a nebula. Likely mitigations: subtle dark backing plate or outline on text, a consistent HUD hue distinct from nebula/skybox hues, and reserving saturated red/orange for hostile/damage states. (This project just tuned its skybox stars and nebula glow — per the git log — so HUD hue should be chosen against those actual captures.)
- A non-designer's fastest calibration: collect 20-40 screenshots from Game UI Database for the chosen reference games, annotate what each element does, and pick one reference as the "north star".

### Gaps
- No GDC talk on Elite/Star Citizen/EVE/Everspace 2/Homeworld 3 HUD design was located in this pass (GDC Vault not searched directly).
- gameuidatabase.com and interfaceingame.com were not fetched (listed as reference libraries from prior knowledge; not cited here).
- No sourced contrast-ratio guidance specific to HUDs over space backgrounds found; WCAG-style ratios would be an inference.
- Specific font recommendations (e.g., licensed sci-fi-compatible sans/mono families) not sourced.

## 6. Design systems / tokens for games: a small style guide AI tools can follow

### Takeaway
No game-specific source was found describing a solo-dev token system for AI agents; the transferable pattern comes from web/Figma: define tokens (color, type scale, spacing, radius/line weight) once, expose them as Figma Variables (readable via Figma MCP) or directly as USS custom properties, and have every agent-generated screen reference tokens instead of literals.

### Cited Findings
- Figma MCP exposes design-system metadata (variables, components, styles) to Claude Code, and agents can extract/convert tokens (demoed as Tailwind config generation) — [Clauder Navi](https://www.clauder-navi.com/en/claude-to-figma); [Figma Help Center](https://help.figma.com/hc/en-us/articles/39888612464151-Claude-Code-and-Figma-Set-up-the-MCP-server)
- Practitioner-leaning guide on design systems with Figma MCP + Claude Code exists (Design Systems Collective) — [Studio Hola](https://www.designsystemscollective.com/demystifying-design-systems-and-using-figmas-mcp-with-claude-cli-674d1b66468b?gi=367422e09f78)
- Nano Banana Pro and Stitch both lack design-system understanding / consistency across screens, which is why the system has to be imposed downstream — [Raw.Studio](https://raw.studio/blog/ui-design-with-nano-banana-pro); [vibecoding.app](https://vibecoding.app/blog/google-stitch-review)
- UnityUI-Transformer maps Figma Auto-Layout padding/gap into USS — i.e., spacing tokens can survive a Figma -> Unity conversion if authored as Auto Layout — [GitHub](https://github.com/argentium0/UnityUI-Transformer)

### Inferences
- Minimal token set a solo dev could own on one page: ~6-8 colors (bg plate, primary HUD hue, secondary, friendly, hostile, warning, disabled, text), a 4-5 step type scale (one sans + one mono for numerals), a 4 or 8 px spacing scale, 1-2 line weights, 1 corner treatment (e.g., clipped 45-degree corners), and a component list (panel, button, bar/meter, target bracket, label, icon slot).
- In UI Toolkit this maps directly to USS custom properties (`--color-hostile`, etc.) in one theme stylesheet — a text file the agent reads before writing any UI and a reviewer/lint can check for raw hex literals. The same token values can be fed into image-model prompts and Recraft palette settings so concept art, icons and in-engine UI share one palette.
- Figma Variables are only worth it if the art director actually works in Figma; otherwise the USS token file (plus a rendered swatch/contact sheet) is the single source.

### Gaps
- No game-UI-specific source on token systems or on how solo devs document style guides for AI agents was found.
- No evidence on whether Figma Variables survive the Figma -> UXML converters as USS variables (vs flattened literals).
