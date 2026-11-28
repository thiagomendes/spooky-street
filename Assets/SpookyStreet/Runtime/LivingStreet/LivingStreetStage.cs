using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// The living street's test slice in play (docs/living-street.md §9 phase 1, D33/D34; docs/living-street-slice.md):
    /// the east lane, the crowd on its routes, the three doors, the paper lantern table, Pip's lantern on her porch with
    /// groups ringing her bell once it is lit, the neighbourhood file and the frame-time readout. The case director ticks
    /// it and owns the modal state; every rule lives in Core (<see cref="CrowdClock"/>, <see cref="NeighbourDoors"/>,
    /// <see cref="GiveWay"/>, <see cref="NeighbourhoodStore"/>, <see cref="LivingStreetRules"/>).
    /// <para>Trick-or-treating (owner, 2026-11-28): each press of Use at a door rings the bell, and the door opens on the
    /// third ring; the house shows someone is coming in between (the hall light, the shadow across the window, a "Coming!"
    /// caption). The neighbour reaches for the door, it eases open, they come down the step and put a treat into Pip's
    /// pail (or each child's in turn). At Pip's porch she hands one to each child of the group. No treat flies: it rides
    /// the giver's hand and drops into the pail at the clip's release.</para>
    /// </summary>
    public sealed class LivingStreetStage : MonoBehaviour
    {
        /// <summary>Crowd sounds and calls are heard (and captioned) only this close to Pip.</summary>
        public const float HearingDistance = DoorCalls.HearingDistance;

        /// <summary>A sweet takes this long to drop from the hand into the pail at the release, and rests there this long.</summary>
        public const float DropSeconds = 0.14f;

        public const float RestSeconds = 0.45f;

        /// <summary>Pip turns to face a door or a child at this rate (degrees per second): a quick, smooth turn.</summary>
        public const float PipTurnRate = 420f;

        /// <summary>Pip counts as walking above this ground speed (walkers then part to let her through).</summary>
        public const float PipWalking = 0.5f;

        /// <summary>A sweet's journey: in the giver's hand, dropping into a pail, resting in it, then gone.</summary>
        private sealed class Sweet
        {
            public Transform Candy;
            public Func<Vector3> Hand;
            public Func<Vector3> Pail;
            public float Time;
            public int Stage; // 0 in hand, 1 dropping, 2 in the pail
            public Vector3 From;
            public string Key;
        }

        private readonly List<CrowdEvent> _events = new List<CrowdEvent>();
        private readonly List<DoorEvent> _doorEvents = new List<DoorEvent>();
        private readonly List<Sweet> _sweets = new List<Sweet>();
        private readonly List<Transform> _candyPool = new List<Transform>();
        private readonly Dictionary<string, bool> _visitHeard = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _pipLine = new Dictionary<string, string>();
        private readonly HashSet<int> _thanked = new HashSet<int>();
        private NeighbourhoodStore _store;
        private MaterialLibrary _materials;
        private Transform _pip;
        private PipAvatar _avatar;
        private CaptionStrip _captions;
        private Vector3 _lastPip;
        private bool _hasLastPip;
        private float _faceYaw = float.NaN;

        public LivingStreetSet Set { get; private set; }

        public CrowdView Crowd { get; private set; }

        /// <summary>The three doors (the crowd's clock rings them too, so it owns them).</summary>
        public NeighbourDoors Doors => Crowd.Doors;

        /// <summary>Which neighbours' doors have called while Pip stays near (a door's visit is heard once per stay).</summary>
        public DoorCalls Calls { get; } = new DoorCalls();

        /// <summary>Group visits at neighbours' doors that were heard (bell and caption; tests).</summary>
        public int GroupCallsHeard { get; private set; }

        /// <summary>Group visits at neighbours' doors, heard or not (tests).</summary>
        public int GroupDoorRings { get; private set; }

        public LanternView LanternScreen { get; private set; }

        /// <summary>Frame times for the device readout (recorded while playing).</summary>
        public FrameTimeStats Frames { get; } = new FrameTimeStats();

        /// <summary>What the free world remembers (empty until loaded).</summary>
        public NeighbourhoodState State => _store != null ? _store.State : NeighbourhoodState.Empty;

        /// <summary>The Use targets this stage adds to the case's (doors, the table, treats at Pip's porch).</summary>
        public List<ExtraUseTarget> Targets { get; } = new List<ExtraUseTarget>();

        /// <summary>Whether Pip may use a free-world target now (playing, no modal, no cutscene).</summary>
        public Func<bool> CanInteract { get; set; }

        /// <summary>The director's modal switch (the lantern screen holds the controls).</summary>
        public Action<bool> SetModal { get; set; }

        /// <summary>Rings, answers and treats (tests and the log).</summary>
        public event Action<string> Happened;

        /// <summary>Doors that opened for Pip (her third ring; tests).</summary>
        public int PipRings { get; private set; }

        /// <summary>Every ring of Pip's that the bell took (tests).</summary>
        public int PipBellPresses { get; private set; }

        /// <summary>Times a group rang Pip's bell (tests).</summary>
        public int PorchRings { get; private set; }

        /// <summary>Treats Pip handed out this session (tests; never shown).</summary>
        public int TreatsGiven { get; private set; }

        /// <summary>Treats that dropped into Pip's own pail this session (tests; never shown).</summary>
        public int TreatsReceived { get; private set; }

        /// <summary>Sweets on their way now (in a hand or a pail; tests and frames).</summary>
        public int SweetsShown => _sweets.Count;

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
            _avatar = _pip != null ? _pip.GetComponent<PipAvatar>() : null;
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
                    // Offered whenever the bell would take a ring: between rings the prompt comes back once the bell has
                    // sounded, which itself says "ring again".
                    Offered = feet => Allowed() && Doors.CanRing(door.Id, Ringer.Pip) && Reach(feet, door.StandX, door.StandZ, LivingStreetLayout.BellReach),
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
            Calls.Reset();
            _visitHeard.Clear();
            _pipLine.Clear();
            foreach (var sweet in _sweets)
            {
                sweet.Candy.gameObject.SetActive(false);
            }

            _sweets.Clear();
            _faceYaw = float.NaN;
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
            Vector3 moved = _hasLastPip && dt > 0f ? (pip - _lastPip) / dt : Vector3.zero;
            moved.y = 0f;
            _lastPip = pip;
            _hasLastPip = true;
            string hintArea = HintAreas.For(CaseHints.ObjectiveFor(caseState ?? CaseState.Empty))?.Id;
            PorchOpen = LivingStreetRules.PorchOpen(State.LanternLit, hintArea);
            bool walking = moved.magnitude > PipWalking && moved.magnitude < 12f;
            var inputs = new CrowdInputs
            {
                PipX = pip.x,
                PipZ = pip.z,
                PipOutside = !CaseRouteLayout.Inside(pip.x, pip.z),
                PipHeadingX = walking ? moved.x : 0f,
                PipHeadingZ = walking ? moved.z : 0f,
                PipWalking = walking,
                CaseOpen = LivingStreetRules.CaseOpen(caseState),
                PorchOpen = PorchOpen,
                Frozen = frozen,
            };
            _events.Clear();
            _doorEvents.Clear();
            Calls.Update(pip.x, pip.z);
            Crowd.HideHeart = frozen;
            Crowd.Tick(dt, inputs, _events, _doorEvents);
            foreach (var e in _events)
            {
                OnCrowdEvent(e, pip);
            }

            foreach (var e in _doorEvents)
            {
                OnDoorEvent(e, pip);
            }

            // A door Pip rang once or twice settles when she walks away (nothing timed, nothing lost).
            foreach (var door in LivingStreetLayout.Doors)
            {
                if (Doors.HeldByPip(door.Id) && Doors.Phase(door.Id) == DoorPhase.Called &&
                    !Reach(pip, door.StandX, door.StandZ, NeighbourDoors.AbandonDistance))
                {
                    Doors.Abandon(door.Id);
                }
            }

            Set.ApplyDoors(Doors, dt);
            TickSweets(dt);
            TurnPip(dt, moved.magnitude);
            Frames.Record(Time.unscaledDeltaTime * 1000f, Crowd.LastMs);
        }

        private void OnCrowdEvent(CrowdEvent e, Vector3 pip)
        {
            switch (e.Kind)
            {
                case CrowdEventKind.DoorRung:
                {
                    var door = LivingStreetLayout.Door(e.DoorId);
                    if (door == null)
                    {
                        break;
                    }

                    if (e.Ring == 1)
                    {
                        // A visit is heard (its bells, its "Trick or treat!") once while Pip stays near the door.
                        GroupDoorRings++;
                        bool heard = Calls.Ring(door.Id, door.DoorX, door.DoorZ, pip.x, pip.z);
                        _visitHeard[door.Id] = heard;
                        GroupCallsHeard += heard ? 1 : 0;
                        Happened?.Invoke("group rang " + e.DoorId);
                    }

                    if (_visitHeard.TryGetValue(door.Id, out bool hear) && hear)
                    {
                        Bell();
                    }

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

                    _thanked.Remove(e.Group);
                    Happened?.Invoke("group rang Pip's bell");
                    break;
                }
                case CrowdEventKind.PipHandTreat:
                {
                    // A child stands in front of Pip holding out its pail: she turns to it and hands a sweet over.
                    var child = Crowd.Bodies[e.Walker];
                    FacePip(child.Root.position);
                    bool clip = PlayPip(PipClips.HandTreat) || PlayPip(PipClips.Interact);
                    var sweet = Spawn(PipHand, () => child.PailMouth(), "pip>" + e.Walker);
                    sweet.Time = -WalkerClips.HandTreatSeconds * CrowdClipTimings.HandTreatPickUp; // in her hand from the pick-up
                    if (!_thanked.Contains(e.Group))
                    {
                        AudioCues.Babble(Speaker.Pip, BabbleEmotion.Happy);
                        Say(Keys.PipHappyHalloween, child.Root.position + Vector3.up);
                    }

                    Happened?.Invoke("Pip hands a treat to walker " + e.Walker + (clip ? "" : " (no clip)"));
                    break;
                }
                case CrowdEventKind.PipTreatReleased:
                {
                    Release("pip>" + e.Walker);
                    TreatsGiven++;
                    if (_thanked.Add(e.Group))
                    {
                        Say(Keys.CrowdThanks, Crowd.Bodies[e.Walker].Root.position + Vector3.up);
                    }

                    Happened?.Invoke("treat for walker " + e.Walker);
                    break;
                }
            }
        }

        private void OnDoorEvent(DoorEvent e, Vector3 pip)
        {
            var door = LivingStreetLayout.Door(e.DoorId);
            if (door == null)
            {
                return;
            }

            bool pips = e.Ringer == Ringer.Pip;
            bool heard = pips || (_visitHeard.TryGetValue(door.Id, out bool h) && h);
            var point = LivingStreetSet.DoorPoint(door);
            switch (e.Kind)
            {
                case DoorEventKind.Coming:
                    if (heard)
                    {
                        Say(LivingStreetLayout.ComingLine(door), point);
                    }

                    break;
                case DoorEventKind.Opened:
                    if (pips)
                    {
                        AudioCues.Babble(Speaker.Pip, BabbleEmotion.Happy);
                        Say(Keys.PipTrickOrTreat, point);
                    }
                    else if (heard)
                    {
                        Say(Keys.CrowdTrickOrTreat, point);
                    }

                    Happened?.Invoke("door opens " + door.Id);
                    break;
                case DoorEventKind.Arrived:
                    if (pips && _pipLine.TryGetValue(door.Id, out string line) && line != null)
                    {
                        Say(line, point);
                        FacePip(point);
                    }

                    break;
                case DoorEventKind.Released:
                {
                    var giver = Giver(door);
                    if (giver == null)
                    {
                        break;
                    }

                    string key = "door>" + door.Id;
                    if (pips)
                    {
                        PlayPip(PipClips.Interact);
                        EnsureInHand(key, giver, PipPail);
                        Release(key);
                        TreatsReceived++;
                        Happened?.Invoke("treat for Pip at " + door.Id);
                    }
                    else
                    {
                        int walker = Crowd.Clock.ChildWalker(e.Group, e.Recipient);
                        if (walker >= 0)
                        {
                            var child = Crowd.Bodies[walker];
                            EnsureInHand(key, giver, () => child.PailMouth());
                            Release(key);
                            Happened?.Invoke("treat for walker " + walker + " at " + door.Id);
                        }
                    }

                    break;
                }
            }
        }

        private bool Near(Vector3 pip, Vector3 at) => (pip - at).sqrMagnitude <= HearingDistance * HearingDistance;

        /// <summary>The doorbell's "ding-dong" (bells, never knocks: §3.4 rule 5), for every door and Pip's own button.</summary>
        private static void Bell() => AudioCues.Doorbell();

        private void Say(string key, Vector3 from)
        {
            if (_captions != null && key != null)
            {
                _captions.ShowKey(key, () => from);
            }
        }

        // -------------------------------------------------------------------------------------- interactions

        /// <summary>
        /// Pip rings <paramref name="door"/> (the Use button at its steps; tests call it directly): one ring of three. The
        /// third opens the door, and that is the visit the street remembers.
        /// </summary>
        public bool Ring(NeighbourDoor door)
        {
            if (door == null || _store == null)
            {
                return false;
            }

            int before = _store.State.Visits(door.Id);
            // Its "Coming!" is said right here (below), so the ring raises no events of its own.
            var answer = Doors.Ring(door.Id, Ringer.Pip, before, 1, -1, null);
            if (answer == null)
            {
                return false;
            }

            PipBellPresses++;
            Bell();
            PlayPip(PipClips.Interact);
            FacePip(LivingStreetSet.DoorPoint(door));
            if (answer.Ring == 2)
            {
                Say(LivingStreetLayout.ComingLine(door), LivingStreetSet.DoorPoint(door));
            }

            if (answer.Opens)
            {
                PipRings++;
                _pipLine[door.Id] = answer.LineKey;
                Try(() => _store.Visit(door.Id));
            }

            Happened?.Invoke("Pip rang " + door.Id + " (" + answer.Ring + ")");
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

        /// <summary>
        /// Pip hands out treats to the group waiting at her steps (the Use button on her porch; tests call it): each child
        /// in turn steps up to her and she puts one sweet in its pail.
        /// </summary>
        public bool GiveTreat()
        {
            int group = Crowd.Clock.WaitingAtPorch;
            if (group < 0 || !Crowd.Clock.Serve(group))
            {
                return false;
            }

            FacePip(new Vector3(LivingStreetLayout.PorchStandX, 0.9f, LivingStreetLayout.PorchStandZ));
            Happened?.Invoke("treats for group " + group);
            return true;
        }

        private bool PlayPip(string clip) => _avatar != null && _avatar.Animator != null && _avatar.Animator.PlayOneShot(clip);

        private Transform PipBone(string name) => _pip != null ? ModelKit.FindDeep(_pip, name) : null;

        /// <summary>Pip's free hand (the art's knuckle bone, else her left hand).</summary>
        private Vector3 PipHand()
        {
            var hand = PipBone(CrowdClipTimings.TreatHandBone) ?? PipBone("Hand_L");
            return hand != null ? hand.position
                : _pip.position + _pip.forward * CrowdClipTimings.HandTreatReleaseForward + Vector3.up * CrowdClipTimings.HandTreatReleaseHeight;
        }

        /// <summary>The mouth of Pip's pumpkin pail (her <c>Pail</c> bone; the pail hangs from it).</summary>
        private Vector3 PipPail()
        {
            var pail = PipBone("Pail");
            return pail != null ? pail.position + Vector3.down * 0.06f : _pip.position + _pip.forward * 0.2f + Vector3.up * 0.5f;
        }

        /// <summary>Turns Pip smoothly to face <paramref name="target"/> (never a snap; her own walking takes over).</summary>
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
                _faceYaw = Quaternion.LookRotation(to, Vector3.up).eulerAngles.y;
            }
        }

        private void TurnPip(float dt, float speed)
        {
            if (_pip == null || float.IsNaN(_faceYaw) || dt <= 0f)
            {
                return;
            }

            if (speed > PipWalking)
            {
                _faceYaw = float.NaN; // she walks off: her own facing wins
                return;
            }

            var want = Quaternion.Euler(0f, _faceYaw, 0f);
            _pip.rotation = Quaternion.RotateTowards(_pip.rotation, want, PipTurnRate * dt);
            if (Quaternion.Angle(_pip.rotation, want) < 0.5f)
            {
                _faceYaw = float.NaN;
            }
        }

        // ------------------------------------------------------------------------------------ sweets handed over

        private CrowdBody Giver(NeighbourDoor door)
        {
            foreach (var rig in Set.Doors)
            {
                if (rig.Door.Id == door.Id && rig.Figures.Count > 0)
                {
                    return rig.Figures[0];
                }
            }

            return null;
        }

        private Sweet Find(string key)
        {
            foreach (var s in _sweets)
            {
                if (s.Key == key && s.Stage == 0)
                {
                    return s;
                }
            }

            return null;
        }

        private Sweet Spawn(Func<Vector3> hand, Func<Vector3> pail, string key)
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
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = _materials != null ? _materials.Glow(Palette.CandyCornYellow, 1.2f) : renderer.sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                candy = go.transform;
                _candyPool.Add(candy);
            }

            candy.localScale = new Vector3(0.075f, 0.06f, 0.06f);
            candy.position = hand();
            candy.gameObject.SetActive(false);
            var sweet = new Sweet { Candy = candy, Hand = hand, Pail = pail, Key = key };
            _sweets.Add(sweet);
            return sweet;
        }

        /// <summary>The neighbour's sweet is in their hand (from the pick-up to the release of GiveTreat).</summary>
        private void EnsureInHand(string key, CrowdBody giver, Func<Vector3> pail)
        {
            if (Find(key) == null)
            {
                Spawn(giver.TreatHand, pail, key);
            }
        }

        /// <summary>The hand lets go over the pail: the sweet drops the last few centimetres into it.</summary>
        private void Release(string key)
        {
            var s = Find(key);
            if (s == null)
            {
                return;
            }

            s.Candy.gameObject.SetActive(true);
            s.From = s.Hand();
            s.Stage = 1;
            s.Time = 0f;
        }

        private void TickSweets(float dt)
        {
            // A neighbour handing out has the sweet in their hand from the clip's pick-up (polled from the door's pose).
            foreach (var rig in Set.Doors)
            {
                if (rig.Figures.Count == 0 ||
                    !Doors.Figure(rig.Door.Id, 0, out _, out _, out _, out _, out _, out var clip, out float clipTime, out _) ||
                    clip != WalkerClip.GiveTreat ||
                    clipTime < WalkerClips.Seconds(WalkerClip.GiveTreat) * CrowdClipTimings.GiveTreatPickUp ||
                    clipTime >= WalkerClips.GiveRelease)
                {
                    continue;
                }

                var giver = rig.Figures[0];
                string key = "door>" + rig.Door.Id;
                if (Find(key) == null)
                {
                    var door = rig.Door;
                    bool pips = Doors.HeldByPip(door.Id);
                    int walker = pips ? -1 : Crowd.Clock.ChildWalker(Doors.Holder(door.Id), Doors.Turn(door.Id));
                    if (pips)
                    {
                        Spawn(giver.TreatHand, PipPail, key);
                    }
                    else if (walker >= 0)
                    {
                        var child = Crowd.Bodies[walker];
                        Spawn(giver.TreatHand, () => child.PailMouth(), key);
                    }
                }
            }

            for (int i = _sweets.Count - 1; i >= 0; i--)
            {
                var s = _sweets[i];
                s.Time += dt;
                switch (s.Stage)
                {
                    case 0:
                        s.Candy.gameObject.SetActive(s.Time >= 0f);
                        s.Candy.position = s.Hand();
                        break;
                    case 1:
                    {
                        // Let go over the pail: it drops the last few centimetres straight in (never thrown).
                        float u = Mathf.Clamp01(s.Time / DropSeconds);
                        s.Candy.position = Vector3.Lerp(s.From, s.Pail(), u * u);
                        if (u >= 1f)
                        {
                            s.Stage = 2;
                            s.Time = 0f;
                        }

                        break;
                    }
                    default:
                    {
                        // It rests in the pail a moment and sinks out of sight.
                        float u = Mathf.Clamp01(s.Time / RestSeconds);
                        s.Candy.position = s.Pail() + Vector3.down * (0.07f * u * u);
                        if (u >= 1f)
                        {
                            s.Candy.gameObject.SetActive(false);
                            _sweets.RemoveAt(i);
                        }

                        break;
                    }
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
