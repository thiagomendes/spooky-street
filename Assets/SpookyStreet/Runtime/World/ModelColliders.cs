using SpookyStreet.Core.Movement;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Builds a model's colliders from its <see cref="ColliderRecipe"/> (Core <see cref="CollisionRules"/>): a
    /// "Colliders" child turned by the placement's yaw only (so the parts sit in the model's upright frame whatever
    /// rotation the FBX root carries), with one child per part: boxes, pitched boxes, boxes fitted to the meshes
    /// and capsules between two points.
    /// </summary>
    public static class ModelColliders
    {
        public const string HolderName = "Colliders";
        public const string PartPrefix = "Collider_";

        /// <summary>Adds the recipe's parts to <paramref name="instance"/>; returns the holder (null without parts).</summary>
        public static Transform Apply(GameObject instance, ColliderRecipe recipe, float yaw)
        {
            if (instance == null || recipe == null || !recipe.Solid || recipe.Parts.Length == 0)
            {
                return null;
            }

            var holder = new GameObject(HolderName).transform;
            holder.SetParent(instance.transform, false);
            Transform parent = instance.transform.parent;
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            holder.SetPositionAndRotation(instance.transform.position, parentRotation * Quaternion.Euler(0f, yaw, 0f));
            holder.gameObject.layer = instance.layer;
            Bounds? mesh = null;
            foreach (var part in recipe.Parts)
            {
                var go = new GameObject(PartPrefix + part.Name);
                go.layer = instance.layer;
                go.transform.SetParent(holder, false);
                switch (part.Shape)
                {
                    case ColliderShape.Box:
                        go.transform.localPosition = new Vector3(part.AX, part.AY, part.AZ);
                        go.transform.localRotation = Quaternion.Euler(part.Pitch, 0f, 0f);
                        go.AddComponent<BoxCollider>().size = new Vector3(part.BX, part.BY, part.BZ);
                        break;
                    case ColliderShape.FittedBox:
                        Bounds b = mesh ??= MeshBounds(instance, holder);
                        float y0 = float.IsNaN(part.AY) ? b.min.y : Mathf.Max(b.min.y, part.AY);
                        float y1 = float.IsNaN(part.BY) ? b.max.y : Mathf.Min(b.max.y, part.BY);
                        go.transform.localPosition = new Vector3(b.center.x, (y0 + y1) * 0.5f, b.center.z);
                        go.AddComponent<BoxCollider>().size = new Vector3(b.size.x, Mathf.Max(0.01f, y1 - y0), b.size.z);
                        break;
                    case ColliderShape.Capsule:
                        var a = new Vector3(part.AX, part.AY, part.AZ);
                        var c = new Vector3(part.BX, part.BY, part.BZ);
                        Vector3 axis = c - a;
                        go.transform.localPosition = (a + c) * 0.5f;
                        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                        var capsule = go.AddComponent<CapsuleCollider>();
                        capsule.direction = 1;
                        capsule.radius = part.Radius;
                        capsule.height = axis.magnitude + 2f * part.Radius;
                        break;
                }

                // S003: the few parts marked for the wall jump or the ledge grab (faces turn with the model).
                SurfaceMarks marks = CollisionRules.MarksForPart(recipe.Model, part.Name, out float faceX, out float faceZ);
                ClimbSurface.Mark(go, marks, holder.rotation * new Vector3(faceX, 0f, faceZ));
            }

            return holder;
        }

        /// <summary>Bounds of every mesh under <paramref name="root"/> in <paramref name="frame"/>'s local space.</summary>
        public static Bounds MeshBounds(GameObject root, Transform frame)
        {
            Matrix4x4 toFrame = frame.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 m = toFrame * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z));
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }
    }
}
