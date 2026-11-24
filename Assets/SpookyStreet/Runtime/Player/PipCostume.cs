using System.Collections.Generic;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Pip's keepsake costume pieces (S004 §2.1, stream A2), worn in play and on the title, all three at once if she
    /// likes (three slots). Cosmetic only. The star-lined cape swaps the base map of the material her atlas meshes share
    /// for <see cref="KeepsakeArt.StarCapeTexture"/> (on a copy, so nothing else that shares the material changes). The
    /// showman's hat rides her <see cref="KeepsakeArt.HatBone"/> and the lantern pail her
    /// <see cref="KeepsakeArt.PailBone"/> in place of the <see cref="KeepsakeArt.PipPailMesh"/> renderer, each placed from
    /// the skinned meshes' bind pose (the prefab holds an exported pose, CI run 161; formula on
    /// <see cref="KeepsakeArt.ShowmanHat"/>). Without Pip's model (the primitive avatar) nothing is worn.
    /// </summary>
    public sealed class PipCostume : MonoBehaviour
    {
        private readonly Dictionary<Renderer, Material[]> _plain = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Material, Material> _starred = new Dictionary<Material, Material>();
        private MaterialLibrary _materials;
        private Transform _model;
        private GameObject _hat;
        private GameObject _pail;
        private Renderer _plainPail;
        private Texture2D _starTexture;

        /// <summary>The pieces worn now (stable IDs).</summary>
        public IReadOnlyCollection<string> Worn => _worn;

        private readonly HashSet<string> _worn = new HashSet<string>();

        public GameObject Hat => _hat;

        public GameObject Pail => _pail;

        public Renderer PlainPail => _plainPail;

        /// <summary>True while the cape shows its star lining.</summary>
        public bool CapeStarred { get; private set; }

        /// <summary>True when Pip's model is there to dress.</summary>
        public bool CanDress => _model != null;

        public static PipCostume Attach(PipAvatar avatar, MaterialLibrary materials)
        {
            var costume = avatar.gameObject.GetComponent<PipCostume>();
            if (costume == null)
            {
                costume = avatar.gameObject.AddComponent<PipCostume>();
            }

            costume._materials = materials;
            costume._model = avatar.UsesModel && avatar.Body != null ? avatar.Body.Find("PipModel") : null;
            if (costume._model != null)
            {
                var pail = ModelKit.FindDeep(costume._model, KeepsakeArt.PipPailMesh);
                costume._plainPail = pail != null ? pail.GetComponent<Renderer>() : null;
            }

            return costume;
        }

        /// <summary>Wears exactly <paramref name="pieceIds"/> (unknown IDs ignored).</summary>
        public void Apply(IEnumerable<string> pieceIds)
        {
            _worn.Clear();
            if (pieceIds != null)
            {
                foreach (string id in pieceIds)
                {
                    if (CostumePieces.TryGet(id, out _))
                    {
                        _worn.Add(id);
                    }
                }
            }

            if (_model == null)
            {
                return;
            }

            SetStarCape(_worn.Contains(CostumePieces.StarCape.Id));
            bool lantern = _worn.Contains(CostumePieces.LanternPail.Id);
            if (lantern && _pail == null)
            {
                _pail = AttachPiece(KeepsakeArt.LanternPail, KeepsakeArt.PailBone);
            }

            if (_pail != null)
            {
                _pail.SetActive(lantern);
            }

            if (_plainPail != null)
            {
                _plainPail.enabled = !(lantern && _pail != null);
            }

            bool hat = _worn.Contains(CostumePieces.ShowmanHat.Id);
            if (hat && _hat == null)
            {
                _hat = AttachPiece(KeepsakeArt.ShowmanHat, KeepsakeArt.HatBone);
            }

            if (_hat != null)
            {
                _hat.SetActive(hat);
            }
        }

        private void SetStarCape(bool on)
        {
            if (on == CapeStarred)
            {
                return;
            }

            if (on)
            {
                if (_starTexture == null)
                {
                    _starTexture = Resources.Load<Texture2D>(KeepsakeArt.StarCapeTexture);
                }

                if (_starTexture == null)
                {
                    Debug.LogWarning("[PipCostume] Missing " + KeepsakeArt.StarCapeTexture);
                    return;
                }

                foreach (var renderer in _model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var starred = StarredOf(materials[i]);
                        if (starred != null)
                        {
                            if (!changed)
                            {
                                _plain[renderer] = (Material[])materials.Clone();
                            }

                            materials[i] = starred;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }
            }
            else
            {
                foreach (var pair in _plain)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.sharedMaterials = pair.Value;
                    }
                }

                _plain.Clear();
            }

            CapeStarred = on;
        }

        /// <summary>The star-lined copy of a material that draws Pip's atlas (null for any other material).</summary>
        private Material StarredOf(Material material)
        {
            if (material == null || !material.HasProperty("_BaseMap"))
            {
                return null;
            }

            var baseMap = material.GetTexture("_BaseMap");
            if (baseMap == null || baseMap.name != PipAvatar.ModelName + "_BaseColor")
            {
                return null;
            }

            if (!_starred.TryGetValue(material, out var starred))
            {
                starred = new Material(material) { name = material.name + "_StarCape" };
                starred.SetTexture("_BaseMap", _starTexture);
                if (starred.HasProperty("_MainTex"))
                {
                    starred.SetTexture("_MainTex", _starTexture);
                }

                _starred.Add(material, starred);
            }

            return starred;
        }

        /// <summary>
        /// Spawns <paramref name="piece"/> on Pip's <paramref name="boneName"/> where the bind pose puts it:
        /// <c>m = bindposes[i] * skin.worldToLocal * piece.localToWorld</c> from the Pip prefab asset.
        /// </summary>
        private GameObject AttachPiece(string piece, string boneName)
        {
            var bone = ModelKit.FindDeep(_model, boneName);
            var pipPrefab = Resources.Load<GameObject>(PipAvatar.ModelPath);
            var kit = new ModelKit(_materials, KeepsakeArt.CostumeFolder) { FallbackAtlas = KeepsakeArt.CostumeAtlas };
            var piecePrefab = kit.Load(piece);
            if (bone == null || pipPrefab == null || piecePrefab == null || !TryBindPose(pipPrefab, boneName, out var bind,
                    out var skinWorldToLocal))
            {
                Debug.LogWarning($"[PipCostume] Cannot attach {piece} to {boneName}");
                return null;
            }

            Matrix4x4 m = bind * skinWorldToLocal * piecePrefab.transform.localToWorldMatrix;
            var instance = kit.Spawn(piece, bone, Vector3.zero);
            if (instance == null)
            {
                return null;
            }

            instance.transform.localPosition = m.GetColumn(3);
            instance.transform.localRotation = m.rotation;
            instance.transform.localScale = m.lossyScale;
            return instance;
        }

        private static bool TryBindPose(GameObject pipPrefab, string boneName, out Matrix4x4 bind, out Matrix4x4 skinWorldToLocal)
        {
            foreach (var skin in pipPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = skin.bones;
                var mesh = skin.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                var poses = mesh.bindposes;
                for (int i = 0; i < bones.Length && i < poses.Length; i++)
                {
                    if (bones[i] != null && bones[i].name == boneName)
                    {
                        bind = poses[i];
                        skinWorldToLocal = skin.transform.worldToLocalMatrix;
                        return true;
                    }
                }
            }

            bind = Matrix4x4.identity;
            skinWorldToLocal = Matrix4x4.identity;
            return false;
        }
    }
}
