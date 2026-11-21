using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Renders the output camera (with the UI temporarily moved to Screen Space - Camera so it appears in
    /// the frame) into a texture, writes PNG + JPG to ci-out/captures and measures error-shader magenta.
    /// </summary>
    public static class FrameCapture
    {
        public const string OutputDirectory = "ci-out/captures";

        public static Texture2D LastFrame { get; private set; }

        public static IEnumerator Capture(Camera camera, Canvas canvas, string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Capture_" + name };
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            // Let UI layout settle in its new render mode. WaitForEndOfFrame never resumes in batchmode,
            // so frames are advanced with plain yields and rendered explicitly below.
            yield return null;
            Canvas.ForceUpdateCanvases();

            // Warm-up render: the first one after a change can be blank while shaders compile.
            for (int i = 0; i < 3; i++)
            {
                RenderInto(camera, target);
                yield return null;
            }

            RenderInto(camera, target);

            var previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false) { name = name };
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frame.Apply();
            RenderTexture.active = previousActive;

            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlane;
            target.Release();
            Object.Destroy(target);

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), frame.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".jpg"), frame.EncodeToJPG(80));
            if (LastFrame != null)
            {
                Object.Destroy(LastFrame);
            }

            LastFrame = frame;
            Debug.Log($"[FrameCapture] {name}: {width}x{height}, magenta {MagentaFraction(frame):P2}, " +
                      $"mean luminance {MeanLuminance(frame):0.000}");
        }

        /// <summary>Renders one frame of the camera into the target through the active render pipeline.</summary>
        private static void RenderInto(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }

            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
        }

        /// <summary>Fraction of pixels that look like Unity's error shader (saturated magenta).</summary>
        public static float MagentaFraction(Texture2D frame)
        {
            Color32[] pixels = frame.GetPixels32();
            int hits = 0;
            foreach (var p in pixels)
            {
                if (p.r > 220 && p.g < 60 && p.b > 220)
                {
                    hits++;
                }
            }

            return pixels.Length == 0 ? 0f : (float)hits / pixels.Length;
        }

        public static float MeanLuminance(Texture2D frame)
        {
            Color32[] pixels = frame.GetPixels32();
            double sum = 0;
            foreach (var p in pixels)
            {
                sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;
            }

            return pixels.Length == 0 ? 0f : (float)(sum / pixels.Length);
        }
    }
}
