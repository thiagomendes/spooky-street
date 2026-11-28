# Living street: the east lane's own houses in the game

Branch `feature/lane-houses-swap` (from `main` after the houses' art merge). Contract: the swap list in
`docs/living-street-houses-art.md`; rules: `docs/living-street.md` §3.4 and §8, `docs/living-street-slice.md`.

## Resumo para o dono

**O que a criança vê:** ao passar pela abertura da cerca viva, a rua nova agora tem as casas de verdade: a casa alta
verde do Sr. Fogg com a torrezinha, o chalé cor-de-rosa da Vovó Pearl com a varanda comprida e a casa de dois
telhados dos Abbott. Cada porta é a da própria casa (a laranja do Sr. Fogg, a amarela com guirlanda da Vovó Pearl, a
roxa arredondada dos Abbott), cada uma com seu lampião, sua campainha de apertar e o corredor aceso por trás. Quando
a porta abre, aparece o vizinho de roupa de casa. As casas do Sr. Fogg e da Vovó Pearl ganharam uma luzinha quente na
frente da porta, para a fachada não ficar escura; a da Vovó Pearl esquenta quando ela abre. O Sr. Fogg fica um
pouquinho mais para fora da porta, para a mão dele não sumir atrás do batente quando acena.

**O que não muda:** a campainha é de apertar (ninguém bate), cada porta abre com a mesma brincadeira e as mesmas
falas, ninguém fala do mistério, e a janela do sótão da casa da coruja continua a luz mais forte da rua.

## What changed

| Swap item | Where |
|---|---|
| Houses: `LaneHouseArt.House(id)` where the placeholder stood (same position and yaw, bounds collider, under the lane's static root so it is culled with the lane) | `LivingStreetSet.Build` |
| Leaves: `LaneHouseArt.DoorLeaf(id)` on the rig's hinge with the house's atlas; placeholder knob dropped | `LivingStreetSet.BuildDoor` |
| Lamps: `LaneHouseArt.PorchLamp(id)` in the lamp slot; the rig drives its `Lamp_Glow` at 0.9 (Nana Pearl's 1.7 while she answers) | `BuildDoor`, `ApplyDoors` |
| Dropped: the `Doorbell` disc and the flat `Doorway` card (the houses carry their push button and lit hall) | `BuildDoor` (kept only for the no-model fallback) |
| Neighbours: nothing to do (`CrowdKit.Find` already returns them) | |
| `ArtNames` is now the lane's names per door (`House`, `DoorLeaf`, `PorchLamp`) | `LivingStreetSet.ArtNames` |

Without models (greybox), the lane still falls back to boxes, a paper leaf with a knob, a lamp ball, a bell disc and a
flat doorway card, as before.

## Decisions

- **Porch lights: added, for Mr. Fogg and Nana Pearl only.** One point light each (`LivingStreetLayout.PorchLight*`):
  amber, 0.55 at rest, range 4.5 m, no shadows, 2.6 m up and 1 m in front of the door (above the door head, under the
  hood or porch roof). Nana Pearl's warms in step with her lamp (0.55 x 1.7 / 0.9 = 1.04) while she answers. Both stay
  under the owl house's door lamp (1.6), so the owl house remains the best-lit front; emissive glow values are
  unchanged (windows 1.2, faces 1.8, lamps 0.9, Pearl's 1.7), all under the attic window's 4. The Abbotts face the moon
  and read without one. Core tests prove the lights stay in the lane, end more than their range before the case
  block, and are more than range + 1.5 m from every clue, so they can never light a clue or read as a hint.
  Cost on the phone: two unshadowed per-object lights in forward rendering (limit 4 per object) that only touch the
  lane's objects within 4.5 m; no device time has been measured yet (§8 says the same of the whole slice).
- **Mr. Fogg's wave:** he stands 0.45 m out from the wall instead of 0.3 m (his landing is 0.5 m deep), so his raised
  hand is in front of the door casing's head. The capture test measures the wave (wrist height, distance from the
  wall, and in how many frames the camera's line to the hand crosses the casing head or the hood) and logs it.
- Kept `NeighbourDoor.House` (the placeholder name) as the bounds-check size; it equals `LaneHouseArt.Footprint`
  (tested in `LaneHouseArtTests`). Kept `LivingStreetLayout.NeighbourRecipes` as the fallback.
- The art pass's staged capture (`LaneHouseArtCaptureTests.SwapIn`, frames 98 to 101) is replaced by the same test
  capturing the game as it builds itself (frames 102 to 107): nothing staged, so the frames show what ships.

## Tests

- Core (`LanePorchLightTests`, 5): which doors get a light, levels under the owl door lamp and in step with the lamp,
  never reaching the case block or a clue, hanging between door and lamp above the door head, the neighbours on their
  landing with Mr. Fogg further out.
- PlayMode (`LivingStreetPlayTests.TheLaneStandsTheNeighboursOwnHousesDoorsAndLamps`): each door has its house art
  (name, place, LOD group, collider, lit hall) under the lane's static root, the placeholder house gone, its own leaf on
  the hinge, its lamp driven by the rig, no knob, leaf card, lamp ball, bell disc or doorway card; the neighbours are
  kit bodies at their rest; the porch lights are there (and only there) at their values; Nana Pearl's lamp and porch
  light warm while she answers and cool after.
- Visual (`LaneHouseArtCaptureTests.CaptureTheLaneHousesInTheGame`): asserts the art names in the running game and
  captures frames 102 to 107.

## Evidence

Pending the CI run.
