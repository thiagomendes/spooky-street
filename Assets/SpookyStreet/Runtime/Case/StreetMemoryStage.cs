using System;
using SpookyStreet.App;
using SpookyStreet.Core.Case;
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

        private StreetStore _store;
        private Light _boothLamp;
        private GameObject _sticker;

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
