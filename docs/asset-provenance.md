# Asset provenance

| Asset | Source | License / attribution |
|---|---|---|
| All C# under `Assets/SpookyStreet` | Written for this project | Project-owned |
| Greybox geometry, lights, post-processing profile, Pip's primitive body | Generated at runtime from Unity primitives and code | Project-owned |
| UI sprites (rounded paper card, disc, ring, pause, jump chevron, stick arrows, globe, reading fade) | Drawn procedurally by `Runtime/UI/IconFactory.cs`; no binary committed | Project-owned |
| Placeholder app icon (twilight sky, owl house, round attic window, moon) | Painted procedurally by `Core/Art/AppIconPainter.cs` at build time; not committed as a binary | Project-owned |
| Handcrafted sky: sky paint, star mask, paper moon, paper grain, cut-paper clouds and bats, halo, shooting star, `SkyDome`/`SkyPaper` shaders | Painted and cut procedurally at load by `Core/Art/SkyPainter.cs`, `Core/World/SkyShapes.cs` and `Runtime/World/SkyMeshes.cs`; shaders written for this project in `Art/Shaders/`; no binary committed. Review renders: `tools/sky_preview/`, `docs/evidence/s000.7/` | Project-owned (original, spec v1.3) |
| `Content/Resources/Fonts/Andika-Regular.ttf`, `Andika-Bold.ttf` (UI reading text) | `github.com/google/fonts`, `ofl/andika` | SIL Open Font License 1.1, © SIL International; licence in `Content/FontLicenses/Andika-OFL.txt`. Reserved Font Names "Andika", "SIL": not modified or renamed |
| `Content/Resources/Fonts/Sniglet-ExtraBold.ttf` (title and headings) | `github.com/google/fonts`, `ofl/sniglet` | SIL Open Font License 1.1, © Haley Fiege, Brenda Gallo, Pablo Impallari; licence in `Content/FontLicenses/Sniglet-OFL.txt` |
| Built-in UI font (`LegacyRuntime.ttf`) | Unity built-in resource | Fallback only, if a bundled font fails to load |
| `ProjectSettings/*.asset` (Audio, Dynamics, Editor, Input, Memory, NavMesh, Physics2D, Preset, Time, URPProject, VersionControl, Graphics, Quality templates) | Derived from `Unity-Technologies/Graphics` @ `6000.3/staging`, `Tests/SRPTests/Projects/BatchRendererGroup_URP`, then edited | Unity Companion License (settings templates); edited values project-owned |
| `ci/unity/container.sh` activation sequence | Modelled on GameCI `unity-builder` v4.8.2 ubuntu steps | MIT (GameCI); re-implemented, not copied verbatim |
| `Content/Resources/Models/Characters/Pip/` (Pip.fbx rig, 15 clips, baked BaseColor/Normal/Face textures) | Built from scratch by `tools/blender/chars/*` and `tools/blender/export/pip_*.py` (headless Blender 4.0.2); review sheet `docs/evidence/s000.2/pip_game_review.png` | Project-owned (original, spec v1.3) |
| `Content/Resources/Models/Environment/` `OwlHouseInterior`, `WorkshopMechanism`, `ShedRamp`, `SillBridge`, `TreehouseCabin`, `ClothSnag` (FBX) and the `OwlHouseInterior_*`, `WorkshopMechanism_*`, `Routes_*` textures | Built from scratch by `tools/blender/env/interior.py`, `routes.py` and `env_export_interior.py` (headless Blender 4.0.2); review sheet `docs/evidence/s002/interior_review.jpg` | Project-owned (original, spec v1.3) |
| Sound set S003 (`docs/evidence/s003/listening/audio/*.mp3`, spectrograms, WAV masters regenerated into `tools/audio/out/`) | Synthesised from code by `tools/audio/` (numpy/scipy: modal, plucked-string, mallet, shaped-noise and formant models); no recordings, samples or sound packs (D20) | Project-owned (original, spec v1.3) |
| Babble voices v2 and music v2 (`docs/evidence/s003/listening_v2/`, sources in `tools/audio/generated/`); **for review only, not in the game** | Source speech from Google Gemini TTS (`gemini-3.8-flash-tts`; prebuilt voices Laomedeia, Zephyr, Sadaltager, Achernar, Sulafat) reading made-up syllables. Music from Google Lyria (`lyria-3-pro-preview`, served as `lyria-3.5`, and `lyria-3-clip-preview`). Cut, pitched, sped up, looped and mastered by `tools/audio/` (D20.1). Prompts, seeds and response ids are in `tools/audio/generated/manifest.json` | Generated output under the Gemini API terms (see below); the post-processing is project-owned. Owner approval and a terms check are needed before release |
| Imported third-party art, audio | None | — |

## Generated audio and the Gemini API terms (D20.1, checked 2026-11-23)

**What could be read.** The live terms could not be fetched from the build session, because the network blocks
`ai.google.dev` and `policies.google.com`. This summary comes from web-search snippets of archived versions of
the Gemini API Additional Terms of Service. The owner should read the current page,
<https://ai.google.dev/gemini-api/terms>, before any generated audio ships.

**What the archived terms said.**

- **Ownership.** The 17 October 2025 version says Google does not claim ownership of original content the Services
  generate, and that Google may generate the same or similar content for others. So the outputs are usable, but
  they are not exclusive.
- **Responsibility.** The 7 October 2025 version makes the user responsible for how generated content is used. It
  also extends the user's content licence to prompts and responses as far as applicable law requires.
- **Free tier.** On unpaid (free-tier) use, Google may use prompts and responses to improve its products. Our prompts
  hold no personal data: only character directions, made-up syllables and music descriptions.
- **Age.** The archived 2 May 2024 version required API users to be 18 or older. It also barred using the Services
  "as part of a website, application, or other service … directed towards or likely to be accessed by individuals
  under the age of 18". It could not be confirmed whether this clause is still in force.

**How we use the service.** We call it only at development time, from build tools. The game never calls the API,
and the audio is baked in. Even so, this clause matters for a children's app. **Before release, the owner should
confirm that the current terms allow shipping offline generated audio in a Kids-category app.** If they do not, the
code-synthesised S003 set remains the fallback.

**Watermark.** Google describes Lyria and Gemini audio as watermarked with SynthID. This was not verified here, and it
does not change how the audio is used.

**Cost.** No money was spent: all calls went through the owner's key. Call counts are in
`tools/audio/generated/manifest.json`.
