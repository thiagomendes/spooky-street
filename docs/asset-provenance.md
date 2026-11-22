# Asset provenance

| Asset | Source | License / attribution |
|---|---|---|
| All C# under `Assets/SpookyStreet` | Written for this project | Project-owned |
| Greybox geometry, lights, post-processing profile, Pip's primitive body | Generated at runtime from Unity primitives and code | Project-owned |
| UI sprites (rounded paper card, disc, ring, pause, jump chevron, stick arrows, globe, reading fade) | Drawn procedurally by `Runtime/UI/IconFactory.cs`; no binary committed | Project-owned |
| Placeholder app icon (twilight sky, owl house, round attic window, moon) | Painted procedurally by `Core/Art/AppIconPainter.cs` at build time; not committed as a binary | Project-owned |
| `Content/Resources/Fonts/Andika-Regular.ttf`, `Andika-Bold.ttf` (UI reading text) | `github.com/google/fonts`, `ofl/andika` | SIL Open Font License 1.1, © SIL International; licence in `Content/FontLicenses/Andika-OFL.txt`. Reserved Font Names "Andika", "SIL": not modified or renamed |
| `Content/Resources/Fonts/Sniglet-ExtraBold.ttf` (title and headings) | `github.com/google/fonts`, `ofl/sniglet` | SIL Open Font License 1.1, © Haley Fiege, Brenda Gallo, Pablo Impallari; licence in `Content/FontLicenses/Sniglet-OFL.txt` |
| Built-in UI font (`LegacyRuntime.ttf`) | Unity built-in resource | Fallback only, if a bundled font fails to load |
| `ProjectSettings/*.asset` (Audio, Dynamics, Editor, Input, Memory, NavMesh, Physics2D, Preset, Time, URPProject, VersionControl, Graphics, Quality templates) | Derived from `Unity-Technologies/Graphics` @ `6000.3/staging`, `Tests/SRPTests/Projects/BatchRendererGroup_URP`, then edited | Unity Companion License (settings templates); edited values project-owned |
| `ci/unity/container.sh` activation sequence | Modelled on GameCI `unity-builder` v4.8.2 ubuntu steps | MIT (GameCI); re-implemented, not copied verbatim |
| `Content/Resources/Models/Characters/Pip/` (Pip.fbx rig, 12 clips, baked BaseColor/Normal/Face textures) | Built from scratch by `tools/blender/chars/*` and `tools/blender/export/pip_*.py` (headless Blender 4.0.2); review sheet `docs/evidence/s000.2/pip_game_review.png` | Project-owned (original, spec v1.3) |
| `Content/Resources/Models/Environment/` `OwlHouseInterior`, `WorkshopMechanism`, `ShedRamp`, `SillBridge`, `TreehouseCabin`, `ClothSnag` (FBX) and the `OwlHouseInterior_*`, `WorkshopMechanism_*`, `Routes_*` textures | Built from scratch by `tools/blender/env/interior.py`, `routes.py` and `env_export_interior.py` (headless Blender 4.0.2); review sheet `docs/evidence/s002/interior_review.jpg` | Project-owned (original, spec v1.3) |
| Imported third-party art, audio | None | — |
