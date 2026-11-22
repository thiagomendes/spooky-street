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

## What is not proven here

- No Unity import, collider, placement or device check has run: these are Blender re-import renders only.
- The exterior `OwlHouse` still has the lit pane, muntins and backing slab in June's window and a 1.2 m dark recess
  behind the front door, all in its main mesh. The interior cannot be entered or seen through them until the
  exterior is hidden while Pip is inside or re-exported with those pieces as separate children.
