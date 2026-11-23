# S003a stream B: testimony gestures and hint glances

Branch `feature/s003a-gestures`. Roadmap §1 (S003a) and §1.1, narrative bible §6 order 3 ("More character
actions"). This adds six clips to the five character models, the clip names the game cues them by, and one small
prop: Mr. Vale's wristwatch. Case content and the cutscenes are untouched (the cutscene pass is another stream).

## The clips

All are one-shots at 30 fps, in place, starting and ending on frame 0 of the character's rest loop, authored as
pose maths in `tools/blender/export/` like every other clip.

| Model | Clip (`CharacterClips` / `PipClips`) | Frames | Seconds | What happens | Serves |
|---|---|---|---|---|---|
| Bea | `ShadeEyes` | 66 | 2.2 | She pops up on her toes, right hand flat over her eyes like a visor, left hand planted on her pumpkin, head back; searches left, stretches, searches right, stretches; drops back. Hand on the brow frames 11–50. | `c01.line.bea.saw_cape`, `bea.repeat` ("Something silver flashed up high", "Up high, over the hedge. Then it was gone!") |
| Mr. Vale | `CheckWatch` | 72 | 2.4 | Seated. The left forearm comes up across his chest, watch to his eyes; he leans in and reads it; two small beats of the wrist with a nod each (frames 27, 35); then he looks up at the listener and rolls the wrist over to show the dial (frames 41–56); lowers it. | `c01.line.vale.repeat`, `vale.was_shut` ("Not one creak from that door all evening", "It was shut when I looked") |
| June | `Listen` | 66 | 2.2 | Right hand cupped behind her ear, out through the cape; she leans that ear out and holds still, leans a little further (frame 30); her eyes come back to the listener (frames 41–52); the hand comes down. | `c01.line.june.listen_floor`, `june.greet` ("I came down to find the knocks. Listen…", "Did you hear the knocks too?") |
| Aunt Kit | `Ponder` | 66 | 2.2 | Left hand to her cheek, elbow out, weight on one hip, head tilted into the hand; two small taps (frames 27, 35); her head tips the other way and she looks up, asking (frames 40–53); the hand comes down. The lantern stays low. | `c01.line.kit.ask` ("So, detective, what happened here?") |
| Pip | `GlanceLeft` | 40 | 1.33 | A tiny duck the other way, then head and shoulders snap round to her left (face 89° off straight ahead), up on her toes, leaning after her nose, free hand coming up; a second push of the chin (frame 18); back. Peering frames 9–28. | Hint tier 1 and the stuck-deduction nudge (`mystery-first.md` §6): "that way", toward an area |
| Pip | `GlanceRight` | 40 | 1.33 | The same to her right; on this side the pail arm lifts out and the pail swings. | as above |

Pip had no glance before (`pip_anims.py` had fifteen clips; the tier-1 head turn in `HintGuide` is a procedural
turn of a witness's head). June and Kit get one gesture each because their lines call for one; nothing else was
added, to keep the set small.

### Why none of them gives an answer (D26, bible §5)

- `ShadeEyes` shows where Bea was looking, which her line already says, and that she is searching: she saw a flash,
  not what made it. It aims along her own forward and at no object.
- `CheckWatch` is about when. It points at nothing.
- `Listen` leans to June's own side, not toward the floor, the grille or any place, and it does not beat the
  knock rhythm (the rhythm is a clue: one slow lean, no taps).
- `Ponder` is a question. Kit asks; she never tells.
- The glances turn Pip toward a side. The clip cannot point at a thing: the game picks left or right from where the
  hint's **area** lies (`PipClips.GlanceToward`).

## Mr. Vale's watch

His gesture needs something on that wrist, and a mummy's bandaged wrist with nothing on it read as "fiddling with
the cauldron" on the first review sheet. He now wears a chunky watch on his left forearm: a dark strap and case, a
pale dial, two hands at ten past ten.

- It is its own mesh, `Vale_Watch` (180 triangles), on its own bone, `Watch` (child of `LowerArm_L`). The bone turns
  about the forearm with the hand, because the rig has no forearm twist and a watch that stayed put while the wrist
  turned over faced the wrong way (second review sheet).
- **No texture changed.** The watch's UVs sit inside flat 16-pixel squares that `Vale_BaseColor.png` already has
  (the dark at uv 0.699, 0.418 and the pale bandage colour at 0.879, 0.145), where `Vale_Normal.png` is flat too.
  `witness_export.flat_texel` finds them in the committed textures at export time.
- The game needs no change for it: `NpcAvatar` gives every mesh that is not `*_Face` the atlas material.
- It shows in his old clips too (on the thigh in `SitIdle`, on the raised wrist in `Wave`); see
  `docs/evidence/s003a/Vale_existing.jpg`.

## How the hands are placed

`tools/blender/export/gesture_fit.py` (new, pure maths): `fit_hand` turns the arm and wrist until the knuckles are
at a point **and** the hand faces the way the gesture needs, in one damped least-squares solve; an optional elbow
hint picks the arm's swivel. Each gesture fits the hand to this frame's head or chest (brow, ear, cheek, in front
of the breastbone), so the hand rides the head through the search or the tilt instead of sliding over it.

## Review (rendered and looked at before the files went into the game)

`tools/blender/export/gesture_review.py` (new) renders one sheet per gesture from the **re-imported FBX files the
game loads**, with the committed textures: eight frames from the front three-quarter, the same eight from the
other side (from behind for Pip), four frames at the gameplay camera (3.8 m, 50° vertical, phone-shaped frame, as
`PipMovementTuning`), three close-ups of the hand, and the last frame beside the rest loop's frame 0.

| Sheet | What it shows |
|---|---|
| `docs/evidence/s003a/Bea_ShadeEyes.jpg` | Bea |
| `docs/evidence/s003a/Vale_CheckWatch.jpg` | Mr. Vale and the watch (dial up while he reads it, toward the listener at frame 48) |
| `docs/evidence/s003a/June_Listen.jpg` | June |
| `docs/evidence/s003a/Kit_Ponder.jpg` | Aunt Kit |
| `docs/evidence/s003a/Pip_GlanceLeft.jpg`, `Pip_GlanceRight.jpg` | Pip, second row and the game camera from behind |
| `docs/evidence/s003a/<Name>_existing.jpg` (five) | key frames of every clip each character already had, after the change |

What the iterations fixed, by looking:

- **Bea.** First pass: the left arm hung out to the side like a ta-da. Now the hand is planted on the pumpkin.
- **Vale.** First pass: the hand crossed to his right hand and read as touching the cauldron handle; no watch.
  Second: the watch strap was sized from the loose bandage end and floated like a hoop, and the dial faced forward.
  Third: the dial faced his eyes but the listener never saw it. Now the forearm is higher and left of centre, the
  strap is sized from the forearm alone, and he rolls the dial toward the listener when he looks up.
- **June.** First pass: the hand was in front of her cheek, like a wave. Second: buried in her hair behind the ear.
  Now it stands beside the hair at the ear, palm forward, and the lean and head tilt are larger.
- **Kit.** First pass (hand under the chin): charming close up, but the whole arm was hidden under the shawl, so
  nothing changed in her silhouette at play distance. Second (hand to cheek, elbow far out): the shawl's wing
  lifted and showed too much of the dark bodice. Now the elbow is forward and lower: the white forearm stands in
  front of the shawl up to her cheek.
- **Pip.** First pass turned 78° and the right glance barely changed her outline from behind (her bob hides her
  face). Now 89° with a clear lean, the free hand up on the left glance and the pail arm out on the right glance.

Known limits, accepted:

- Eyes are part of the face maps and do not move: every "look" is the head.
- Mr. Vale's two beats are small at play distance; the raised forearm, the bowed head and the dark watch carry it.
- Kit's forearm passes through the hem of the shawl's wing near her elbow (the shawl is one rigid wing per arm).
- June's fingertips touch the edge of her hair.
- From straight behind, Pip's right glance reads by her lean and the pail arm more than by her face.

## Nothing else in the models changed

`tools/blender/export/pip_fbx_diff.py` compares the committed FBX (before) with the new one. It now also compares
without vertex order and lists added bones and meshes.

| Model | Meshes, UVs, skin weights | Skeleton | Old clips | New |
|---|---|---|---|---|
| Bea | identical, vertex for vertex | 28 bones identical | Idle, Talk, Wave, Point: curve difference 0 | `ShadeEyes` |
| June | identical | 32 identical | Idle, Talk, KneeTap, Wave, Point, Walk: 0 | `Listen` |
| Kit | identical | 32 identical | Idle, Talk, OpenBooth, Wave: 0 | `Ponder` |
| Pip | identical | 34 identical | all fifteen: 0 | `GlanceLeft`, `GlanceRight` |
| Vale | same model, vertices in another order (every corner, position + UV, and every weighted vertex matches) | 29 identical, + `Watch` | SitIdle, SitTalk, Wave: 0 for the old bones, plus curves for `Watch` | `CheckWatch`, mesh `Vale_Watch` |

(The diff imports with Blender, which drops leaf bones, so it counts 28 of Bea's 33 bones and so on.)

How: the saved scenes of the original exports no longer exist, so each character was rebuilt from scratch into a
scratch folder. A rebuild is the same skeleton bone for bone and the same clips curve for curve, but not the same
mesh: Bea, June and Vale come out with their vertices in another order, and Pip and Kit with other triangles and
UVs than the committed textures fit (their decimation does not repeat exactly). So the rebuilt mesh was **not**
used for those four:

- `tools/blender/export/fbx_add_takes.py` (new) copies only the new takes (animation stack, layer, curve nodes,
  curves, connections) from the rebuilt export into the committed FBX, matching bones by name. Bea, June, Kit and
  Pip were made this way. The diff of the fresh export against the result shows the new takes' curves identical.
- Vale needed a new bone and mesh, so his FBX is the full rebuilt export (reordered, as above). His textures were
  not replaced: the rebuilt bake differs from the committed one on a handful of pixels by at most 4/255.

No texture and no `.meta` changed. The FBX metas still list the old clips; `ModelImportRules.OnPreprocessAnimation`
rebuilds the list from the takes on import, as for Bea's `Point` and Pip's climb clips.

## In the game

- **Clip names.** `CharacterClips.ShadeEyes`, `CheckWatch`, `Listen`, `Ponder`; `CharacterClips.TestimonyGestures`
  and `TestimonyGestureOf(model)`; `PipClips.GlanceLeft`, `GlanceRight` (appended to `PipClips.All`, so the
  animator's mixer inputs keep their order). All are one-shots (`IsLooping` false).
- **Cueing a witness.** `Witness.PlayGesture(clip, aim)` as before, so `CastCue.Gesture(at, actor, clip, point)`
  and a vignette's `Gesture` cue work with the new names.
- **Turning.** `CharacterClips.TurnsToAim(clip)` is false for `CheckWatch`, `Listen` and `Ponder`:
  `PlayGesture` plays those without turning the body, whatever point the cue carries. They aim nowhere, and turning
  would spin Mr. Vale on his bench. `ShadeEyes`, `Point` and `Wave` turn as before, so cue `ShadeEyes` with the
  point Bea should face (only its direction on the ground is used).
- **Missing or unknown clip.** `PlayGesture` returns false and changes nothing, without logging, for a clip the
  model lacks, null or empty. `NpcAvatar.PlayOneShot` and `PipAnimator.PlayOneShot` already did. Nothing stands in
  for a glance (`PipClips.Fallback` is null).
- **Pip's glance.** `PipClips.GlanceToward(forward, to)` picks the clip from the side a place lies on, and
  `GlanceAngle` gives the signed angle (a glance reads best 30° to 150° to one side). `PipAnimator.PlayGlance(point)`
  plays it while she stands; walking off (over 0.6 m/s), a jump or a fall ends it.
- **Not wired, on purpose.** Nothing calls `PlayGlance` or cues the new gestures yet: hints, lines and cutscenes
  belong to streams C and D.

Notes for the cutscene and wiring pass:

- `Witness` is disabled by a cutscene `Loop` cue, and a disabled `Witness` does not turn the body for a gesture
  (existing behaviour). `ShadeEyes` after a `Loop` cue plays facing wherever Bea already faces.
- In `ShadeEyes` Bea's head is tipped back about 40° along her forward and sweeps about 25° to each side.
- The glance takes 0.3 s to turn and peers for 0.63 s; hold a tier-1 line or the breeze at least that long.

## Tests

- Core (`tools/core-tests`, also EditMode): `TestimonyGesturesPlayOnceAndBelongToOneCastMemberEach`,
  `OnlyGesturesAimedAheadTurnTheBody` (`WitnessTests.cs`), `HintGlancePicksTheSideThePlaceIsOn`, and the clip
  contract with seventeen clips (`PipAnimationTests.cs`).
- EditMode (`ModelImportRulesTests`): `CharactersHaveTheirGestureClips` (six cases: the take exists on the imported
  model, plays once, has its length, loads from Resources) and `ValeWearsHisWatchOnItsOwnBone`.
- PlayMode (`GestureClipTests`): `TestimonyGesturesPlayOverTheRestLoopAndComeBack` for Bea, Vale, June and Kit
  (the clip plays over the rest loop, the hand rises and comes to the head or chest, the body turns only for
  `ShadeEyes`, everything returns, and a foreign, unknown, null or empty gesture is refused quietly) and
  `PipGlancesTowardTheSideAPlaceIsOnAndComesBack` (the head really turns to the named side in the engine, by 55° to
  115°, and comes back).

## Evidence

- Local: `./tools/check-local.sh` passes (core tests: 618 passed after merging `origin/main` at 65854fc).
- CI: see "CI" below.

## CI

Run **144** of `ci.yml` (id 45132539396) on `423c2d9` (this branch with `origin/main` at `65854fc` merged in):
**success**, read from the job log.

| Results | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| EditMode | 658 | 658 | 0 | 0 |
| PlayMode | 130 | 130 | 0 | 0 |
| Visual captures | 14 | 14 | 0 | 0 |

- `SpookyStreet.Tests.PlayMode.GestureClipTests` 5/5 passed (the four testimony gestures and Pip's glances, in the
  engine, on the imported models; none ignored, so all five models and their clips were found).
- `SpookyStreet.Tests.Project.ModelImportRulesTests` 21/21 passed (with the six `CharactersHaveTheirGestureClips`
  cases and `ValeWearsHisWatchOnItsOwnBone`): Unity imports the FBX files written by `fbx_add_takes.py`.
- `SpookyStreet.Tests.Core.CharacterClipsTests` 5/5 and `PipClipsTests` 13/13 passed.
- The fixtures that use the changed models passed unchanged: `WitnessTests` 3/3, `CutscenePlayTests` 5/5,
  `WallLedgeTests` 8/8, `MovesetTests` 4/4, `CaseInteriorTests` 6/6.
- Static checks job: success (core tests, meta check, linters).

Not done: the CI frames were **not looked at**. This session could not download the log or the artifact (the
storage host is blocked here), and the frames are printed as base64 lines too large to read through the log tool.
No CI frame shows a new clip anyway, since nothing cues them yet; frames that show Mr. Vale will now show his
watch. The review of the clips themselves is the rendered sheets above.

Seen in the run, not from this branch: the "Unity normalised files" step lists audio `.meta` files
(`Content/Resources/Audio/**`); they come with `origin/main`.
