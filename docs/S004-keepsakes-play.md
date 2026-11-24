# S004 stream A2: keepsakes in play (the real bat, the mobile, the trunk, the sketch pages)

Branch `feature/s004-keepsakes-play` (from `main` b7c2e5e, with `fix/ci-visual-timeout`). Plan: `docs/S004-plan.md`
§2.1 and §3 "Stream A" (A2 scope). Builds on A1 (`docs/S004-keepsakes.md`) and the B art (`docs/S004-art.md`).

## What A2 adds

- **The real collectible bat** (`PaperBatCollectibles`, A1's swap point now `KeepsakeArt.PaperBat` =
  `PaperBatKeepsake.fbx`, at 1.2×). Shut it is ink black with a cream belly (wings at +/-150° about their local Z);
  within 2.4 m of Pip it turns its face to her, lifts 4 cm and spreads its wings to show the cream, pencilled inner
  fold, beating a little (still with reduced motion). It never glows: the decorative bats keep their glowing edge.
  Perched bats stand on their surface (origin at the body's lowest point = `SitY`), hanging and airborne bats are
  centred on their spot. The greybox stays as the fallback without the model.
- **A1 issue 2 (decorative bats next to collectibles).** Three decorative bats moved: the two round the street lamp
  (they sat 0.55–0.65 m from keepsake bat 21 on the lamp head), and the two under the owl house's porch eave (1.1 m
  from bats 15 and 25). `PaperBatLayoutTests.TheDecorativeBatsStayDecoration` now asks for the bats' own spacing,
  1.5 m, instead of 0.5 m.
- **The bat mobile** (`Runtime/World/BatMobile.cs`, rules `Core/Keepsakes/KeepsakePlay.cs` `BatMobileRules`). The
  arrival porch has no eave, so the mobile hangs from a timber lantern pole on the east rail (`KeepsakePorch`:
  hook at 1.6, 3.4, −13.6; bottom 1.8 m over the deck; made in code like the moving boxes). Hook *n* and its bat show
  with bat *n* (no empty hook, no number); ring 1 is the frame, rings 2 and 3 appear with their first bat; at 10/20/30
  the ring's closing piece and its lantern (`KeepsakeLanternN_Glow`) show. Bats hang by their ears at
  `MobileBatScale`, wings 25° short of open, facing out; the mobile turns slowly in play (still on the title and with
  reduced motion).
- **The dress-up trunk** (`Runtime/Case/CostumeTrunk.cs`, `Runtime/UI/DressUpView.cs`). `DressUpTrunk.fbx` replaces
  the opening's small moving box on the porch's back corner (0.7, 0.6, −14.62), front to the street, with its own
  0.6 m `BoxCollider` (**A1 issue 4**: checked in play by a raycast onto its top, `KeepsakeDressUpTests`). It is a Use
  target ("Dress up", `ui.verb.dress_up`) through a new `ExtraUseTarget` on `CaseUseTargets` (never over a case
  point; the owl booth's knock is 26 m away), offered within 1 m of its front and **only once a piece is earned**
  (it never opens empty). Use: the lid swings open (+105°), Pip stands in front of it turned to the street, a fixed
  camera frames her with the trunk behind, and the screen shows one paper card per **earned** piece (picture + name),
  a worn piece marked with a candy stitch and a tick. A tap puts it on or takes it off at once and saves. Close disc,
  or Back on a pad or keyboard. Navigation is the F1 base (`UiFocusScreen`, graph `DressUpFocus`, tested sound in
  every state). Until all three pieces are earned a plum cloth covers the trunk's contents (the model holds all three
  things), so no unearned piece is ever seen.
- **Pip's costume** (`Runtime/Player/PipCostume.cs`): the star cape swaps the base map on a copy of the atlas material;
  the hat (`Head`) and the lantern pail (`Pail`, hiding `Pip_Pail`) are attached from the skinned meshes' bind poses
  with `KeepsakeArt.ShowmanHat`'s formula. Applied on load, so the title shows it; a confirmed New game takes it off
  with the keepsakes (decision 4).
