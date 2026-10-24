using System.Collections.Generic;
using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// シーン内のオブジェクトにIDを付け、StoryDirectorなどから名前で参照できるようにする。
    /// 仮モデルを本番モデルに差し替えるときは、このコンポーネントを残しておけばコード側の変更は不要。
    /// </summary>
    public class StoryAnchor : MonoBehaviour
    {
        public string id;

        static readonly Dictionary<string, StoryAnchor> registry = new();

        /// <summary>非アクティブなエリアも含めて全アンカーを登録し直す。</summary>
        public static void RebuildRegistry()
        {
            registry.Clear();
            foreach (var a in FindObjectsByType<StoryAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(a.id)) continue;
                if (registry.ContainsKey(a.id))
                    Debug.LogWarning($"[StoryAnchor] IDが重複しています: {a.id}", a);
                registry[a.id] = a;
            }
        }

        public static T Get<T>(string id) where T : Component
        {
            if (!registry.TryGetValue(id, out var a) || a == null)
            {
                Debug.LogError($"[StoryAnchor] 見つかりません: {id}");
                return null;
            }
            var c = a.GetComponent<T>();
            if (c == null) Debug.LogError($"[StoryAnchor] {id} に {typeof(T).Name} がありません", a);
            return c;
        }

        public static Transform Get(string id) => Get<Transform>(id);
    }
}
