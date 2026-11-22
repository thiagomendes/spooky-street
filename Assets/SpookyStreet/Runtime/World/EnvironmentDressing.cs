using SpookyStreet.Core.World;
using SpookyStreet.Player;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Dresses Lantern Lane with the original environment models (tools/blender/env, exported to
    /// <see cref="ModelFolder"/>) following <see cref="EnvironmentLayout"/>: the owl house and its Halloween
    /// dressing where the greybox house stood, the arrival porch kit with the candy bowl, the lined path, plaza
    /// props, front-yard hedge, gate and fence, and the side-yard maple and treehouse. Gives the models their
    /// baked URP materials (glow parts emissive), fits colliders where Pip should be stopped, starts the ambient
    /// motion and reports where the warm practical lights belong.
    /// Same pattern as <see cref="PipAvatar"/>: when the models are not committed, <see cref="Available"/> is
    /// false, <see cref="Build"/> does nothing and the greybox stays exactly as before.
    /// The street set (S000.4: lawn, stepping-stone path, sidewalk, boundary hedges, neighbour houses and leaf
    /// scatter) comes in when all of its models are committed (<see cref="StreetSetAvailable"/>). It draws over the
    /// greybox ground, path, hedge and skyline, whose gameplay colliders stay (see
    /// <see cref="GreyboxBuilder.UseStreetSet"/>). The pieces never move, so they spawn under their own
    /// <see cref="StreetRoot"/> and are statically batched; nothing animated lives there.
    /// </summary>
    public sealed class EnvironmentDressing
    {
        public const string ModelFolder = "Models/Environment";
        public const string HousePath = ModelFolder + "/" + EnvironmentLayout.OwlHouse;

        /// <summary>
        /// The wind wheel's axle in its local frame. Exported about Blender's Y (front to back), which is Unity's
        /// local Z after the importer's axis conversion.
        /// </summary>
        public static readonly Vector3 WindWheelAxis = Vector3.forward; // world space

        /// <summary>Pitch (degrees about local X) that stands the flat-modelled paper bats up toward the street.</summary>
        public const float BatTilt = 65f;

        private readonly ModelKit _kit;
        private readonly ModelKit _streetKit;
        private readonly int _safeLayer;

        public EnvironmentDressing(MaterialLibrary materials)
        {
            _kit = new ModelKit(materials, ModelFolder) { FallbackAtlas = ModelKit.DefaultFallbackAtlas };
            int layer = LayerMask.NameToLayer(SafeGroundTracker.SafeGroundLayerName);
            _safeLayer = layer >= 0 ? layer : 0;
            // Street-set models share the Street atlas (the Lawn has its own textures, which take precedence).
            _streetKit = new ModelKit(materials, ModelFolder) { FallbackAtlas = EnvironmentLayout.StreetAtlas };
            Available = _kit.Has(EnvironmentLayout.OwlHouse);
            StreetSetAvailable = Available && HasAll(_streetKit, EnvironmentLayout.StreetSetModels);
            Anchors = AtmosphereAnchors.Greybox;
        }

        /// <summary>True when the owl house model is committed (the rest of the kit comes with it).</summary>
        public bool Available { get; }

        /// <summary>True when every street-set model is committed (they replace the greybox ground and skyline).</summary>
        public bool StreetSetAvailable { get; }

        /// <summary>True once <see cref="Build"/> has spawned the street set.</summary>
        public bool UsesStreetSet { get; private set; }

        /// <summary>Static, batched parent of the street set (null without it).</summary>
        public Transform StreetRoot { get; private set; }

        /// <summary>Number of street-set instances spawned (included in <see cref="SpawnedCount"/>).</summary>
        public int StreetCount { get; private set; }

        /// <summary>True once <see cref="Build"/> has spawned the models.</summary>
        public bool UsesModels { get; private set; }

        public Transform Root { get; private set; }
        public Transform House { get; private set; }
        public Transform AtticWindow { get; private set; }
        public Transform WindWheel { get; private set; }
        public Transform OwlPuppet { get; private set; }
        public Transform CandyBowl { get; private set; }
        public AmbientMotion Motion { get; private set; }

        /// <summary>
        /// Extra yaw applied to every model: 0 when the FBX import keeps the export's facing (fronts toward -Z, as
        /// learned with Pip), 180 when the attic window turned up behind the house origin instead, meaning the
        /// importer turned the kit around. Logged so CI captures can be read against it.
        /// </summary>
        public float ModelYawCorrection { get; private set; }

        /// <summary>Number of model instances spawned.</summary>
        public int SpawnedCount { get; private set; }

        /// <summary>Where the warm practical lights go (greybox positions until the models are built).</summary>
        public AtmosphereAnchors Anchors { get; private set; }

        /// <summary>Spawns the models under <paramref name="parent"/>. Returns null (and changes nothing) without them.</summary>
        public Transform Build(Transform parent)
        {
            if (!Available || UsesModels)
            {
                return Root;
            }

            Root = new GameObject("Environment_Original").transform;
            Root.SetParent(parent, false);
            Motion = Root.gameObject.AddComponent<AmbientMotion>();

            Transform streetLamp = null;
            Transform houseDressing = null;
            foreach (var placement in EnvironmentLayout.House)
            {
                var instance = Spawn(placement);
                if (instance == null)
                {
                    continue;
                }

                if (placement.Model == EnvironmentLayout.OwlHouse)
                {
                    House = instance;
                }
                else
                {
                    houseDressing = instance;
                }
            }

            AtticWindow = ModelKit.FindDeep(House, EnvironmentLayout.AtticWindowGlow);
            CheckFacing(houseDressing);
            BuildHouseColliders();
            WindWheel = ModelKit.FindDeep(House, EnvironmentLayout.WindWheel);
            OwlPuppet = ModelKit.FindDeep(House, EnvironmentLayout.OwlPuppet);
            // Children keep Blender's Z-up local frames (only the FBX root carries the axis conversion), so express
            // the axle in world space (front-to-back, along the street axis) and convert it to the wheel's frame.
            if (WindWheel != null)
            {
                Motion.AddSpinner(WindWheel, Quaternion.Inverse(WindWheel.rotation) * WindWheelAxis);
            }
            Motion.AddSway(OwlPuppet);

            foreach (var placement in EnvironmentLayout.Street)
            {
                var instance = Spawn(placement);
                if (instance == null)
                {
                    continue;
                }

                switch (placement.Model)
                {
                    case EnvironmentLayout.CandyBowl:
                        CandyBowl = instance;
                        break;
                    case EnvironmentLayout.StreetLamp:
                        streetLamp = instance;
                        break;
                    case EnvironmentLayout.FenceGate:
                        var gate = ModelKit.FindDeep(instance, EnvironmentLayout.Gate);
                        if (gate != null)
                        {
                            gate.rotation = Quaternion.AngleAxis(EnvironmentLayout.GateOpenYaw, Vector3.up) * gate.rotation;
                        }

                        break;
                }
            }

            foreach (var placement in EnvironmentLayout.PaperBats)
            {
                // The bat is modelled flying flat; tip it up so its silhouette and glowing edge face the street.
                var bat = Spawn(placement);
                if (bat != null)
                {
                    bat.rotation = Quaternion.AngleAxis(-BatTilt, Vector3.right) * bat.rotation;
                }

                Motion.AddBob(bat);
            }

            BuildStreetSet();
            Anchors = FindAnchors(streetLamp, houseDressing);
            UsesModels = true;
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[EnvironmentDressing] {SpawnedCount} models, attic glow {(AtticWindow != null)} at " +
                          $"{Anchors.AtticWindow}, wind wheel {(WindWheel != null)}, owl {(OwlPuppet != null)}, " +
                          $"animated {Motion.Count}, street set {StreetCount}, yaw correction {ModelYawCorrection:F0}");
            }

            return Root;
        }

        /// <summary>
        /// The house front (round attic window) must face the arrival porch (-Z). If the import turned the models
        /// around, the window sits behind the origin: turn the house (and every later model) by 180 degrees, which
        /// is exact because an axis conversion is a rotation about Y.
        /// </summary>
        private void CheckFacing(Transform houseDressing)
        {
            if (House == null || !TryCentre(AtticWindow, out var attic))
            {
                return;
            }

            if (House.InverseTransformPoint(attic).z <= 0f)
            {
                return;
            }

            ModelYawCorrection = 180f;
            Debug.LogWarning("[EnvironmentDressing] Owl house imported facing +Z (attic window behind its origin); " +
                             "turning the environment models 180 degrees.");
            House.localRotation = Quaternion.Euler(0f, ModelYawCorrection, 0f) * House.localRotation;
            if (houseDressing != null)
            {
                houseDressing.localRotation = Quaternion.Euler(0f, ModelYawCorrection, 0f) * houseDressing.localRotation;
            }
        }

        /// <summary>
        /// Lawn, path, sidewalk, boundary hedges, neighbour houses and leaves under one static root, then batched.
        /// Ground pieces are at most 3 cm tall, so Pip keeps walking on the greybox's flat colliders; the hedge
        /// colliders stay greybox boxes too, and the neighbours stand outside the block.
        /// </summary>
        private void BuildStreetSet()
        {
            if (!StreetSetAvailable)
            {
                return;
            }

            StreetRoot = new GameObject("Street_Static").transform;
            StreetRoot.SetParent(Root, false);
            int before = SpawnedCount;
            SpawnAll(EnvironmentLayout.LawnTiles);
            SpawnAll(EnvironmentLayout.PathTiles);
            SpawnAll(EnvironmentLayout.SidewalkTiles);
            SpawnAll(EnvironmentLayout.BoundaryHedges);
            SpawnAll(EnvironmentLayout.NeighbourHouses);
            SpawnAll(EnvironmentLayout.LeafScatters);
            StreetCount = SpawnedCount - before;
            StaticBatchingUtility.Combine(StreetRoot.gameObject);
            UsesStreetSet = true;
        }

        private void SpawnAll(Placement[] placements)
        {
            foreach (var placement in placements)
            {
                Spawn(placement, _streetKit, StreetRoot);
            }
        }

        private static bool HasAll(ModelKit kit, string[] models)
        {
            foreach (var model in models)
            {
                if (!kit.Has(model))
                {
                    return false;
                }
            }

            return true;
        }

        private Transform Spawn(in Placement placement) => Spawn(placement, _kit, Root);

        private Transform Spawn(in Placement placement, ModelKit kit, Transform parent)
        {
            var instance = kit.Spawn(placement.Model, parent, new Vector3(placement.X, placement.Y, placement.Z),
                placement.Yaw + ModelYawCorrection, placement.Scale);
            if (instance == null)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[EnvironmentDressing] Missing model {kit.Folder}{placement.Model}; skipped.");
                }

                return null;
            }

            SpawnedCount++;
            switch (placement.Collider)
            {
                case PlacementCollider.Bounds:
                    ModelKit.AddBoundsCollider(instance);
                    break;
                case PlacementCollider.Trunk:
                    // Trunk only (maple trunk centre sits ~0.1 m off the origin); the crown stays open overhead.
                    AddBox(instance, new Vector3(0.1f, 1.25f, 0f), new Vector3(0.7f, 2.5f, 0.7f));
                    break;
                case PlacementCollider.Pole:
                    AddBox(instance, new Vector3(0f, 1.55f, 0f), new Vector3(0.3f, 3.1f, 0.3f));
                    break;
            }

            return instance.transform;
        }

        private static void AddBox(GameObject instance, Vector3 center, Vector3 size)
        {
            var box = instance.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        /// <summary>
        /// Simple boxes for the owl house (model space, origin at the base centre of body + porch, front at -Z):
        /// body 6 × 6 m behind the front wall (z = -2), the roof in two steps, the porch deck at 0.6 m with its
        /// steps (safe ground) and the porch rails and step posts so Pip cannot walk through them.
        /// </summary>
        private void BuildHouseColliders()
        {
            var origin = new Vector3(EnvironmentLayout.HouseX, 0f, EnvironmentLayout.HouseZ);
            HouseBox("OwlHouse_Body", origin, new Vector3(0f, 1.4f, 1f), new Vector3(6.1f, 2.8f, 6f), false);
            HouseBox("OwlHouse_RoofLow", origin, new Vector3(0f, 3.4f, 1f), new Vector3(5f, 1.2f, 6.6f), false);
            HouseBox("OwlHouse_RoofHigh", origin, new Vector3(0f, 4.9f, 1f), new Vector3(2.6f, 1.8f, 6.6f), false);
            HouseBox("OwlHouse_Porch", origin, new Vector3(0f, 0.3f, -3f), new Vector3(6f, 0.6f, 2f), true);
            for (int i = 0; i < 2; i++)
            {
                float height = 0.4f - i * 0.2f;
                float tread = GreyboxBuilder.KitTread;
                HouseBox("OwlHouse_Step" + i, origin, new Vector3(0f, height * 0.5f, -4f - tread * (i + 0.5f)),
                    new Vector3(1.8f, height, tread), true);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                HouseBox("OwlHouse_FrontRail", origin, new Vector3(side * 1.95f, 1.1f, -3.9f), new Vector3(1.8f, 1f, 0.15f),
                    false);
                HouseBox("OwlHouse_SideRail", origin, new Vector3(side * 2.9f, 1.1f, -2.95f), new Vector3(0.15f, 1f, 1.8f),
                    false);
                HouseBox("OwlHouse_StepPost", origin, new Vector3(side * 1f, 1.6f, -3.9f), new Vector3(0.2f, 2f, 0.2f),
                    false);
            }
        }

        private void HouseBox(string name, Vector3 origin, Vector3 center, Vector3 size, bool safe)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(Root, false);
            go.transform.localPosition = origin + center;
            go.GetComponent<BoxCollider>().size = size;
            go.layer = safe ? _safeLayer : 0;
        }

        private AtmosphereAnchors FindAnchors(Transform streetLamp, Transform houseDressing)
        {
            var anchors = AtmosphereAnchors.Greybox;
            if (TryCentre(AtticWindow, out var attic))
            {
                // Just in front of the round window, as in the key frame (spill onto the gable and porch roof).
                anchors.AtticLight = attic + new Vector3(0f, -0.2f, -0.9f);
                anchors.AtticWindow = attic;
            }

            if (TryCentre(ModelKit.FindDeep(streetLamp, EnvironmentLayout.LampGlow), out var lantern))
            {
                anchors.ArrivalLight = lantern + new Vector3(0f, -0.25f, 0f);
            }

            if (TryCentre(ModelKit.FindDeep(houseDressing, EnvironmentLayout.LampGlow), out var porchLamp))
            {
                anchors.DoorLight = porchLamp + new Vector3(0f, -0.3f, -0.2f);
            }

            return anchors;
        }

        private static bool TryCentre(Transform part, out Vector3 centre)
        {
            centre = default;
            var renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return false;
            }

            centre = renderer.bounds.center;
            return true;
        }
    }
}
