using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Remembers the most recent safe surface Pip stood on and decides when a fall should return there
    /// (spec §5: "within two seconds", no fall damage, progress kept).
    /// Only colliders on the SafeGround layer become anchors, so ledges and props are never used.
    /// </summary>
    public sealed class SafeGroundTracker : MonoBehaviour
    {
        public const string SafeGroundLayerName = "SafeGround";

        [SerializeField] private float anchorStableTime = 0.2f;
        [SerializeField] private float killPlaneY = -20f;

        private int _safeLayer = -1;
        private Collider _groundCollider;
        private float _groundedTime;
        private float _fallingTime;

        public Vector3 Anchor { get; private set; }

        public bool HasAnchor { get; private set; }

        public float FallResetDepth { get; set; } = 5f;

        public float MaxFallTime { get; set; } = 1.4f;

        private void Awake()
        {
            _safeLayer = LayerMask.NameToLayer(SafeGroundLayerName);
        }

        public void SetAnchor(Vector3 position)
        {
            Anchor = position;
            HasAnchor = true;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.7f)
            {
                _groundCollider = hit.collider;
            }
        }

        /// <summary>Returns true when Pip should be returned to <see cref="Anchor"/>.</summary>
        public bool Tick(bool grounded, float verticalVelocity, float deltaTime)
        {
            if (grounded)
            {
                _fallingTime = 0f;
                _groundedTime += deltaTime;
                bool safe = _groundCollider != null && (_safeLayer < 0 || _groundCollider.gameObject.layer == _safeLayer);
                if (safe && _groundedTime >= anchorStableTime)
                {
                    SetAnchor(transform.position);
                }
            }
            else
            {
                _groundedTime = 0f;
                _groundCollider = null;
                _fallingTime = verticalVelocity < 0f ? _fallingTime + deltaTime : 0f;
            }

            if (!HasAnchor)
            {
                return false;
            }

            float y = transform.position.y;
            return y < killPlaneY || y < Anchor.y - FallResetDepth || _fallingTime > MaxFallTime;
        }

        public void OnReturned()
        {
            _fallingTime = 0f;
            _groundedTime = 0f;
        }
    }
}
