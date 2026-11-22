# World and cases: the expansion framework (proposal, pending owner approval)

A designer, human or agent, should be able to write a new Spooky Street case from this page plus
`docs/narrative-bible.md`. Everything beyond case 01 is a **proposal**: the spec (§1, scope) makes future cases
separate decisions after playtesting. Constraints that always hold: the art bible (a real street dressed for
Halloween night, never a haunted ruin; humans in costumes; cloth silver only for clues of its case), D24/D24.1 for
enterable places, D25 portability, spec §6 premium bar and §1 exclusions (no combat, lives, currencies, ads).

## 1. The neighbourhood as a world

The town is **Lantern Hollow** *(proposal)*, a small valley neighbourhood. Lantern Lane is its oldest street. The
map grows outward one place per case, connected on foot (a street-level hub, no world map menu), so every new place
also opens a new view back on the old ones.

| Place | Mood | Signature mechanic or traversal | Residents | Mystery it hosts | Enterable spaces (D24.1) |
|---|---|---|---|---|---|
| **Lantern Lane** (case 01) | cosy party street, candlelit porches | ramp and bridge, bench viewpoint, glide off roofs | Bea, June, Mr. Vale, Aunt Kit | sounds from a shut house | owl house (bigger inside), treehouse (seamless) |
| **Bramble Row** | narrow, tall houses, washing lines and roof lanterns | **ledge grab** and rooftop paths | Mrs. Ortega the retired lamplighter (owl-and-moon witch), the Penn twins (skeleton and ghost sheets) | a light that walks across roofs | lamplighter's loft (seamless) |
| **Hollow Park** | open lawns, bandstand, hilltop kites | **glide on updrafts** from vents and hilltops | Coach Amara (scarecrow), Theo (dragon, age 7) | kites that fly with nobody holding them | bandstand under-stage (seamless) |
| **Lantern Hollow School** | after-hours school dressed for the Halloween fair | **wall jump** in the courtyard chimneys and stairwells | Mr. Dunmore the caretaker (friendly ghost-in-overalls), Ms. Lee the music teacher (bat) | a bell that rings at midnight | hall and bell tower (bigger inside) |
| **Market Corner** | bakery, cart stalls, warm smells | **push and carry** light crates to make steps | Rosa the baker (gingerbread witch), Mr. Okafor the cart man (pirate) | footprints in the flour at night | bakery (bigger inside) |
| **The Old Mill** | brook, mossy wheel, fireflies (paper) | **turning wheels and floating logs** (timed only by water, never a countdown) | Grandpa Ilse the miller (wizard) | singing from the mill at night | mill floors (bigger inside) |
| **Woods' Edge** | the orchard and lantern-lit trail where the town ends, never deep dark woods | **torch beam** reveals shadow shapes; no dark navigation (spec §6) | Bea and June's dad (fox costume), the orchard keeper | a giant fox shadow on the trees | orchard shed (seamless) |
| **Town Square** (finale) | the whole town at the Night of Lanterns | every move, the child's choice of route | everyone | where are the last Players, and can the festival return? | the old Players' hall (bigger inside) |

Rules for places: a place is a real, lived-in neighbourhood spot; it has at least two distinct uses (spec §4
density); its signature mechanic is taught by a safe, optional moment before any clue needs it; its enterable
spaces state seamless or bigger-on-the-inside in their design note; it keeps a sightline to at least one older
place.

## 2. Cast framework

Every resident is written as: **desire**, **secret or worry** (gentle, never shameful), **costume theme** (human in
costume, Halloween-first, distinct silhouette and headgear), **how they help or hinder Pip** (always by honest
limitation: position, habit, assumption), **voice** (babble timbre and three signature words), **return plan**.

