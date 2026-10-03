# AI Image-Generation and Image-Editing Models for Game Concept Art and Asset Design (as of 2026-09-22)

Scope note: research done 2026-09-22 with 17 search/fetch calls. The main web sources are official Google/OpenAI/BFL pages, Wikipedia, and the live LMArena (arena.ai) leaderboards. Nearly all the "practitioner" comparisons I found come from **vendor blogs** (platforms that resell these models), not independent game artists. I label them that way below. I found no Reddit or 80.lv threads specifically about ortho-view or hard-surface-ship failures in this pass.

## 1. What is "GPT Astra"?

### Takeaway
"GPT Astra" is almost certainly **GPT-6 Astra**, OpenAI's flagship LLM. It went to a limited preview on 2026-09-03 and became generally available on 2026-09-04, less than three weeks before this research. It's a reasoning, agentic and text model, not an image generator. In ChatGPT, images come from a separate model family: GPT Image 2 / ChatGPT Images 2.5. That split fits the user's plan to use it as the "driver" for design and creative-direction tasks. Google's Project Astra (DeepMind's universal-assistant prototype) is a different thing, and I found nothing suggesting the user meant it.

### Cited Findings
- GPT-6 Astra is an OpenAI LLM. It was released to approved users on September 3, 2026, with public release on September 4, 2026. Its official name is "GPT-6 Astra" — [Wikipedia: GPT-6 Astra](https://en.wikipedia.org/wiki/GPT-6_Astra)
- OpenAI calls it a "generational leap" for cybersecurity, professional work, software engineering and science. It is strongest at coding, math, and computer/web navigation, and Wikipedia lists "video game development" among its example use cases — [Wikipedia](https://en.wikipedia.org/wiki/GPT-6_Astra)
- Context window is 1,050,000 tokens with up to 128,000 completion tokens. API pricing is $10.00/M input and $50.00/M output — [OpenRouter listing](https://openrouter.ai/openai/gpt-6-astra) (from search snippet; page not fetched)
- Rollout goes first to limited orgs, then to ChatGPT Plus ($20/mo), Pro ($100/mo), Business ($200/mo) and Enterprise, plus the OpenAI API and AWS. A "GPT-6 Astra Pro" variant exists for higher tiers. It is marketed as roughly 2x faster at computer use and strong at document, spreadsheet and presentation creation. In Codex it can take notes across context windows — [9to5Mac, 2026-09-04](https://9to5mac.com/2026/09/04/openai-releasing-major-upgrade-to-chatgpt-and-codex-with-gpt-6-astra-details-here/)
- Marketing benchmarks: 98% FrontierMath Tier 4, 99.9% ARC-AGI-3, 100% ExploitBench. These are OpenAI claims as reported by the press, not independently verified — [9to5Mac](https://9to5mac.com/2026/09/04/openai-releasing-major-upgrade-to-chatgpt-and-codex-with-gpt-6-astra-details-here/)
- Criticism: a new "recurrent depth" reasoning technique hides some or all of the model's reasoning, and AI-safety experts raised monitorability concerns. OpenAI rated it "Critical" for cybersecurity capability and used a phased rollout — [Wikipedia](https://en.wikipedia.org/wiki/GPT-6_Astra); [9to5Mac](https://9to5mac.com/2026/09/04/openai-releasing-major-upgrade-to-chatgpt-and-codex-with-gpt-6-astra-details-here/)
- Neither Wikipedia nor 9to5Mac says anything about native image generation for GPT-6 Astra — [Wikipedia](https://en.wikipedia.org/wiki/GPT-6_Astra); [9to5Mac](https://9to5mac.com/2026/09/04/openai-releasing-major-upgrade-to-chatgpt-and-codex-with-gpt-6-astra-details-here/)
- Other official OpenAI pages exist: a launch post, a system card, a safety overview and "Path to Astra" — [OpenAI launch post](https://openai.com/index/gpt-6-astra/) (returned 403 when fetched); [System card](https://deploymentsafety.openai.com/gpt-6-astra); [Path to Astra](https://openai.com/index/path-to-astra/)

### Inferences
- In practice, "GPT Astra as the creative driver" means GPT-6 Astra inside ChatGPT or Codex. It would write briefs, style bibles and prompts, critique renders (vision input is likely but I didn't confirm it), and drive Blender through scripts or computer use. Actual pixels would come from GPT Image 2 / Images 2.5 inside ChatGPT, or from Nano Banana if the user routes prompts to Google.
- Its "computer use" strength may matter more for a Blender/Unity pipeline than its image skills. That's an inference; I found no source testing it on Blender.
- It's three weeks old, so almost no practitioner reports on creative or art-direction work exist yet.

### Gaps
- I couldn't confirm whether GPT-6 Astra accepts image input or calls GPT Image natively in ChatGPT. The official launch page returned 403.
- No practitioner reports yet on GPT-6 Astra for art direction or design critique.
- I didn't check whether the user might mean a third-party product with "Astra" in its name. Search for "GPT Astra" returned only GPT-6 Astra results.

## 2. The Nano Banana family (Google Gemini image models)

### Takeaway
There are now four Nano Banana models. The original **Nano Banana** (gemini-2.5-flash-image) is deprecated and **shuts down on 2026-10-02**. **Nano Banana Pro** (gemini-3-pro-image, Nov 2025) is the premium, compositionally precise model. **Nano Banana 2** (gemini-3.1-flash-image, 2026-02-26) is the recommended workhorse, with 4K, up to 14 references (10 object + 4 character + 3 style) and image-search grounding. **Nano Banana 2 Lite** (gemini-3.1-flash-lite-image, 2026-06-30) is the cheap, fast, 1K-only, single-reference option. Every output carries a SynthID watermark and there's no free API tier for image output. On blind-vote leaderboards, Google's image models currently sit **below OpenAI, Microsoft, xAI, Reve and Meta**.

### Cited Findings
- Model IDs and roles: Nano Banana 2 Lite `gemini-3.1-flash-lite-image` (fastest/cheapest), Nano Banana 2 `gemini-3.1-flash-image` ("generalist workhorse"), Nano Banana Pro `gemini-3-pro-image` ("premium choice for complex visual tasks"), and legacy Nano Banana `gemini-2.5-flash-image` (Google recommends migrating to 2 Lite) — [Gemini API image-generation docs](https://ai.google.dev/gemini-api/docs/image-generation)
- Reference-image limits: 2 Lite takes 14 object images but no character or style references. NB2 takes 10 object, 4 character and 3 style references. Pro takes 6 object and 5 character references and has no separate style-reference slot — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
  - Conflict: the same docs also say "2 Lite does not support multiple reference inputs or multi-turn sequential editing", which contradicts the 14-object figure in their own table. Treat 2 Lite's multi-reference support as unclear — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
- Resolutions: 2 Lite is 1K only; NB2 supports 512px, 1K, 2K and 4K; Pro supports 1K, 2K and 4K. Aspect ratios for 2 Lite and NB2 are 1:1, 3:2, 2:3, 3:4, 4:3, 4:5, 5:4, 9:16, 16:9 and 21:9 — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
- Multi-turn conversational editing works through `previous_interaction_id`. "Thinking" is always on and makes interim composition images. Thinking tokens are billed. NB2 and 2 Lite have `minimal` and `high` thinking levels — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
- Web-search grounding works on all models. Image-search grounding works only on Gemini 3.1 Flash Image (NB2) — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
- "All generated images include a SynthID watermark" — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation)
- Batch API is supported at a 50% discount — [Gemini API pricing](https://ai.google.dev/gemini-api/docs/pricing)
- API pricing per image, standard/batch:
  - NB2: 512px $0.045/$0.022, 1K $0.067/$0.034, 2K $0.101/$0.050, 4K $0.151/$0.076
  - Pro: 1K/2K $0.134/$0.067, 4K $0.24/$0.12
  - 2 Lite: 1K $0.0336/$0.0168
  - Legacy 2.5 Flash Image: $0.039/$0.0195, deprecated and shutting down 2026-10-02
  - No free tier for image output on any of them
  - [Gemini API pricing](https://ai.google.dev/gemini-api/docs/pricing)
  - Conflict: Google's 2 Lite launch blog reportedly says "$0.034 per 1,000 images at 1K". The pricing page shows about $0.034 **per image**, so the blog figure looks like a typo or a bad summary — [Google blog, 2026-06-30](https://blog.google/innovation-and-ai/models-and-research/gemini-models/gemini-omni-flash-nano-banana-2-lite/) vs [pricing page](https://ai.google.dev/gemini-api/docs/pricing)
- NB2 release date is 2026-02-26 — [TechCrunch](https://techcrunch.com/2026/02/26/google-launches-nano-banana-2-model-with-faster-image-generation/); [Workspace Updates](https://workspaceupdates.googleblog.com/2026/02/introducing-nano-banana-2-in-gemini-app.html)
- Google-attributed claims (marketing): NB2 delivers about 95% of Pro's visual quality at 2–5x the speed and half the cost, takes 3–4 s per image, and in the Gemini app keeps up to 5 characters' resemblance and 10 objects' fidelity in one workflow — [Google blog: Nano Banana 2](https://blog.google/innovation-and-ai/technology/ai/nano-banana-2/); [Build with Nano Banana 2](https://blog.google/innovation-and-ai/technology/developers-tools/build-with-nano-banana-2/) (via search snippets)
- Nano Banana 2 Lite and Gemini Omni Flash (a video generation/editing model at $0.10 per output second, up to 10 s) launched 2026-06-30. 2 Lite is billed as having "reliable prompt adherence, strong character consistency and legible in-image text rendering" — [Google blog](https://blog.google/innovation-and-ai/models-and-research/gemini-models/gemini-omni-flash-nano-banana-2-lite/)
- Nano Banana Pro (Gemini 3 Pro Image) was released November 2025. Vendor comparisons credit it with "compositional precision, spatial control, and native 4K" — [ChatCut blog](https://chatcut.io/blog/gpt-image-2-vs-nano-banana-pro) (vendor, via snippet)
- Access surfaces are AI Studio, the Gemini API, Vertex AI / Gemini Enterprise Agent Platform, the Gemini app, and Search AI Mode — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation); [Google blog](https://blog.google/innovation-and-ai/models-and-research/gemini-models/gemini-omni-flash-nano-banana-2-lite/)
- LMArena text-to-image, 2026-09-21: gemini-3.1-flash-image is #9 (1260), gemini-3.1-flash-lite-image #13 (1250), gemini-3-pro-image-2k #14 (1246) — [arena.ai text-to-image](https://arena.ai/leaderboard/text-to-image)
- LMArena image-edit, 2026-09-21: gemini-3-pro-image-2k is #9 (1390), gemini-3.1-flash-image #12 (1388), gemini-3-pro-image-preview #13 (1385) — [arena.ai image-edit](https://arena.ai/leaderboard/image-edit)

### Inferences
- If the user's pipeline uses "Nano Banana" through the API with `gemini-2.5-flash-image`, it will break on 2026-10-02. They should move to NB2 (for quality and references) or 2 Lite (for cheap ideation).
- For an asset set, NB2 is the only Google model with explicit **style-reference** slots (3). That's the best fit for keeping dozens of ship and prop concepts in one style.
- Pro's lower leaderboard rank than NB2 doesn't mean it's worse at precise, multi-constraint edits. The arena measures general preference, not spatial fidelity.
- SynthID is invisible and survives on outputs. It shouldn't matter for textures or concept art used internally, but it's worth knowing for any shipped 2D asset.

### Gaps
- Rate limits (RPM, images/day) per tier weren't on the pages I fetched. The "900 images per hour" figure for NB2 comes from a secondary summary, so I left it unverified.
- Consumer Gemini app quotas for NB2 and Pro weren't retrieved.

## 3. Competitors and which wins for specific game-art tasks

### Takeaway
As of 2026-09-21, **OpenAI dominates both blind-vote arenas**: GPT Image 2.5 "Sunburst" and "Flare", then gpt-image-2, hold #1–3 on both text-to-image and image-edit. Google, ByteDance Seedream 5.0 Pro and Alibaba Qwen-Image 3.0 Pro cluster about 120–170 points lower. Vendor-blog comparisons agree on a rough split: **GPT Image for UI/HUD mockups, text, layouts and precise edits**; **Nano Banana for photographic lighting and atmosphere, native 4K and compositional control**; and for game illustration it's a style-preference coin-flip. Midjourney (V8.x) stays the aesthetic and moodboard tool but has weaker editing and reference plumbing. FLUX.2 [dev] is the leading open-weight option for local ComfyUI and LoRA pipelines. I found **no source that directly tests hard-surface spaceships or ortho sheets** across models.

### Cited Findings
**Leaderboards (independent, crowdsourced, general-purpose, not game-specific)**
- Text-to-image top 15 on 2026-09-21:
  1. gpt-image-2.5-sunburst 1423
  2. gpt-image-2.5-flare 1401
  3. gpt-image-2 medium 1381
  4. mai-image-2.6 (Microsoft) 1334
  5. grok-imagine-image-2.0 low 1302
  6. reve-2.1 1301
  7. muse-image (Meta) 1276
  8. reve-2.0 1270
  9. gemini-3.1-flash-image 1260
  10. seedream-5.0-pro 1256
  11. qwen-image-3.0-pro 1254
  12. mai-image-2.5 1254
  13. gemini-3.1-flash-lite-image 1250
  14. gemini-3-pro-image-2k 1246
  15. gpt-image-1.5-high-fidelity 1239
  - [arena.ai text-to-image](https://arena.ai/leaderboard/text-to-image)
- Image-edit top 10 on 2026-09-21, from 29.9M votes across 56 models:
  1. gpt-image-2.5-sunburst 1526
  2. gpt-image-2.5-flare 1482
  3. gpt-image-2 1461
  4. grok-imagine-image-2.0 1430
  5. mai-image-2.6 1429
  6. muse-image 1402
  7. mai-image-2.5 1400
  8. seedream-5.0-pro 1394
  9. gemini-3-pro-image-2k 1390
  10. grok-imagine-image-quality 1390
  - [arena.ai image-edit](https://arena.ai/leaderboard/image-edit)
- A secondary summary claimed "As of December 2026, GPT Image 1.5 dominates". That date is impossible, and the claim contradicts the live board. Don't use it — [WaveSpeed blog](https://wavespeed.ai/blog/posts/lm-arena-text-to-image-rankings-2026/) (via search snippet)

**OpenAI GPT Image**
- GPT Image 2 was released in April 2026: API and Codex on 04-21, ChatGPT on 04-22 across all plans. It reasons about the prompt before generating, which drives its gains in instruction following and text — [Wikipedia: GPT Image](https://en.wikipedia.org/wiki/GPT_Image); [MindStudio](https://www.mindstudio.ai/blog/what-is-gpt-image-2); [OpenAI Dev Community announcement](https://community.openai.com/t/introducing-gpt-image-2-available-today-in-the-api-and-codex/1379479)
- Resolution is reported as native 2K with 4K in beta, and aspect ratios from 3:1 to 1:3. "98% prompt accuracy" and "near-perfect text" are marketing-grade claims — [Picsart model page](https://picsart.com/ai-models/gpt-2/) (reseller, via snippet)
- ChatGPT Images 2.5 was released 2026-09-08:
  - Up to 50% lower latency than Images 2.0
  - A new @Sketch feature that turns in-chat drawings into references
  - Better preservation of subjects from reference photos, and more reliable multi-turn edit following
  - Two API models: **Flare** (fast default) and **Sunburst** (precise editing), both at $8/$30 per 1M input/output tokens
  - Available to all ChatGPT and Codex users
  - [Unite.AI](https://www.unite.ai/openai-releases-chatgpt-images-2-5-with-sketch-and-two-new-api-models/); [AI Weekly](https://aiweekly.co/alerts/openai-ships-chatgpt-images-25-with-sketch-input-cuts-latency-50-over-images-20); [OpenAI: Introducing ChatGPT Images 2.5](https://openai.com/index/introducing-chatgpt-images-2-5/)

**Head-to-head game-art comparison (vendor: SpriteCook resells both models; no bias disclosure; dated 2026-04-22, before Images 2.5)**
- UI/HUDs/menus: GPT-Image-2 wins on layout control, credited to its reasoning — [SpriteCook](https://www.spritecook.ai/blog/gpt-image-2-comparison)
- Text rendering: GPT-Image-2 wins — [SpriteCook](https://www.spritecook.ai/blog/gpt-image-2-comparison)
- Tiny 16–32px sprites: Nano Banana wins with a "checkerboard trick". The article is internally inconsistent about whether this was NB2 or Pro — [SpriteCook](https://www.spritecook.ai/blog/gpt-image-2-comparison)
- Character sprites, splash art and detailed illustration are ties. "Nano leans cartoonier, GPT leans more concept-art realistic." Run-to-run style variance "can be bigger than the gap between the models" — [SpriteCook](https://www.spritecook.ai/blog/gpt-image-2-comparison)
- A second vendor comparison says GPT Image 2 is the better first pick for text, structured layouts, UI mockups and multi-panel concepts. It says NB2 is better for photographic polish, natural light, product surfaces and environmental atmosphere — [PixVerse blog](https://pixverse.ai/en/blog/gpt-image-2-vs-nano-banana-2) (vendor, via snippet)
- Another vendor claims you can generate "10 variations for the cost of one Nano Banana Pro image" with GPT Image 2. That's unverified and conflicts with token-based pricing, so treat it as marketing — [ChatCut](https://chatcut.io/blog/gpt-image-2-vs-nano-banana-pro) (vendor, via snippet)

**Midjourney**
- The V8.0 alpha launched 2026-03-17. V8.1 became the default. **V8.2** adds an **Edit Model** that replaces Omni Reference, Character Reference and Retexture, and takes up to 4 reference images. Using Omni Reference in V8 silently falls back to V7. Style references, personalization and moodboards carry over from V7 — [Blake Crosley's Midjourney 8.2 guide](https://blakecrosley.com/guides/midjourney); [Creativity AI #79 newsletter](https://geekycuriosity.substack.com/p/creativity-ai-79-midjourney-makes); [Midjourney Omni Reference docs](https://docs.midjourney.com/hc/en-us/articles/36285124473997-Omni-Reference) (all via search snippets; the official Version page returned 403)
- Midjourney models don't appear in the LMArena top 15 for either category — [arena.ai](https://arena.ai/leaderboard/text-to-image)

**Black Forest Labs FLUX.2**
- Released 2025-11-25. It supports up to 10 reference images for character, product and style consistency, and edits at up to 4 MP. It's roughly a 32B architecture: a Mistral-3 24B VLM plus a rectified-flow transformer. Variants are [pro] and [flex] (hosted, proprietary) and [dev] (open weights; commercial use needs a BFL license) — [BFL blog: FLUX.2](https://bfl.ai/blog/flux-2); [VentureBeat](https://venturebeat.com/ai/black-forest-labs-launches-flux-2-ai-image-models-to-challenge-nano-banana); [The Decoder](https://the-decoder.com/black-forest-labs-launches-flux-2-with-a-new-multi-reference-feature/)
- NVIDIA published RTX/ComfyUI optimizations for FLUX.2, which supports local runs — [NVIDIA blog](https://blogs.nvidia.com/blog/rtx-ai-garage-flux-2-comfyui/)
- A "Flux 2 Flash" variant appears in a 2026 vendor comparison, but I didn't research its details — [MindStudio](https://www.mindstudio.ai/blog/artlist-studio-nano-banana-gpt-image-2-flux-2-flash-comparison)

**Others**
- Seedream 5.0 Pro (ByteDance) is #10 in T2I and #8 in edit. Qwen-Image 3.0 Pro (Alibaba) is #11 in T2I — [arena.ai T2I](https://arena.ai/leaderboard/text-to-image); [arena.ai edit](https://arena.ai/leaderboard/image-edit)
- An Ideogram 4.0 vs Nano Banana Pro vs GPT Image 2 comparison exists, but I didn't fetch it — [Imagine.art](https://www.imagine.art/blogs/ideogram-4-0-vs-nano-banana-pro-vs-gpt-image-2)

### Inferences (task-by-task, weakly evidenced; no direct ship or ortho test found)
- **Hard-surface sci-fi ship concepts (mood/silhouette exploration):** Midjourney V8.x or GPT Image 2.5 for aesthetics; NB2 or Pro for lighting and atmosphere. Keep exploration cheap with 2 Lite or Flare.
- **Iterative inpainting / "change only this part" edits:** GPT Image 2.5 Sunburst has the strongest independent signal (#1 image-edit by 44+ points), with Nano Banana Pro/NB2 behind it. This matters most for the art-director workflow of drilling into specific pieces.
- **UI mockups (HUD, menus):** GPT Image 2 / 2.5, per two vendor comparisons plus text-rendering claims.
- **Style consistency across a set:** NB2 (3 style-reference slots), Midjourney `--sref` and moodboards, FLUX.2 (10 references), or a trained LoRA locally. See section 4.
- **Livery, decals, textures:** there's no model-specific evidence. Text rendering (for markings and decals) favors GPT Image, and NB2's 4K output favors texture resolution.
- **Local/ComfyUI:** FLUX.2 [dev] is the modern open base. Commercial use needs a paid BFL license.

### Gaps
- **No test of hard-surface spaceship concepts or ortho turnaround sheets comparing models.** Every turnaround source I found is about characters.
- Krea and Recraft (vector/SVG, useful for UI icons and decals) weren't researched in this pass.
- Midjourney pricing and V8.x resolution/text abilities weren't verified (official page 403).
- GPT Image 2 per-image API pricing at each quality/resolution wasn't retrieved. Images 2.5 has token pricing only.
- There's no independent (non-vendor) practitioner comparison from game artists (80.lv, Game Developer, r/gamedev) in my results.

## 4. How practitioners keep a consistent art style across dozens of assets

### Takeaway
There are two families of approach. **Reference-anchored hosted models** feed the same style-reference images every time (NB2 style slots, Midjourney `--sref` and moodboards, FLUX.2 multi-reference, GPT Image reference photos). **Trained local style models** mean a LoRA on your own approved assets, with a frozen checkpoint, LoRA weights, sampler, CFG, seed and negative prompt. The advice common to both is to lock the style or identity early, write the exact configuration down, and not change it mid-production. Sources here are mostly vendor and SEO blogs, so treat the numbers as anecdotal.

### Cited Findings
- To keep consistency, document the exact configuration (checkpoint, LoRAs, weights, sampler, CFG), don't change it mid-production, use consistent negative prompts, save seeds, and batch-generate with the same settings — [Flowith blog: indie studios and Civitai](https://flowith.io/blog/how-indie-game-studios-use-civitai-custom-ai-art-styles/) (vendor, via snippet)
- A custom LoRA trained on a game's own art style can produce concept art in that style for fast iteration on designs, enemies and environments — [Flowith](https://flowith.io/blog/how-indie-game-studios-use-civitai-custom-ai-art-styles/) (via snippet)
- Scenario (a game-asset platform) trains custom LoRAs from 5–100 reference images, using the team's own art bible or style references — [Sonilo: best AI game asset generators 2026](https://sonilo.com/blog/comparisons/best-ai-game-asset-generators) (via snippet; attribution within the result set is approximate)
- Claim: "Teams that locked character identity early and used reference-anchored generation delivered final assets 3x faster than those relying on prompt-only methods." It's unsourced, so treat it as anecdotal — [Lovart guide](https://www.lovart.ai/blog/complete-guide-consistent-ai-character-design) (via snippet)
- An article from 2026-09-05 discusses when to train a LoRA versus relying on one reference image — [Nerdbot](https://nerdbot.com/2026/09/05/from-one-reference-image-to-a-reusable-character-asset/) (not fetched)
- Built-in style plumbing: NB2 takes 3 style-reference images and 10 object references per call — [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation). FLUX.2 takes up to 10 references for "character and style consistency" — [BFL](https://bfl.ai/blog/flux-2). Midjourney keeps style references, personalization and moodboards in V8.x — [Blake Crosley guide](https://blakecrosley.com/guides/midjourney)
- A single-model comparison found run-to-run style variance can exceed the difference between models. That argues for fixed references over prompt-only style control — [SpriteCook](https://www.spritecook.ai/blog/gpt-image-2-comparison)

### Inferences
- For this user, the natural "style anchor" is their own Blender renders: the hero ship with its livery, plus the procedural skybox. Feeding those as style/object references (NB2 style slots or GPT Image 2.5 references) ties new concepts to what's actually in the game, rather than to earlier ChatGPT concept PNGs.
- A written "style bible" prompt block (palette, materials, panel-line density, lighting, "no" list) that GPT-6 Astra keeps and injects into every prompt is the LLM-driver version of the configuration lock above.
- Given the user's stack, a LoRA/ComfyUI setup is heavier than needed until the asset count is large.

### Gaps
- I found no independent, quantified study of style drift across dozens of assets for hosted models.

## 5. Known failure modes (ortho views, cross-view inconsistency, tiling, "AI look")

### Takeaway
Evidence here is thin. The one concrete practitioner tip I found is to **force "orthographic projection" / "flat view"** in the prompt to avoid perspective distortion in turnaround sheets. Turnaround tooling (Scenario, 3D AI Studio, NB-based generators) is almost entirely character-focused. I found no sourced write-up on symmetry and cross-view detail mismatches for mechanical or hard-surface objects, or on the "over-rendered AI look" in 2026 models.

### Cited Findings
- To keep turnaround sheets accurate enough for modeling, explicitly force "orthographic projection" or "flat view" in the prompt to avoid perspective distortion — [PrintPal blog: Nano Banana for 3D model generation](https://blog.printpal.io/nano-banana-for-3d-model-generation-best-prompts-tips-and-workflows/) (via snippet)
- Nano Banana can output standard turnarounds (front, side, back), expression sheets and equipment breakdowns — [PrintPal blog](https://blog.printpal.io/nano-banana-for-3d-model-generation-best-prompts-tips-and-workflows/) (via snippet)
- Turnaround tooling is character-first: 3D AI Studio's Character Sheet Generator makes a 4-angle grid from one image — [3D AI Studio docs](https://docs.3daistudio.com/image-studio/character-sheet). Scenario markets "consistent multi-view" character turnarounds — [Scenario blog](https://www.scenario.com/blog/generate-character-turnarounds-scenario)
- For AI tileable textures, 80.lv has a tutorial — [80.lv: Using AI to create seamless tilable textures](https://80.lv/articles/tutorial-using-ai-to-create-seamless-tilable-textures) (not fetched; date unknown, possibly pre-2025)

### Inferences
- The need to prompt-force "orthographic" implies these models default to perspective. For a ship whose front, side and top views must match, the practical workaround is to generate one hero view, then do the other views as edits of it (multi-turn or reference). Even then, check each detail across views before modeling. This is an inference from how the tools are described, not a sourced test.
- For this user, who already has a Blender hero ship, the more reliable direction is **Blender first, AI on top**. Render ortho views from the actual model, then use AI edits (livery, greebles, damage variants, concept paintovers) on those renders. That avoids the cross-view consistency problem entirely. This is also an inference.

### Gaps
- No sourced practitioner reports on symmetry errors, left/right mismatches, or inconsistent greebles across AI ortho views of vehicles or ships.
- No sourced 2025–2026 discussion of the "over-rendered AI look" for specific models. A search summary noted that a tile can repeat cleanly and still carry an obvious recurring feature, and that textures need scale and repetition review in the target engine, but I couldn't pin that to a specific page, so it's unattributed.
- No sourced evaluation of AI-generated **paint masks / livery** workflows. The user's existing approach isn't benchmarked anywhere I found.
- Reddit (r/gamedev, r/StableDiffusion, r/blender) and YouTube concept-to-3D creators didn't show up in my searches. That's a coverage gap from this pass, not evidence that such content doesn't exist.
