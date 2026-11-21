using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Dresses Lantern Lane with real models from the CC0 KayKit Halloween Bits kit (docs/asset-provenance.md):
    /// jack-o'-lanterns on both porches, standing lanterns lining the path, a picket fence across the owl
    /// house's front yard (gap at the path), a bench by the plaza and autumn pines behind the skyline.
    /// KayKit is built at chunky toy scale (a jack-o'-lantern is 1.3 m), so every prop is scaled to Pip
    /// (1.1 m). Missing models are skipped with a warning; the greybox stays playable.
    /// </summary>
    public static class StreetDressing
    {
        public const string Folder = "Models/Halloween";
        public const string Atlas = "halloweenbits_texture";

        public static Transform Build(Transform world, MaterialLibrary materials)
        {
            var root = new GameObject("StreetDressing").transform;
            root.SetParent(world, false);
            var kit = new ModelKit(materials, Folder, Atlas);

            // Arrival porch (top at y = 0.6): a grinning jack-o'-lantern and a small pumpkin by the steps.
            kit.Spawn("pumpkin_orange_jackolantern", root, new Vector3(-1.45f, 0.6f, -13.3f), 200f, 0.38f);
            kit.Spawn("pumpkin_orange_small", root, new Vector3(-1.6f, 0.6f, -14.15f), 40f, 0.5f);
            kit.Spawn("candle_triple", root, new Vector3(1.55f, 0.6f, -14.5f), 0f, 0.5f);

            // Standing lanterns pace the path toward the owl house.
            float[] lanternZ = { -9f, -3.5f, 2f, 7f };
            foreach (float z in lanternZ)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    kit.Spawn("lantern_standing", root, new Vector3(side * 1.3f, 0f, z), side * 90f, 0.7f, solid: true);
                }
            }

            // Owl house porch (top at y = 0.3).
            kit.Spawn("pumpkin_yellow_jackolantern", root, new Vector3(-1.9f, 0.3f, 10.05f), 180f, 0.4f);
            kit.Spawn("pumpkin_orange", root, new Vector3(1.95f, 0.3f, 10.0f), 20f, 0.45f);
            kit.Spawn("pumpkin_orange_small", root, new Vector3(2.45f, 0.3f, 10.3f), 75f, 0.45f);

            // Picket fence across the front yard, open where the path passes (x = ±1).
            const float fenceScale = 0.5f; // 4 m × 2.2 m → 2 m × 1.1 m
            const float fenceZ = 8.4f;
            float[] fenceX = { -2f, 2f, 4f };
            foreach (float x in fenceX)
            {
                kit.Spawn("fence", root, new Vector3(x, 0f, fenceZ), 0f, fenceScale, solid: true);
            }

            float[] pillarX = { -3f, -1f, 1f, 3f, 5f };
            foreach (float x in pillarX)
            {
                kit.Spawn("fence_pillar", root, new Vector3(x, 0f, fenceZ), 0f, fenceScale, solid: true);
            }

            // Bench beside the plaza, facing the path (Mr. Vale's seat in S001).
            kit.Spawn("bench_decorated", root, new Vector3(3.6f, 0f, -1.5f), -90f, 0.55f, solid: true);

            // Autumn pines and a bare tree frame the block behind the skyline and along the hedges.
            kit.Spawn("tree_pine_orange_large", root, new Vector3(-14f, 0f, 13.5f), 10f, 1f);
            kit.Spawn("tree_pine_yellow_large", root, new Vector3(-9.5f, 0f, 15.5f), 70f, 0.85f);
            kit.Spawn("tree_pine_orange_medium", root, new Vector3(11f, 0f, 15f), 130f, 1f);
            kit.Spawn("tree_pine_orange_large", root, new Vector3(16f, 0f, 11.5f), 200f, 1.1f);
            kit.Spawn("tree_pine_orange_medium", root, new Vector3(-18.5f, 0f, -1f), 30f, 0.9f);
            kit.Spawn("tree_pine_yellow_large", root, new Vector3(18.5f, 0f, -4f), 300f, 0.9f);
            kit.Spawn("tree_dead_medium", root, new Vector3(7.5f, 0f, -8.5f), 40f, 0.8f, solid: true);

            return root;
        }
    }
}