| Resident | Desire | Secret or worry | Costume | Helps / hinders | Returns |
|---|---|---|---|---|---|
| Bea | be believed | thinks she is "just the little sister" | pumpkin, bike with foil moons | fast, sees from far, jumps to conclusions | lookout in every case; case 04 she gives the key testimony carefully |
| June | adventure | slips off without telling anyone | star witch, silver comet cape | knows shortcuts, keeps secrets | Pip's partner from case 02; reveals the Players' crest |
| Mr. Vale | still be useful | lonely since he stopped walking the street | friendly mummy | precise but fixed viewpoint | leaves the bench in case 05; his timekeeping solves the mill |
| Aunt Kit | revive the shows | misses the troupe, thinks it is too late | fortune teller, crystal-ball lantern | knows the machines but is rarely home | hosts every reopening; tells one Player's name per case |
| Mrs. Ortega | keep the roof lamps lit | her knees no longer love ladders | owl-and-moon witch | sees the rooftops from her loft, mistakes a cat for a ghost | the finale's lamp-lighting |

**Rules for adding people.** (1) Each new resident has a reason to be on the street that night. (2) No adult is a
villain, fool or threat; children are never in danger. (3) One new face per case at most, plus returning ones.
(4) A costume must read at phone size and differ in silhouette from everyone else (art bible). (5) A witness's
limitation is physical or habitual, never a lie. (6) Representation: vary ages, families and abilities across the
cast; nobody's difference is a clue or a joke.

**Returning characters change.** Each solved case changes one thing about a resident (a greeting, a new place they
stand, an object they carry) and the notebook's character page records it with a sticker.

## 3. Case template

Copy this block for every new case (`docs/cases/C0x-<name>.md`), fill it before any asset or line exists, and run
the bible's checklist (§6) on it.

```
Case ID / title / place / level (1–4)
Questions (2–3), each with: when the world raises it · slots · accepted idea groups
True story: minute-by-minute what happened, why it looked spooky, why it is kind
Physical clues (3–5 per question): what · where · what finding it needs (move, angle, time of cycle) · its diegetic attraction · which idea it covers
Testimonies (2–3): who · where they stood · what they perceived · what they get wrong or leave out
Decoy (0–1): what · how the child disproves it by touch or comparison
Deduction steps: which stickers connect for each question; which aha it should produce
Reveal: the child's world action that shows the truth · the answer comic panels · what the street keeps afterwards
Seed: the object, line or sketch that points to a later case
New mechanic or place: what is taught, where it is taught safely, which clue needs it
Hint areas: authored areas per question; which blockers are moves (tier-3 demos)
Continuity: facts this case adds to the street state; facts it must not contradict
Risks: where children may stall, and the soft net for each
```

**Case 01 filled in, short:** *Three Knocks on Lantern Lane* · Lantern Lane · level 1 · Q-source (2), Q-entry (3),
Q-moved (2) · true story spec §2 · clues `mystery-first.md` §3 · testimonies Bea (flash up high), Vale (door never
opened) · decoy the foil streamer · reveal crank → the owl bows; booth reopens · seed: Kit's troupe photo and the
crest · mechanic: bench viewpoint, ramp, bridge, glide.

## 4. Season arc (proposals)

| # | Case | Seed | Place · cast · new mechanic |
|---|---|---|---|
| 02 | **The Walking Lantern** | A lantern drifts across the rooftops at night. Mrs. Ortega has been testing the Players' old roof-lamp trolley, a lamp on a wire, to see if she still can. | Bramble Row · Mrs. Ortega, the Penn twins · ledge grab |
| 03 | **The Kites Nobody Holds** | Moth kites fly over the park by themselves. They are tethered to the bandstand's wind wheel, the Players' second machine, which Theo found and is too shy to show. | Hollow Park · Coach Amara, Theo · updraft glide |
| 04 | **The Midnight Bell** | The school bell rings at midnight. The caretaker's old Players' music box clockwork was moved into the bell tower; Bea gives her first careful testimony. | School · Mr. Dunmore, Ms. Lee · wall jump, rhythm clues (audio) |
| 05 | **Footprints in the Flour** | Small footprints cross the bakery every night. The Players' wind-up "baker bear" walks when the cooling oven's warm air turns its little fan. Mr. Vale leaves his bench to help. | Market Corner · Rosa, Mr. Okafor · push and carry |
| 06 | **The Singing Mill** | The mill sings after dark. Water reaches the old organ pipes Grandpa Ilse built for the Players when the sluice is opened for the fair. | Old Mill · Grandpa Ilse · water wheels and logs |
| 07 | **The Fox on the Trees** | A giant fox shadow crosses the orchard. Bea and June's dad is rehearsing a lantern shadow show from the Players' fox puppet as a surprise for his daughters. | Woods' Edge · their dad, the orchard keeper · torch beam |
| 08 | **The Night of Lanterns** (finale) | Kit's photo has one face Pip never met. Where is the last Player? Answers from every case become stickers; the trail leads to the Players' hall and the festival is relit with the whole town. | Town Square · everyone · all moves, open routes |

