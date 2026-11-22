using SpookyStreet.Core.Animation;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Characters
{
    /// <summary>
    /// Places the witnesses of the first case following <see cref="EnvironmentLayout.Witnesses"/>: Bea west of the
    /// plaza with her bicycle beside her, and Mr. Vale seated on the plaza bench facing the owl house door. Each
    /// witness reacts to Pip (<see cref="Witness"/>). Bea gets a capsule collider and the bicycle a box; Mr. Vale is
    /// covered by the bench's. A missing model is skipped without a log; Mr. Vale needs the bench, so he only comes
    /// with the environment models. Nothing here is dialogue or case logic (S002).
    /// </summary>
    public sealed class WitnessCast
    {
        public Transform Root { get; private set; }
        public Witness Bea { get; private set; }
        public Witness Vale { get; private set; }
        public Transform Bicycle { get; private set; }

        /// <summary>Number of instances spawned (characters and the bicycle).</summary>
        public int Count { get; private set; }

        /// <summary>
        /// Spawns whatever is committed under <paramref name="parent"/>, watching <paramref name="target"/> (Pip).
        /// <paramref name="benchPresent"/> is true when the plaza bench model stands at its layout position.
        /// </summary>
        public static WitnessCast Build(Transform parent, MaterialLibrary materials, Transform target, bool benchPresent)
        {
            var cast = new WitnessCast();
            var props = new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = null };
            foreach (var placement in EnvironmentLayout.Witnesses)
            {
                switch (placement.Model)
                {
                    case EnvironmentLayout.Bea:
                        cast.Bea = cast.SpawnWitness(parent, materials, placement, CharacterClips.Standing, target);
                        break;
                    case EnvironmentLayout.Vale:
                        if (benchPresent)
                        {
                            cast.Vale = cast.SpawnWitness(parent, materials, placement, CharacterClips.Seated, target);
                        }

                        break;
                    case EnvironmentLayout.BeaBicycle:
                        if (!props.Has(placement.Model))
                        {
                            break;
                        }

                        var bike = props.Spawn(placement.Model, cast.EnsureRoot(parent),
                            new Vector3(placement.X, placement.Y, placement.Z), placement.Yaw, placement.Scale,
                            solid: placement.Collider == PlacementCollider.Bounds);
                        cast.Bicycle = bike.transform;
                        cast.Count++;
                        break;
                }
            }

            if (cast.Count > 0 && Debug.isDebugBuild)
            {
                Debug.Log($"[WitnessCast] {cast.Count} spawned: Bea {Describe(cast.Bea)}, Vale {Describe(cast.Vale)}, " +
                          $"bicycle {(cast.Bicycle != null)}");
            }

            return cast;
        }

        private static string Describe(Witness witness)
        {
            if (witness == null)
            {
                return "absent";
            }

            var avatar = witness.Avatar;
            return $"at {witness.transform.position} yaw {witness.transform.eulerAngles.y:F0}, " +
                   $"{avatar.ClipNames.Count} clips, animated {avatar.IsAnimated}, loop {avatar.Loop ?? "none"}";
        }

        private Transform EnsureRoot(Transform parent)
        {
            if (Root == null)
            {
                Root = new GameObject("Witnesses").transform;
                Root.SetParent(parent, false);
            }

            return Root;
        }

        private Witness SpawnWitness(Transform parent, MaterialLibrary materials, in Placement placement,
            WitnessClipSet clips, Transform target)
        {
            if (!NpcAvatar.Exists(placement.Model))
            {
                return null;
            }

            float facing = placement.Yaw + EnvironmentLayout.ModelFacingOffset;
            var avatar = NpcAvatar.Spawn(materials, placement.Model, EnsureRoot(parent),
                new Vector3(placement.X, placement.Y, placement.Z), facing, clips.Rest);
            if (avatar == null)
            {
                return null;
            }

            Count++;
            if (placement.Collider == PlacementCollider.Capsule)
            {
                var capsule = avatar.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = EnvironmentLayout.WitnessRadius;
                capsule.height = EnvironmentLayout.WitnessHeight;
                capsule.center = new Vector3(0f, EnvironmentLayout.WitnessHeight * 0.5f, 0f);
            }

            var witness = avatar.gameObject.AddComponent<Witness>();
            witness.Configure(clips, target);
            return witness;
        }
    }
}
