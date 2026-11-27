# Living street: the test slice (phase 1 of `docs/living-street.md`)

Status: **built on `feature/living-street-slice`, not merged.** Implements §9 phase 1 of `docs/living-street.md`
(approved as D33 and D34 in `docs/decisions.md`). Evidence: the CI run and frames listed at the end.

## Resumo para o dono

**O que a criança vê:** a cerca viva do lado leste da rua agora tem uma abertura onde a Rua das Lanternas continua.
Do outro lado há três casas com varanda (o Sr. Fogg, a Vovó Pearl e os Abbott), postes acesos, abóboras e, no fim,
o cantinho da pracinha com a mesa das lanternas de papel sob um varal de luzinhas. Oito pessoas andam pela rua: dois
grupos de crianças fantasiadas com um adulto, indo de porta em porta, e um casal passeando. No máximo três entram no
quarteirão do mistério ao mesmo tempo, sempre longe das pistas.

**O que a criança faz:** toca a campainha de cada casa. Cada uma abre de um jeito: o Sr. Fogg escancara a porta com
um tranco, a Vovó Pearl abre devagar e a luz da varanda fica mais quente, e os Abbott aparecem os dois juntos dando
pulinhos. Na segunda visita chamam a Pip pelo nome, na terceira fazem outra brincadeira. Na mesa das lanternas ela
escolhe uma forma (morcego, gato, fantasminha, folha) e uma cor; a lanterna fica acesa na varanda dela e, a partir daí,
os grupos passam a tocar a campainha da Pip, e ela entrega um doce. O caderno ganha uma página da rua com a varanda e
os vizinhos que ela já conheceu.

**O que não muda:** o mistério. Ninguém da rua fala do caso, ninguém olha para onde o rádio aponta, ninguém bate na
porta (só campainha). Nada é contado na frente da criança. "Jogo novo" apaga a lanterna e as visitas; "Jogar o caso de
novo" mantém.

**Para medir no iPhone:** na página "Para adultos" (atrás do portão dos pais), as versões de teste mostram o tempo dos
quadros (mediana, 95% e 99%) e quanto a multidão custa por quadro.

**Quem anda na rua:** as crianças e os adultos fantasiados do kit novo (fantasminha de lençol, esqueleto, gatinhos,
robô de papelão, milho doce, rainha e rei de coroa de papel), cada um uma peça só, andando no passo da própria
animação.

**O que ainda é provisório:** as portas, os vizinhos (com roupa de casa, feitos de peças do kit), a mesa e as
lanternas são peças simples até a arte delas; o som da campainha é um sininho já aprovado no lugar do som novo; os
vizinhos não têm voz própria.

## 1. What was built

| §9 phase 1 item | Where | State |
|---|---|---|
| Core crowd clock, loops, yield rule, exclusion zones, tests at 30/60 fps | `Core/LivingStreet/Crowd.cs`, `CrowdRoutes.cs`, `LivingStreetRules.cs` | done |
| Crowd kit v1 (bodies, costumes, clips) | merged from main (`Core/Crowd/CrowdKit.cs`, `docs/living-street-crowd-art.md`); wired in `CrowdKitBodyFactory` | done: 8 of the 9 recipes walk |
| East lane: three porch houses, lane extension, the Green's first corner, hedge gap, route and sightline tests | `Core/LivingStreet/LivingStreetLayout.cs`, `Runtime/LivingStreet/LivingStreetSet.cs`, `EnvironmentLayout`, `CollisionRules`, `GreyboxBuilder` | done (houses reuse the street set's reviewed porch houses) |
| Doors: bell, three gags, treats, first and repeat lines, pt-BR | `NeighbourDoors.cs`, `LivingStreetStage.cs` | done; treat is a flight to Pip, not yet shown in her pail; no vignettes or voices for the neighbours (§6) |
| Lantern table: shape and colour picker, Pip's porch lantern, groups ringing Pip's bell, her treat | `Neighbourhood.cs` (`LanternCraft`), `LanternView.cs`, `Crowd` porch excursion | done |
| `neighbourhood.json` and the street page | `NeighbourhoodStore`, `NotebookView.Street.cs`, `StreetPageRules` | done |
| Performance: LOD1, standees, culling, quality tiers, frame-time readout | `CrowdBudget.cs` (`CrowdBudget`, `FrameTimeStats`, `FrameReadoutPolicy`), `CrowdView.cs`, grown-ups' page | culling, LOD1 (the kit's `_Lo` meshes) from 12 m and beyond the animated cap, 15 Hz animation band, tiers done; flat paper standees are not made (the kit has none): far walkers show LOD1 without animation |
| CI run, frames, evidence doc | this file | see §8 |

