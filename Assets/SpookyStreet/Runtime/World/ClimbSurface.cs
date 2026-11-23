using System.Collections.Generic;
using SpookyStreet.Core.Movement;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Marks one collider as a climbable wall and/or a grabbable ledge (S003). Added only from
    /// <see cref="SpookyStreet.Core.World.CollisionRules.TraversalMarks"/> (by <see cref="ModelColliders"/> and the case
    /// stage) or by a test rig: Pip's probes ignore every collider without it, so the house siding and every unmarked
    /// edge never start a wall slide, a wall jump or a ledge grab. <see cref="Face"/> limits the marks to one side.
    /// </summary>
    public sealed class ClimbSurface : MonoBehaviour
    {
        private static readonly List<ClimbSurface> s_active = new List<ClimbSurface>();

        /// <summary>Every enabled marked surface (a handful), so Pip's probes need no physics overlap query.</summary>
        public static IReadOnlyList<ClimbSurface> Active => s_active;

        /// <summary>The marked collider on this object.</summary>
        public Collider SurfaceCollider { get; private set; }

        private void OnEnable()
        {
            SurfaceCollider = GetComponent<Collider>();
            if (!s_active.Contains(this))
            {
                s_active.Add(this);
            }
        }

        private void OnDisable() => s_active.Remove(this);

        /// <summary>What the surface allows.</summary>
        public SurfaceMarks Marks;

        /// <summary>World outward normal of the one face the marks apply to; zero for every side face.</summary>
        public Vector3 Face;

        /// <summary>True when <paramref name="kind"/> is allowed on the face whose outward normal is <paramref name="normal"/>.</summary>
        public bool Accepts(SurfaceMarks kind, Vector3 normal)
        {
            if ((Marks & kind) == 0)
            {
                return false;
            }

            if (Face.sqrMagnitude < 1e-6f)
            {
                return true;
            }

            normal.y = 0f;
            Vector3 face = Face;
            face.y = 0f;
            return Vector3.Dot(face.normalized, normal.normalized) > 0.9f;
        }

        /// <summary>Adds (or updates) the mark on <paramref name="target"/>; nothing for <see cref="SurfaceMarks.None"/>.</summary>
        public static ClimbSurface Mark(GameObject target, SurfaceMarks marks, Vector3 face)
        {
            if (target == null || marks == SurfaceMarks.None)
            {
                return null;
            }

            if (!target.TryGetComponent(out ClimbSurface surface))
            {
                surface = target.AddComponent<ClimbSurface>();
            }

            surface.Marks = marks;
            surface.Face = face;
            return surface;
        }
    }
}
