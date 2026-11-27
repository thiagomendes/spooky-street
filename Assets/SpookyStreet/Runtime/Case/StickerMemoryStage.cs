using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Draws the notebook's sticker memories (D32) from the real street and house: a small camera of its own renders, on
    /// request, into a 3:1 texture the memory card shows (like the cutaway's owl inset, never a second full-screen
    /// render). For each picture it frames the world as the child saw it then, and gives it straight back afterwards:
    /// <list type="bullet">
    /// <item>Pip, June and Aunt Kit are left out (Pip is wherever she stands now; the reveal leaves June and Kit out of
    /// its sticker shots too). Bea and Mr. Vale stay: their memories are seen from beside them.</item>
    /// <item>An interior memory keeps the owl house interior drawn while it shows, and the sky dome, which cuts through
    /// the interior, is left out of its picture.</item>
    /// <item>The door memories show the front door shut, the workshop ones look through the near wall (as through the
    /// grille), only for the instant of the picture.</item>
    /// </list>
    /// Nothing here touches the case, the save, time or input.
    /// </summary>
    public sealed class StickerMemoryStage : MonoBehaviour, IStickerMemoryStage
    {
        public const int Width = 960;
        public const int Height = 320;

        private Camera _camera;
        private RenderTexture _texture;
        private CaseDirector _director;
        private GameBootstrap _game;
        private StickerMemory _memory;
        private bool _forcedInterior;
        private bool _interiorWasForced;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Renderer> _scratch = new List<Renderer>();

        public Texture Texture => _texture;

        public Camera Camera => _camera;

        /// <summary>The memory being drawn, or null.</summary>
        public StickerMemory Current => _memory;

        /// <summary>Pictures drawn since the scene loaded (tests).</summary>
        public int RenderCount { get; private set; }

        public static StickerMemoryStage Create(CaseDirector director, GameBootstrap game)
        {
            var go = new GameObject("StickerMemoryStage");
            go.transform.SetParent(director.transform, false);
            var stage = go.AddComponent<StickerMemoryStage>();
            stage._director = director;
            stage._game = game;
            stage.Build(game.CameraRig != null ? game.CameraRig.OutputCamera : null);
            return stage;
        }

        private void Build(Camera output)
        {
            // No render target without a graphics device (batchmode -nographics PlayMode runs): the card draws the sticker.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                _texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "StickerMemory" };
            }

            var cameraObject = new GameObject("StickerMemoryCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false; // rendered on request only
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 120f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.NightViolet;
            if (output != null)
            {
                _camera.cullingMask = output.cullingMask & ~(1 << LayerMask.NameToLayer("UI"));
                _camera.clearFlags = output.clearFlags;
                _camera.backgroundColor = output.backgroundColor;
                _camera.allowHDR = output.allowHDR;
                var from = output.GetUniversalAdditionalCameraData();
                var to = _camera.GetUniversalAdditionalCameraData();
                to.renderPostProcessing = from.renderPostProcessing;
                to.antialiasing = from.antialiasing;
            }
        }

        public void Begin(StickerMemory memory)
        {
            End();
            _memory = memory;
            var house = _director != null ? _director.House : null;
            if (memory != null && memory.Indoors && house != null)
            {
                _interiorWasForced = house.ForceInteriorVisible;
                house.ForceInteriorVisible = true; // the house shows its rooms from the next frame
                _forcedInterior = true;
            }

            if (_texture != null)
            {
                // Never a frame of the last memory while the world gets ready.
                var active = RenderTexture.active;
                RenderTexture.active = _texture;
                GL.Clear(true, true, Palette.NightViolet);
                RenderTexture.active = active;
            }
        }

        public bool Render(CamPose pose)
        {
            if (_texture == null || _memory == null)
            {
                return false;
            }

            var house = _director != null ? _director.House : null;
            if (_memory.Indoors && house != null && !house.InteriorShown)
            {
                return false; // the rooms appear on the house's next tick
            }

            var eye = new Vector3(pose.Eye.X, pose.Eye.Y, pose.Eye.Z);
            var aim = new Vector3(pose.Aim.X, pose.Aim.Y, pose.Aim.Z);
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye, Vector3.up));
            _camera.fieldOfView = pose.Fov;

            // Frame the world as it was, for this one picture only.
            _hidden.Clear();
            Hide(_game != null && _game.Pip != null ? _game.Pip.transform : null);
            var cast = _director != null ? _director.Cast : null;
            if (cast != null)
            {
                Hide(cast.June != null ? cast.June.transform : null);
                Hide(cast.Kit != null ? cast.Kit.transform : null);
            }

            if ((_memory.Look & MemoryLook.NookOpen) != 0 && house != null)
            {
                Hide(house.NookWall);
            }

            var sky = _game != null ? _game.Sky : null;
            bool skyWasHidden = sky != null && sky.Hidden;
            if (sky != null && _memory.Indoors)
            {
                sky.Hidden = true;
            }

            bool shutDoor = (_memory.Look & MemoryLook.DoorShut) != 0 && house != null && house.DoorOpen &&
                            house.DoorAngle >= OwlHouseStage.DoorOpenDegrees - 0.5f;
            if (shutDoor)
            {
                house.SetDoor(false);
            }

            try
            {
                var request = new RenderPipeline.StandardRequest { destination = _texture };
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                }
                else
                {
                    _camera.Render();
                }

                RenderCount++;
            }
            finally
            {
                if (shutDoor)
                {
                    house.SetDoor(true);
                }

                if (sky != null)
                {
                    sky.Hidden = skyWasHidden;
                }

                foreach (var r in _hidden)
                {
                    if (r != null)
                    {
                        r.forceRenderingOff = false;
                    }
                }

                _hidden.Clear();
            }

            return true;
        }

        public void End()
        {
            if (_forcedInterior && _director != null && _director.House != null)
            {
                _director.House.ForceInteriorVisible = _interiorWasForced;
            }

            _forcedInterior = false;
            _memory = null;
        }

        private void Hide(Transform root)
        {
            if (root == null)
            {
                return;
            }

            root.GetComponentsInChildren(false, _scratch);
            foreach (var r in _scratch)
            {
                if (r != null && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    _hidden.Add(r);
                }
            }

            _scratch.Clear();
        }

        private void OnDestroy()
        {
            End();
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
