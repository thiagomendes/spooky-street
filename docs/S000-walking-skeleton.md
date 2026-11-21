# S000 — Walking skeleton

**Outcome.** A fresh checkout opens in Unity 6000.3.28f1 and plays title → walk → pause in a landscape
URP scene, and the same commit can be exported to Xcode on a GitHub-hosted Linux runner, then signed and
uploaded to TestFlight from a GitHub-hosted macOS runner, with no local machine involved.

**Status: verified in Unity CI and delivered to TestFlight (0.0.1 build 3.1, `VALID`); physical iPhone launch pending owner feedback.** See *Evidence*.

## Assumptions (state before work)

1. The repository was empty apart from the spec, so a new minimal Unity project lives at the repo root.
2. No Unity editor, Mac, signing credentials or iPhone are reachable from the cloud session; Unity hosts
   (`packages.unity.com`, Unity download CDN) are blocked and Docker Hub pulls were rate-limited, so no Unity
   compile could run here. Unity-dependent checks run in GitHub Actions once the licence secrets exist.
3. The owner uses a Unity **Personal** licence. CI activates it with email + password through Unity's licensing client (verified by the licence probe); Plus/Pro works via `UNITY_SERIAL`.
4. The owner has, or will obtain, an Apple Developer Program membership. This work spends nothing and
   enrolls in nothing.
5. GitHub Free private repository: macOS minutes are 10× and capped by an Actions budget the owner sets.

## Scope delivered

| Area | Files |
|---|---|
| Pinned project | `ProjectSettings/ProjectVersion.txt` (6000.3.28f1), `Packages/manifest.json`, `ProjectSettings/*` |
| Landscape URP scene | `Assets/SpookyStreet/Scenes/LanternLane.unity`, `Assets/SpookyStreet/Settings/URP-Mobile*.asset`, Graphics/Quality settings |
| Localizable title and first prompt | `Content/Resources/Localization/en.json` (semantic keys + translator context), `Core/Localization/*`, `Runtime/Localization/*`; dev-build pseudo-language toggle (+35 %) |
| Input | `Runtime/Controls/GameInput.cs` (one action map for keyboard/mouse + gamepad), `TouchControls.cs` (floating stick, edge-triggered Jump, orbit drag, left-handed mirror flag) |
| Player capsule + motor | `Runtime/Player/*`, `Core/Movement/MovementModel.cs` (4 m/s, 0.15 s / 0.12 s, 1 m apex, 0.10 s coyote, 0.12 s buffer, frame-rate-independent), `Config/PipMovementTuning.asset` |
| Camera | `Runtime/Camera/CameraRig.cs` (Cinemachine 3 Third Person Follow, 3.8 m, boom shortens against walls), `Core/Movement/CameraOrbitModel.cs` (manual orbit wins, delayed auto-recenter) |
| Safe floor | `Runtime/World/GreyboxBuilder.cs` (44 × 36 m floor, arrival porch with steps, hedges, owl-house silhouette with round attic window), `SafeGroundTracker.cs` (fall → last safe ground < 2 s) |
| Title / pause | `Runtime/UI/GameUi.cs`, `Core/Flow/GameFlow.cs`; pause stops time, audio and gameplay input; backgrounding opens paused |
| Batch build | `Editor/BuildCommand.cs` (contract v1, `-sss*` flags), `Editor/ProjectChecks.cs` |
| CI | `.github/workflows/ci.yml`, `.github/workflows/ios-testflight.yml`, `ci/unity/*`, `ci/ios/*`, `ios/fastlane/Fastfile`, `ios/Gemfile(.lock)`, `tools/ci/*` |
| Tooling | `tools/unity_meta.py`, `tools/check-local.sh`, `tools/core-tests/` |

Out of scope (later increments): the block, interactables and Use verbs, facts/saves, notebook, hints,
settings UI, language-pack downloads, audio, Android CI.

## Evidence

### Ran in the cloud session (2026-11-21)

| Check | Command | Result |
|---|---|---|
| Engine-free core tests (flow, movement at 30/60 fps, coyote/buffer, stick, orbit, localization, pseudo, build args) | `dotnet test tools/core-tests` (.NET 8.0.134, NUnit 3.14) | **46 passed, 0 failed** |
| Project structure (version pins vs image tags, digest pins, settings invariants, GUID references, asmdefs, engine-free Core, keys vs table, hard-coded UI text, secret material) | `python3 tools/ci/validate_project.py` | 0 problems; seeded faults (version drift, input handler, broken scene GUID, hard-coded text) were all reported |
| .meta completeness / GUID uniqueness | `python3 tools/unity_meta.py --check` | pass |
| Workflow lint | `actionlint` 1.7.12 | pass |
| Shell lint | `shellcheck` 0.11.0 | pass |
| YAML lint | `yamllint` 1.38.0 | pass |
| Fastlane lane | `ruby -c`; every action/option checked against fastlane 2.242.0 `available_options` | pass |
| Artifact provenance | synthetic export archive → `write_provenance.py` → `verify-artifact.sh` | exec bits + symlinks kept; wrong commit and tampered archive rejected |
| Unity Personal activation in CI | *Unity licence probe* runs 1–2 (GitHub Actions, image 6000.3.28f1) | Hub entitlement file rejected (hardware-bound); `--activate-all --include-personal` and `--activate-ulf` both licensed the editor with iOS entitlement; seat released |

### Ran in GitHub Actions

| Check | Run | Result |
|---|---|---|
| Unity 6000.3.28f1 compile + EditMode + PlayMode (`ci.yml`, image `ubuntu-6000.3.28f1-base-3`) | CI run 1, commit `a5aea60`, 2026-11-21 | Personal seat activated with email + password and released; **EditMode 50/50, PlayMode 7/7 passed**, 0 compile errors. Unity generated `packages-lock.json`, URP global settings and re-serialized settings (committed on `fix/s000-unity-ci`). |
| Same, with the generated files committed | CI run 4, commit `6247aff` | **EditMode 50/50, PlayMode 7/7** (5 `WalkingSkeletonTests` + 2 Input System `IntegrationTests`); only `ShaderGraphSettings.asset` still generated, now committed. |

| iOS export → signed archive → App Store Connect upload → processing | *Build for TestFlight* run 3 (`44934519952`), commit `07ea2ce`, 2026-11-21 | Unity 6000.3.28f1 iOS export 2m46s (Linux, cached Library); provenance verified on macOS-15; Xcode 26 selected; fastlane 2.242.0 signed with the Apple Distribution certificate + App Store profile and uploaded; **version 0.0.1 build 3.1 processed `VALID`** in ~2 min. |

Earlier attempts and their fixes: run 1 failed in preflight (credential-less `git fetch` on the private repo); run 2 built and signed the IPA but App Store Connect rejected it with *Missing app icon (1024×1024)*, fixed by the generated icon (`Core/Art/AppIconPainter.cs`).

### Not yet run — needs the named dependency

| Check | Blocked on | How to verify |
|---|---|---|
| **S000 pass: launch on the physical iPhone** | Owner's iPhone + TestFlight | Install the build from TestFlight. Report: build number, iPhone model, iOS version, and whether: title shows "Spooky Street / Three Knocks on Lantern Lane"; *New game* starts on the porch facing the house with the round attic window visible; the first prompt tag above the stick ghost reads "Drag here to walk."; left-thumb drag walks and the prompt disappears; Jump works; dragging the right side orbits; Pause opens the pause menu; leaving the app and returning shows the pause menu; no control sits under the notch/home indicator. Time-to-control from a cold start (target ≤ 8 s). |

No device or TestFlight pass is claimed from editor or unit results.