- **Card pictures**: `UI/CostumeIcons/costume_icon_{star_cape,lantern_pail,showman_hat}.png`, 256², rendered from the
  exported FBX by `tools/blender/chars/costume_pieces.py icons` (Pip re-imported in her rest pose, the pieces on their
  bones; the cape seen from below in the glide so the star lining shows). Import rules in `KeepsakeArtImportRules`
  (v2), tested in `KeepsakeArtImportTests.CostumeIconsImportAsTransparentSprites`. Sheet:
  `docs/evidence/s004/keepsakes_play/costume_icons.png` (looked at: hat and lantern read at once; the cape card shows
  the violet star lining under her arms, the stars are small).
- **The 10th-bat moment and the sketch page** (`Runtime/UI/NotebookView.Keepsakes.cs`, `KeepsakeNotebook`). The find
  that completes a page wobbles the notebook disc once (the sticker-arrival cue, no pop-up, play goes on) and the
  page waits. The notebook gets its own **keepsakes page** after the case pages (the plan's "keepsakes page after the
  case page") from the first bat: only earned drawings, each with its `keepsake.page.*` caption, and a small pile of
  the folded bats not yet in a page with its own number (`keepsake.notebook.bat_stack`, plural, 1–9; never a total).
  The next time the notebook opens it turns to that page and the drawing assembles from ten pieces cut on its 5 × 2
  crease grid, flying from the pile (fading in place with reduced motion), once.
- **Costume remarks**: three case lines with context, vignettes (`C01Vignettes.Remarks.cs`), voices and portraits:
  June on the cape ("Stars on your cape! You look like the night sky!" — the plan's "Like my comet!" was changed so
  June never points at her own cape, a clue), Mr. Vale on the pail, Aunt Kit on the hat after the stamp. Conversation
  never picks them; after a talk the director appends the one `CostumeRemarks.Due`, records it in `keepsakes.json`,
  and it is never said again.
- **Continue line** (`ui.title.continue_detail`, "{question} · N paper bats"): shown only with a case to continue and
  at least one bat (no "0 paper bats"); a count, never a total (plan §3 A2 "count only"; engagement §2.3).

## Frames

A1 noted that 65–68 collide with S003a; 69–81 are reserved by other S004 streams, so A2 uses **82–85**.

(filled in after CI)

## Tests

- Core (`tools/core-tests`, also EditMode): `KeepsakePlayTests` (6): the mobile shows one hook per bat and lights at
  10/20/30 only; trunk and mobile placement on the porch (on the deck, clear of the crate, the opening walk, every bat
  and every case Use target, not offered at the spawn, the mobile over Pip's head and away from bat 01); the trunk
  only with an earned piece and its focus graph sound for every count; a page assembles once; the Continue rule;
  each remark a real case line with voice, portrait and valid vignette, never picked by a conversation. Updated:
  `VignetteTests` (45 lines, 43 vignettes), `PaperBatLayoutTests` (decor spacing), `NonverbalUiTests` (the remark
  lines). Local: `./tools/check-local.sh` passed (core tests all green, l10n 237 keys).
- PlayMode `KeepsakeDressUpTests` (7): the 10th bat (disc wobble, no pop-up, page 1 assembles once on the next
  opening, the 11th in the stack); the mobile hangs exactly the found bats; the trunk's collider, Use, cards, Pip
  dressed (hat on the head bone, lantern in hand, pumpkin pail hidden), saved, Back closes, reload shows the costume
  on the title; the trunk stays shut with no piece and hides unearned ones; New game takes the costume off; June's
  remark once; the Continue line.

## CI

(filled in after CI)

## Open issues

- The mobile hangs from a pole made in code: the plan's "eave" does not exist on the arrival porch. A Blender pole or
  a porch roof would be a stream B task.
- The trunk model holds all three pieces; a cloth covers them until all are earned. Splitting the contents (B) would
  let earned pieces show inside.
- Decision 4 (New game clears keepsakes and the costume) is still the owner's call.
