using SpookyStreet.Characters;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// June and Aunt Kit on the stage (S002 interior wiring), placed from <see cref="CaseWorldView"/>: June taps the
    /// knock rhythm on her knee on her stool by the attic window until Pip talks to her; then she walks her authored
    /// path down the stair to her foyer mark (presentation only: the mark persists, the walk does not); at the
    /// reopening she stands on the porch beside Aunt Kit, who appears at her booth and plays OpenBooth. Both use the
    /// witness behaviour (head turn toward Pip, the engaged loop while talking). A missing model is skipped.
    /// </summary>
    public sealed class CaseCast : MonoBehaviour
    {
        /// <summary>June stays seated while she talks in the attic (KneeTap is her listening loop too).</summary>
        public static readonly WitnessClipSet JuneSeated =
            new WitnessClipSet(CharacterClips.KneeTap, CharacterClips.KneeTap, null);

        private float _walked = -1f;
        private float _walkTime;
        private float _walkLength;
        private JunePlacement _placement = JunePlacement.Attic;
        private Transform _pip;

        public Witness June { get; private set; }
        public Witness Kit { get; private set; }

        /// <summary>True while June walks down to the foyer.</summary>
        public bool JuneWalking => _walked >= 0f;

        public JunePlacement JuneMark => _placement;

        public static CaseCast Create(Transform parent, MaterialLibrary materials, Transform pip)
        {
            var go = new GameObject("CaseCast");
            go.transform.SetParent(parent, false);
            var cast = go.AddComponent<CaseCast>();
            cast._pip = pip;
            cast.June = Spawn(go.transform, materials, "June", CaseStage.JuneAttic, JuneSeated, pip);
            cast.Kit = Spawn(go.transform, materials, "Kit", CaseStage.KitBooth, CharacterClips.Standing, pip);
            cast._walkLength = CaseStage.PathLength(CaseStage.JuneWalk);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CaseCast] June {(cast.June != null ? string.Join(",", cast.June.Avatar.ClipNames) : "absent")}, " +
                          $"Kit {(cast.Kit != null ? string.Join(",", cast.Kit.Avatar.ClipNames) : "absent")}");
            }

            return cast;
        }

        private static Witness Spawn(Transform parent, MaterialLibrary materials, string model, CastSpot spot,
            WitnessClipSet clips, Transform pip)
        {
            if (!NpcAvatar.Exists(model))
            {
                return null;
            }

            var p = spot.Position;
            var avatar = NpcAvatar.Spawn(materials, model, parent, new Vector3(p.X, p.Y, p.Z), spot.FacingYaw, clips.Rest);
            if (avatar == null)
            {
                return null;
            }

            var capsule = avatar.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = EnvironmentLayout.WitnessRadius;
            capsule.height = EnvironmentLayout.WitnessHeight;
            capsule.center = new Vector3(0f, EnvironmentLayout.WitnessHeight * 0.5f, 0f);
            var witness = avatar.gameObject.AddComponent<Witness>();
            witness.Configure(clips, pip);
            return witness;
        }

        /// <summary>Instantly places everyone for <paramref name="view"/> (resume, new game, replay).</summary>
        public void Apply(CaseWorldView view)
        {
            _walked = -1f;
            PlaceJune(view.June);
            if (Kit != null)
            {
                Kit.gameObject.SetActive(view.KitPresent);
            }
        }

        private void PlaceJune(JunePlacement placement)
        {
            _placement = placement;
            if (June == null)
            {
                return;
            }

            June.enabled = true;
            var spot = placement == JunePlacement.Attic ? CaseStage.JuneAttic
                : placement == JunePlacement.Foyer ? CaseStage.JuneFoyer
                : CaseStage.JunePorch;
            var p = spot.Position;
            June.transform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), Quaternion.Euler(0f, spot.FacingYaw, 0f));
            June.Configure(placement == JunePlacement.Attic ? JuneSeated : CharacterClips.Standing, _pip);
        }

        /// <summary>June found: she walks down to her foyer mark (call after her lines finish).</summary>
        public void WalkJuneToFoyer()
        {
            if (June == null)
            {
                _placement = JunePlacement.Foyer;
                return;
            }

            June.Conversing = false;
            June.enabled = false;
            June.Avatar.PlayLoop(CharacterClips.Idle);
            _walked = 0f;
            _walkTime = 0f;
        }

        /// <summary>The reopening: June on the porch, Kit at her booth opening it.</summary>
        public void StageReopening()
        {
            _walked = -1f;
            PlaceJune(JunePlacement.Porch);
            if (June != null)
            {
                June.Avatar.PlayOneShot(CharacterClips.Wave);
            }

            if (Kit != null)
            {
                Kit.gameObject.SetActive(true);
                Kit.Configure(CharacterClips.Standing, _pip);
                Kit.Avatar.PlayOneShot(CharacterClips.OpenBooth);
            }
        }

        public Witness For(Speaker speaker) =>
            speaker == Speaker.June ? June : speaker == Speaker.Kit ? Kit : null;

        private void Update()
        {
            if (_walked < 0f || June == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // No walk clip yet: she turns toward the path, eases in and out, and turns into corners ahead of them.
            _walkTime += dt;
            _walked = CaseStage.JuneWalkDistance(_walkTime);
            var p = CaseStage.Along(CaseStage.JuneWalk, _walked, out _, out _);
            CaseStage.JuneFacing(_walked, out float hx, out float hz);
            June.transform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z),
                Quaternion.Slerp(June.transform.rotation, Quaternion.LookRotation(new Vector3(hx, 0f, hz)), 10f * dt));
            if (_walked >= _walkLength - 1e-3f)
            {
                PlaceJune(JunePlacement.Foyer);
                _walked = -1f;
            }
        }
    }
}
