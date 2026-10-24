using UnityEngine;

namespace JPHACKS2026
{
    /// <summary>
    /// 達成したエンディングと、オートセーブ（最後に通過した分岐）をPlayerPrefsに保存する。
    /// </summary>
    public static class SaveData
    {
        const string EndingsKey = "urashima.endings";
        const string CheckpointKey = "urashima.checkpoint";
        const string EyeOffsetKey = "urashima.eyeOffset";

        /// <summary>目線の高さの補正（m）。エンディングの記録とは別の設定なので ResetAll では消さない。</summary>
        public static float EyeHeightOffset
        {
            get => PlayerPrefs.GetFloat(EyeOffsetKey, 0f);
            set { PlayerPrefs.SetFloat(EyeOffsetKey, value); PlayerPrefs.Save(); }
        }

        public static bool HasEnding(EndingId id) => (PlayerPrefs.GetInt(EndingsKey, 0) & (1 << (int)id)) != 0;

        public static int EndingCount
        {
            get
            {
                int mask = PlayerPrefs.GetInt(EndingsKey, 0), n = 0;
                for (int i = 1; i <= EndingCatalog.Count; i++) if ((mask & (1 << i)) != 0) n++;
                return n;
            }
        }

        /// <summary>エンディングを記録する。初めての達成ならtrue。</summary>
        public static bool RecordEnding(EndingId id)
        {
            bool isNew = !HasEnding(id);
            PlayerPrefs.SetInt(EndingsKey, PlayerPrefs.GetInt(EndingsKey, 0) | (1 << (int)id));
            PlayerPrefs.Save();
            return isNew;
        }

        public static bool TryGetCheckpoint(out Chapter chapter)
        {
            int v = PlayerPrefs.GetInt(CheckpointKey, -1);
            chapter = (Chapter)Mathf.Max(v, 0);
            return v >= 0;
        }

        public static void SaveCheckpoint(Chapter chapter)
        {
            PlayerPrefs.SetInt(CheckpointKey, (int)chapter);
            PlayerPrefs.Save();
        }

        public static void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(EndingsKey);
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }
    }
}
