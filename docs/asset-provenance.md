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
| `Content/Resources/Models/Halloween/*.fbx`, `halloweenbits_texture.png` (pumpkins, lanterns, fence, bench, candles, trees) | KayKit **Halloween Bits 1.0** by Kay Lousberg, `github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0`, `Assets/fbx(unity)` | CC0 1.0 (`Content/ThirdPartyLicenses/KayKit-Halloween-Bits-LICENSE.txt`); credit appreciated: Kay Lousberg, www.kaylousberg.com. Unmodified files; scaled and placed in code (`World/StreetDressing.cs`) |
| `Content/Resources/Models/Characters/Pip/Pip.fbx`, `pip_cape.png`, `pip_fuzz.png` | Built by `tools/blender/build_pip.py` (Blender 4.0.2) from KayKit **Character Pack: Adventures 1.0** `Rogue_Hooded.glb` by Kay Lousberg, `github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0` | CC0 1.0 (`Content/ThirdPartyLicenses/KayKit-Character-Pack-Adventures-LICENSE.txt`). Modified: weapons removed; atlas recoloured (green → amber cape, cream fuzz); original moth antennae and wings added; 18 of 76 animations kept. Modifications project-owned |
| Audio | None yet | — |
