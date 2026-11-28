using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// The living street's test slice in play (docs/living-street.md §9 phase 1, D33/D34; docs/living-street-slice.md):
    /// the east lane, the crowd on its loops, the three doors that answer a ring, the paper lantern table, Pip's lantern
    /// on her porch with groups ringing her bell once it is lit, the neighbourhood file and the frame-time readout. The
    /// case director ticks it and owns the modal state; every rule lives in Core (<see cref="CrowdClock"/>,
    /// <see cref="NeighbourDoors"/>, <see cref="NeighbourhoodStore"/>, <see cref="LivingStreetRules"/>).
    /// </summary>
    public sealed class LivingStreetStage : MonoBehaviour
    {
        /// <summary>Crowd sounds and calls are heard (and captioned) only this close to Pip.</summary>
        public const float HearingDistance = DoorCalls.HearingDistance;

        /// <summary>A treat flies from hand to hand in this long.</summary>
        public const float TreatFlightSeconds = 0.45f;

        private sealed class Flight
        {
            public Transform Candy;
            public Vector3 From;
            public Vector3 To;
            public float Time;
        }

        private readonly List<CrowdEvent> _events = new List<CrowdEvent>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Transform> _candyPool = new List<Transform>();
        private NeighbourhoodStore _store;
        private MaterialLibrary _materials;
        private Transform _pip;
        private CaptionStrip _captions;

        public LivingStreetSet Set { get; private set; }

        public CrowdView Crowd { get; private set; }

        public NeighbourDoors Doors { get; } = new NeighbourDoors();

        /// <summary>Which neighbours' doors have called while Pip stays near (a door calls once per stay).</summary>
        public DoorCalls Calls { get; } = new DoorCalls();

        /// <summary>Group calls at neighbours' doors that played (bell and caption; tests).</summary>
        public int GroupCallsHeard { get; private set; }

        /// <summary>Group rings at neighbours' doors, heard or not (tests).</summary>
        public int GroupDoorRings { get; private set; }

        public LanternView LanternScreen { get; private set; }

        /// <summary>Frame times for the device readout (recorded while playing).</summary>
        public FrameTimeStats Frames { get; } = new FrameTimeStats();

        /// <summary>What the free world remembers (empty until loaded).</summary>
        public NeighbourhoodState State => _store != null ? _store.State : NeighbourhoodState.Empty;

        /// <summary>The Use targets this stage adds to the case's (doors, the table, a treat at Pip's porch).</summary>
        public List<ExtraUseTarget> Targets { get; } = new List<ExtraUseTarget>();

        /// <summary>Whether Pip may use a free-world target now (playing, no modal, no cutscene).</summary>
        public Func<bool> CanInteract { get; set; }

        /// <summary>The director's modal switch (the lantern screen holds the controls).</summary>
        public Action<bool> SetModal { get; set; }

        /// <summary>Rings, answers and treats (tests and the log).</summary>
        public event Action<string> Happened;

        /// <summary>Doors Pip rang that answered (tests).</summary>
        public int PipRings { get; private set; }

        /// <summary>Times a group rang Pip's bell (tests).</summary>
        public int PorchRings { get; private set; }

        /// <summary>Treats Pip handed out this session (tests; never shown).</summary>
        public int TreatsGiven { get; private set; }

        /// <summary>The crowd's frame time measured last frame, and the tier it budgets for.</summary>
        public CrowdTier Tier { get; private set; }

        /// <summary>True when the last tick let groups visit Pip's porch.</summary>
        public bool PorchOpen { get; private set; }

        public static LivingStreetStage Create(Transform parent, GameBootstrap game, RectTransform canvas, CaptionStrip captions)
        {
            var go = new GameObject("LivingStreet");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<LivingStreetStage>();
            stage.Build(game, canvas, captions);
            return stage;
        }

        private void Build(GameBootstrap game, RectTransform canvas, CaptionStrip captions)
        {
            _materials = game.Materials;
            _pip = game.Pip != null ? game.Pip.transform : null;
            _captions = captions;
            Set = LivingStreetSet.Create(transform, game.Materials, game.Environment);
            Tier = CrowdBudget.TierForQualityLevel(QualitySettings.GetQualityLevel());
            Crowd = CrowdView.Create(transform, game.Materials, new CrowdKitBodyFactory());
            Crowd.Budget = CrowdBudget.For(Tier);
            Crowd.View = game.CameraRig != null ? game.CameraRig.OutputCamera : null;
            LanternScreen = LanternView.Create(canvas);
            LanternScreen.Folded += OnFolded;
            LanternScreen.Closed += () => SetModal?.Invoke(false);

            foreach (var rig in Set.Doors)
            {
                var door = rig.Door;
                Targets.Add(new ExtraUseTarget
                {
                    VerbKey = Keys.VerbRing,
                    IconName = UiSpriteCatalog.IconExamine,
                    Marker = new Vector3(door.StandX + (door.DoorX - door.StandX) * 0.55f, 1.9f, door.StandZ + (door.DoorZ - door.StandZ) * 0.55f),
                    Offered = feet => Allowed() && !Doors.Busy(door.Id) && Reach(feet, door.StandX, door.StandZ, LivingStreetLayout.BellReach),
                    Interact = () => Ring(door),
                });
            }

            Targets.Add(new ExtraUseTarget
            {
                VerbKey = Keys.VerbMake,
                IconName = UiSpriteCatalog.IconExamine,
                Marker = new Vector3(LivingStreetLayout.TableX, 1.75f, LivingStreetLayout.TableZ),
                Offered = feet => Allowed() && LivingStreetLayout.TableInReach(feet.x, feet.z),
                Interact = OpenTable,
            });
            Targets.Add(new ExtraUseTarget
            {
                VerbKey = Keys.VerbTreat,
                IconName = UiSpriteCatalog.IconTalk,
                Marker = new Vector3(LivingStreetLayout.PorchStandX, 1.7f, LivingStreetLayout.PorchStandZ),
                Offered = feet => Allowed() && Crowd.Clock.WaitingAtPorch >= 0 && LivingStreetLayout.TreatInReach(feet.x, feet.z),
                Interact = () => GiveTreat(),
            });
        }

        private bool Allowed() => CanInteract == null || CanInteract();

        private static bool Reach(Vector3 feet, float x, float z, float reach)
        {
            float dx = feet.x - x;
            float dz = feet.z - z;
            return dx * dx + dz * dz <= reach * reach;
        }

        // ---------------------------------------------------------------------------------------------- saves

        /// <summary>Loads "neighbourhood.json" beside the case saves and shows Pip's lantern if she has one.</summary>
        public void Load(string directory)
        {
            _store = new NeighbourhoodStore(new DiskSaveFileSystem(), directory);
            try
            {
                _store.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LivingStreet] Neighbourhood load failed: " + e.Message);
            }

            Set.ShowLantern(_store.State.Lantern);
        }

        /// <summary>A confirmed New game: the street forgets Pip's lantern and visits (D31).</summary>
        public void OnNewGame()
        {
            Try(() => _store?.Reset());
            Set.ShowLantern(null);
            ResetPresentation();
        }

        /// <summary>Replay case keeps the free world (D31); only the moment's presentation resets.</summary>
        public void OnReplay()
        {
            _store?.Replay();
            ResetPresentation();
        }

        public void ResetPresentation()
        {
            LanternScreen.Cancel();
            Doors.Reset();
            Calls.Reset();
            foreach (var flight in _flights)
            {
                flight.Candy.gameObject.SetActive(false);
            }

            _flights.Clear();
        }

        private static void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                // A failed write never stops play; the previous copy stays valid.
                Debug.LogWarning("[LivingStreet] Save failed: " + e.Message);
            }
        }

        // ---------------------------------------------------------------------------------------------- frame

        /// <summary>
        /// One frame of the street while playing. <paramref name="frozen"/> holds the crowd for a cutscene (§3.4 rule 7);
        /// <paramref name="caseState"/> gives the heart's cap and the radio's hint area.
        /// </summary>
        public void Tick(float dt, bool frozen, CaseState caseState)
        {
            Vector3 pip = _pip != null ? _pip.position : Vector3.zero;
            string hintArea = HintAreas.For(CaseHints.ObjectiveFor(caseState ?? CaseState.Empty))?.Id;
            PorchOpen = LivingStreetRules.PorchOpen(State.LanternLit, hintArea);
            var inputs = new CrowdInputs
            {
                PipX = pip.x,
                PipZ = pip.z,
                PipOutside = !CaseRouteLayout.Inside(pip.x, pip.z),
                CaseOpen = LivingStreetRules.CaseOpen(caseState),
                PorchOpen = PorchOpen,
                Frozen = frozen,
            };
            _events.Clear();
            Calls.Update(pip.x, pip.z);
            Crowd.HideHeart = frozen;
            Crowd.Tick(dt, inputs, _events);
            foreach (var e in _events)
            {
                OnCrowdEvent(e, pip);
            }

            Doors.Advance(dt);
            Set.ApplyDoors(Doors, dt);
            TickFlights(dt);
            Frames.Record(Time.unscaledDeltaTime * 1000f, Crowd.LastMs);
        }

        private void OnCrowdEvent(CrowdEvent e, Vector3 pip)
        {
            switch (e.Kind)
            {
                case CrowdEventKind.DoorRung:
                {
                    var door = LivingStreetLayout.Door(e.DoorId);
                    var answer = Doors.Ring(e.DoorId, Ringer.Group);
                    GroupDoorRings++;
                    // A door calls once while Pip stays near it (a long stay by one door no longer repeats the call).
                    if (answer != null && door != null && Calls.Ring(door.Id, door.DoorX, door.DoorZ, pip.x, pip.z))
                    {
                        GroupCallsHeard++;
                        Bell();
                        Say(Keys.CrowdTrickOrTreat, LivingStreetSet.DoorPoint(door));
                    }

                    Happened?.Invoke("group rang " + e.DoorId);
                    break;
                }
                case CrowdEventKind.PorchRung:
                {
                    PorchRings++;
                    Set.PressBell();
                    var porch = new Vector3(LivingStreetLayout.PorchStandX, 1.2f, LivingStreetLayout.PorchStandZ);
                    if (Near(pip, porch))
                    {
                        Bell();
                        Say(Keys.CrowdTrickOrTreat, porch);
                    }

                    Happened?.Invoke("group rang Pip's bell");
                    break;
                }
            }
        }

        private bool Near(Vector3 pip, Vector3 at) => (pip - at).sqrMagnitude <= HearingDistance * HearingDistance;

        /// <summary>The doorbell's "ding-dong" (bells, never knocks: §3.4 rule 5), for every door and Pip's own button.</summary>
        private static void Bell() => AudioCues.Doorbell();

        private void Say(string key, Vector3 from)
        {
            if (_captions != null)
            {
                _captions.ShowKey(key, () => from);
            }
        }

        // -------------------------------------------------------------------------------------- interactions

        /// <summary>Pip rings <paramref name="door"/> (the Use button at its steps; tests call it directly).</summary>
        public bool Ring(NeighbourDoor door)
        {
            if (door == null || _store == null)
            {
                return false;
            }

            int before = _store.State.Visits(door.Id);
            var answer = Doors.Ring(door.Id, Ringer.Pip, before);
            if (answer == null)
            {
                return false;
            }

            PipRings++;
            Bell();
            FacePip(LivingStreetSet.DoorPoint(door));
            AudioCues.Babble(Speaker.Pip, BabbleEmotion.Happy);
            Say(Keys.PipTrickOrTreat, LivingStreetSet.DoorPoint(door));
            Say(answer.LineKey, LivingStreetSet.DoorPoint(door));
            Try(() => _store.Visit(door.Id));
            // The treat comes once the door is open (a hand-to-hand flight into Pip's pail; never a number).
            Fly(LivingStreetSet.DoorPoint(door) + Vector3.down * 0.4f, PipHand(), door.Gag.OpenSeconds + 0.5f);
            Happened?.Invoke("Pip rang " + door.Id);
            return true;
        }

        /// <summary>Use at the lantern table: the screen opens on Pip's lantern (or the default).</summary>
        public void OpenTable()
        {
            if (LanternScreen.IsOpen)
            {
                return;
            }

            SetModal?.Invoke(true);
            FacePip(new Vector3(LivingStreetLayout.TableX, 1f, LivingStreetLayout.TableZ));
            LanternScreen.Open(State.Lantern ?? LanternCraft.Default);
        }

        private void OnFolded(PipLantern lantern)
        {
            MakeLantern(lantern);
            SetModal?.Invoke(false);
        }

        /// <summary>Pip's lantern is folded: it is saved and hangs lit on her porch (tests call it directly).</summary>
        public void MakeLantern(PipLantern lantern)
        {
            if (_store == null)
            {
                return;
            }

            Try(() => _store.MakeLantern(lantern));
            Set.ShowLantern(_store.State.Lantern);
            AudioCues.StickerDrop();
            AudioCues.Babble(Speaker.Pip, BabbleEmotion.Happy);
            Say(Keys.PipMyLantern, Set.PorchLantern.position);
            Happened?.Invoke("lantern " + lantern);
        }

        /// <summary>Pip hands the waiting group a sweet from her bowl (the Use button on her porch; tests call it).</summary>
        public bool GiveTreat()
        {
            int group = Crowd.Clock.WaitingAtPorch;
            if (group < 0 || !Crowd.Clock.Serve(group))
            {
                return false;
            }

            TreatsGiven++;
            var porch = new Vector3(LivingStreetLayout.PorchStandX, 0.9f, LivingStreetLayout.PorchStandZ);
            FacePip(porch);
            AudioCues.Babble(Speaker.Pip, BabbleEmotion.Happy);
            Say(Keys.PipHappyHalloween, porch);
            Say(Keys.CrowdThanks, porch);
            Fly(PipHand(), porch, 0f);
            Happened?.Invoke("treat for group " + group);
            return true;
        }

        private Vector3 PipHand() => _pip != null ? _pip.position + Vector3.up * 0.75f : Vector3.zero;

        private void FacePip(Vector3 target)
        {
            if (_pip == null)
            {
                return;
            }

            Vector3 to = target - _pip.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
            {
                _pip.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
        }

        // ----------------------------------------------------------------------------------------- treat flights

        private void Fly(Vector3 from, Vector3 to, float delay)
        {
            Transform candy = null;
            foreach (var c in _candyPool)
            {
                if (!c.gameObject.activeSelf)
                {
                    candy = c;
                    break;
                }
            }

            if (candy == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Treat";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.11f;
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = _materials != null ? _materials.Glow(Palette.CandyCornYellow, 1.2f) : renderer.sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                candy = go.transform;
                _candyPool.Add(candy);
            }

            candy.position = from;
            candy.gameObject.SetActive(delay <= 0f);
            _flights.Add(new Flight { Candy = candy, From = from, To = to, Time = -delay });
        }

        private void TickFlights(float dt)
        {
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                f.Time += dt;
                if (f.Time < 0f)
                {
                    continue;
                }

                f.Candy.gameObject.SetActive(true);
                float t = Mathf.Clamp01(f.Time / TreatFlightSeconds);
                f.Candy.position = Vector3.Lerp(f.From, f.To, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                if (t >= 1f)
                {
                    f.Candy.gameObject.SetActive(false);
                    _flights.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------------------- grown-ups' page

        /// <summary>
        /// The grown-ups' page lines (behind the parent gate): neighbours greeted (the only total), and in test builds the
        /// frame times of this session with the crowd's own cost (§8).
        /// </summary>
        public void AppendParentSections(ReadingSheet sheet, BuildInfo build)
        {
            if (sheet == null)
            {
                return;
            }

            var state = State;
            sheet.Paragraph(Keys.NeighboursMet, 30, null, null, 0f,
                () => new object[] { state.NeighboursMet, LivingStreetLayout.Doors.Count });
            bool test = build != null && build.runId != "local";
            if (!FrameReadoutPolicy.Shown(Debug.isDebugBuild, test, false))
            {
                return;
            }

            sheet.Heading(Keys.FrameHeading);
            if (!Frames.Ready)
            {
                sheet.Paragraph(Keys.FrameWaiting, 26);
                return;
            }

            var frames = Frames;
            sheet.Paragraph(Keys.FrameLine, 28, null, null, 0f,
                () => new object[] { frames.Median.ToString("F1"), frames.P95.ToString("F1"), frames.P99.ToString("F1") });
            var crowd = Crowd;
            sheet.Paragraph(Keys.FrameCrowd, 26, null, null, 0f,
                () => new object[] { frames.CrowdMs.ToString("F2"), crowd.Clock.WalkerCount, crowd.Drawn });
        }
    }
}