## 2. The map change

- The heart's east hedge opens over 6 m at z −5.4…+0.6 (`EnvironmentLayout.EastGapMinZ/MaxZ`), not the design's −3…+3:
  keepsake paper bat 17 perches on that hedge's top at z = 1 and the 30 bats stay as tested (CI run 201 caught it).
  The two modules beside the gap
  overlap their neighbours and step out 12 cm so they never z-fight. The north hedge stays closed: the owl house's yard,
  the side yard and the treehouse get no new approach.
- The east lane runs x 22…46, z −14…+18. It is the largest area that stays inside the painted sky dome (radius 52 m
  centred at (0, −3, 8)): the Green's full x 46…64 would poke through the sky, so the slice stops at the Green's first
  corner, as §9 says. Moving or enlarging the dome is a phase 2 job.
- The lawn grid widens to x = 46 (`LawnMaxX`), which also widens the ground ring under it. The skyline house that stood
  at (29, 3) gave way to the lane.
- Walls: the north wall runs on to x = 47; the lane has its own south wall (z = −14) and east wall (x = 46); a stub
  closes the heart's south-east corner. `CollisionRules.InsideWalls` is the one test of "inside the street", used by
  the boundary PlayMode test (now with two lane sides).
- Houses: Mr. Fogg (tower house, `NeighbourHouseA`, door at (29.1, 6.6)), Nana Pearl (gambrel house with the long
  porch, `NeighbourHouseB`, door at (39.2, 8.7)) and the Abbotts (crooked house, `NeighbourHouseC`, south side facing
  the lane, door at (34.45, −5.6)). Door positions come from `tools/blender/env/street.py` (each house is exported
  centred; the front wall sits 3 m in front of the centre, 1.9 m for the gambrel's porch).

## 3. Rules and how they are proved

| Rule | Proof |
|---|---|
| At most 3 walkers in the case block during case 01 (D34) | `Crowd` holds a group at the heart's gate until it fits (`HeartGate`/`HeartExit`). Core: 20 simulated minutes with Pip wandering at random, counted by position every frame; PlayMode: the same count in the running game. |
| None within 1.5 m of a clue | Core: every loop, both laps, every member's slot and the step-aside offset, sampled every 10 cm against `CaseStage.AllPoints`; closest is logged. PlayMode: every walker every frame for 3 game minutes. |
| Nobody glances at hint areas | No stop faces an outdoor hint area within 12 m; no heart leg heads into one within 6 m. Pip's own porch stop faces her porch, which is the `arrival_porch` hint area: groups visit only while the radio's area is elsewhere (`LivingStreetRules.PorchOpen`), so a crowd at her steps never coincides with a hint pointing there. |
| Out of the side yard, the bench's view of the door, the hedges | Every sample is inside the walls, outside `PaperBatLayout.InHiddenRegion`, more than 1 m from the bench-to-door sightline, crosses the hedge only through the gap, and heart legs stay south of the plaza. |
| Walkers step aside for Pip | Pip within 1.25 m in front slows a group; Pip within 1.6 m makes it step 0.6 m away from her and creep past at a fifth of its pace. Bodies have no colliders (PlayMode checks), so Pip never gets stuck and the camera ignores them. |
| Doorbells, not knocks | Doors and Pip's porch ring; the bell sound is a chime. String lint: no line mentions knocks, the owl, June, the blue house or the attic. |
| Treats are never a currency or a count; no timers | Visits are capped at 3 (they only pick the line). No number in any child-facing key (test). The only total ("Neighbours greeted: 2 of 3") is on the grown-ups' page. A group at Pip's steps waits 4 s and moves on if she is away, longer (up to 20 s) while she is near; nothing is lost. |
| Reserved looks | `CrowdLooks.IsAllowed` refuses every reserved theme; every route's members are checked. |
| Cutscenes are staged | While a cutscene, the finale, the cutaway or a threshold crossing holds the screen, the crowd stands still and walkers in the heart are hidden. |
| Strings | 36 keys in `en.json` and `pt-BR.json` with the same contexts; Pip is a girl in every context; pt-BR from §4.10 where it had drafts. |
| Save | `neighbourhood.json` beside the case and street files, same atomic write, previous copy and recovery; a newer schema is kept untouched. New game clears it (and asks first if it holds anything); Replay keeps it (D31). |
| The case's tests | Unchanged except the two that described the closed east edge: the hedge coverage test now proves the gap and the closed rest, and the boundary run test uses `InsideWalls`. |

## 4. The crowd kit in the street

`Runtime/LivingStreet/CrowdBodies.cs`, following `docs/living-street-crowd-art.md`:

- Each walker's look is a kit recipe (`WalkerLook.Recipe`): group A is `CatGrownUp` with `GhostKid` and `CatKid`,
  group B is `SkeletonGrownUp` with `RobotKid` and `QueenKid`, the strollers are `KingGrownUp` with `CandyCornKid` (a
  grandpa and grandchild). `SkeletonKid` is the one recipe not walking (the slice keeps 8 walkers).
- `CrowdKitBodyFactory` instantiates the body's FBX, merges the recipe's parts into one `SkinnedMeshRenderer` per LOD
  (concatenation remapped onto one bone array, as the shared bind poses allow), destroys the unused parts, and gives the
  shared Crowd atlas (the glow variant at 0.9 for lantern carriers). Merged meshes are cached per recipe and LOD.
- The clips (Idle, Walk, Wave, Talk) run on a manual Playables graph per walker: Walk while moving, its speed scaled
  by ground speed over `CrowdKit.WalkSpeed` (the routes move at exactly that pace, so feet do not slide); Wave for a
  cheer at Pip's porch; Talk for a stop to look at a porch; Idle otherwise. Culled walkers do no animation work.
- Neighbours in their doorways are kit grown-up parts in house clothes (`LivingStreetLayout.NeighbourRecipes`), a
  placeholder until their own art.
- `ICrowdBodyFactory` stays the swap point; `PlaceholderCrowdBodies` is only the fallback for a checkout without the
  kit's models.
- Art gaps (placeholders, swap point `LivingStreetSet.ArtNames`): the door leaf (`LivingStreet_DoorLeaf`) and Pip's
  lanterns (`LivingStreet_Lantern_<shape>`) are paper primitives until models with those names are committed in
  `Models/Environment`; the houses reuse the street set's reviewed porch houses; the table is the party table with
  string lights.
- D35: the lantern shapes are bat, cat, ghost and leaf (never moon, star or pumpkin); the screen and the street page
  draw them from paper shapes (`LanternGlyph`).

## 5. iPhone budget

- Tiers from the URP quality level: high 8 / medium 6 / low 4 animated walkers in view, standees 12 / 8 / 6.
- Off-screen walkers are not drawn or animated (frustum test per walker); beyond 12 m they animate at 15 Hz, staggered;
  beyond 18 m, or past the tier's animated cap, they show the kit's LOD1 mesh without animation (the "standee" band; flat paper standees are not made).
- The lane's static pieces are one statically batched root; nothing in it casts a shadow; walkers have a paper disc,
  not a shadow.
- `CrowdView` times its own work each frame with a stopwatch (logic, culling and posing). Measured cost: §8.
- The readout: the grown-ups' page (behind the parent gate) shows the frame-time median, 95th and 99th percentiles of
  this session's play and the crowd's mean cost, in development and test builds only (`FrameReadoutPolicy`; test build
  = a CI build, `BuildInfo.runId` not `local`). **Before a store release the build must pass `storeRelease = true`**:
  there is no release flag in `BuildInfo` yet, so this is a known to-do for the release pipeline.

## 6. Weak points and what is left

- The door leaves, the neighbours' house clothes, the table pieces and the lanterns are placeholders (see §4).
- No flat paper standees: beyond 18 m a walker keeps its LOD1 mesh without animation.
- The doorbell borrows the approved notebook chime; a real bell needs synthesis and the owner's ear (D20.1).
- Neighbours have no voice or vignette: their lines play as captions. Free-world vignettes are a phase 2 item.
- The treat flies to Pip's hands; showing it in her pail waits for the pail art.
- Groups ring a door every lap; heard only within 14 m of Pip, but a long stay by one door repeats the call.
- No crowd audio bed (phase 2); the crowd is silent except calls and bells.
- The street page uses paper shapes, not drawn art.

## 7. Decisions taken (reversible)

- The slice ends at x = 46 (sky dome), the Green is its first corner only.
- Groups visit Pip's porch through the heart along the south of the plaza and down her path; they skip it while the
  radio points to her porch.
- After the case is stamped the heart's cap rises to 6 (the slice has 8 walkers, so in practice a group and the
  strollers).
- The lantern can be refolded at the table at any time (a new shape or colour replaces the old one).
- A New game asks for confirmation when only the neighbourhood holds something.

## 8. Evidence

Filled in after the CI run.
