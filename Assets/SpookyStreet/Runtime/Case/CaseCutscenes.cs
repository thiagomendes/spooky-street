using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Case 01's cutscenes in the running game (S002.5b, D28): when they play, the seen set beside the replay seen set
    /// (<see cref="CutsceneProgressStore"/>, outside the case save), and the stage they play on. The opening plays on a
    /// new game before control and ends with Pip on the arrival porch; the reveal waits for the step that stamps the
    /// case (the three deductions and the reopening, D26) and starts once nothing else holds the screen (the finale's
    /// framing, the notebook, a page-turn, a replay), so it never doubles the reopening and never runs ahead of the
    /// child. Continue and a reload never start one. Both are rewatchable from the notebook. While one plays the HUD
    /// steps aside and gameplay input is off (the director's modal hold); Pip walks through the motor with a scripted
    /// stick, so the real clips and collisions play; witnesses take only existing clips, gestures and head turns.
    /// </summary>
    public sealed class CaseCutscenes : MonoBehaviour, ICutsceneStage
    {
        /// <summary>Production plays the opening on a new game; tests that start at the porch switch it off.</summary>
        public static bool AutoPlayOpening { get; set; } = true;

        /// <summary>Pip counts as arrived this close to the walk's end (m).</summary>
        public const float ArriveDistance = 0.14f;

        private CaseDirector _director;
        private GameBootstrap _game;
        private GameFlow _flow;
        private CutsceneProgressStore _store;
        private bool _revealPending;
        private bool _reopenNotebook;
        private CastCue _pipWalk;
        private int _pipSegment;
        private Vector3 _pipBefore;
        private Quaternion _pipRotationBefore;
        private readonly List<NpcWalk> _walks = new List<NpcWalk>();
        private readonly Dictionary<Witness, List<Renderer>> _hidden = new Dictionary<Witness, List<Renderer>>();
        private SpriteRenderer _glint;
        private float _glintTime = -1f;
        private AnchorFrame? _photo;

        private sealed class NpcWalk
        {
            public Witness Witness;
            public CastCue Cue;
            public float Time;
        }

        public CutscenePlayer Player { get; private set; }

        public CutsceneProgressStore Progress => _store;

        /// <summary>The notebook's rewatch discs.</summary>
        public NotebookRewatch Rewatch { get; private set; }

        /// <summary>The moving boxes on the arrival porch (the new kid's porch).</summary>
        public Transform Boxes { get; private set; }

        public bool Playing => Player != null && Player.Active;

        /// <summary>The reveal waits for the screen to be free (tests).</summary>
        public bool RevealPending => _revealPending;

        public SpriteRenderer Glint => _glint;

        public static CaseCutscenes Create(CaseDirector director, GameBootstrap game)
        {
            var cutscenes = director.gameObject.AddComponent<CaseCutscenes>();
            cutscenes.Initialize(director, game);
            return cutscenes;
        }

        private void Initialize(CaseDirector director, GameBootstrap game)
        {
            _director = director;
            _game = game;
            _flow = game.Flow;
            _store = new CutsceneProgressStore(new DiskSaveFileSystem(), director.SaveDirectory);
            Try(() => _store.Load(director.State));

            Player = CutscenePlayer.Create(director.transform, game.CameraRig.OutputCamera, (RectTransform)director.UiCanvas.transform);
            Player.Stage = this;
            Player.Ended += OnEnded;
            Player.CueFired += cue =>
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[CaseCutscenes] {cue.Channel} cue {cue.Id} (audio arrives with S003)");
                }
            };

            Rewatch = NotebookRewatch.Attach(director.Notebook);
            Rewatch.Rewatchable = () => CutscenePolicy.Rewatchable(_director.State);
            Rewatch.Watch = id => WatchAgain(id);

            Boxes = BuildBoxes(director.transform, game.Materials);
            _glint = new GameObject("PhotoGlint").AddComponent<SpriteRenderer>();
            _glint.transform.SetParent(director.transform, false);
            _glint.sprite = PuppetAtlas.Get(PuppetParts.Sparkle);
            _glint.sortingOrder = 60;
            _glint.gameObject.SetActive(false);

            director.Stepped += OnStepped;
            _flow.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }

            if (_director != null)
            {
                _director.Stepped -= OnStepped;
            }
        }

        private static void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                // Losing the seen set is harmless: at worst a cutscene plays once more.
                Debug.LogWarning("[CaseCutscenes] " + e.Message);
            }
        }

        // ------------------------------------------------------------------------------------------ triggers

        /// <summary>A new game has just started on the arrival porch: the opening plays before control.</summary>
        public void OnNewGame()
        {
            if (!AutoPlayOpening)
            {
                return;
            }

            string id = CutscenePolicy.OnNewGame(_store.Seen);
            if (id != null)
            {
                Play(id, false, cutIn: true);
            }
        }

        /// <summary>New game, fresh start or a confirmed case replay: nothing plays on, and the seen set starts over.</summary>
        public void OnCaseReset()
        {
            _revealPending = false;
            _reopenNotebook = false;
            Player.End(false);
            Try(() => _store.Reset());
        }

        private void OnStepped(CaseStep step)
        {
            Try(() => _store.Observe(step.State));
            if (CutscenePolicy.AfterStep(step, _store.Seen) != null)
            {
                _revealPending = true;
            }
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Title)
            {
                _revealPending = false;
                _reopenNotebook = false;
                Player.End(false);
            }
        }

        /// <summary>True when nothing else holds the screen (the reveal waits for this).</summary>
        public bool ScreenFree =>
            _flow.State == GameState.Playing && !_director.ModalOpen && !_director.FinalePlaying &&
            (_director.Travel == null || !_director.Travel.Active) && (_director.Cutaway == null || !_director.Cutaway.Active) &&
            (_director.Nonverbal == null || !_director.Nonverbal.MemoryReplay.Active) && !Player.Active;

        private void Update()
        {
            if (_revealPending && !_director.State.Has(CaseFact.CaseStamped))
            {
                _revealPending = false;
            }

            if (_revealPending && ScreenFree)
            {
                _revealPending = false;
                Play(C01Cutscenes.RevealId, false, cutIn: false);
            }
        }

        /// <summary>
        /// Plays <paramref name="id"/> now when the policy allows it (the reveal only once the case is stamped). A first
        /// play is marked seen before it starts, so a skip, crash or quit never plays it again by itself.
        /// </summary>
        public bool Play(string id, bool rewatch, bool cutIn = false)
        {
            var cutscene = CutscenePolicy.Build(id, _director.State, _director.Answers);
            if (cutscene == null)
            {
                Debug.LogWarning("[CaseCutscenes] " + id + " may not play now (never ahead of the child's deduction)");
                return false;
            }

            if (!rewatch)
            {
                Try(() => _store.MarkSeen(id, _director.State));
            }

            Player.Play(cutscene, AmbientMotion.ReducedMotion, rewatch, cutIn);
            return true;
        }

        /// <summary>The notebook's rewatch: closes the notebook, plays it, and opens the notebook again after.</summary>
        public bool WatchAgain(string id)
        {
            if (!CutscenePolicy.CanRewatch(id, _director.State) || Player.Active)
            {
                return false;
            }

            _director.Notebook.Close();
            _reopenNotebook = true;
            if (!Play(id, true))
            {
                _reopenNotebook = false;
                return false;
            }

            return true;
        }

        private void OnEnded(Cutscene cutscene, bool skipped)
        {
            if (_reopenNotebook)
            {
                _reopenNotebook = false;
                _director.OpenNotebook();
            }
        }

        // ------------------------------------------------------------------------------------------ the stage

        public void Begin(Cutscene cutscene, bool rewatch)
        {
            _director.HoldForCutscene(true);
            _pipBefore = _game.Pip.transform.position;
            _pipRotationBefore = _game.Pip.transform.rotation;
            _pipWalk = null;
            _walks.Clear();
            _photo = null;
            _game.Controls.ScriptedMove = Vector2.zero;
            if (cutscene.HidePip)
            {
                _director.HidePipForCutscene(true);
            }
        }

        public void Cast(CastCue cue)
        {
            if (cue.Actor == CastActor.Pip)
            {
                CastPip(cue);
                return;
            }

            var witness = WitnessFor(cue.Actor);
            if (witness == null)
            {
                return; // no model in this build (greybox): the shot still plays
            }

            var avatar = witness.Avatar;
            switch (cue.Kind)
            {
                case CastCueKind.Place:
                    _walks.RemoveAll(w => w.Witness == witness);
                    witness.transform.SetPositionAndRotation(V(cue.Point), Quaternion.Euler(0f, cue.Yaw, 0f));
                    break;
                case CastCueKind.WalkTo:
                    _walks.RemoveAll(w => w.Witness == witness);
                    witness.enabled = false; // the cutscene drives the loop while it walks
                    witness.transform.position = V(cue.Path[0]);
                    _walks.Add(new NpcWalk { Witness = witness, Cue = cue, Time = 0f });
                    break;
                case CastCueKind.Loop:
                    witness.enabled = false;
                    avatar?.PlayLoop(cue.Clip);
                    break;
                case CastCueKind.OneShot:
                    avatar?.PlayOneShot(cue.Clip);
                    break;
                case CastCueKind.Gesture:
                    witness.PlayGesture(cue.Clip, V(cue.Point));
                    break;
                case CastCueKind.LookAt:
                    witness.LookAt(V(cue.Point), cue.Seconds);
                    break;
                case CastCueKind.Hide:
                    SetShown(witness, false);
                    break;
                case CastCueKind.Show:
                    SetShown(witness, true);
                    break;
            }
        }

        /// <summary>Hides a witness by its renderers (its animation keeps running), or shows it again.</summary>
        private void SetShown(Witness witness, bool shown)
        {
            if (!shown)
            {
                if (_hidden.ContainsKey(witness))
                {
                    return;
                }

                var renderers = new List<Renderer>();
                foreach (var renderer in witness.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        renderers.Add(renderer);
                    }
                }

                _hidden.Add(witness, renderers);
                return;
            }

            if (!witness.gameObject.activeSelf)
            {
                witness.gameObject.SetActive(true);
            }

            if (_hidden.TryGetValue(witness, out var hidden))
            {
                foreach (var renderer in hidden)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }

                _hidden.Remove(witness);
            }
        }

        private void CastPip(CastCue cue)
        {
            var pip = _game.Pip;
            switch (cue.Kind)
            {
                case CastCueKind.Place:
                    _pipWalk = null;
                    _game.Controls.ScriptedMove = Vector2.zero;
                    pip.Teleport(V(cue.Point));
                    pip.transform.rotation = Quaternion.Euler(0f, cue.Yaw, 0f);
                    break;
                case CastCueKind.WalkTo:
                    if ((pip.transform.position - V(cue.Path[0])).sqrMagnitude > 0.25f)
                    {
                        pip.Teleport(V(cue.Path[0]));
                    }

                    _pipWalk = cue;
                    _pipSegment = 1;
                    break;
                case CastCueKind.Hide:
                    _director.HidePipForCutscene(true);
                    break;
                case CastCueKind.Show:
                    _director.HidePipForCutscene(false);
                    break;
            }
        }

        public void World(string id)
        {
            var house = _director.House;
            switch (id)
            {
                case CutsceneWorld.DoorShut:
                    house?.SetDoor(false);
                    break;
                case CutsceneWorld.DoorRestore:
                    house?.SetDoor(_director.View.FrontDoor == DoorState.Open);
                    break;
                case CutsceneWorld.NookOpen:
                    if (house != null)
                    {
                        house.NookWallHidden = true;
                    }

                    break;
                case CutsceneWorld.NookClose:
                    if (house != null)
                    {
                        house.NookWallHidden = false;
                    }

                    break;
                case CutsceneWorld.MechanismCycle:
                    _director.Workshop?.PlayCycle();
                    break;
                case CutsceneWorld.PhotoGlint:
                    _glintTime = 0f;
                    break;
            }
        }

        public void Tick(float dt, CamPose camera, CutscenePlayer player)
        {
            // A lens indoors sees the interior at once (the interior is a separate place, D24.1).
            if (_director.House != null)
            {
                _director.House.ForceInteriorVisible = CaseRouteLayout.Inside(camera.Eye.X, camera.Eye.Z);
            }

            StepPipWalk(player);
            if (dt > 0f)
            {
                StepWalks(dt);
                StepGlint(dt);
            }
        }

        private void StepPipWalk(CutscenePlayer player)
        {
            if (_pipWalk == null)
            {
                return;
            }

            var pip = _game.Pip;
            var path = _pipWalk.Path;
            Vector3 at = pip.transform.position;
            Vector3 target = V(path[_pipSegment]);
            Vector3 to = target - at;
            to.y = 0f;
            if (to.magnitude < ArriveDistance)
            {
                _pipSegment++;
                if (_pipSegment >= path.Count)
                {
                    _pipWalk = null;
                    _game.Controls.ScriptedMove = Vector2.zero;
                    player.Signal(CutsceneWorld.PipArrived);
                    return;
                }

                target = V(path[_pipSegment]);
                to = target - at;
                to.y = 0f;
            }

            // The motor reads the stick in camera space: turn the world direction into the lens's frame.
            var lens = _game.CameraRig.OutputCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(lens.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.ProjectOnPlane(lens.up, Vector3.up);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 dir = to.normalized;
            float max = _game.Pip.TuningData != null ? _game.Pip.TuningData.MaxSpeed : 4f;
            float magnitude = Mathf.Clamp01(_pipWalk.Speed / Mathf.Max(0.1f, max));
            _game.Controls.ScriptedMove = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward)) * magnitude;
        }

        private void StepWalks(float dt)
        {
            for (int i = _walks.Count - 1; i >= 0; i--)
            {
                var walk = _walks[i];
                var witness = walk.Witness;
                if (witness == null || !witness.gameObject.activeInHierarchy)
                {
                    continue;
                }

                walk.Time += dt;
                var cue = walk.Cue;
                float distance = walk.Time * cue.Speed;
                Vector3 position = Along(cue.Path, distance, out Vector3 heading);
                bool done = distance >= cue.PathLength;
                var avatar = witness.Avatar;
                if (avatar != null)
                {
                    string loop = !done && avatar.HasClip(CharacterClips.Walk) ? CharacterClips.Walk : CharacterClips.Idle;
                    if (avatar.Loop != loop)
                    {
                        avatar.PlayLoop(loop);
                    }

                    avatar.SetClipSpeed(CharacterClips.Walk, Mathf.Clamp(cue.Speed / CaseCast.ClipPace, 0.3f, 1.5f));
                }

                heading.y = 0f;
                var rotation = heading.sqrMagnitude > 1e-4f
                    ? Quaternion.Slerp(witness.transform.rotation, Quaternion.LookRotation(heading), 10f * dt)
                    : witness.transform.rotation;
                witness.transform.SetPositionAndRotation(position, rotation);
                if (done)
                {
                    _walks.RemoveAt(i);
                }
            }
        }

        private static Vector3 Along(IReadOnlyList<StagePoint> path, float distance, out Vector3 heading)
        {
            heading = Vector3.forward;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = V(path[i]);
                Vector3 b = V(path[i + 1]);
                float length = Vector3.Distance(a, b);
                heading = b - a;
                if (distance <= length || i + 2 == path.Count)
                {
                    return Vector3.Lerp(a, b, length > 0f ? Mathf.Clamp01(distance / length) : 1f);
                }

                distance -= length;
            }

            return V(path[path.Count - 1]);
        }

        private void StepGlint(float dt)
        {
            if (_glintTime < 0f)
            {
                _glint.gameObject.SetActive(false);
                return;
            }

            _glintTime += dt;
            const float seconds = 1.6f;
            if (_glintTime > seconds)
            {
                _glintTime = -1f;
                _glint.gameObject.SetActive(false);
                return;
            }

            var frame = Anchor(CutsceneAnchors.KitPhoto) ?? CutsceneAnchors.Default(CutsceneAnchors.KitPhoto);
            // On one face in the photo, a hand's width in front of the glass.
            Vector3 at = V(frame.Apply(new StagePoint(0.09f, 0.07f, 0.04f)));
            var cam = _game.CameraRig.OutputCamera.transform;
            float u = _glintTime / seconds;
            float size = 0.16f * Mathf.Sin(u * Mathf.PI) * (1f + 0.15f * Mathf.Sin(_glintTime * 18f));
            _glint.gameObject.SetActive(true);
            _glint.transform.SetPositionAndRotation(at, cam.rotation * Quaternion.Euler(0f, 0f, _glintTime * 60f));
            float native = _glint.sprite != null ? _glint.sprite.bounds.size.y : 1f;
            _glint.transform.localScale = Vector3.one * (Mathf.Max(0.001f, size) / Mathf.Max(0.01f, native));
            _glint.color = new Color(1f, 0.95f, 0.75f, Mathf.Sin(u * Mathf.PI));
        }

        public Vector3 Settle(Cutscene cutscene, bool rewatch)
        {
            _pipWalk = null;
            _walks.Clear();
            _game.Controls.ScriptedMove = Vector2.zero;
            var pip = _game.Pip;
            if (rewatch)
            {
                // A rewatch never moves Pip: back where the child was.
                pip.Teleport(_pipBefore);
                pip.transform.rotation = _pipRotationBefore;
            }
            else
            {
                foreach (var cue in cutscene.Settle)
                {
                    if (cue.Actor == CastActor.Pip && cue.Kind == CastCueKind.Place)
                    {
                        pip.Teleport(V(cue.Point));
                        pip.transform.rotation = Quaternion.Euler(0f, cue.Yaw, 0f);
                    }
                }
            }

            RestoreWorld();
            _director.CutCamera();
            return pip.transform.position;
        }

        public void Finish(Cutscene cutscene, bool rewatch, bool skipped)
        {
            _pipWalk = null;
            _walks.Clear();
            _game.Controls.ScriptedMove = Vector2.zero;
            RestoreWorld();
            _director.HidePipForCutscene(false);
            _director.HoldForCutscene(false);
        }

        /// <summary>Everything a cutscene touched goes back to what the facts say.</summary>
        private void RestoreWorld()
        {
            foreach (var pair in _hidden)
            {
                foreach (var renderer in pair.Value)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }

            _hidden.Clear();
            _glintTime = -1f;
            _glint.gameObject.SetActive(false);
            var house = _director.House;
            if (house != null)
            {
                house.ForceInteriorVisible = false;
                house.NookWallHidden = false;
                house.SetDoor(_director.View.FrontDoor == DoorState.Open);
            }

            var cast = _director.Cast;
            if (cast != null)
            {
                cast.Apply(_director.View);
            }

            foreach (var witness in new[] { _game.Witnesses?.Bea, _game.Witnesses?.Vale })
            {
                if (witness != null)
                {
                    witness.enabled = true;
                    witness.StopLooking();
                }
            }
        }

        public AnchorFrame? Anchor(string id)
        {
            switch (id)
            {
                case CutsceneAnchors.Owl:
                    var owl = _director.Workshop != null ? _director.Workshop.Owl : null;
                    if (owl == null)
                    {
                        return null;
                    }

                    var bounds = ModelKit.RendererBounds(owl.gameObject);
                    return bounds.size.sqrMagnitude > 1e-6f ? new AnchorFrame(S(bounds.center), 180f) : (AnchorFrame?)null;
                case CutsceneAnchors.KitPhoto:
                    return _photo ?? (_photo = FindPhoto());
                default:
                    return null;
            }
        }

        /// <summary>
        /// Kit's photo in the mechanism model: the centre of its meshes (from the mesh data, so it works while the
        /// interior is hidden), facing the wall axis that looks most into the hall.
        /// </summary>
        private AnchorFrame? FindPhoto()
        {
            var mechanism = _director.House != null ? _director.House.Mechanism : null;
            var photo = mechanism != null ? ModelKit.FindDeep(mechanism, "KitPhoto") : null;
            if (photo == null)
            {
                return null;
            }

            bool any = false;
            var box = new Bounds();
            foreach (var filter in photo.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var local = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var world = filter.transform.TransformPoint(corner);
                    if (!any)
                    {
                        box = new Bounds(world, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(world);
                    }
                }
            }

            Vector3 centre = any ? box.center : photo.position;
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Vector3 toHall = V(foyer) - centre;
            // A framed picture is thin along its face normal: that axis, turned toward the hall.
            bool alongX = any ? box.size.x <= box.size.z : Mathf.Abs(toHall.x) >= Mathf.Abs(toHall.z);
            float yaw = alongX ? (toHall.x >= 0f ? 90f : 270f) : (toHall.z >= 0f ? 0f : 180f);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CaseCutscenes] Kit's photo at {centre:F2}, size {box.size:F2}, facing {yaw}");
            }

            return new AnchorFrame(S(centre), yaw);
        }

        private Witness WitnessFor(CastActor actor)
        {
            switch (actor)
            {
                case CastActor.June:
                    return _director.Cast != null ? _director.Cast.June : null;
                case CastActor.Kit:
                    return _director.Cast != null ? _director.Cast.Kit : null;
                case CastActor.Bea:
                    return _game.Witnesses != null ? _game.Witnesses.Bea : null;
                case CastActor.Vale:
                    return _game.Witnesses != null ? _game.Witnesses.Vale : null;
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------------------------------ props

        private static readonly Color Kraft = new Color32(0xD9, 0xB0, 0x7C, 0xFF);
        private static readonly Color KraftDark = new Color32(0xC4, 0x9A, 0x66, 0xFF);
        private static readonly Color Tape = new Color32(0xEF, 0xE2, 0xBC, 0xFF);

        /// <summary>
        /// Moving boxes on the back corner of the arrival porch: the new kid has just moved in (narrative bible §3). Plain
        /// kraft boxes with tape, made in code like the greybox; clear of Pip's path and of the play camera's boom.
        /// </summary>
        private static Transform BuildBoxes(Transform parent, MaterialLibrary materials)
        {
            var root = new GameObject("MovingBoxes").transform;
            root.SetParent(parent, false);
            var corner = V(C01Cutscenes.BoxesCorner);
            Box(root, materials, "BigBox", corner + new Vector3(0.15f, 0.2f, 0f), new Vector3(0.52f, 0.4f, 0.42f), 8f, Kraft, true);
            Box(root, materials, "TopBox", corner + new Vector3(0.13f, 0.54f, 0.02f), new Vector3(0.38f, 0.28f, 0.32f), -12f, KraftDark, false);
            Box(root, materials, "SmallBox", corner + new Vector3(-0.45f, 0.15f, -0.12f), new Vector3(0.34f, 0.3f, 0.32f), 22f, Kraft, true);
            return root;
        }

        private static void Box(Transform root, MaterialLibrary materials, string name, Vector3 centre, Vector3 size, float yaw,
            Color color, bool solid)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root, false);
            box.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = size;
            if (!solid)
            {
                Destroy(box.GetComponent<Collider>());
            }

            if (materials != null)
            {
                materials.Apply(box.GetComponent<Renderer>(), color);
            }

            // A strip of tape over the lid and down both sides.
            var tape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tape.name = name + "Tape";
            Destroy(tape.GetComponent<Collider>());
            tape.transform.SetParent(box.transform, false);
            tape.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            tape.transform.localScale = new Vector3(0.16f, 1.02f, 1.02f);
            if (materials != null)
            {
                materials.Apply(tape.GetComponent<Renderer>(), Tape);
            }
        }

        private static Vector3 V(StagePoint p) => new Vector3(p.X, p.Y, p.Z);

        private static StagePoint S(Vector3 v) => new StagePoint(v.x, v.y, v.z);
    }
}
