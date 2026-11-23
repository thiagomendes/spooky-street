using System;
using System.Collections.Generic;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// A tiny engine-free world for the S003 wall and ledge tests: axis-aligned boxes on flat ground at y = 0, Pip as a
    /// vertical capsule (radius 0.28, height 1.1) moved per axis and pushed out of the boxes, and the same probes the
    /// runtime measures (<c>ClimbProbes</c>): the nearest wall at chest height and the nearest ledge in catch reach,
    /// with the room checks. With <see cref="ReportUnmarked"/> the probes also report unmarked boxes (Marks None), so a
    /// test can prove the rules ignore them.
    /// </summary>
    public sealed class ClimbTestWorld
    {
        public const float Radius = 0.28f;
        public const float Height = 1.1f;
        public const float ChestHeight = 0.55f;

        public readonly struct Box
        {
            public Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, SurfaceMarks marks)
            {
                MinX = minX;
                MinY = minY;
                MinZ = minZ;
                MaxX = maxX;
                MaxY = maxY;
                MaxZ = maxZ;
                Marks = marks;
            }

            public float MinX { get; }
            public float MinY { get; }
            public float MinZ { get; }
            public float MaxX { get; }
            public float MaxY { get; }
            public float MaxZ { get; }
            public SurfaceMarks Marks { get; }

            /// <summary>Horizontal distance from (x, z) to the footprint (0 inside) and the nearest footprint point.</summary>
            public float FootprintDistance(float x, float z, out float px, out float pz)
            {
                px = Math.Max(MinX, Math.Min(MaxX, x));
                pz = Math.Max(MinZ, Math.Min(MaxZ, z));
                float dx = x - px;
                float dz = z - pz;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }

        public readonly List<Box> Boxes = new List<Box>();
        public readonly MovementTuningData Tuning = new MovementTuningData();
        public MotorState State = MotorState.Initial;
        public float X;
        public float Y;
        public float Z;
        public float FacingX;
        public float FacingZ = 1f;
        public bool Grounded = true;
        public bool ReportUnmarked;
        public float Time;

        public ClimbTestWorld Add(float minX, float minY, float minZ, float maxX, float maxY, float maxZ,
            SurfaceMarks marks)
        {
            Boxes.Add(new Box(minX, minY, minZ, maxX, maxY, maxZ, marks));
            return this;
        }

        public void Place(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            Grounded = y <= 1e-4f;
            State = MotorState.Initial;
            State.Grounded = Grounded;
            if (!Grounded)
            {
                State.TimeSinceGrounded = 1f;
            }
        }

        /// <summary>One frame: probes, the motor, then the move against the boxes.</summary>
        public MotorStep Frame(MotorInput input, float dt)
        {
            input.FacingX = FacingX;
            input.FacingZ = FacingZ;
            input.FeetY = Y;
            float ground = GroundBelow(X, Z, Y);
            input.GroundDistance = Grounded ? 0f : Math.Max(1e-3f, Y - ground);
            if (!State.Grounded && !State.Hanging && !State.Climbing)
            {
                input.Wall = WallProbe(Y - ground);
                input.Ledge = LedgeProbe();
            }

            MotorStep step = MovementModel.Step(ref State, input, Grounded, dt, Tuning);
            Move(step.DeltaX, step.DeltaY, step.DeltaZ);
            Time += dt;
            if (State.FacingWall)
            {
                FacingX = -State.FacedNormalX;
                FacingZ = -State.FacedNormalZ;
            }
            else if (State.VelocityX * State.VelocityX + State.VelocityZ * State.VelocityZ > 0.04f)
            {
                float speed = State.HorizontalSpeed;
                FacingX = State.VelocityX / speed;
                FacingZ = State.VelocityZ / speed;
            }

            return step;
        }

        /// <summary>Highest top under the point at or below <paramref name="maxY"/> (the ground at 0 otherwise).</summary>
        public float GroundBelow(float x, float z, float maxY)
        {
            float best = 0f;
            foreach (var box in Boxes)
            {
                if (box.FootprintDistance(x, z, out _, out _) < Radius * 0.5f && box.MaxY <= maxY + 1e-3f &&
                    box.MaxY > best)
                {
                    best = box.MaxY;
                }
            }

            return best;
        }

        public WallProbe WallProbe(float feetClearance)
        {
            var probe = new WallProbe { FeetClearance = feetClearance };
            float chest = Y + ChestHeight;
            float reach = Radius + Tuning.WallContactSlack + 0.05f;
            float best = float.MaxValue;
            foreach (var box in Boxes)
            {
                bool marked = (box.Marks & SurfaceMarks.WallJump) != 0;
                if ((!marked && !ReportUnmarked) || chest < box.MinY || chest > box.MaxY)
                {
                    continue;
                }

                float distance = box.FootprintDistance(X, Z, out float px, out float pz);
                if (distance < 1e-4f || distance > reach || distance >= best)
                {
                    continue;
                }

                best = distance;
                probe.Hit = true;
                probe.Marks = box.Marks & SurfaceMarks.WallJump;
                probe.NormalX = (X - px) / distance;
                probe.NormalZ = (Z - pz) / distance;
                probe.Distance = distance;
            }

            return probe;
        }

        public LedgeProbe LedgeProbe()
        {
            var probe = new LedgeProbe();
            float lowestTop = Tuning.LedgeHangHandHeight - Tuning.LedgeGrabReachBelow;
            float sampleY = Y + lowestTop - 0.05f;
            float reach = Tuning.BodyRadius + Tuning.LedgeGrabReachForward;
            float best = float.MaxValue;
            foreach (var box in Boxes)
            {
                bool marked = (box.Marks & SurfaceMarks.Ledge) != 0;
                if (!marked && !ReportUnmarked)
                {
                    continue;
                }

                float distance = box.FootprintDistance(X, Z, out float px, out float pz);
                if (distance < 1e-4f || distance > reach || distance >= best || box.MaxY < sampleY - 1f)
                {
                    continue;
                }

                float nx = (X - px) / distance;
                float nz = (Z - pz) / distance;
                float top = box.MaxY;
                float hangX = px + nx * Tuning.LedgeHangWallOffset;
                float hangZ = pz + nz * Tuning.LedgeHangWallOffset;
                float hangY = top - Tuning.LedgeHangHandHeight;
                float climbX = px - nx * Tuning.LedgeClimbInset;
                float climbZ = pz - nz * Tuning.LedgeClimbInset;
                best = distance;
                probe.Hit = true;
                probe.Marks = box.Marks & SurfaceMarks.Ledge;
                probe.NormalX = nx;
                probe.NormalZ = nz;
                probe.EdgeDistance = distance;
                probe.TopAbove = top - Y;
                probe.RoomToHang = Free(hangX, hangY, hangZ);
                probe.RoomToClimb = Free(climbX, top + Tuning.LedgeClimbLift, climbZ);
                probe.HangClearance = hangY - GroundBelow(hangX, hangZ, hangY);
            }

            return probe;
        }

        /// <summary>Pip's capsule (a little slimmer) at these feet overlaps no box.</summary>
        public bool Free(float x, float feet, float z)
        {
            float r = Radius - 0.03f;
            foreach (var box in Boxes)
            {
                if (feet + 0.04f < box.MaxY && feet + Height > box.MinY && box.FootprintDistance(x, z, out _, out _) < r)
                {
                    return false;
                }
            }

            return true;
        }

        private void Move(float dx, float dy, float dz)
        {
            float startY = Y;
            X += dx;
            PushOutX(dx);
            Z += dz;
            PushOutZ(dz);
            Y += dy;
            Grounded = false;
            foreach (var box in Boxes)
            {
                if (box.FootprintDistance(X, Z, out _, out _) >= Radius)
                {
                    continue;
                }

                if (dy <= 0f && startY >= box.MaxY - 1e-4f && Y < box.MaxY)
                {
                    Y = box.MaxY;
                    Grounded = true;
                }
                else if (dy > 0f && startY + Height <= box.MinY + 1e-4f && Y + Height > box.MinY)
                {
                    Y = box.MinY - Height;
                    if (State.VelocityY > 0f)
                    {
                        MovementModel.OnCeilingHit(ref State);
                    }
                }
            }

            if (Y <= 0f)
            {
                Y = 0f;
                Grounded = true;
            }
        }

        private bool Overlaps(Box box) =>
            Y + 0.02f < box.MaxY && Y + Height > box.MinY && box.FootprintDistance(X, Z, out _, out _) < Radius - 1e-4f;

        private void PushOutX(float dx)
        {
            foreach (var box in Boxes)
            {
                if (Overlaps(box) && Z > box.MinZ - Radius && Z < box.MaxZ + Radius)
                {
                    X = dx > 0f ? box.MinX - Radius : box.MaxX + Radius;
                }
            }
        }

        private void PushOutZ(float dz)
        {
            foreach (var box in Boxes)
            {
                if (Overlaps(box) && X > box.MinX - Radius && X < box.MaxX + Radius)
                {
                    Z = dz > 0f ? box.MinZ - Radius : box.MaxZ + Radius;
                }
            }
        }
    }
}
