using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.Save;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// The 30 collectible paper bats in the world (S004 stream A1): one at every <see cref="PaperBatLayout"/> spot not yet
    /// found, folded shut until Pip comes near, then fluttering open to show its cream, pencilled inner fold (spec §4:
    /// "flutter when Pip is near", a perception and never a marker). Walking into one takes it: a paper puff, the paper
    /// peel sound, and the bat is gone for good (bats never respawn). Every find is written to <c>keepsakes.json</c> at
    /// once. No counter, no pop-up and no total anywhere (engagement §2): <see cref="Found"/> lets the notebook disc wobble
    /// once when a page completes (stream A2).
    /// <para><b>Art (stream B2, wired in A2).</b> Each bat is <see cref="KeepsakeModel"/> from
    /// <c>Resources/Models/Environment</c>: ink black when shut, its hinged wings (<c>Wing_L</c>, <c>Wing_R</c>, turning
    /// about their local Z, <see cref="KeepsakeArt.WingShutDegrees"/> from open) spreading as Pip comes near to show the
    /// cream, pencilled inner fold. It never glows, unlike the decorative bats. Without the model it is the greybox: the
    /// decorative <see cref="EnvironmentLayout.PaperBat"/> mesh over a cream paper card, or two plain paper quads.</para>
    /// </summary>
    public sealed class PaperBatCollectibles : MonoBehaviour
    {
        /// <summary>The B2 collectible bat model (Resources/Models/Environment/PaperBatKeepsake.fbx).</summary>
        public const string KeepsakeModel = KeepsakeArt.PaperBat;

        /// <summary>The collectible's size in the world: 0.54 m open, so it reads at the play camera's distance (CI run 180
        /// showed the 0.41 m bat as a speck from the path).</summary>
        public const float ModelScale = 1.6f;

        /// <summary>Half the shut bat's height (unscaled): a bat in the air or on a hook is centred on its spot.</summary>
        public const float ModelHalfHeight = 0.06f;

        /// <summary>The wings beat this far (degrees) about open while a bat flutters.</summary>
        public const float FlapDegrees = 18f;

        /// <summary>Pip nearer than this (metres from her chest) makes a bat flutter open.</summary>
        public const float FlutterDistance = 2.4f;

        /// <summary>Seconds a bat takes to open or fold.</summary>
        public const float OpenSeconds = 0.35f;

        /// <summary>The greybox bat's size (the decorative mesh scaled as the knock beat's bat).</summary>
        public const float BatScale = 0.9f;

        /// <summary>Size of the greybox inner fold card (metres) when fully open.</summary>
        public const float FoldWidth = 0.16f;

        public const float FoldHeight = 0.08f;

        /// <summary>The inner fold's paper colour (bone paper, kept under the bloom threshold).</summary>
        public static readonly Color InnerFold = new Color(0.92f, 0.86f, 0.72f);

        private readonly Dictionary<string, BatView> _views = new Dictionary<string, BatView>(StringComparer.Ordinal);
        private KeepsakeStore _store;
        private Transform _pip;
        private Transform _eye;
        private MoveFeedback _feedback;

        private sealed class BatView
        {
            public PaperBatSpot Spot;
            public Transform Root;
            public Transform Wings;
            public Transform Fold;
            public Transform WingLeft;
            public Transform WingRight;
            public Quaternion LeftRest;
            public Quaternion RightRest;
            public float Open;
            public float Phase;
        }

        /// <summary>When false, nothing is taken (title, cutscenes, modals). Null: always.</summary>
        public Func<bool> CanCollect { get; set; }

        /// <summary>Raised after every new find (the completed page, if any, rides on the result).</summary>
        public event Action<BatFindResult> Found;

        /// <summary>The keepsakes as loaded or last changed.</summary>
        public KeepsakeState State => _store != null ? _store.State : KeepsakeState.Empty;

        public KeepsakeStore Store => _store;

        /// <summary>True when the B2 model replaced the greybox.</summary>
        public bool UsesKeepsakeModel { get; private set; }

        /// <summary>Bats still in the world (tests).</summary>
        public int Remaining
        {
            get
            {
                int count = 0;
                foreach (var view in _views.Values)
                {
                    if (view.Root.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static PaperBatCollectibles Create(Transform parent, GameBootstrap game)
        {
            var go = new GameObject("PaperBatKeepsakes");
            go.transform.SetParent(parent, false);
            var bats = go.AddComponent<PaperBatCollectibles>();
            bats.Build(game);
            return bats;
        }

        /// <summary>The bat's root in the world (null for an unknown ID).</summary>
        public Transform ViewOf(string batId) => _views.TryGetValue(batId ?? string.Empty, out var view) ? view.Root : null;

        /// <summary>How open the bat is (0 folded, 1 open; tests and captures).</summary>
        public float OpenAmount(string batId) => _views.TryGetValue(batId ?? string.Empty, out var view) ? view.Open : 0f;

        private void Build(GameBootstrap game)
        {
            _pip = game != null && game.Pip != null ? game.Pip.transform : null;
            _eye = game != null && game.CameraRig != null && game.CameraRig.OutputCamera != null
                ? game.CameraRig.OutputCamera.transform
                : null;
            _feedback = game != null ? game.Feedback : null;
            MaterialLibrary materials = game != null ? game.Materials : null;
            var kit = materials != null ? new ModelKit(materials, EnvironmentDressing.ModelFolder) : null;
            // The keepsake bat draws from the shared Keepsakes atlas (run 183: through the Env atlas it came out patchwork).
            var keepsakeKit = materials != null
                ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = KeepsakeArt.KeepsakesAtlas }
                : null;
            UsesKeepsakeModel = kit != null && kit.Has(KeepsakeModel);
            bool greyboxMesh = kit != null && kit.Has(EnvironmentLayout.PaperBat);
            int index = 0;
            foreach (var spot in PaperBatLayout.All)
            {
                var root = new GameObject(spot.Id).transform;
                root.SetParent(transform, false);
                root.position = new Vector3(spot.X, spot.Y, spot.Z);
                // Folded, each bat shows its face to where its move starts (the way a child comes to it), turned a
                // little differently each, so the street's bats never read as a pattern.
                var from = new Vector3(spot.FromX - spot.X, 0f, spot.FromZ - spot.Z);
                float jitter = (index * 47f) % 50f - 25f;
                root.rotation = from.sqrMagnitude > 0.04f && !float.IsNaN(from.x)
                    ? Quaternion.LookRotation(-from.normalized, Vector3.up) * Quaternion.Euler(0f, jitter, 0f)
                    : Quaternion.Euler(0f, (index * 47f) % 360f, 0f);
                var view = new BatView { Spot = spot, Root = root, Phase = index * 0.61f };
                var wings = new GameObject("Wings").transform;
                wings.SetParent(root, false);
                view.Wings = wings;
                GameObject art = null;
                if (UsesKeepsakeModel)
                {
                    // Origin at the body's lowest point: on its surface when perched, centred when it hangs or floats.
                    float rest = float.IsNaN(spot.SitY) ? -ModelHalfHeight * ModelScale : spot.SitY - spot.Y;
                    art = keepsakeKit.Spawn(KeepsakeModel, wings, new Vector3(0f, rest, 0f), 0f, ModelScale);
                    if (art != null)
                    {
                        view.WingLeft = ModelKit.FindDeep(art.transform, KeepsakeArt.WingLeft);
                        view.WingRight = ModelKit.FindDeep(art.transform, KeepsakeArt.WingRight);
                        view.LeftRest = view.WingLeft != null ? view.WingLeft.localRotation : Quaternion.identity;
                        view.RightRest = view.WingRight != null ? view.WingRight.localRotation : Quaternion.identity;
                    }
                }
                else if (greyboxMesh)
                {
                    art = kit.Spawn(EnvironmentLayout.PaperBat, wings, Vector3.zero, 0f, BatScale);
                }

                if (art == null)
                {
                    art = PaperQuad("GreyboxBat", wings, Palette.InkBlack, materials, new Vector3(0.34f, 0.16f, 1f));
                }

                if (!UsesKeepsakeModel)
                {
                    // The cream inner fold on the wings' face: hidden folded, spreading as the bat opens.
                    view.Fold = PaperQuad("InnerFold", wings, InnerFold, materials, new Vector3(FoldWidth, FoldHeight, 1f)).transform;
                    view.Fold.localPosition = new Vector3(0f, 0.012f, 0f);
                }

                _views.Add(spot.Id, view);
                Pose(view);
                index++;
            }
        }

        private static GameObject PaperQuad(string name, Transform parent, Color color, MaterialLibrary materials, Vector3 scale)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            // Lying in the bat's flat (flying) plane, face up, like the decorative bat mesh.
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = scale;
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (materials != null)
            {
                materials.Apply(renderer, color);
            }

            return quad;
        }

        /// <summary>Loads the keepsakes file beside the case save and hides every bat already found.</summary>
        public void Load(string directory)
        {
            _store = new KeepsakeStore(new DiskSaveFileSystem(), directory);
            TryStore(() => _store.Load());
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Keepsakes] {_store.LoadStatus}: {_store.State}");
            }

            Refresh();
        }

        /// <summary>Confirmed "Replay case": every keepsake stays (bats never respawn).</summary>
        public void OnReplay()
        {
            _store?.Replay();
            Refresh();
        }

        /// <summary>Confirmed New game: <see cref="KeepsakeRules.AfterNewGame"/> (decision 4: every bat is back).</summary>
        public void OnNewGame()
        {
            if (_store != null)
            {
                TryStore(() => _store.Reset());
            }

            Refresh();
        }

        /// <summary>Puts an earned costume piece on or takes it off, written at once (S004 A2). False when nothing changed.</summary>
        public bool Wear(string pieceId, bool on)
        {
            bool changed = false;
            if (_store != null)
            {
                TryStore(() => changed = _store.Wear(pieceId, on));
            }

            return changed;
        }

        /// <summary>Records a costume remark as said (once ever), written at once.</summary>
        public bool MarkSaid(string remarkId)
        {
            bool changed = false;
            if (_store != null)
            {
                TryStore(() => changed = _store.Said(remarkId));
            }

            return changed;
        }

        /// <summary>Takes a bat (the pickup, and tests): writes the find, puffs, plays the paper sound. False if not new.</summary>
        public bool Collect(string batId)
        {
            if (_store == null || !_views.TryGetValue(batId ?? string.Empty, out var view))
            {
                return false;
            }

            BatFindResult result = default;
            bool wrote = TryStore(() => result = _store.Find(batId));
            if (!wrote || result.Outcome != BatFind.Found)
            {
                // A failed write still takes the bat for this session; the store kept it in memory.
                if (!wrote && _store.State.HasFound(batId))
                {
                    view.Root.gameObject.SetActive(false);
                }

                return false;
            }

            view.Root.gameObject.SetActive(false);
            if (_feedback != null)
            {
                _feedback.Puff(view.Root.position - Vector3.up * 0.1f);
            }

            AudioCues.StickerPick();
            Found?.Invoke(result);
            return true;
        }

        private void Refresh()
        {
            var state = State;
            foreach (var view in _views.Values)
            {
                view.Root.gameObject.SetActive(!state.HasFound(view.Spot.Id));
                view.Open = 0f;
                Pose(view);
            }
        }

        private void Update()
        {
            if (_pip == null || _store == null)
            {
                return;
            }

            bool collect = CanCollect == null || CanCollect();
            Vector3 chest = _pip.position + Vector3.up * PaperBatLayout.ChestHeight;
            float dt = Time.deltaTime;
            string take = null;
            foreach (var view in _views.Values)
            {
                if (!view.Root.gameObject.activeSelf)
                {
                    continue;
                }

                float distance = Vector3.Distance(chest, view.Root.position);
                float target = distance < FlutterDistance ? 1f : 0f;
                view.Open = Mathf.MoveTowards(view.Open, target, dt / OpenSeconds);
                if (view.Open > 0f)
                {
                    // Opening, it turns its face (its -Z side) to Pip.
                    // It shows its face to the child: toward the camera that follows Pip (edge-on to the lens it
                    // vanished in run 180's frames), else toward Pip.
                    Vector3 away = view.Root.position - (_eye != null ? _eye.position : _pip.position);
                    away.y = 0f;
                    if (away.sqrMagnitude > 1e-4f)
                    {
                        view.Root.rotation = Quaternion.Slerp(view.Root.rotation, Quaternion.LookRotation(away, Vector3.up),
                            Mathf.Clamp01(dt * 6f));
                    }
                }

                Pose(view);
                if (collect && take == null && distance <= PaperBatLayout.PickupRadius)
                {
                    take = view.Spot.Id;
                }
            }

            if (take != null)
            {
                Collect(take);
            }
        }

        /// <summary>
        /// Folded: the bat lies flat on its surface (as the mesh is modelled), the inner fold hidden. Open: it tips up like
        /// the decorative bats, its face to Pip, the cream fold spread on it, and (without reduced motion) its wings
        /// flutter a little.
        /// </summary>
        private static void Pose(BatView view)
        {
            float open = Mathf.SmoothStep(0f, 1f, view.Open);
            if (view.WingLeft != null || view.WingRight != null)
            {
                // The model: shut, the wings fold over the front (ink black, a cream belly); opening, they spread to
                // show the cream inner fold and (without reduced motion) beat a little.
                float flap = AmbientMotion.ReducedMotion ? 0f
                    : (Mathf.Sin(Time.time * 9f + view.Phase) * 0.5f + 0.5f) * FlapDegrees * open;
                float shut = KeepsakeArt.WingShutDegrees * (1f - open) + flap;
                if (view.WingLeft != null)
                {
                    view.WingLeft.localRotation = view.LeftRest * Quaternion.AngleAxis(shut, Vector3.forward);
                }

                if (view.WingRight != null)
                {
                    view.WingRight.localRotation = view.RightRest * Quaternion.AngleAxis(-shut, Vector3.forward);
                }

                // A small lift as it wakes, so it reads as fluttering up to Pip.
                view.Wings.localPosition = new Vector3(0f, 0.04f * open, 0f);
                return;
            }

            float flutter = AmbientMotion.ReducedMotion ? 0f : Mathf.Sin(Time.time * 7f + view.Phase) * 10f * open;
            view.Wings.localRotation = Quaternion.Euler(-EnvironmentDressing.BatTilt * open + flutter, 0f, 0f);
            view.Wings.localPosition = new Vector3(0f, 0.05f * open, 0f);
            if (view.Fold != null)
            {
                view.Fold.localScale = new Vector3(FoldWidth * Mathf.Max(0.05f, open), FoldHeight, 1f);
                view.Fold.gameObject.SetActive(open > 0.02f);
            }
        }

        private static bool TryStore(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                // A failed write never stops play; the previous copy stays valid.
                Debug.LogWarning("[Keepsakes] Save failed: " + e.Message);
                return false;
            }
        }
    }
}
