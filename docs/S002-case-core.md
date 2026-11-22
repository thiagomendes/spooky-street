# S002 (part 1): case core, engine-free

Branch `feature/s002-case-core`. This pass builds the logic of the mystery in `Assets/SpookyStreet/Core/Case` and
`Core/Save`, plus a minimal disk adapter in `Runtime/Save`. It adds no UI, no scene wiring and no art. The wiring pass
connects it to the world.

## Fact table (`Core/Case/CaseDefinition.cs`, the one definition)

| Beat | Spec name | Stable ID | Role | Implies |
|---|---|---|---|---|
| Arrival | heardKnocks | `c01.arrival.knocks_heard` | Route | |
| Conflicting views | beaSpoke | `c01.plaza.bea_spoke` | Optional | |
| Conflicting views | valeSpoke | `c01.plaza.vale_spoke` | Optional | |
| Conflicting views | checkedView | `c01.bench.view_checked` | Optional | |
| Conflicting views | examinedStreamer (§4 bicycle foil) | `c01.bicycle.streamer_examined` | Optional | |
| Physical evidence | examinedCloth | `c01.treehouse.cloth_examined` | Optional | |
| Physical evidence | enteredByWindow | `c01.attic.entered` | Route | |
| Physical evidence | atticFirst (window entry before any interview) | `c01.attic.entered_first` | Optional | |
| Physical evidence | examinedSill | `c01.attic.sill_examined` | Optional | |
| Find June | foundJune | `c01.attic.june_found` | Route | enteredByWindow |
| Find June | frontOpen | `c01.foyer.front_open` | **Required** | enteredByWindow |
| Find the source | sawCam | `c01.workshop.cam_seen` | Route | |
| Find the source | linkedArm | `c01.workshop.linked` | Route | sawCam |
| Find the source | testedOwl | `c01.workshop.owl_tested` | **Required** | linkedArm |
| Resolve | caseClosed | `c01.porch.case_closed` | Route | |
| Resolve | foundSweet (§4 bowl after solving) | `c01.bench.sweet_found` | Optional | |

Saves store only the stable IDs. Safe anchors are `c01.anchor.*` (`CaseAnchors`).

## Solvability rules and where they are enforced

| Rule (spec §3) | Enforcement | Test |
|---|---|---|
| The world accepts correct actions early | No action checks conversations. Physical implications are added automatically (latch ⇒ window entry, arm ⇒ cam seen). | `TheWorldAcceptsCorrectActionsEarly` |
| Entering the attic first awards the route proof, and June acknowledges it | `EnterAttic` gains `enteredByWindow`. With no witness spoken it also gains `atticFirst`, which picks June's shortcut greeting. | `EnteringTheAtticFirst…`, CASE-02 |
| Past tense after the door opens | Every line has a `LineCondition` (requires/forbids facts). `frontOpen` switches Vale to "It was shut when I looked." | `EveryLineInEveryReachableStateMakesTemporalSense` |
| Ending = frontOpen + testedOwl + porch | `ArriveAtPorch` closes the case only with `EndingRequires`. Otherwise it returns `NotYet`. | `TheEndingNeeds…` |
| Optional evidence never deadlocks | Only two facts gate anything. A BFS over every reachable state checks that the mandatory route still closes the case, and that optional facts never change route gains. | `NoReachableStateCanDeadlockTheCase`, `OptionalFactsNeverGateAnyAction` |
| Crank before the arm is a tap and a gap | `TurnCrank` without `linkedArm` returns `CrankGap` and a caption, with no fact. | `CrankBeforeTheArm…` |
| Repeats change no persistent state twice | Facts are a set. A repeat returns the same state instance and no gains, so no autosave and no cutaway. | `EveryActionIsIdempotentFromEveryReachableState` |
| Reset during a reveal rebuilds the world from facts | `CaseWorldView.From(state)` derives the door, June, panel, owl, booth and sweet from facts only. | SAVE-01 tests |

## Save contract (`Core/Save`)

- **File content:** `case-c01.json` holds `schemaVersion`, `caseId`, `facts` (in acquisition order), `anchorId` and
  `notebook`. `preferences.json` is a separate slot.
- **Write plan (`SaveWritePlanner`):** write `*.tmp` durably. If there is no latest file, move the temp into place.
  Otherwise, replace the latest with the temp. A valid latest becomes `*.prev.json`; a corrupt latest is discarded, so
  it never pushes out the good previous copy.
- **Load (`SaveSlotStore.Load`):** reads the latest file, then a complete temp file, then the previous copy.
  - A newer schema is reported and the file is preserved: writes are blocked until the player explicitly starts fresh,
    and the file is then moved to `*.kept.json`.
  - If nothing is readable, the load reports `Unreadable`, and the player can start fresh.
- **Validation:**
  - An unknown anchor, or an inside anchor without the window entry, falls back to the plaza.
  - Unknown fact IDs are dropped and implied facts are restored.
  - The notebook is rebuilt from the facts.
  - A `Migrations` table upgrades older schemas step by step. It is empty at schema 1.
- **Replay** writes a fresh case save and leaves `preferences.json` untouched.
- **Disk adapter:** `Runtime/Save/DiskSaveFileSystem` uses System.IO: `FileStream.Flush(true)` and `File.Replace`,
  with a rename fallback. It compiles against netstandard2.1 alongside Core (checked locally; Unity not run).

## Evidence (cloud session, no Unity)

- `./tools/check-local.sh`: all checks passed; `dotnet test tools/core-tests`: **197 passed** (150 before, 47 new).
- The crash-injection test interrupts each step of the save (truncated temp, between the two renames of a replace).
  It checks three starting states: first save, no previous copy, and full rotation. Every case reloads the old or the
  new save.
- **CASE-03 is only half done here.** The sightline contract and the evaluator are tested. The geometric check still
  needs S001 block markers and hedge heights: bench blocked by the real hedge, treehouse clear. It is **pending**.
- **Not run:** Unity EditMode run of the new tests (CI), PlayMode, device. No pass is claimed for them.
