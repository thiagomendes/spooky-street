using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Street;
using SpookyStreet.Core.World;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// "The street remembers" in the world (S002.5b, D29): loads the street file beside the case save, records a case
    /// once its notebook is stamped, and shows what the street remembers with existing art only. Kit's booth stays open
    /// and lit (Kit at her booth and a warm booth lamp), the rooftop owl keeps bowing with the wind, Bea's bike carries
    /// an owl sticker (the puppet atlas owl on a paper disc), the front door stands open, and June plays outside by her
    /// treehouse way. The world view (June's mark, the door, the booth) is laid over the case's on load, after the case
    /// is closed; the lamp and the sticker come as soon as the stamp lands, since Bea is already "putting an owl on her
    /// bike". While a replayed case is open again its memories rest (<see cref="StreetView"/>).
    /// </summary>
    public sealed class StreetMemoryStage : MonoBehaviour
    {
        /// <summary>The booth lamp: warm, small and unshadowed, over Kit's ticket door.</summary>
        public const float BoothLampIntensity = 2.4f;

        public const float BoothLampRange = 3.6f;

        /// <summary>Diameter of the owl sticker's paper disc on Bea's basket (metres).</summary>
        public const float StickerSize = 0.15f;

        /// <summary>Where the booth's little bell dings (beside the ticket shutter, under the eave).</summary>
        public static readonly Vector3 BellPoint = new Vector3(CaseStage.KitBooth.Position.X + 0.55f, 2.15f,
            CaseStage.KitBooth.Position.Z + 0.35f);

        /// <summary>Mr. Vale checks his watch on the hour only within this distance of the lens (and on screen).</summary>
        public const float ValeInViewDistance = 24f;

        private const float BellSeconds = 0.9f;

        private StreetStore _store;
        private Light _boothLamp;
        private GameObject _sticker;
        private SpriteRenderer _bell;
        private float _bellTime = -1f;
        private float _bellSize = 1f;

        /// <summary>The owl booth's show "on the hour" and the three-knock touch reward (S004).</summary>
        public BoothShowClock ShowClock { get; } = new BoothShowClock();

        /// <summary>Bell dings so far (tests).</summary>
        public int BellDings { get; private set; }

        /// <summary>Times Mr. Vale checked his watch on the hour (tests).</summary>
        public int ValeWatchChecks { get; private set; }

        /// <summary>Raised when a show plays in the world (on the hour, or after three knocks).</summary>
        public event Action<BoothShow> ShowPlayed;

        public SpriteRenderer Bell => _bell;

        /// <summary>The street file (null until <see cref="Load"/>).</summary>
        public StreetStore Store => _store;

        /// <summary>What the street remembers.</summary>
        public StreetState State => _store != null ? _store.State : StreetState.Empty;

        /// <summary>The memories laid over the world view, fixed when the world was last rebuilt (load, replay, new game).</summary>
        public StreetView WorldView { get; private set; } = StreetView.None;

        /// <summary>The memories showing right now (the lamp and the sticker follow it at once).</summary>
        public StreetView Live { get; private set; } = StreetView.None;

        public Light BoothLamp => _boothLamp;

        public GameObject BeaSticker => _sticker;

        public static StreetMemoryStage Create(Transform parent, GameBootstrap game)
        {
            var go = new GameObject("StreetMemory");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<StreetMemoryStage>();
            stage.Build(game);
            return stage;
        }

        private void Build(GameBootstrap game)
        {
            var lamp = new GameObject("BoothLamp").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.type = LightType.Point;
            lamp.color = Palette.PumpkinAmber;
            lamp.intensity = BoothLampIntensity;
            lamp.range = BoothLampRange;
            lamp.shadows = LightShadows.None;
            var kit = CaseStage.KitBooth.Position;
            lamp.transform.position = new Vector3(kit.X, kit.Y + 1.9f, kit.Z - 0.55f);
            lamp.enabled = false;
            _boothLamp = lamp;

            _bell = new GameObject("BoothBellDing").AddComponent<SpriteRenderer>();
            _bell.transform.SetParent(transform, false);
            _bell.sprite = PuppetAtlas.Get(PuppetParts.Sparkle);
            _bell.sortingOrder = 60;
            _bell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bell.gameObject.SetActive(false);

            if (game != null && game.Witnesses != null && game.Witnesses.Bicycle != null)
            {
                _sticker = BuildSticker(game.Witnesses.Bicycle.parent);
            }
        }

        /// <summary>
        /// The owl sticker: the puppet atlas owl on a white paper disc, on the side of Bea's basket that faces the plaza.
        /// Placed from the bicycle's layout (its basket sits 0.40 m ahead of the wheelbase centre, 0.64 m up, 0.1 m to
        /// each side), so it does not depend on the imported model's axis conversion.
        /// </summary>
        private static GameObject BuildSticker(Transform parent)
        {
            var owl = PuppetAtlas.Get("pg.owl");
            if (owl == null)
            {
                return null;
            }

            Placement bike = default;
            bool found = false;
            foreach (var placement in EnvironmentLayout.Witnesses)
            {
                if (placement.Model == EnvironmentLayout.BeaBicycle)
                {
                    bike = placement;
                    found = true;
                }
            }

            if (!found)
            {
                return null;
            }

            // The bicycle's basket end, as imported: CI run 137 (r_bea_sticker) showed the model's +Z is the saddle end.
            float r = bike.Yaw * Mathf.Deg2Rad;
            var front = -new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            var side = new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
            // The side of the basket that faces the plaza (east).
            if (side.x < 0f)
            {
                side = -side;
            }

            var root = new GameObject("BeaOwlSticker");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(bike.X, bike.Y, bike.Z) + front * 0.40f + side * 0.11f + Vector3.up * 0.64f;
            // A sprite reads unmirrored from the side its forward points away from: forward into the basket.
            root.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);

            var paper = new GameObject("Paper").AddComponent<SpriteRenderer>();
            paper.transform.SetParent(root.transform, false);
            paper.sprite = IconFactory.Disc;
            paper.color = new Color(0.93f, 0.91f, 0.86f);
            Fit(paper, StickerSize);
            paper.sortingOrder = 0;

            var art = new GameObject("Owl").AddComponent<SpriteRenderer>();
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            art.sprite = owl;
            art.color = new Color(0.92f, 0.92f, 0.95f);
            Fit(art, StickerSize * 0.86f);
            art.sortingOrder = 1;
            root.SetActive(false);
            return root;
        }

        private static void Fit(SpriteRenderer renderer, float size)
        {
            float extent = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            renderer.transform.localScale = Vector3.one * (extent > 1e-4f ? size / extent : 1f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Loads the street (and records a stamped save that predates the file), then fixes the views.</summary>
        public void Load(string directory, CaseState caseState)
        {
            _store = new StreetStore(new DiskSaveFileSystem(), directory);
            TryStore(() =>
            {
                _store.Load();
                _store.Observe(CaseDefinition.CaseId, caseState);
            });
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[StreetMemory] {_store.LoadStatus}: {_store.State}");
            }

            Refresh(caseState, true);
        }

        /// <summary>The case world view with the street's memories (as fixed at the last rebuild) laid over it.</summary>
        public CaseWorldView Compose(CaseWorldView view) => WorldView.Apply(view);

        /// <summary>
        /// What a friend says on a talk with the street's memories (as fixed at the last rebuild): the line from the
        /// night case 01 was solved becomes their changed greeting (<see cref="StreetGreetings"/>). Like the world view,
        /// the greetings arrive on the next load after the stamp and rest while case 01 is replayed.
        /// </summary>
        public IReadOnlyList<ScriptLine> Retell(Speaker speaker, IReadOnlyList<ScriptLine> lines) =>
            StreetGreetings.Retell(speaker, lines, WorldView);

        /// <summary>After every case step: the stamp teaches the street, and the lamp and sticker follow at once.</summary>
        public void Observe(CaseState caseState)
        {
            if (_store == null)
            {
                return;
            }

            TryStore(() => _store.Observe(CaseDefinition.CaseId, caseState));
            Refresh(caseState, false);
        }

        /// <summary>The world is rebuilt from facts (resume, replay, fresh start): the world view is fixed again.</summary>
        public void Rebuild(CaseState caseState) => Refresh(caseState, true);

        /// <summary>Confirmed "Replay case": the street keeps its memories; the replayed case's rest until its stamp.</summary>
        public void OnReplay(CaseState caseState)
        {
            _store?.Replay();
            Refresh(caseState, true);
        }

        /// <summary>Confirmed New game: the street forgets.</summary>
        public void OnNewGame(CaseState caseState)
        {
            if (_store != null)
            {
                TryStore(() => _store.Reset());
            }

            Refresh(caseState, true);
        }

        /// <summary>
        /// Every frame of play (the director): once the street remembers case 01 (and its memories are not resting),
        /// the owl's 9 s wind cycle becomes the show clock: every <see cref="BoothShowClock.IntervalSeconds"/> of play on
        /// the street (<paramref name="onStreet"/>: outside, nothing holding the screen) the bell dings, the owl bows
        /// three times, and Mr. Vale checks his watch if he is in view.
        /// </summary>
        public void Tick(float dt, bool onStreet, WorkshopView workshop, Witness vale, Camera lens)
        {
            ShowClock.SetRunning(Live.OwlBows && workshop != null);
            if (ShowClock.Running && workshop.WindEnabled)
            {
                // The show clock drives the owl now; the replay's rebuild turns the wind back on (ApplyWorld).
                workshop.WindEnabled = false;
            }

            if (ShowClock.Advance(dt, onStreet) == BoothShow.OnTheHour)
            {
                RingBell();
                workshop.PlayShow();
                if (vale != null && lens != null && InView(lens, vale.transform.position + Vector3.up))
                {
                    vale.PlayGesture(CharacterClips.CheckWatch, new Vector3(0f, 1.4f, CaseRouteLayout.FrontZ - 0.1f));
                    ValeWatchChecks++;
                }

                ShowPlayed?.Invoke(BoothShow.OnTheHour);
            }

            StepBell(dt, lens);
        }

        /// <summary>
        /// A knock on the booth's shutter (Use, only offered after the stamp): a small ding of paper light; the third in
        /// a row makes the owl bow. Inert while the clock is off. Returns what happened.
        /// </summary>
        public BoothShow KnockShutter(WorkshopView workshop)
        {
            var show = ShowClock.Knock();
            if (!ShowClock.Running)
            {
                return BoothShow.None;
            }

            Ding(0.45f);
            if (show == BoothShow.Knocked && workshop != null)
            {
                workshop.PlayShow();
                ShowPlayed?.Invoke(BoothShow.Knocked);
            }

            return show;
        }

        /// <summary>The booth's little bell dings (on the hour, and in the cutscenes).</summary>
        public void RingBell()
        {
            BellDings++;
            Ding(1f);
        }

        private void Ding(float size)
        {
            _bellTime = 0f;
            _bellSize = size;
        }

        private static bool InView(Camera lens, Vector3 point)
        {
            var v = lens.WorldToViewportPoint(point);
            return v.z > 0f && v.z < ValeInViewDistance && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
        }

        private void StepBell(float dt, Camera lens)
        {
            if (_bell == null || _bellTime < 0f)
            {
                return;
            }

            _bellTime += dt;
            if (_bellTime > BellSeconds || lens == null)
            {
                _bellTime = -1f;
                _bell.gameObject.SetActive(false);
                return;
            }

            float u = _bellTime / BellSeconds;
            float size = 0.22f * _bellSize * Mathf.Sin(u * Mathf.PI);
            _bell.gameObject.SetActive(true);
            _bell.transform.SetPositionAndRotation(BellPoint, lens.transform.rotation * Quaternion.Euler(0f, 0f, _bellTime * 90f));
            float native = _bell.sprite != null ? _bell.sprite.bounds.size.y : 1f;
            _bell.transform.localScale = Vector3.one * (Mathf.Max(0.001f, size) / Mathf.Max(0.01f, native));
            _bell.color = new Color(1f, 0.93f, 0.7f, Mathf.Sin(u * Mathf.PI));
        }

        private void Refresh(CaseState caseState, bool world)
        {
            Live = StreetView.From(State, CaseDefinition.CaseId, caseState);
            if (world)
            {
                WorldView = Live;
            }

            _boothLamp.enabled = Live.BoothOpen;
            if (_sticker != null)
            {
                _sticker.SetActive(Live.BeaOwlSticker);
            }
        }

        private static void TryStore(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                // A failed write never stops play; the previous copy stays valid and a stamped save rebuilds the file.
                Debug.LogWarning("[StreetMemory] Save failed: " + e.Message);
            }
        }
    }
}