Case 01's seeds in this arc: the troupe photo (four other puppets: fox → 07, moth → 03, moon/lamp → 02, bell → 04),
the owl crest (found on every Players' machine), and the paper bat sketch pages (each points to a next place).

## 5. Continuity rules

- **What the street remembers** (a small `street` state beside each case save, proposal): solved cases, reopened
  places (the owl booth stays open and bows on the hour), relationship stickers, and up to three visible changes
  per case (Bea's owl on her bike, Vale's saved sweet, the attic lantern). Nothing else carries over.
- **Timeline.** All cases happen on the evenings of one Halloween week, ending with the Night of Lanterns; the moon
  waxes case by case (a visible clock in the sky, `MagicSky`). No school-day or daytime cases in season 1.
- **Facts that cannot contradict.** The Players built every machine; machines are wind, water, heat or clockwork,
  never magic or electricity; Kit gave the children permission to use the treehouse; Vale did not know the owl
  machine; June's cape is the only silver cloth on the street; nobody is hurt or punished in any case.
- **Naming.** Places are plain English compounds a child can say (Bramble Row, Hollow Park). People have one short
  name or a title plus surname (Mrs. Ortega). Stable IDs: `c0x.<place>.<thing>` for facts, `c0x.note.*` for
  notebook, `c0x.line.<speaker>.*` for lines, `street.*` for remembered state.
- **Tone.** As the bible §5; every case ends with something shared (candy, a show, a song) at its own porch shot.

## 6. Engagement across the arc

| Lever | How the arc uses it |
|---|---|
| Cliffhangers | every final card ends on the seed: a new puppet shape in the photo glows, a sketch page fills, a new light comes on in the next street |
| Revisits | each new move opens one older place's secret (ledge grab → Lantern Lane rooftops; wall jump → the owl house chimney) |
| Collectibles | 30 paper bats per place; every 10 unlock a sketch page with lore and a costume piece; the last page of a place hints at the next case |
| Relationships | the character page in the notebook gains a sticker per case; greetings change |
| The street remembers | reopened places run their machines; the finale assembles them all |

Details and guardrails: `docs/engagement.md`.

## 7. Open questions for the owner

1. **Season length.** (a) 8 cases including the finale. (b) 4 cases first, then decide. **Recommend (b) planned
   as (a):** design the arc for 8 but commit to cases 02–04 after case 01's playtest.
2. **One street or a growing map?** (a) Each case adds a connected place you can walk to. (b) Each case is a separate
   level picked from a menu. **Recommend (a):** the living neighbourhood is a pillar; the D24.1 threshold
   technique keeps scene costs down.
3. **The town's name, Lantern Hollow?** (a) Adopt. (b) Owner names it. **Recommend** the owner choose; Lantern
   Hollow is a placeholder that fits the lantern motif.
4. **Machines are never magic?** (a) Always a mechanical, kind explanation. (b) Allow one gentle unexplained wonder
   in the finale. **Recommend (a)** for the cases, with (b) as a final shooting-star wink only.
