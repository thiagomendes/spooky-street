# S002: interior, workshop mechanism and treehouse routes (assets only)

Branch `feature/s002-interior`. Original Blender-script assets for the spaces the first case needs (spec §2–§4).
No C# changed; nothing is placed in the game yet.

## Assets

Built by `tools/blender/env/interior.py` and `routes.py`, exported by `env_export_interior.py` (same conventions as
`env_export.py`: metres, base-centre origins, front toward Blender −Y, `*_Glow` parts). Triangle counts, pivots and
texel densities are in `tools/blender/env/interior_manifest.json`. The review sheet, rendered from the re-imported
FBX files by `env_interior_review.py`, is `docs/evidence/s002/interior_review.jpg`.

| Model | Triangles | Textures | Children |
|---|---|---|---|
| `OwlHouseInterior` | 25.0k | own `OwlHouseInterior_*` (2048) | `Attic`, `NookWall`, `InnerDoor`, `DoorLatch`, `SillSnag`, glows |
| `WorkshopMechanism` | 5.4k | own `WorkshopMechanism_*` (1024) | `Cam`, `Follower`, `LinkArm_Slipped`, `LinkArm_Seated`, `Crank`, `ServicePanel`, `KitPhoto` |
| `ShedRamp` | 11.9k | shared `Routes_*` (2048) | glows |
| `SillBridge` | 1.5k | shared `Routes_*` | — |
| `TreehouseCabin` | 5.4k | shared `Routes_*` | `Lamp_Glow` |
| `ClothSnag` | 0.1k | shared `Routes_*` | — |

`Routes_*` is a shared atlas: load those four models with `ModelKit.FallbackAtlas = "Routes"`. The skilled ascent
reuses the approved `Crate` and `Awning` unchanged.

## Culling

The exterior rule (a face stays if some outside viewpoint sees it) would delete a closed room. The interior shell
uses the same ray test with a 0.3 m reach against the static shell; the other models use the exterior rule
unchanged. Every room wall is a single inward face, so a camera outside the shell sees in through the near walls.

## Exterior split (branch `feature/s002-owlhouse-split`)

`OwlHouse.fbx` is re-exported so the two ways in are open. Its root is now an empty; the static mesh is divided into
named children (pivots in model space, Blender axes, from `env_manifest.json`):

| Child | Triangles | Pivot | Use |
|---|---|---|---|
| `OwlHouseBody` | 14.5k | origin | walls, roofs, dormer, trim, owl saddle and mast; hide while Pip is inside |
| `Porch` | 6.4k | origin | deck, steps, posts, rails, porch roof; keep visible |
| `FrontDoor` | 1.9k | (-0.55, -2.015, 0.6) | unchanged |
| `DoorRecessCard` | 2 | origin | dark card 0.125 m behind the door plane; hide whenever the interior is shown |
| `SideWindowPane` | 88 | (-3.045, 0.5, 2.5), the south jamb at the sill | muntins; hide (or swing) to open June's window |
| `SideWindow_Glow` | 48 | child of `SideWindowPane` | the lit pane; hidden with its parent |
| `OwlPuppet`, `WindWheel`, `AtticWindow_Glow` | unchanged | unchanged | unchanged |

Total 25.6k triangles (was 25.4k). The 1.2 m recess box and the window's backing slab are gone, and the main roof
is opened under the dormer so nothing of the exterior crosses June's alcove. June's window is now 1.0 m wide with a
1.45 m apex (spring line at 0.95 m), sill still at 2.5 m; `OwlHouseInterior` has the matching inner frame, reveal
and `SillSnag` (pivot now (-2.83, 1.47, 2.92)). The porch keeps the faces seen from the foyer through the door.

Review sheet from the re-imported FBX files (`env_split_review.py`): `docs/evidence/s002/split_review.jpg`. Rows:
street before / after; west side before / after and the open door; foyer looking out with the body hidden, and the
window from the bridge with a Pip-sized capsule; the inner and outer window frames.

## What is not proven here

- No Unity import, collider, placement or device check has run: these are Blender re-import renders only.
- The game code does not yet hide `OwlHouseBody`, `DoorRecessCard` or `SideWindowPane`; until it does, an opened
  door shows the dark card and the window stays closed.
- The inner sill board sits 3 mm above the outer sill where they overlap; watch for shimmer on device.

## Integration branch

`feature/s002-assets` combines three branches into one, so a single CI run can check every new S002 asset before the
interior wiring pass:

- the interior, mechanism and routes (`feature/s002-interior`);
- the split owl house (`feature/s002-owlhouse-split`);
- June and Aunt Kit (`feature/s002-june-kit`).

No game code uses the new models yet, apart from the import rules for the June/Kit clips.
