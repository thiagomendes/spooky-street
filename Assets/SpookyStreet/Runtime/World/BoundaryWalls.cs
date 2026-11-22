using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Closes the block for Pip (owner feedback, TestFlight 13.1: jumping a boundary hedge dropped her into limbo
    /// and the safe-ground return put her back). Invisible walls stand just outside the boundary hedges
    /// (<see cref="CollisionRules.BoundaryWalls"/>, 14 m tall, so no jump or glide gets over them) on the
    /// Ignore Raycast layer: they stop Pip's CharacterController but not the camera boom or the ground probes.
    /// Ground strips (<see cref="CollisionRules.GroundRing"/>) carry the whole lawn ring around the block, so any
    /// spot Pip could ever reach has ground and the safe-ground return stays a last resort.
    /// </summary>
    public static class BoundaryWalls
    {
        public const string RootName = "Boundary";
        public const string IgnoreRaycastLayerName = "Ignore Raycast";

        public static Transform Build(Transform parent, int groundLayer)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            int wallLayer = LayerMask.NameToLayer(IgnoreRaycastLayerName);
            foreach (var wall in CollisionRules.BoundaryWalls)
            {
                Box(root, wall, wallLayer >= 0 ? wallLayer : 0);
            }

            foreach (var ground in CollisionRules.GroundRing)
            {
                Box(root, ground, groundLayer);
            }

            return root;
        }

        private static void Box(Transform root, ColliderPart part, int layer)
        {
            var go = new GameObject(part.Name, typeof(BoxCollider)) { layer = layer };
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(part.AX, part.AY, part.AZ);
            go.GetComponent<BoxCollider>().size = new Vector3(part.BX, part.BY, part.BZ);
        }
    }
}
