# S004 plan: keepsakes, seeds, languages, the parent page and the accessibility pass

Status: **plan, for the owner's review** (2026-11-23). Docs only; nothing here is built yet. Contract: spec v1.4 §4
(paper bats), §6 (cutscenes), §7 (language packs), §8 (LOC-01, A11Y-01, MAP-01), §9 S004 row; `docs/roadmap.md` §1
S004 row and §1.1 "Street remembers and seed"; `docs/narrative-bible.md` §3, §5, §6; `docs/world-and-cases.md` §3–§6;
`docs/engagement.md` §1.6–§1.9, §2; `docs/portability.md` (D25); D3, D4, D9, D20.1, D23.1, D26–D29.

The roadmap's S004 row asks for: paper bats with meaning (sketch pages, costume pieces, bat mobile), the parent
progress page behind the gate, language packs, an accessibility pass (deduction by gamepad and keyboard), dialogue
portraits, and the troupe photo and crest seeds with case 01's "street remembers" and seed cutscenes. The spec's S004
row adds the language screen and remote pseudo pack, settings, credits, both optional discoveries and MAP-01; they
are folded into the streams below.

## 1. What exists today (checked in code on `main` 2c44716)

| Area | State | What S004 reuses |
|---|---|---|
| Paper bats | **Decoration only.** `EnvironmentLayout.PaperBats` has 6 hanging bats (lamp, porch eave, maple); no pickup, no count, no save. `ui.title.continue_detail` ("{0} · {1} paper bats") exists but nothing calls it. The HUD comment in `GameUi` reserves "no bat counter until paper bats can be collected". `x.paper_bat` puppet part exists. | the folded-bat mesh and the `PaperBat` glow level; the puppet part for vignettes |
| Notebook | Question pages, deduction tray and slots, case page (final card), rewatch discs (`NotebookRewatch`), gamepad and keyboard cursor (`DeductionCursor`, bindings inline in `NotebookView.Deduction.cs`) | a new partial file for the keepsakes pages; the carousel; `StickerCard` |
| Street state | `Core/Street` (`StreetMemory`, `StreetStore` in `street.json`, `StreetGreetings`); five case 01 memories; the owl bows on a 9 s wind cycle after the stamp (`StreetMemoryStage`) | the store pattern for the keepsakes file; the stage for the show clock |
| Cutscenes | `Core/Cutscenes` (data, evaluator at 30/60 fps, skip gate, policy, seen set `case-c01-cutscenes.json`), `C01Cutscenes` opening (20.5 s) and reveal (28.4 s). The reveal ends with a slow push on Kit's photo and one glint (`world.photo_glint`) | everything; S004 only adds content and two policy rules |
| Kit's photo | `KitPhoto` part in `tools/blender/env/interior.py` (`photo_motif`): **Aunt Kit alone at her booth**, 0.4 × 0.5 m, on the foyer side of the partition; the mechanism's push rod crosses it in the seed shot (S002.5b gap). No crest exists anywhere. | the paper cut-out motif style, `frame_art`, the anchor lookup by part name |
| Parent gate | `Core/Nonverbal/ParentGate` (hold two spots 3 s) with gamepad and keyboard hints; guards the settings card (Helper and three volume sliders) in `NonverbalMenus` | the gate as is |
| Preferences | `PreferencesData` already stores `Locale`, `ReducedMotion`, `LargeCaptions`, `OpaqueCaptions`, `Haptics`, `InvertY`, `CameraSensitivity`, `LeftHanded`; **no settings UI for any of them** except volumes | the fields; only UI is new |
| Localization | D3 custom JSON tables (`StringTable`, `LocalizationCatalog`, `PseudoLocalizer`), one bundled `en.json` (199 keys, about 1,100 words), a dev pseudo toggle pill on the title. No plurals, no pack download, no language screen. Addressables not installed (D4). | the table format as the pack format; the catalog's fallback to English |
| Fonts | Andika (reading) and Sniglet ExtraBold (display), bundled. Checked with fontTools: **both cover every pt-BR, es, fr and de character**; Sniglet lacks Cyrillic, neither has CJK. | no new font for the first languages |
| Dialogue | `DialogueView` portrait = a felt disc with the speaker's initial ("portrait art is a later pass"); speech bubbles show a face disc. `LineVoices` gives every line a babble emotion (neutral, happy, surprised, thinking, whisper). | emotion per line picks the portrait expression |
| Moves | Wall slide, wall jump and ledge grab exist (S003); marked surfaces: shed west wall, gate posts, street lamp head | bat challenges use these marks |
| Gestures | S003a: Bea `ShadeEyes`, Vale `CheckWatch` (with his wristwatch), June `Listen`, Kit `Ponder`, Pip `GlanceLeft`, plus `Wave`, `Point`, `OpenBooth` | cutscene beats need no new clip |

## 2. Content decisions (proposed; the narrative bible checklist is in §5)

### 2.1 The 30 paper bats: what they mean

**What they are.** The collectible bats are folded from old drawing paper: black outside, a cream inner fold with
faint pencil lines that shows when a bat flutters open as Pip comes near (spec §4: "flutter when Pip is near").
That tells them apart from the ink-black decorative bats and the opening's falling bat, which stay decoration. Pip
unfolds each one into her notebook. **Every 10 bats piece together one page of an old sketchbook.** Who folded them
is left open in case 01. The recommended payoff is June in case 02 ("You found my bats!"): she found the sketchbook
in the treehouse. Case 02's design doc decides.

**What every 10 give** (engagement §1.8, owner answer 1a):

