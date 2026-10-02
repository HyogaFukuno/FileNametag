using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NameTag.Editor
{
    /// <summary>
    /// 名前タグの登録名と、アセット(GUID)ごとの割り当てを保持する設定。
    /// ProjectSettings/NameTagSettings.asset に保存されるため、バージョン管理でチーム共有できる。
    /// </summary>
    [FilePath("ProjectSettings/NameTagSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class NameTagSettings : ScriptableSingleton<NameTagSettings>
    {
        [Serializable]
        public class Assignment
        {
            public string guid;
            public string name;
        }

        [SerializeField] List<string> m_Names = new List<string>();
        [SerializeField] List<Assignment> m_Assignments = new List<Assignment>();

        /// <summary>設定内容が変更されたときに呼ばれる。</summary>
        public static event Action Changed;

        // 読み込み(または保存)時点の設定ファイルの更新日時。
        // インスタンスはドメインリロードをまたいで残るため、SessionState に保持する
        const string k_KnownWriteTimeKey = "NameTag.KnownSettingsWriteTimeUtc";
        const long k_Unknown = -1;

        Dictionary<string, string> m_AssignmentMap;
        HashSet<string> m_NameSet;

        public IReadOnlyList<string> Names => m_Names;
        public IReadOnlyList<Assignment> Assignments => m_Assignments;

        void OnEnable()
        {
            // SettingsProvider から SerializedObject 経由で編集できるようにする
            hideFlags &= ~HideFlags.NotEditable;

            // ドメインリロード時の OnEnable で上書きすると、リロード前に起きた外部更新を見逃すため、
            // ファイルから読み込んだとき(未記録のとき)だけ記録する
            if (KnownWriteTimeTicks == k_Unknown) KnownWriteTimeTicks = GetFileWriteTimeUtc().Ticks;
        }

        static long KnownWriteTimeTicks
        {
            get => long.TryParse(SessionState.GetString(k_KnownWriteTimeKey, ""), out var ticks) ? ticks : k_Unknown;
            set => SessionState.SetString(k_KnownWriteTimeKey, value.ToString());
        }

        /// <summary>
        /// git pull などで設定ファイルが外部から更新されていたら読み直す。
        /// 読み直さずに保存すると、他のメンバーの変更を古い内容で上書きしてしまう。
        /// </summary>
        public static void ReloadIfChangedOnDisk()
        {
            var known = KnownWriteTimeTicks;
            if (known == k_Unknown) return;
            if (GetFileWriteTimeUtc().Ticks == known) return;

            // ScriptableSingleton は破棄すると次回の instance アクセス時にファイルから再読み込みされる
            KnownWriteTimeTicks = k_Unknown;
            DestroyImmediate(instance);
            Changed?.Invoke();
        }

        /// <summary>登録済みの名前かどうか。</summary>
        public bool IsRegistered(string tagName)
        {
            EnsureCache();
            return IsValidName(tagName) && m_NameSet.Contains(tagName);
        }

        /// <summary>名前タグとして有効な文字列か(空白のみは無効)。</summary>
        public static bool IsValidName(string tagName) => !string.IsNullOrWhiteSpace(tagName);

        /// <summary>アセット自身に設定されている名前タグ(未登録名も含む)。なければ null。</summary>
        public string GetOwnTag(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            EnsureCache();
            return m_AssignmentMap.TryGetValue(guid, out var tagName) ? tagName : null;
        }

        /// <summary>名前タグを設定する。null または空文字で解除。保存はしない。</summary>
        public void SetTag(string guid, string tagName)
        {
            if (string.IsNullOrEmpty(guid)) return;

            // マージなどで同じ GUID のエントリが重複していても確実に置き換える
            m_Assignments.RemoveAll(a => a.guid == guid);
            if (!string.IsNullOrEmpty(tagName))
            {
                m_Assignments.Add(new Assignment { guid = guid, name = tagName });
            }
            m_AssignmentMap = null;
        }

        /// <summary>存在しなくなったアセットへの割り当てを削除する。削除件数を返す。</summary>
        public int RemoveMissingAssignments()
        {
            var removed = m_Assignments.RemoveAll(a => !AssetExists(a.guid));
            if (removed > 0) m_AssignmentMap = null;
            return removed;
        }

        /// <summary>
        /// GUID のアセットが現存するか。GUIDToAssetPath は削除直後のアセットのパスも返すため、存在確認を併用する。
        /// </summary>
        public static bool AssetExists(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
        }

        public void SaveAndNotify()
        {
            m_AssignmentMap = null;
            m_NameSet = null;
            Save(true);
            KnownWriteTimeTicks = GetFileWriteTimeUtc().Ticks;
            Changed?.Invoke();
        }

        static DateTime GetFileWriteTimeUtc()
        {
            var path = GetFilePath();
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        void EnsureCache()
        {
            if (m_AssignmentMap != null && m_NameSet != null) return;

            m_NameSet = new HashSet<string>();
            foreach (var n in m_Names)
            {
                if (IsValidName(n)) m_NameSet.Add(n);
            }

            m_AssignmentMap = new Dictionary<string, string>();
            foreach (var a in m_Assignments)
            {
                if (!string.IsNullOrEmpty(a.guid)) m_AssignmentMap[a.guid] = a.name;
            }
        }
    }

    /// <summary>
    /// 設定ファイルの外部更新を定期的に確認する。
    /// </summary>
    [InitializeOnLoad]
    static class NameTagSettingsFileWatcher
    {
        const double k_PollInterval = 1.0;
        static double s_NextPollTime;

        static NameTagSettingsFileWatcher()
        {
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_NextPollTime) return;
            s_NextPollTime = EditorApplication.timeSinceStartup + k_PollInterval;
            NameTagSettings.ReloadIfChangedOnDisk();
        }
    }
}
