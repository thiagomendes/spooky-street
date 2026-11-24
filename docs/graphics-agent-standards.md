# Graphics agent standards

Owner policy (2026-11-24): every 3D, animation or 2D-art task runs on Opus 5.5 at high effort. These are the
working habits that gave the Fable-era art its quality (Pip, the cast, house v3, testimony gestures, portraits,
troupe photo and keepsakes). Read this file and `docs/art-bible.md` before starting; the brief of every graphics
agent links here.

## 1. Before modelling

- Read `docs/art-bible.md` (style, palette, outline, texel density, lighting) and the evidence doc of the asset
  family you extend (`docs/S000.2-original-art.md`, `docs/S003-pip-climb-clips.md`, `docs/S003a-gestures.md`,
  `docs/S004-art.md`, `docs/S004-portraits-art.md`).
- Extend the existing pipeline in `tools/blender/` (style, env, cast, chars, pip, export). Never restart a
  pipeline or rebuild a shipped model from scratch: the committed meshes and UVs are the truth.
- To add animation to a shipped character, copy only the new takes into the committed FBX
  (`tools/blender/export/fbx_add_takes.py`) and prove meshes, UVs, skin, skeleton and old clips are unchanged
  (`tools/blender/export/pip_fbx_diff.py`, curve difference 0).
- Art is 100 % original and made by code. Never download assets, fonts, textures or references into the repo.

## 2. The look

- Match the shipped cast and street: same materials, baked maps, outline, palette and roughness. Reuse the
  existing atlases where a part belongs to them; give a separate model its own texels when the shared atlas
  would be too coarse (check px per metre against the art bible).
- Silhouette first: every asset must read at phone size from the gameplay camera (3.8 m, 50° FOV) before any
  detail counts.
- Night scene: painted faces of signs and posters that must read at night use lit paint (a glow part under the
  bloom threshold), not dark albedo.
- Characters: hands never pass through the body, face or costume; feet plant; motion is lively and readable for
  6–9-year-olds; poses start and end on the rest loop's frame 0 so clips blend.
- Expressions and gestures must never give a clue's answer away (D26).

## 3. Review loop (the part that makes the quality)

- Render review sheets from the files the game loads (the exported FBX with the committed textures, not the
  Blender scene) and LOOK at every one before the asset enters the game.
- Each sheet shows: the gameplay camera distance, a close view, a side or back view, and for animation 8+ frames
  across the clip plus the last frame beside rest frame 0.
- Iterate until the asset meets the art bible's bar at phone size; write down what you changed after each look.
- Re-render the existing clips or assets you touched (regression sheet) and compare.
- After CI, extract the capture frames from the job log (`tools/ci/extract_frames.py`; decode base64 lines via
  `mcp__github__get_job_logs` or `gh api .../jobs/<id>/logs`) and look at them in the real game lighting. Fix
  what the game shows (darker signs, clipping, framing), not only what Blender showed.
- Commit the final sheets under `docs/evidence/<stage>/` and list known limits honestly in the evidence doc.

## 4. Unity import

- Imported children keep the builder's axes (Z-up to Y-up on the root, Z mirrored on children): measure in the
  prefab's world frame and prove turns (hinges, lids, wings) with import tests.
- A character prefab holds an exported pose, not its bind pose: attach props from the skinned meshes' bind
  poses.
- Blend shapes are off in the import rules: use hinged parts or bones instead.
- Add EditMode import tests for every new asset (exists, scale, pivots, child names, import settings) following
  `ModelImportRulesTests`, `KeepsakeArtImportTests` and `PortraitAtlasImportTests`.
- New files under `Assets/` get metas from `python3 tools/unity_meta.py`; never rewrite existing metas.

## 5. Machine and budget

- One Blender process per agent at a time, `-t 2` render threads (`-t 1` when another graphics agent runs).
- Render small first and full resolution only for the final sheets.
- Use `[skip ci]` while iterating and push for CI only when the asset is final.

## 6. What to avoid (seen in this project; extend the list when a review finds a new one)

- Signs, posters and painted faces that go dark at night: use lit paint (section 2).
- Hands or props passing through the body, face or costume, in any frame of a clip.
- A new collectible that looks like the decorative glowing bats: keepsakes must read as different at a glance.
- Detail that only reads close up: judge from the gameplay camera first.
- Characters framed from behind or cut by a post or the letterbox in cutscene shots.
- Generic "default" looks: no flat grey placeholders left in the game, no off-palette colours, no stock-looking
  gradients; stay in the felt-and-paper style of the art bible.

