using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Crowd;
using SpookyStreet.EditorTools;
using SpookyStreet.Player;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The trick-or-treat clips as Unity imports them (docs/living-street-crowd-art.md "Trick-or-treat clips"): every
    /// clip of <see cref="CrowdClipTimings"/> is in its FBX (the crowd bodies, the door neighbours and Pip), plays once,
    /// has the contract's length, starts and ends on the rest pose it promises, puts the sweet where the contract says,
    /// and the walk transitions keep the planted foot still when the root moves by <see cref="CrowdClipTimings.RootTravel"/>.
    /// The shipped clips are still there. Missing files skip.
    /// </summary>
    public class TreatClipImportTests
    {
        private const string CrowdFolder = "Assets/SpookyStreet/Content/Resources/" + CrowdKit.Folder + "/";
        private const float PoseMetres = 0.004f;
        private const float PoseDegrees = 1.0f;

        private static IEnumerable<string> Models()
        {
            yield return CrowdKit.ChildModel;
            yield return CrowdKit.AdultModel;
            yield return CrowdKit.NeighbourModel;
        }

        private static string FbxPath(string model) =>
            model == "Pip" ? ModelImportRules.ModelsRoot + "Characters/Pip/Pip.fbx" : CrowdFolder + model + ".fbx";

        private static string ResourcePath(string model) => model == "Pip" ? PipAvatar.ModelPath : CrowdKit.Folder + "/" + model;

        private static IReadOnlyList<CrowdClipTiming> TimingsOf(string model) =>
            model == "Pip" ? CrowdClipTimings.Pip : CrowdClipTimings.For(model);

        private static AnimationClip Clip(string model, string name)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(ResourcePath(model)))
            {
                if (PipClips.CleanClipName(c.name) == name)
                {
                    return c;
                }
            }

            return null;
        }

        private sealed class Figure : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();

            public Figure(string model)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath(model));
                if (prefab == null)
                {
                    Assert.Ignore(model + ".fbx is not committed.");
                }

                Root = Object.Instantiate(prefab);
                Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var t in Root.GetComponentsInChildren<Transform>(true))
                {
                    Bones[t.name] = t;
                }
            }

            public void Sample(AnimationClip clip, float u) => clip.SampleAnimation(Root, Mathf.Clamp01(u) * clip.length);

            public Dictionary<string, (Vector3 pos, Quaternion rot)> Snapshot()
            {
                var s = new Dictionary<string, (Vector3, Quaternion)>();
                foreach (var kv in Bones)
                {
                    s[kv.Key] = (kv.Value.localPosition, kv.Value.localRotation);
                }

                return s;
            }

            /// <summary>The character's forward on the ground: from the ankle to the ball of the foot at rest.</summary>
            public Vector3 Forward()
            {
                Vector3 f = Bones["Toe_L"].position - Bones["Foot_L"].position;
                f.y = 0f;
                return f.normalized;
            }

            public void Dispose() => Object.DestroyImmediate(Root);
        }

        private static void AssertSamePose(Dictionary<string, (Vector3 pos, Quaternion rot)> want,
            Dictionary<string, (Vector3 pos, Quaternion rot)> got, string what)
        {
            foreach (var kv in want)
            {
                var g = got[kv.Key];
                Assert.Less(Vector3.Distance(kv.Value.pos, g.pos), PoseMetres, $"{what}: {kv.Key} position");
                Assert.Less(Quaternion.Angle(kv.Value.rot, g.rot), PoseDegrees, $"{what}: {kv.Key} rotation");
            }
        }

        [Test]
        public void EveryModelHasItsTrickOrTreatClipsAndKeepsTheShippedOnes([ValueSource(nameof(AllModels))] string model)
        {
            var importer = AssetImporter.GetAtPath(FbxPath(model)) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            var names = new Dictionary<string, ModelImporterClipAnimation>();
            foreach (var c in importer.clipAnimations)
            {
                names[c.name] = c;
            }

            foreach (var t in TimingsOf(model))
            {
                Assert.IsTrue(names.TryGetValue(t.Name, out var c), $"{model}.fbx has no {t.Name} take");
                Assert.IsFalse(c.loopTime, t.Name + " plays once");
                Assert.AreEqual(t.Frames, c.lastFrame - c.firstFrame, 0.01f, t.Name + " frames");
                var clip = Clip(model, t.Name);
                Assert.IsNotNull(clip, t.Name + " not loadable from Resources");
                Assert.AreEqual(t.Seconds, clip.length, 0.5f / CrowdClipTimings.Fps, t.Name + " seconds");
            }

            var shipped = model == "Pip" ? PipClips.All : CrowdKit.Clips;
            foreach (var name in shipped)
            {
                Assert.IsTrue(names.ContainsKey(name), $"{model}.fbx lost {name}");
            }
        }

        private static IEnumerable<string> AllModels()
        {
            foreach (var m in Models())
            {
                yield return m;
            }

            yield return "Pip";
        }

        [Test]
        public void TheClipsStartAndEndOnTheRestPose([ValueSource(nameof(AllModels))] string model)
        {
            using (var fig = new Figure(model))
            {
                var idle = Clip(model, "Idle");
                var walk = Clip(model, "Walk");
                fig.Sample(idle, 0f);
                var rest = fig.Snapshot();
                foreach (var t in TimingsOf(model))
                {
                    var clip = Clip(model, t.Name);
                    Assert.IsNotNull(clip, t.Name);
                    var start = rest;
                    var end = rest;
                    if (t.Name == CrowdClipTimings.WalkStart)
                    {
                        fig.Sample(walk, CrowdClipTimings.WalkStartEndPhase(model));
                        end = fig.Snapshot();
                    }
                    else if (t.Name == CrowdClipTimings.WalkStop)
                    {
                        fig.Sample(walk, 0f);
                        start = fig.Snapshot();
                    }

                    fig.Sample(clip, 0f);
                    AssertSamePose(start, fig.Snapshot(), $"{model} {t.Name} first frame");
                    fig.Sample(clip, 1f);
                    AssertSamePose(end, fig.Snapshot(), $"{model} {t.Name} last frame");
                }
            }
        }

        [TestCase(CrowdKit.ChildModel, "WalkStart", "Toe_R")]
        [TestCase(CrowdKit.ChildModel, "WalkStop", "Toe_L")]
        [TestCase(CrowdKit.AdultModel, "WalkStart", "Toe_R")]
        [TestCase(CrowdKit.AdultModel, "WalkStop", "Toe_L")]
        [TestCase(CrowdKit.NeighbourModel, "WalkStart", "Toe_R")]
        [TestCase(CrowdKit.NeighbourModel, "WalkStop", "Toe_L")]
        public void WalkTransitionsKeepThePlantedFootStill(string model, string clipName, string ball)
        {
            using (var fig = new Figure(model))
            {
                fig.Sample(Clip(model, "Idle"), 0f);
                Vector3 forward = fig.Forward();
                var clip = Clip(model, clipName);
                int frames = clipName == CrowdClipTimings.WalkStart ? CrowdClipTimings.WalkStartFrames : CrowdClipTimings.WalkStopFrames;
                var heights = new List<float>();
                var places = new List<Vector3>();
                for (int f = 0; f <= frames; f++)
                {
                    float u = f / (float)frames;
                    fig.Sample(clip, u);
                    // where the ball of the planted foot is on the ground, with the root moved as the contract says
                    Vector3 p = fig.Bones[ball].position + forward * CrowdClipTimings.RootTravel(clipName, model, u);
                    heights.Add(p.y);
                    places.Add(new Vector3(p.x, 0f, p.z));
                }

                float low = Mathf.Min(heights.ToArray());
                var down = new List<Vector3>();
                for (int i = 0; i < heights.Count; i++)
                {
                    if (heights[i] <= low + 0.01f)
                    {
                        down.Add(places[i]);
                    }
                }

                Assert.GreaterOrEqual(down.Count, 6, $"{model} {clipName}: the {ball} ball is on the ground most of the clip");
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                float slide = 0f, drift = 0f;
                foreach (var a in down)
                {
                    foreach (var b in down)
                    {
                        slide = Mathf.Max(slide, Mathf.Abs(Vector3.Dot(a - b, forward)));
                        drift = Mathf.Max(drift, Mathf.Abs(Vector3.Dot(a - b, side)));
                    }
                }

                // no skating: the planted ball stays put along the way the walker goes
                Assert.Less(slide, 0.01f, $"{model} {clipName}: the planted foot slides {slide * 100f:F1} cm");
                // Walk sets the feet narrower than Idle (3.6 cm on the grown-up), so the ball pivots in a little as the
                // weight comes over it; nothing more than that (plus the import's curve rounding)
                Assert.Less(drift, 0.045f, $"{model} {clipName}: the planted foot drifts {drift * 100f:F1} cm sideways");
            }
        }

        [TestCase(CrowdKit.AdultModel, "GiveTreat", "Fingers_L", CrowdClipTimings.GiveTreatRelease,
            CrowdClipTimings.GiveTreatReleaseForward, CrowdClipTimings.GiveTreatReleaseHeight)]
        [TestCase(CrowdKit.NeighbourModel, "GiveTreat", "Fingers_L", CrowdClipTimings.GiveTreatRelease,
            CrowdClipTimings.GiveTreatReleaseForward, CrowdClipTimings.GiveTreatReleaseHeight)]
        [TestCase(CrowdKit.AdultModel, "OpenDoorPeek", "Fingers_L", CrowdClipTimings.OpenDoorPeekHandOnDoor,
            CrowdClipTimings.OpenDoorPeekDoorForward, CrowdClipTimings.OpenDoorPeekDoorHeight)]
        [TestCase("Pip", "HandTreat", "Fingers_L", CrowdClipTimings.HandTreatRelease,
            CrowdClipTimings.HandTreatReleaseForward, CrowdClipTimings.HandTreatReleaseHeight)]
        public void TheHandIsWhereTheContractSaysAtTheMarker(string model, string clipName, string bone, float u,
            float forward, float height)
        {
            using (var fig = new Figure(model))
            {
                fig.Sample(Clip(model, "Idle"), 0f);
                Vector3 ahead = fig.Forward();
                fig.Sample(Clip(model, clipName), u);
                Vector3 p = fig.Bones[bone].position;
                Assert.AreEqual(forward, Vector3.Dot(p, ahead), 0.02f, $"{model} {clipName}: {bone} ahead of the root");
                Assert.AreEqual(height, p.y, 0.02f, $"{model} {clipName}: {bone} height");
            }
        }

        [Test]
        public void TheChildHoldsThePailMouthWhereTheGiverReleases()
        {
            using (var fig = new Figure(CrowdKit.ChildModel))
            {
                fig.Sample(Clip(CrowdKit.ChildModel, "Idle"), 0f);
                Vector3 ahead = fig.Forward();
                fig.Sample(Clip(CrowdKit.ChildModel, CrowdClipTimings.HoldPail), 0.5f);
                // the Pail bone is the grip on the bail; the pail's mouth hangs 0.085 m under it (crowd_build.pail)
                Vector3 grip = fig.Bones["Pail"].position;
                Assert.AreEqual(CrowdClipTimings.HoldPailMouthForward, Vector3.Dot(grip, ahead), 0.02f, "pail ahead");
                Assert.AreEqual(CrowdClipTimings.HoldPailMouthHeight, grip.y - 0.085f, 0.02f, "pail mouth height");
            }
        }
    }
}
