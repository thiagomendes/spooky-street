# S002: Bea's `Point` clip (asset only)

Branch `feature/bea-point`. This adds the clip and the hand that can make it. Nothing in the game plays it yet.

## The clip

`Point` in `Bea.fbx`, a one-shot per `CharacterClips` (not in `WitnessLooping`). 54 frames at 30 fps, 1.8 s, in
place. It starts and ends on Idle frame 0, like `Wave`.

| Frames | What happens |
|---|---|
| 0–8 | Wind-up: she rocks back and crouches a little, the right hand comes up to her chin, the left foot lifts. |
| 8–13 | Strike: the left foot stamps 13 cm forward, the right arm shoots out, the right heel peels up. |
| 13–41 | On target (0.93 s) with two eager bounces on her toes, weight on the front foot. |
| 41–54 | The arm comes down and she steps back to the idle pose. |

- **Aim:** her own forward axis, since the game turns her to face the target. Measured on the re-imported FBX at
  the hold: shoulder to knuckle 24° up and 3° toward her centre line, index finger 25° up and 1° off forward.
  The bounces move it between 21° and 26°.
- **Arm:** the right. Pip arrives from behind her and to her right when she faces the owl house's side window, so
  the right arm is on the camera's side and clear of the pumpkin. Her bicycle stands behind that arm, not under it.
- **Head:** tilts up about 14° to 21° along the arm. Her eyes are part of the face map and cannot move on their
  own.

## The hand

The shared skeleton moves all four fingers with one bone, which can only make a paddle. Bea's rig now has two more
bones per hand, added by `witness_export.split_hand`:

- `Index_L` / `Index_R`: the index finger alone.
- `FingerTips_L` / `FingerTips_R`: the second joint of the other three fingers.

Only skin weights moved. Vertices, UVs and textures are untouched, and Idle, Talk and Wave deform the mesh exactly
as before (largest vertex difference against the previous FBX: under a micrometre). Bea has 33 bones, up from 29.

The pointing hand has the index finger out, the other three closed into a fist and the thumb tucked under the
index finger against the middle one. The game fingers are a few uneven rings of triangles, so from below and very
close the fist shows creases; at play distance it reads as a fist.

## Evidence

Rendered from the re-imported `Bea.fbx` by `tools/blender/export/bea_point_review.py` (Blender 4.0.2, Eevee):

- `docs/evidence/s002/bea_point/bea_point_sheet.jpg`: six frames from the front three-quarter and from the side,
  the hand, the view from behind, and the last frame beside Idle 0.
- `docs/evidence/s002/bea_point/bea_point_hand.jpg`: the hand from eight sides.
- `docs/evidence/s002/bea_point/bea_point_arrival.jpg`: frame 25 from 1.5 m up on Pip's arrival side. The lit
  window is a stand-in placed 7 m along her arm, not the owl house.

Local checks: `./tools/check-local.sh` passes. No Unity run has imported this FBX yet: the branch was pushed with
`[skip ci]`.

## Open points for the wiring

- `Bea.fbx.meta` was left alone, so its stored clip list still names only Idle, Talk and Wave.
  `ModelImportRules.OnPreprocessAnimation` rebuilds the list from the takes on every import, so `Point` should
  appear without a meta change. The new EditMode test `BeaHasAOneShotPointClip` checks exactly that on the next
  CI run.
- `Witness` turns the head toward Pip while conversing. That should be off while `Point` plays, or her head will
  leave the arm.
- The real window is about 7° above her shoulder from where she stands. The clip aims 23° up as asked, which
  reads better but points above the window if the game does not also pitch her.
- Blender's FBX importer drops a bone that is its parent's only child and has no children (`Toe_*`,
  `Ponytail_2`, and now `FingerTips_*`) unless asked not to. `witness_review.import_model` gained a `leaf_bones`
  switch for this review; the older review sheets were rendered without those bones.