| Bats | Sketch page (lore and seed, a picture plus one caption) | Costume piece (cosmetic, worn in play and on the title) |
|---|---|---|
| 10 | **"An old drawing: lanterns all along Lantern Lane."** The lane long ago on the Night of Lanterns: paper lanterns strung across the street, the booth freshly painted with the owl crest over its shutter, small costumed children, a parade of five puppet carriers seen from behind. | **Star-lined cape:** the berry lining becomes night violet with candy-corn paper stars. It shows when the cape opens in a glide. A material variant only. |
| 20 | **"An old drawing: five friends painting lanterns."** The Players young, at a long table, painting paper lanterns; puppets lean on the wall behind them as silhouettes. | **Lantern pail:** the jack-o'-lantern pail becomes a round paper lantern (bone paper, orange ribs, a warm inner glow kept under the bloom threshold, `GlowLevels` entry). |
| 30 | **"An old drawing: tall houses, and a moon on a chimney."** Narrow tall houses with washing lines and roof lamps, a crescent-moon puppet sitting on a chimney. It is Bramble Row and the moon puppet (case 02's seed). It does not show the lamp trolley, the wire or anything else that would answer case 02. | **Showman's hat:** a small felt top hat with the owl crest on its band, tilted between her bat ears. The ears still bounce. |

- **Spoiler rule for pages.** A page can be earned before any question is solved, so no page shows the owl
  machine, the cam or arm, the treehouse route, the side yard or the bowing owl. Pages are about the Players, the
  festival and the next place. Captions describe what the drawing shows and never what it means (bible §5). The
  picture carries the meaning for pre-readers, and the art itself contains no text (spec §7).
- **Bat mobile.** It hangs from the eave of Pip's own porch, the arrival porch, which is in the title frame's upper
  right and in the world. Each found bat hangs on it, so it is the diegetic count. At 10, 20 and 30 a ring closes
  around a little paper lantern. The engagement doc says "over Pip's bed"; the porch gives the same title moment
  without a new interior, and the child also sees it in play.
- **Costume trunk.** One of the moving boxes on the arrival porch becomes Pip's dress-up trunk (Use: "Dress up").
  It shows only the pieces she has earned, each with an on/off toggle. There are three slots (cape lining, pail,
  hat), so all three can be worn together. The choice is saved and shows on the title.
- **Witness remarks** (engagement §1.9). There is one new line per piece, said once by a different friend when Pip
  wears it. Proposals: June on the cape ("Stars on your cape! Like my comet!"), Mr. Vale on the pail ("A lantern
  pail. Very proper for the evening."), Aunt Kit on the hat, after the stamp ("A showman's hat! You'd fit right
  in."). Each line has a vignette (D23.1) and a `LineVoices` row.
- **No collect pressure** (engagement §2.3, §2.5, §2.7). No "x / 30" anywhere in the child's flow. The notebook shows
  earned pages and a small stack of folded bats with its number, with no empty slots and no silhouettes of locked
  pages or pieces. The mobile has hooks only where bats hang. There are no pop-ups. The 10th bat wobbles the
  notebook disc once (the sticker-arrival cue), and the page assembles from its ten bats the next time the notebook
  opens. Bats are never hinted by the radio, never respawn, are never spent and never gate a clue. The parent page
  alone shows "17 of 30".
- **Saved** in their own file, `keepsakes.json` (the `StreetStore` pattern: atomic write, previous copy,
  recovery). It holds the found bat IDs, the worn pieces and the remark lines already said. Pages and pieces are
  derived from the count, so they are never stored twice. Replay keeps keepsakes (spec §4: bats never respawn). New
  game clears them (open decision 4).

**Where the 30 hide in case 01's space.** The spec §4 split is a third on the main loop, a third on skilled routes
and a third behind small challenges (each under 30 s, restarting from the nearest safe anchor). IDs are stable and
place-keyed (`bat.lantern_lane.01`–`30`), never case-keyed, so a replay never touches them. The final positions live
in a new engine-free `PaperBatLayout` and are tested. This table is the authored intent:

| # | Tier | Area | Spot | Needs |
|---|---|---|---|---|
| 01 | loop | arrival porch | top of the porch rail post, the first thing in reach (teaches "they flutter when near") | walk |
| 02 | loop | arrival porch | on the moving boxes | tap jump |
| 03 | loop | plaza | low branch of the maple, over the planter | tap jump |
| 04 | loop | plaza | the street lamp's foot, plaza side | walk |
| 05 | loop | bicycle corner | the west hedge's end post, ≥ 1.5 m from the streamer and the bike | tap jump |
| 06 | loop | bench corner | the bench-side fence post, ≥ 1.5 m from the bowl | walk |
| 07 | loop | side yard | inside the corridor, on the ramp's first post, facing in | walk |
| 08 | loop | treehouse deck | the deck's roof beam, on the plaza-facing side away from the cloth's splinter | tap jump |
| 09 | loop | attic loft (inside) | on a loft beam by June's lamp, away from the sill thread | tap jump |
| 10 | loop | hall (inside) | the coat hook by the stair foot | walk |
| 11 | skilled | side yard | the fixed crate (skilled ascent) | tap jump |
| 12 | skilled | side yard | the sloped awning's ridge | high jump |
| 13 | skilled | shed | the shed roof's south edge | chain jump |
| 14 | skilled | plaza west | along the west hedge top | chain jump |
| 15 | skilled | owl house | the porch roof, from the porch rail | high jump |
| 16 | skilled | bench corner | the east neighbour's awning | high jump |
| 17 | skilled | front yard | along the front fence top | walk on the fence |
| 18 | skilled | arrival side | the south-east neighbour's porch roof | high jump |
| 19 | skilled | owl house roof | the far chimney, the ridge end away from the wind wheel | high jump + glide |
| 20 | skilled | treehouse | the treehouse roof, beside its flag | chain jump from the deck rail |
| 21 | challenge | plaza | on the street lamp head | high jump + **ledge grab** (marked) |
| 22 | challenge | shed | the gable top, up the shed's west wall | **wall jump** (marked) |
| 23 | challenge | front yard | on the gate arch | **wall kicks** between the gate posts (marked) |
| 24 | challenge | treehouse → plaza | mid-air on the glide from the treehouse roof to the maple crown | **glide** |
| 25 | challenge | bench corner | the top of three stacked planters and a sill at 2.4 m | **chain jump** to the third stage |
| 26 | challenge | owl house → bench | mid-air on the glide from the porch roof to the east awning | **glide** |
| 27 | challenge | front yard | the low gap under the porch steps' side lattice | **roll** |
| 28 | challenge | plaza east | a hedge-top island in the north-east corner | high jump + glide |
| 29 | challenge | arrival side | a dive gap between the two south porch roofs | **dive** |
| 30 | challenge | treehouse | the top of the treehouse flagpole, the block's highest point | chain jump + ledge grab on the flag bracket (new mark) |

Placement rules, tested in Core (`PaperBatLayoutTests`):

1. **10 / 10 / 10** by tier. Every loop bat needs only walk and a tap jump. No bat lies on a clue's only path, and no
   bat needs a move on the mandatory route.
2. **No bat marks a clue.** Each bat is at least 1.5 m from every examine target that can become a sticker, the
   decoy included. That is the same rule as "no signal sorts clues from non-clues" (`mystery-first.md` §2.7). There
   are no bats in the workshop nook.
3. **No bat leads into a hidden answer.** No bat in the side-yard and treehouse region is visible from the plaza,
   front-yard or bench eye points (`CaseSightlines`). Bats 07–08, 11–13, 20, 22, 24 and 30 sit on inner faces. Once
   inside, they teach the ramp and the bridge. Collectible bats stay still and sit on surfaces; only the opt-in tier-2
   hint trail flies.
4. **Every challenge restarts within 2 s** from a safe anchor (spec §5), with each challenge's anchor named in the
   layout data.
5. Bats in the interior are only in the attic loft and the hall (D24 camera band respected).

These moves already exist (S003), so the 30 bats use them. The revisit secrets of later cases
(`engagement.md` §1.7: a crest, a hidden sketch on a Lantern Lane rooftop) are extra and not part of the 30.

### 2.2 The troupe photo and the owl crest

**The troupe photo** replaces today's "Kit at her booth" motif in `interior.py`, in the same layered paper cut-out
style.

- **Tone.** Sepia-toned (warm paper and bone tones with a faded orange cast, so it reads as old).
- **Size and place.** A landscape frame of 0.7 × 0.45 m, hung higher on the foyer side of the partition so the push
  rod no longer crosses it (the S002.5b gap).
- **Content.** The owl booth, young and freshly painted, with the crest over its shutter. In front of it stand
  **five young Players in costume**, each holding a puppet: Kit (headscarf, the crystal-ball lantern) with the
  **owl**, then the **fox**, the **moth** (kite wings), the **moon** (a crescent on a pole) and the **bell**.
- **Faces.** They are simple cut-paper discs with distinct hair and hat silhouettes. Only Kit is clearly
  recognisable, so later cases can say who is who without repainting. The fifth Player stands half out of the frame
  at the edge: the finale's "one face Pip never met".
- **The glint.** The puppets are cut from gilt paper, so the seed's glint is the hall lamp catching foil. It is
  light, not magic (`world-and-cases.md` §5).

**The owl crest** is a round, wordless emblem: a paper-lantern outline whose flame is a round owl's eye (the round eye
repeats the attic window, as the art bible asks), ringed by **five small stars**, one per Player. It is painted in
jack-o'-lantern orange and bone on night violet and reads at 0.35 m. It goes in three places:

- carved and painted over the booth's ticket shutter on the owl house front, readable from the plaza;
- on the photo's mount, lower corner;
- on sketch page 1, and later on every Players' machine.

It is **not** placed on the workshop mechanism in case 01. An owl emblem on the crank housing would be an owl clue
that is not a sticker, and Q-moved's answer is "an owl" (checklist 3 and 6). For the same reason it is not an
examine target in case 01: it is dressing, so nothing new competes with the owl-booth clue's Use target.

### 2.3 The two cutscenes (bible §5 slots 4 and 5, checklist 16)

**Seed cutscene `c01.seed`** (about 13 s). It plays once, straight after the reveal, and can be rewatched from the
case page. The reveal loses its last beat (the photo push), so it becomes about 25 s and ends on Kit's reopening.
The night still ends on the seed: every final card ends on the seed (`world-and-cases.md` §6).

| # | Beat | Shot | Caption (key, perception only) | s |
|---|---|---|---|---|
| 1 | The old photo | page-turn into the hall; the troupe photo, wide | existing `c01.cutscene.reveal.photo` "In the hall, an old photo: Aunt Kit and friends." | 3.0 |
| 2 | The owl wakes in the photo | push to the owl puppet in young Kit's hands; the lamp's light runs along its gilt edge | "[The little owl catches the light.]" | 3.0 |
| 3 | The next one | a slow pan along the five puppets; the **moon** glints, softer | "[Then the moon, too.]" | 3.0 |
| 4 | The same crest | the crest on the photo's mount, then a match cut to the crest over Kit's open booth; Pip (on the porch) looks up at it | "[An owl crest, on the booth too.]" | 3.0 |
| 5 | Back | the drift into the play camera behind Pip, where she stood | — | 1.0 |

The moon is the glowing puppet because of case 02's seed (`world-and-cases.md` §4). Which puppet glows is data, so a
re-ordered season changes one ID. No beat shows a rooftop light or anything that raises case 02's question before
case 02 exists.

**Street remembers `c01.street_remembers`** (about 18 s). It plays once, on the **first load after the stamp**
(Continue the next time), before control. That is when D29 lays the memories over the world. It can be rewatched
from the case page. It never plays while a replayed case is open (its memories rest, D29). Re-stamping a replay and
reloading plays it again, because the seen set clears with the replay.

| # | Beat | Shot | Caption | s |
|---|---|---|---|---|
| 1 | Back on the lane | crane down over Lantern Lane (it rhymes with the opening's first shot); the booth lit | "Back on Lantern Lane." | 3.0 |
| 2 | On the hour | the booth's little bell dings; Mr. Vale `CheckWatch`, then looks up at the house | "[Ding! Mr. Vale checks his watch.]" | 3.0 |
| 3 | The show | the wind wheel whirrs; the rooftop owl bows three times on three knocks | "[Knock, knock… knock. The owl bows.]" | 3.5 |
| 4 | Bea | Bea by her bike, the owl sticker on the basket; she `Wave`s at the owl | "[Bea waves at the owl.]" | 2.5 |
| 5 | June | June at the foot of her treehouse way, `Wave` toward the porch | — | 2.5 |
| 6 | Kit | Kit at her open booth waves toward the porch; drift into the play camera behind Pip on the arrival porch | — | 3.5 |

It uses existing clips only (`CheckWatch`, `Wave`, `OpenBooth` idle). The friends' S003a "remembers" greetings then
greet Pip in play.

**"Bows on the hour" in play.** After the stamp, the owl's 9 s wind cycle (`StreetMemoryStage`) becomes a show clock.
Every 90 s of play on the street, the booth bell dings, the owl bows three times, and Vale checks his watch if he is
in view. It is a pure `BoothShowClock` in Core, tested at 30/60 fps. Also, knocking three times on the booth's
shutter (Use) makes the owl bow (engagement §1.9). This is a touch reward after the stamp and inert during the case,
so no clue changes.

### 2.4 Languages

- **Packs stay custom JSON** (resolves D3 and D4 for now). A pack is the existing `StringTable` JSON plus a manifest:
  locale, content version, supported game-content schema, size, bundle reference and SHA-256 (spec §7). The bundled
  fonts cover the first languages (§1), so the first packs carry no font and **Addressables stays uninstalled**. A
  non-Latin language later brings a per-target font bundle, and that is when D4 is revisited.
- **pt-BR first.** The owner is Brazilian, and the playtest children are likely to read Portuguese. Opus drafts it
  from the translator contexts:
  - short sentences for 6–9;
  - Bea fast, Vale slow and dry, June conspiratorial, Kit theatrical;
  - no fear words ("perigo", "monstro", "morto");
  - the conclusion lint (CASE-04) extended with a pt-BR word list.

  The owner reviews it on a page with EN | pt-BR | context | length side by side, plus frames of the screens in
  pt-BR (open decision 2). Babble voices carry no language (D20.1), so nothing is re-recorded. "Candlebrook" stays
  untranslated (D27.1). The stitched "Spooky Street" logo is the product name and stays art.
- **Second language (proposal):** es-419 (same fonts, large audience). It ships only when a native reader has
  reviewed it, so it is outside S004 unless one is found.
- **Plurals.** CLDR categories in `StringTable` (`one`/`other` and so on) with an engine-free `PluralRules` for en,
  pt (0 and 1 are "one"), es, fr and de. Used by "{0} stickers", the bat stack and the parent page.
- **First launch** follows the device's preferred language when a matching pack is installed or bundled. Otherwise
  it uses English. Language and saves stay independent (spec §7).
- **Language screen** (title and pause): installed and available packs with native name, size, progress and
  Use / Download / Remove. Switching between installed languages needs no gate. **Download and remove are behind
  the parent gate** (the gate's own doc names the language download). The catalog is fetched only when a grown-up
  opens the download list. There are no background calls, and nothing about the child is sent (engagement §2.6).
- **Pipeline.** Download to staging, then validate version, hash, schema, font coverage (every character of the pack
  against a generated coverage table of the bundled fonts) and loadability, then activate atomically. The old pack is
  kept on any failure. A missing key falls back to English. Offline, installed packs stay selectable. Removing the
  active pack reverts to English. The pseudo pack (+35 %) is published to the host and hidden from the production
  catalog (development builds only).

### 2.5 The parent progress page ("For grown-ups", behind the gate)

It is reached from the title and pause through the gate, next to settings and credits. It is read-only and
**computed on the device from the local files. Nothing leaves the device**: no account, no analytics, no share or
export button (engagement §2.6, §2.8).

| Section | Shows | Source |
|---|---|---|
| Cases | "Three Knocks on Lantern Lane": solved, or the questions solved so far as their picture chips | case save |
| The child's own answers | the answer comics of solved questions, read-only (the notebook scrapbook) | case save, `CaseNotebook` |
| Places explored | Lantern Lane, the owl house inside, the treehouse | facts |
| Friends made | the face discs of everyone Pip talked to | facts |
| Keepsakes | "17 of 30 paper bats", sketch pages earned, costume pieces (the only place with a total) | `keepsakes.json` |
| Time played this week | a total ("about 1 h 20 min") and seven day dots, from local per-day minutes; no goals, no comparison, no "play more" | new `playtime.json` |
| What your child practises | three short lines: noticing details, comparing two accounts, explaining with evidence | strings |
| Privacy | no ads, no purchases, no accounts, nothing leaves this device; language downloads fetch only the pack file | strings |

Wrong notebook tries are never stored (spec §7), so they are never shown. The playtime ledger counts only unpaused
play, caps each tick (a suspended app never inflates it), keeps the last 14 local days, and is never shown to the
child or used for anything else. **Credits** sit beside it: original art and audio by script, generated voices and
music (D20.2, provenance), Andika and Sniglet under the SIL OFL with the license text in the app, Unity and Blender.

### 2.6 Dialogue portraits

Each speaker gets a painted portrait rendered from their real Blender model: Pip, Bea, June, Mr. Vale and Aunt Kit,
each in five expressions matching the babble emotions (neutral, happy, surprised, thinking/unsure, whisper). That is
25 portraits in one atlas, in the felt frame of `DialogueView` and the bubble's face disc. The expression comes from
the line's `LineVoices` emotion, so the data already exists and a translation never changes it. Where a model lacks a
brow or mouth shape, the portrait script adds it (graphics agent).

## 3. Streams

Seven streams run in three waves, each on its own `feature/s004*-<topic>` branch. They are named so the touched files
overlap as little as possible. The shared hotspots and their rule:

- **`en.json`**: each stream adds its keys as one contiguous block; whoever merges second rebases. That is trivial
  for a JSON array.
- **`CaseDirector`**: one-line hooks only.
- **`NonverbalMenus`**: only stream E edits it.
- **`NotebookView`**: new partial files only.

Capture frame numbers are reserved per stream, because S002.5b collided twice.

### Stream F1: UI navigation base (wave 1, first)

- **Goal.** One way for every screen to work by touch, gamepad and keyboard before new screens are built.
- **Scope.** A `UiNavigate` action map in `GameInput` (move, submit, back, page left/right, rewatch). The deduction
  board's inline bindings move there, with no behaviour change. An engine-free `UiFocusGraph` in Core (focus order,
  wrap, default focus per screen) with tests. A `UiFocus` runtime helper that draws the stitched focus outline (the
  key ring retired from case content and kept for UI focus, `mystery-first.md` §8). Glyphs per scheme through
  `ControlGlyphs`.
- **Files.** `Runtime/Controls/GameInput.cs`, `Runtime/UI/NotebookView.Deduction.cs` (bindings only), new
  `Core/Flow/UiFocusGraph.cs`, `Runtime/UI/UiFocus.cs`.
- **Reuses.** `DeductionCursor`, `ControlSchemeTracker`, `ControlGlyphView`.
- **Acceptance.** `UiFocusGraphTests` (every element reachable, no traps, back always exits). `DeductionCursorTests`
  and `CutscenePlayTests` unchanged and green.
- **Frames 80–81** (added in F1): 80 the focus outline on a notebook tray sticker (keyboard); 81 on a slot with a sticker held (gamepad).
- **Cost and model.** 0.5 agent-day, Opus. No dependencies. No TestFlight.

### Stream A: keepsakes, the paper bats with meaning (waves 1–2)

- **Goal.** 30 findable bats that teach routes and vantage points, and turn into sketch pages, costume pieces and the
  mobile (§2.1).
- **A1 scope (Core and world).**
  - **Core:** new `Core/Keepsakes/PaperBatLayout.cs` (30 placements, tier, needed moves, safe anchor),
    `Keepsakes.cs` (state and rules: unlock at 10/20/30 exactly once, derived pages and pieces),
    `KeepsakeStore.cs` (`keepsakes.json`), `SketchPages.cs`, `CostumePieces.cs` and `CostumeRemarks.cs`.
  - **World:** new `Runtime/World/PaperBatCollectibles.cs` (flutter when near, pickup puff, paper sound from the
    S003 library). One new ledge mark on the flag bracket in `CollisionRules.TraversalMarks`. A load and save hook
    in `CaseDirector`.
  - **Greybox:** the existing bat mesh until B2 lands.
- **A2 scope (UI and dress-up).**
  - **Notebook:** `Runtime/UI/NotebookView.Keepsakes.cs` (the keepsakes page after the case page, the bat stack, the
    page assembling from ten bats).
  - **Dress-up:** `Runtime/Case/CostumeTrunk.cs` (the Use target on the porch box, verb `ui.verb.dress_up`) and
    `Runtime/Player/PipCostume.cs` (attach pieces to Pip's sockets, title included).
  - **Mobile:** `Runtime/World/BatMobile.cs`.
  - **Remarks:** three remark lines with vignettes in `C01Vignettes.Street.cs` and `LineVoices` rows.
  - **Continue:** `ui.title.continue_detail` wired (count only).
- **Reuses.** The `StreetStore` pattern, `EnvironmentLayout` placement types, `CaseSightlines`, puppet atlas
  `x.paper_bat`, `StickerCard` styling, the move and safe-anchor system.
- **Acceptance tests.**
  - `PaperBatLayoutTests`: the rules in §2.1.
  - `KeepsakeRulesTests`: unlocks once, never respawn, replay keeps, New game per decision 4, idempotent.
  - `KeepsakeStoreTests`: interruption at every step, recovery, newer file kept.
  - A lint test that no child-facing key contains a total.
  - PlayMode: pick up bats by walking; the 10th wobbles the disc and adds page 1; dress up, reload, still worn, shown
    on the title; a remark plays once; a bat on each challenge tier collected by a scripted route (lamp ledge, shed
    wall jump, a glide).
- **Frames 65–68:** 65 a bat fluttering open by Pip on the treehouse deck; 66 the keepsakes page with page 1; 67 the
  trunk open with Pip in the star cape and hat; 68 the title with the mobile (12 bats) top right, clear of the logo
  and buttons.
- **Cost.** A1 2, A2 1.5 = **3.5 agent-days**, Opus.
- **Dependencies.** A2 after F1, and after B2 for final art (it can merge on greybox art and swap).
- **TestFlight.** Yes, in TF1.

### Stream B: art in Blender (waves 1–2, `fable`)

- **Goal.** Original art for the seeds and keepsakes, every asset rendered and looked at before it enters the game
  (CLAUDE.md).
- **B1 scope (seeds, needed by C).**
  - `tools/blender/env/interior.py`: `photo_motif` becomes the troupe photo, plus the frame size and height.
  - A shared `crest()` in `env_lib.py`.
  - The crest over the booth shutter in `owl_house.py`, and on the photo mount.
  - Re-export the interior and house FBX.
- **B2 scope (keepsakes).**
  - The collectible paper bat (black with the cream pencilled inner fold, a flutter-open shape key).
  - The bat mobile (three rings, hooks, lanterns).
  - The trunk lid on one moving box.
  - Pip's three costume pieces fitted to her rig (`tools/blender/chars/costume_pieces.py`, exported as separate
    meshes on her sockets; no change to her body FBX).
  - The three sketch pages as pencil drawings (Blender Freestyle line renders of scripted scenes, in
    `tools/blender/pages/`, baked to `Content/Resources/UI/SketchPages/*.png`).
- **Reuses.** The paper cut-out helpers (`frame_art`, `prism`, the palette `E[...]`), Pip's rig and sockets, the
  street and house kits for the sketch scenes.
- **Acceptance.**
  - Blender review renders in `docs/evidence/s004/art/`: the photo at phone size and close, the crest at 0.35 m from
    the plaza distance, each costume piece on Pip front, side and in glide, the pages at notebook size.
  - Art bible scorecard of at least 4/5 on finished pieces.
  - `GlowLevels` test for the lantern pail.
  - `ModelImportRulesTests` green.
  - No cloth silver anywhere (art bible).
- **Frames 69–70** (CI): 69 the troupe photo in the hall at the gameplay camera; 70 the booth crest from the plaza.
- **Cost.** B1 1, B2 2 = **3 agent-days**, `fable` at medium effort; if Fable does not improve the graphics, switch to
  Opus 5.5 at high effort.
- **Dependencies.** None.
- **TestFlight.** Ships in TF1.

### Stream C: street remembers and seed cutscenes, the show clock (wave 2)

- **Goal.** §2.3.
- **Scope.**
  - `Core/Cutscenes/C01Cutscenes.cs`: the reveal trimmed to end on the reopening; new `Seed` and `StreetRemembers`.
  - `CutscenePolicy.cs`: the seed after the reveal; street remembers on the first load after the stamp, never while
    the case's memories rest; rewatch both once stamped.
  - New `Core/Street/BoothShowClock.cs`.
  - `Runtime/Case/CaseCutscenes.cs`: new world effects (the booth bell, the crest anchor, the puppet glint
    positions on the new photo).
  - `Runtime/Case/StreetMemoryStage.cs`: the show clock and the three-knock touch reward.
  - `Runtime/UI/NotebookRewatch.cs`: two more discs on the case page.
  - `LocalizationKeys.Cutscenes.cs` and about 9 keys.
- **Reuses.** The whole S002.5b system, S003a gestures, the voices and music cues (`music_reveal`, babble
  `kit.happy`), the seen set.
- **Acceptance tests.**
  - `CutsceneTests` extended. Both new cutscenes have every cue once and in order at 24/30/60 fps; skip at any time;
    reduced motion. Both are 10–40 s, use existing clips only, and their captions are keys with context and no
    conclusion words.
  - **Never ahead.** For every reachable state, seed and street remembers exist only when stamped. Street remembers
    is absent while a replay is open, and plays once, on the first load after the stamp, not in the session where
    the stamp happened.
  - `BoothShowClockTests`.
- **PlayMode.** Solve, reveal, then the seed plays; reload plays street remembers once, then control on the porch;
  rewatch both by touch, keyboard and gamepad.
- **Frames 71–72:** 71 the moon puppet glinting in the troupe photo; 72 Vale checking his watch as the owl bows.
- **Cost and model.** **1.5 agent-days** (roadmap §1.1 said 0.5; the show clock, touch reward and the policy rules add
  1), Opus.
- **Dependencies.** B1 for final frames (it can be built against today's photo and re-anchored).
- **TestFlight.** Yes, in TF1.

### Stream D: language packs and pt-BR (waves 1–3)

- **Goal.** §2.4. LOC-01 passes, and pt-BR is playable.
- **D1 scope (system, wave 1).**
  - `Core/Localization/`: `PluralRules.cs`; `StringTable.cs` plurals with schema 2 (schema 1 still loads);
    `PackManifest.cs`, `PackCatalog.cs`, `PackInstaller.cs` (staging, hash, validation and activation over the save
    file system abstraction, with a pointer file and its previous copy); `FontCoverage.cs`, generated.
  - `Runtime/Localization/`: `PackDownloader.cs` (UnityWebRequest behind an interface, HTTPS only, configured host);
    `Localizer.cs` (startup by the device language).
  - `Runtime/UI/LanguageScreen.cs`, built on F1. It replaces the dev pill in development builds.
  - `tools/l10n/`: the pack builder (manifest, SHA-256, coverage), the pseudo pack and a CI check in
    `check-local.sh`.
- **D2 scope (pt-BR, waves 2–3).**
  - `Content/Resources/Localization/pt-BR.json` (bundled or as a pack, per decision 1).
  - The owner's review page (`tools/l10n/review_page.py` → an HTML table plus frames).
  - Pass 1: everything on `main` (about 200 keys). Pass 2: the S004 delta once A, C, E and G strings freeze.
- **Reuses.** `StringTable`, `LocalizationCatalog` fallback, `PseudoLocalizer`, `SaveSlotStore`, `PreferencesData.Locale`,
  `ParentGate`.
- **Acceptance tests.**
  - Core tests for interrupted download, insufficient storage, old catalog, corrupt file (hash mismatch), newer
    schema, missing glyph rejected, offline restart, removing the active pack, and progress before and after a
    switch (spec §7 list).
  - Plural tests per locale.
  - pt-BR completeness: every key present; contexts' length limits within +35 %; the conclusion and fear-word lint in
    pt-BR.
  - PlayMode against a local HTTP fixture (`HttpListener` in the test): download the pseudo pack, interrupt,
    relaunch offline, remove.
  - LOC-01 recorded in the evidence doc.
- **Frames 73–74:** 73 the language screen (pseudo pack downloading); 74 the notebook's Q-entry page in pt-BR, plus a
  review sheet of the title, HUD, notebook, recap and parent page in pt-BR and pseudo.
- **Cost.** D1 2.5, D2 1.5 = **4 agent-days**, Opus.
- **Dependencies.** D1 after F1 for the screen only. D2 pass 2 after the other streams' strings.
- **TestFlight.** pt-BR in TF1 if D2 pass 1 is merged; the full system in TF2.

### Stream E: parent page, settings and credits (wave 2)

- **Goal.** §2.5, and the settings the accessibility pass needs.
- **Scope.**
  - New `Core/Parent/ParentProgress.cs` (the view-model builder from the case save, street, keepsakes and playtime;
    no answer text beyond the child's solved comics).
  - New `Core/Parent/PlaytimeLedger.cs` and its store (`playtime.json`).
  - `Runtime/UI/ParentPage.cs` and `Runtime/UI/CreditsPage.cs`.
  - `NonverbalMenus.cs` (owned here): the settings card gains reduced motion, large captions, opaque captions,
    haptics, invert Y, camera sensitivity and left-handed (all existing `PreferencesData` fields), "For grown-ups" and
    "Credits" entries, and the language entry.
  - An optional thin iOS plugin behind an interface that reads the system Reduce Motion as the default.
- **Reuses.** The gate, the settings card, `NotebookView` comic rendering (read-only), `PreferencesData`, F1 focus.
- **Acceptance tests.**
  - `ParentProgressTests`: every reachable case state; no field carries an unsolved answer; totals only here.
  - `PlaytimeLedgerTests`: pause and suspend don't count; day roll-over; 14-day window; a clock set backwards.
  - A test that no network type is referenced from `Core/Parent`.
  - PlayMode: the gate, then the page by touch, gamepad and keyboard; toggling reduced motion changes cutscene
    blends; large captions change the caption size.
- **Frames 75–76:** 75 the parent page mid-case; 76 the settings card with the accessibility toggles, plus a credits
  review frame.
- **Cost.** **2 agent-days**, Opus.
- **Dependencies.** F1. A1 for keepsake counts (it can stub at 0).
- **TestFlight.** TF2.

### Stream F2: the accessibility pass (wave 3)

- **Goal.** A11Y-01 and "every interaction with every scheme" (D25, bible §5 portability).
- **Scope.**
  - An audit of every screen with gamepad, keyboard and touch: title, recap, pause, notebook (pages, tray, slots,
    rewatch, keepsakes), the gate, settings, the parent page, credits, the language screen, the trunk, dialogue and
    cutscene skip. Fixes go in each screen's own file.
  - Prompts per scheme everywhere: no "tap" or "WASD" in shared text (a lint on `en.json` and pt-BR).
  - Sound-off equivalents for every required signal: knocks, floor hum and the wind wheel already have captions with
    direction; verify each one.
  - The A11Y-01 scripted run: ramp-only route, muted audio, haptics off, large captions, reduced motion, deduction by
    tap-tap, cutscenes skipped with each scheme.
  - MAP-01 rechecked with the bat placements.
- **Reuses.** F1, `CaptionStrip`, `CutsceneSkipGate`, the case route tests.
- **Acceptance tests.** A PlayMode `AccessibilityPassTests` (one full case by gamepad only and one by keyboard only,
  Core-driven route, cutscenes skipped; the same run under A11Y-01 settings) and the per-screen reachability tests.
- **Frames 77–78:** 77 the deduction tray with the focus outline and gamepad glyphs; 78 the language screen with
  keyboard focus.
- **Cost and model.** **2.5 agent-days**, Opus.
- **Dependencies.** After A2, D1, E and G wiring (it audits their screens).
- **TestFlight.** TF2.

### Stream G: dialogue portraits (waves 1–2)

- **Goal.** §2.6: faces that act on stickers and bubbles.
- **G-art scope.** `tools/blender/export/portraits.py`: 25 renders from the real models at a fixed portrait camera
  and light, baked into `Content/Resources/UI/Portraits/portraits.png`.
- **G-wiring scope.** `Core/Nonverbal/PortraitAtlasLayout.cs`; `Runtime/UI/DialogueView.cs` (felt frame and bubble
  face disc by emotion; the initial stays as the fallback).
- **Reuses.** The character FBX sources, `LineVoices` emotions, the puppet atlas loading pattern.
- **Acceptance.**
  - Review renders in `docs/evidence/s004/portraits/` (each face legible at 96 px, expressions distinct at a glance).
  - `PortraitAtlasLayoutTests` (every speaker × emotion present, no overlap).
  - A PlayMode check that a line picks its emotion's portrait.
- **Frame 79:** Bea's surprised portrait on a speech bubble in the plaza.
- **Cost.** Art 1 (`fable`), wiring 0.5 (Opus) = **1.5 agent-days**.
- **Dependencies.** None.
- **TestFlight.** TF2.

### Summary

| Stream | What | Days | Model | Wave | TestFlight |
|---|---|---|---|---|---|
| F1 | UI navigation base | 0.5 | Opus | 1 | — |
| A | paper bats, pages, costumes, mobile | 3.5 | Opus | 1–2 | TF1 |
| B | troupe photo, crest, keepsake art | 3 | `fable` | 1–2 | TF1 |
| C | seed and street-remembers cutscenes, show clock | 1.5 | Opus | 2 | TF1 |
| D | language packs, pt-BR | 4 | Opus | 1–3 | TF1 (pt-BR), TF2 |
| E | parent page, settings, credits | 2 | Opus | 2 | TF2 |
| F2 | accessibility pass, A11Y-01, MAP-01 | 2.5 | Opus | 3 | TF2 |
| G | dialogue portraits | 1.5 | `fable` + Opus | 1–2 | TF2 |
| **Total** | | **18.5** | | | **2 builds** |

With four sessions in parallel, about six working days. **TestFlight**: two builds, each with `[testflight]` in its
`--no-ff` merge commit. **TF1** comes after A, B and C (and D2 pass 1 if green), and suits a family playtest in
Portuguese. **TF2** comes at the end of S004. Every other merge goes without it. CI: intermediate commits use
`[skip ci]`, with one validation run per stream when it is ready.

**What the owner gets:**

- **TF1.** Paper bats that flutter open all over Lantern Lane and turn into old sketch pages, a star-lined cape, a
  lantern pail and a showman's hat, and a mobile on Pip's porch. Kit's real troupe photo with the owl crest. A night
  that ends on the moon puppet glinting. A street that welcomes the child back with the owl's show on the hour. The
  game in Portuguese.
- **TF2.** A page for grown-ups with progress and the week's playtime, accessibility settings, every screen by
  gamepad and keyboard, downloadable languages, and faces that react in dialogue.

## 4. Out of scope (and where it goes)

- **S005:** device profiling and the formative playtest.
- **When a non-Latin language is chosen:** VoiceOver/screen-reader labels beyond `AccessibleName`; non-Latin
  languages and their font bundles (D4).
- **Case 02's design doc:** revisit secrets that need later moves; who folded the bats.
- **Case 02 approval:** any rooftop light or other preview of case 02.
- **Wanted, with no stage yet (S002.5b gaps):** a lit sign on Kit's booth, Bea's sticker as a decal, and June's
  outdoor loop.

## 5. Narrative bible checklist (§6), run on every S004 item

Columns: **K** keepsakes (bats, pages, costumes, mobile), **P** photo and crest, **C** cutscenes and show clock,
**L** languages, **G** parent page and settings, **X** accessibility, **D** portraits.

| # | Check | Result |
|---|---|---|
| 1 | True story, kind, explains every clue | No item changes case 01's true story. K, P, C and D add no clue or testimony. The photo and crest are dressing and seed. |
| 2 | Questions raised by the world | Unchanged. No page, cutscene or remark raises or names a question. |
| 3 | Answers only from placed stickers | K: pages show no mechanism, route or owl show; bats stay 1.5 m from clue targets. P: no crest on the mechanism, no new examinable. C: seed and street remembers exist only after the stamp (tested). G: shows only the child's solved comics. |
| 4 | Diegetic attraction, no precision moves on a clue's path | K: bats flutter open when near, a perception and not a marker; challenge bats are off every clue path; loop bats need walk and tap jump. |
| 5 | Testimony as perception | Unchanged. The remark lines are greetings, not testimony. |
| 6 | Nothing states a conclusion | Page and cutscene captions describe what is seen ("[Then the moon, too.]"). The conclusion lint covers the new keys and pt-BR. No crest near the owl clue. |
| 7 | Hints to areas, moves only | Bats are never hinted. The tier-2 trail stays the only flying bat. |
| 8 | At most one decoy | Unchanged. No bat sits at the streamer. |
| 9 | Reveal pays off the child's clues and action | The reveal is unchanged apart from its last beat moving to the seed. Street remembers shows effects the child caused. |
| 10 | No deadlocks, any order | K and C change no case fact; keepsakes live in their own file; replay and New game rules are tested. |
| 11 | The street visibly remembers | C: the street-remembers cutscene, the owl's show on the hour, the knock-three-times reward. |
| 12 | Seed and continuity | P: the five Players (the fifth half out of frame), five stars on the crest, the moon glint (case 02). K: page 3 (Bramble Row). Faces stay generic for later cases. Machines stay mechanical: the glint is lamp light on gilt paper. |
| 13 | Mechanics serve mystery or exploration | Bats teach routes and vantage points (the bench, treehouse and rooftop views back on the house). Pages tell the street's story. Costumes are cosmetic. The mobile is the count. |
| 14 | Strings in `en.json` with context, stable IDs | Every new line, caption, page caption, settings and parent string is keyed with context through `LocalizationKeys`. Bat, page and piece IDs are stable and never display text. pt-BR uses the same keys. |
| 15 | Guardrails | No totals, timers, streaks or pop-ups in the child's flow. Playtime is for parents only. Downloads are behind the gate and send nothing. No purchases, ads or sharing. |
| 16 | Cutscenes short, skippable, rewatchable, in-engine, six purposes, never ahead | Seed about 13 s (slot 5), street remembers about 18 s (slot 4). Both are skippable by every scheme and rewatchable from the case page, built from real models and existing clips, never before the stamp, and show no clue, route or hidden place. |

L, G, X and D pass 1–13 trivially (no case content). They are covered by 14–16 and by A11Y-01 and LOC-01.

## 6. Open decisions for the owner

1. **pt-BR inside the app or downloaded?** (a) Bundle pt-BR in the app beside English, so a Brazilian phone starts
   in Portuguese offline from install, and prove the download path with the pseudo pack. (b) Ship only English
   inside and pt-BR as a downloadable pack (the spec's default). **Recommended (a):** it is about 60 KB, the
   playtest children likely read Portuguese, and the spec's download capability is still delivered and tested.
2. **Names in Portuguese, and the review.** (a) Translate the place and troupe names: Lantern Lane → "Rua das
   Lanternas", the Lantern Lane Players → "a Trupe da Rua das Lanternas", Aunt Kit → "Tia Kit". Keep Candlebrook
   (D27.1) and people's names (Pip, Bea, June, Mr. Vale → "Sr. Vale"). (b) Keep every English name. Either way, the
   owner reads one review page (EN | pt-BR | context, about 1 hour). **Recommended (a):** a 6-year-old repeats
   "Rua das Lanternas" easily, and the bible's rule that place names are compounds a child can say holds in their
   language.
3. **Where the downloadable packs live (no spend).** (a) GitHub Pages on this public repo, published from a
   `gh-pages` branch by a workflow. The owner enables Pages once in the repo settings; sessions cannot. (b) Raw files
   in the repo through raw.githubusercontent.com: no setup, but not meant as a download host. (c) No real host
   before release; only a test fixture. **Recommended (a):** it is free and HTTPS, and the spec's "configured HTTPS
   catalog host" is met for the pseudo pack now. The production catalog stays empty until a reviewed second language
   exists.
4. **What a confirmed New game clears** (confirms D29 item 4 and extends it). (a) Everything: case, street,
   keepsakes (bats, pages, costume pieces). Pip is the new kid again and every bat is back in the world, and the
   confirmation shows it with pictures. (b) Keep keepsakes. But found bats never respawn, so the new game's street
   would have no bats to find. **Recommended (a).** The parent page's playtime is not progress and is kept.

Everything else in this plan takes the recommendation above unless the owner says otherwise: the costume pieces,
page subjects, crest design, the mobile on the porch and the cutscene beats are shown as renders and frames for
review before they enter the game.
