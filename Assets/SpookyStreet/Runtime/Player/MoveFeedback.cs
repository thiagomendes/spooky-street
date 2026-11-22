using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Paper effects that make the moveset readable (S000.6 feel pass, rules in <see cref="MoveFeedbackRules"/>):
    /// a confetti-and-star burst at Pip's feet on the 2nd and 3rd chained jumps, short paper speed streaks on a
    /// dive, a dust puff when a dive lands in its roll, and an upward star puff with a vertical speed line when a
    /// held jump's boost engages (the high jump). Craft style: double-sided paper pieces in palette
    /// colours, candy-corn stars as the bright highlight. Cheap on mobile: one fixed pool of
    /// <see cref="PoolSize"/> small unlit quads built once (no particle system, no transparency, no shadows, no
    /// allocations per frame); a full pool recycles its oldest piece. Runs on game time, so pause freezes every
    /// piece in place. Reduced motion (<see cref="AmbientMotion.ReducedMotion"/>) skips the bursts.
    /// </summary>
    public sealed class MoveFeedback : MonoBehaviour
    {
        public const int PoolSize = 40;

        private const float ConfettiGravity = 7f;
        private const float ConfettiDrag = 1.6f;

        private enum Kind
        {
            Confetti,
            Star,
            Streak,
            Puff,
        }

        private struct Piece
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Kind Kind;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Axis;
            public Quaternion Rotation;
            public float Spin;
            public float Size;
            public float Age;
            public float Life;
            public bool Active;
        }

        private readonly Piece[] _pieces = new Piece[PoolSize];
        private PipMotor _motor;
        private Transform _camera;
        private Material[] _confettiMaterials;
        private Material _starMaterial;
        private Material _streakMaterial;
        private Material _streakHighlight;
        private Material[] _puffMaterials;
        private Mesh _confettiMesh;
        private Mesh _starMesh;
        private Mesh _streakMesh;
        private Mesh _puffMesh;
        private int _next;
        private float _streakWaveIn = -1f;
        private uint _seed = 0x9E3779B9u;

        /// <summary>Bursts started since creation (for tests and diagnostics).</summary>
        public int BurstsPlayed { get; private set; }

        /// <summary>Kinds of the bursts played, accumulated (tests).</summary>
        public MoveBursts Played { get; private set; }

        /// <summary>Pieces currently alive.</summary>
        public int ActivePieces
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _pieces.Length; i++)
                {
                    if (_pieces[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Builds the pool in its own scene root (pieces live in world space, not under Pip).</summary>
        public static MoveFeedback Create(PipMotor motor, MaterialLibrary materials, Transform cameraTransform)
        {
            var root = new GameObject("MoveFeedback");
            var feedback = root.AddComponent<MoveFeedback>();
            feedback.Build(motor, materials, cameraTransform);
            return feedback;
        }

        private void Build(PipMotor motor, MaterialLibrary materials, Transform cameraTransform)
        {
            _motor = motor;
            _camera = cameraTransform;
            _confettiMaterials = new[]
            {
                materials.Glow(Palette.JackOrange), materials.Glow(Palette.HauntedPurple),
                materials.Glow(Palette.WarmPaper, 0.9f), materials.Glow(Palette.Plum),
            };
            // Candy-corn stars sit just above the bloom threshold: the small bright highlight of each burst.
            _starMaterial = materials.Glow(Palette.CandyCornYellow, 1.25f);
            _streakMaterial = materials.Glow(Palette.WarmPaper, 0.85f);
            _streakHighlight = materials.Glow(Palette.CandyCornYellow, 1.1f);
            _puffMaterials = new[] { materials.Glow(Palette.PipWing, 0.7f), materials.Glow(Palette.Path, 0.9f) };

            _confettiMesh = DoubleSided("PaperConfetti", new[]
            {
                new Vector3(-0.5f, -0.3f, 0f), new Vector3(0.5f, -0.3f, 0f), new Vector3(0.5f, 0.3f, 0f),
                new Vector3(-0.5f, 0.3f, 0f),
            }, new[] { 0, 2, 1, 0, 3, 2 });
            _starMesh = Star();
            _streakMesh = DoubleSided("PaperStreak", new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, -1f),
                new Vector3(-0.5f, 0f, -1f),
            }, new[] { 0, 1, 2, 0, 2, 3 });
            _puffMesh = Disc("DustPuff", 8);

            for (int i = 0; i < _pieces.Length; i++)
            {
                var go = new GameObject("Piece");
                go.transform.SetParent(transform, false);
                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // Every pooled renderer carries a URP palette material from the start (even while inactive), so
                // nothing can ever draw with the built-in default (magenta on device). Bursts swap it per piece.
                renderer.sharedMaterial = _confettiMaterials[i % _confettiMaterials.Length];
                filter.sharedMesh = _confettiMesh;
                go.SetActive(false);
                _pieces[i] = new Piece { Transform = go.transform, Filter = filter, Renderer = renderer };
            }

            if (_motor != null)
            {
                _motor.Stepped += OnMotorStep;
            }
        }

        private void OnDestroy()
        {
            if (_motor != null)
            {
                _motor.Stepped -= OnMotorStep;
            }

            DestroyMesh(_confettiMesh);
            DestroyMesh(_starMesh);
            DestroyMesh(_streakMesh);
            DestroyMesh(_puffMesh);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void OnMotorStep(MotorStep step)
        {
            MoveBursts bursts = MoveFeedbackRules.BurstsFor(step, AmbientMotion.ReducedMotion);
            if (bursts != MoveBursts.None)
            {
                Play(bursts);
            }
        }

        /// <summary>Plays <paramref name="bursts"/> at Pip now (also used by the visual capture).</summary>
        public void Play(MoveBursts bursts)
        {
            if (_motor == null || AmbientMotion.ReducedMotion)
            {
                return;
            }

            Vector3 feet = _motor.transform.position + Vector3.up * 0.05f;
            Vector3 forward = _motor.transform.forward;
            var state = _motor.State;
            var move = new Vector3(state.MoveDirX, 0f, state.MoveDirZ);
            if ((bursts & MoveBursts.ChainThird) != 0)
            {
                Burst(feet, MoveFeedbackRules.PieceCount(MoveBursts.ChainThird), 1.3f, 0.5f);
                Record(MoveBursts.ChainThird);
            }
            else if ((bursts & MoveBursts.ChainSecond) != 0)
            {
                Burst(feet, MoveFeedbackRules.PieceCount(MoveBursts.ChainSecond), 1f, 0.3f);
                Record(MoveBursts.ChainSecond);
            }

            if ((bursts & MoveBursts.DiveStreak) != 0)
            {
                Streaks(move.sqrMagnitude > 0.01f ? move.normalized : forward);
                _streakWaveIn = 0.08f;
                Record(MoveBursts.DiveStreak);
            }

            if ((bursts & MoveBursts.DiveDust) != 0)
            {
                Dust(feet);
                Record(MoveBursts.DiveDust);
            }

            if ((bursts & MoveBursts.BoostPuff) != 0)
            {
                BoostPuff(_motor.transform.position);
                Record(MoveBursts.BoostPuff);
            }
        }

        /// <summary>
        /// A small paper dust puff at <paramref name="feet"/> (the interior safety net sets Pip down with one where
        /// Pip was and one where Pip lands). Skipped with reduced motion, like every burst.
        /// </summary>
        public void Puff(Vector3 feet)
        {
            if (AmbientMotion.ReducedMotion)
            {
                return;
            }

            Dust(feet + Vector3.up * 0.05f);
            PuffsPlayed++;
        }

        /// <summary>Safety-net puffs played since creation (tests).</summary>
        public int PuffsPlayed { get; private set; }

        private void Record(MoveBursts burst)
        {
            BurstsPlayed++;
            Played |= burst;
        }

        private void Burst(Vector3 feet, int count, float power, float starShare)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random01() * 0.6f) / count * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bool star = i < Mathf.RoundToInt(count * starShare) || i % 3 == 0;
                ref Piece p = ref Take(star ? Kind.Star : Kind.Confetti);
                p.Position = feet + radial * 0.12f;
                p.Velocity = radial * Range(1.4f, 2.4f) * power + Vector3.up * Range(1.6f, 2.8f) * power;
                p.Axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized;
                if (p.Axis.sqrMagnitude < 0.01f)
                {
                    p.Axis = Vector3.up;
                }

                p.Rotation = Quaternion.AngleAxis(Range(0f, 360f), p.Axis);
                p.Spin = Range(360f, 720f);
                p.Size = star ? Range(0.07f, 0.1f) : Range(0.06f, 0.085f);
                p.Life = Range(0.5f, 0.75f);
                p.Renderer.sharedMaterial = star ? _starMaterial : _confettiMaterials[i % _confettiMaterials.Length];
            }
        }

        private void Streaks(Vector3 direction)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 origin = _motor.transform.position - direction * 0.2f;
            int count = MoveFeedbackRules.PieceCount(MoveBursts.DiveStreak);
            for (int i = 0; i < count; i++)
            {
                ref Piece p = ref Take(Kind.Streak);
                float lateral = (i - (count - 1) * 0.5f) * 0.14f;
                p.Position = origin + side * lateral + Vector3.up * Range(0.35f, 0.85f);
                p.Velocity = direction * 0.6f; // trails: Pip dives away much faster
                p.Rotation = Quaternion.LookRotation(direction, Vector3.up);
                p.Axis = Vector3.forward;
                p.Spin = 0f;
                p.Size = Range(0.55f, 0.8f);
                p.Life = 0.28f;
                p.Renderer.sharedMaterial = i == 1 ? _streakHighlight : _streakMaterial;
            }
        }

        private void Dust(Vector3 feet)
        {
            int count = MoveFeedbackRules.PieceCount(MoveBursts.DiveDust);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random01() * 0.5f) / count * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ref Piece p = ref Take(Kind.Puff);
                p.Position = feet + radial * 0.15f + Vector3.up * 0.05f;
                p.Velocity = radial * Range(0.8f, 1.4f) + Vector3.up * Range(0.2f, 0.45f);
                p.Rotation = Quaternion.identity;
                p.Axis = Vector3.forward;
                p.Spin = Range(-90f, 90f);
                p.Size = Range(0.16f, 0.26f);
                p.Life = Range(0.38f, 0.5f);
                p.Renderer.sharedMaterial = _puffMaterials[i % _puffMaterials.Length];
            }
        }

        /// <summary>
        /// High-jump boost: paper stars and confetti puff up and out under Pip's feet (Pip shoots away from them, so
        /// they read as kicked off), plus two short vertical speed lines trailing below.
        /// </summary>
        private void BoostPuff(Vector3 feet)
        {
            int count = MoveFeedbackRules.PieceCount(MoveBursts.BoostPuff);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random01() * 0.6f) / count * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bool star = i % 2 == 0;
                ref Piece p = ref Take(star ? Kind.Star : Kind.Confetti);
                p.Position = feet + radial * 0.1f - Vector3.up * 0.02f;
                p.Velocity = radial * Range(0.7f, 1.3f) + Vector3.up * Range(0.8f, 1.5f);
                p.Axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized;
                if (p.Axis.sqrMagnitude < 0.01f)
                {
                    p.Axis = Vector3.up;
                }

                p.Rotation = Quaternion.AngleAxis(Range(0f, 360f), p.Axis);
                p.Spin = Range(360f, 720f);
                p.Size = star ? Range(0.07f, 0.095f) : Range(0.055f, 0.075f);
                p.Life = Range(0.45f, 0.6f);
                p.Renderer.sharedMaterial = star ? _starMaterial : _confettiMaterials[i % _confettiMaterials.Length];
            }

            // Speed lines: the streak quad points its length down from Pip's feet and faces the camera.
            Vector3 toCamera = _camera != null ? -_camera.forward : Vector3.back;
            toCamera.y = 0f;
            toCamera = toCamera.sqrMagnitude > 1e-4f ? toCamera.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, toCamera);
            for (int i = 0; i < 2; i++)
            {
                ref Piece p = ref Take(Kind.Streak);
                p.Position = feet + side * (i == 0 ? -0.12f : 0.12f) + Vector3.up * 0.25f;
                p.Velocity = Vector3.up * 0.8f; // trails: Pip rises much faster
                p.Rotation = Quaternion.LookRotation(Vector3.up, toCamera);
                p.Axis = Vector3.forward;
                p.Spin = 0f;
                p.Size = Range(0.6f, 0.8f);
                p.Life = 0.22f;
                p.Renderer.sharedMaterial = i == 0 ? _streakHighlight : _streakMaterial;
            }
        }

        private ref Piece Take(Kind kind)
        {
            int index = -1;
            for (int i = 0; i < _pieces.Length; i++)
            {
                int candidate = (_next + i) % _pieces.Length;
                if (!_pieces[candidate].Active)
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0)
            {
                index = _next; // pool full: recycle the oldest-issued slot
            }

            _next = (index + 1) % _pieces.Length;
            ref Piece p = ref _pieces[index];
            p.Kind = kind;
            p.Age = 0f;
            p.Active = true;
            p.Filter.sharedMesh = kind == Kind.Star ? _starMesh
                : kind == Kind.Streak ? _streakMesh
                : kind == Kind.Puff ? _puffMesh
                : _confettiMesh;
            p.Transform.gameObject.SetActive(true);
            return ref p;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused: every piece holds still
            }

            if (_streakWaveIn > 0f)
            {
                _streakWaveIn -= dt;
                if (_streakWaveIn <= 0f && _motor != null && _motor.Diving && !AmbientMotion.ReducedMotion)
                {
                    var state = _motor.State;
                    Streaks(new Vector3(state.MoveDirX, 0f, state.MoveDirZ));
                }
            }

            Quaternion facingCamera = _camera != null ? Quaternion.LookRotation(_camera.forward, Vector3.up)
                : Quaternion.identity;
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref Piece p = ref _pieces[i];
                if (!p.Active)
                {
                    continue;
                }

                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Active = false;
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }

                float t = p.Age / p.Life;
                switch (p.Kind)
                {
                    case Kind.Confetti:
                    case Kind.Star:
                    {
                        // Paper flutters: light gravity, strong air drag, tumbling.
                        p.Velocity += Vector3.down * (ConfettiGravity * dt);
                        p.Velocity *= Mathf.Max(0f, 1f - ConfettiDrag * dt);
                        p.Position += p.Velocity * dt;
                        p.Rotation = Quaternion.AngleAxis(p.Spin * dt, p.Axis) * p.Rotation;
                        float pop = Mathf.Clamp01(p.Age / 0.06f);
                        float shrink = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
                        p.Transform.SetPositionAndRotation(p.Position, p.Rotation);
                        p.Transform.localScale = Vector3.one * (p.Size * pop * shrink);
                        break;
                    }
                    case Kind.Streak:
                    {
                        p.Position += p.Velocity * dt;
                        float length = p.Size * (1f - t);
                        p.Transform.SetPositionAndRotation(p.Position, p.Rotation);
                        p.Transform.localScale = new Vector3(0.05f * (1f - 0.5f * t), 1f, Mathf.Max(0.001f, length));
                        break;
                    }
                    default:
                    {
                        // Dust puff: drifts out low, swells, then shrinks away; always faces the camera so it reads round.
                        p.Velocity *= Mathf.Max(0f, 1f - 3f * dt);
                        p.Position += p.Velocity * dt;
                        float swell = t < 0.35f ? Mathf.Lerp(0.5f, 1f, t / 0.35f) : 1f - (t - 0.35f) / 0.65f;
                        p.Transform.SetPositionAndRotation(p.Position,
                            facingCamera * Quaternion.AngleAxis(p.Spin * t, Vector3.forward));
                        p.Transform.localScale = Vector3.one * (p.Size * Mathf.Max(0f, swell));
                        break;
                    }
                }
            }
        }

        private float Random01()
        {
            // xorshift: deterministic and allocation-free (no UnityEngine.Random state shared with gameplay).
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }

        private float Range(float min, float max) => min + (max - min) * Random01();

        private static Mesh DoubleSided(string name, Vector3[] vertices, int[] front)
        {
            var triangles = new int[front.Length * 2];
            for (int i = 0; i < front.Length; i += 3)
            {
                triangles[i] = front[i];
                triangles[i + 1] = front[i + 1];
                triangles[i + 2] = front[i + 2];
                triangles[front.Length + i] = front[i];
                triangles[front.Length + i + 1] = front[i + 2];
                triangles[front.Length + i + 2] = front[i + 1];
            }

            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Five-point paper star in the XY plane, radius 1.</summary>
        private static Mesh Star()
        {
            var vertices = new Vector3[11];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? 1f : 0.45f;
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            var front = new int[30];
            for (int i = 0; i < 10; i++)
            {
                front[i * 3] = 0;
                front[i * 3 + 1] = (i + 1) % 10 + 1;
                front[i * 3 + 2] = i + 1;
            }

            return DoubleSided("PaperStar", vertices, front);
        }

        /// <summary>Flat disc in the XY plane, radius 0.5 (a felt-cut dust puff).</summary>
        private static Mesh Disc(string name, int sides)
        {
            var vertices = new Vector3[sides + 1];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
            }

            var front = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                front[i * 3] = 0;
                front[i * 3 + 1] = (i + 1) % sides + 1;
                front[i * 3 + 2] = i + 1;
            }

            return DoubleSided(name, vertices, front);
        }
    }
}
