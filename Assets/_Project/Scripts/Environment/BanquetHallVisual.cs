using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>Replace scenery while preserving story anchors and interaction roots.</summary>
    public static class BanquetHallVisual
    {
        public const string ResourcePath = "Scenery/Enkaijou";
        public const string InstanceName = "Enkaijou";
        static readonly string[] ReplacedObjects =
        {
            "HallFloor", "Tatami", "Pillar", "Lantern", "Roof", "RoofTrim",
            "WallBackL", "WallBackR", "WallBackTop", "WallL", "WallR", "Gate", "TableL", "TableR"
        };

        public static void Build(Area area)
        {
            if (area.areaId != "ryugu") return;
            if (area.transform.Find(InstanceName) == null)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null) return;
                var visual = Object.Instantiate(prefab, area.transform, false);
                visual.name = InstanceName;
            }
            // Preserve floor/table colliders and references used by the story.
            foreach (Transform child in area.transform)
                if (System.Array.IndexOf(ReplacedObjects, child.name) >= 0)
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
    }
}
