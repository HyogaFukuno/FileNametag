using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NameTag.Editor
{
    /// <summary>
    /// 名前タグとして使用できる名前の一覧。
    /// ProjectSettings/NameTagSettings.asset に保存されるため、バージョン管理でチーム共有できる。
    /// アセットごとの割り当ては <see cref="NameTagAssignments"/> が別ファイルで管理する。
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

        // 1.0.x までの割り当ての保存先。読み込み時に NameTagAssignments へ移行して空にする
        [SerializeField] List<Assignment> m_Assignments = new List<Assignment>();

        /// <summary>設定内容が変更されたときに呼ばれる。</summary>
        public static event Action Changed;

        // 読み込み(または保存)時点の設定ファイルの更新日時。
        // インスタンスはドメインリロードをまたいで残るため、SessionState に保持する
        const string k_KnownWriteTimeKey = "NameTag.KnownSettingsWriteTimeUtc";
        const long k_Unknown = -1;

        HashSet<string> m_NameSet;

        public IReadOnlyList<string> Names => m_Names;

        /// <summary><see cref="Changed"/> を発行する(割り当ての変更通知にも使う)。</summary>
        internal static void NotifyChanged() => Changed?.Invoke();

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
            // マージで旧形式の割り当てが戻ってきた場合に移行し直せるよう、割り当ても読み直す
            NameTagAssignments.Invalidate();
            NotifyChanged();
        }

        /// <summary>登録済みの名前かどうか。</summary>
        public bool IsRegistered(string tagName)
        {
            if (m_NameSet == null)
            {
                m_NameSet = new HashSet<string>();
                foreach (var n in m_Names)
                {
                    if (IsValidName(n)) m_NameSet.Add(n);
                }
            }
            return IsValidName(tagName) && m_NameSet.Contains(tagName);
        }

        /// <summary>名前タグとして有効な文字列か(空白のみは無効)。</summary>
        public static bool IsValidName(string tagName) => !string.IsNullOrWhiteSpace(tagName);

        /// <summary>旧形式の割り当てを取り出して空にする。取り出した場合は保存する。</summary>
        internal List<(string guid, string tagName)> TakeLegacyAssignments()
        {
            var legacy = m_Assignments
                .Where(a => !string.IsNullOrEmpty(a.guid) && !string.IsNullOrEmpty(a.name))
                .Select(a => (a.guid, a.name))
                .ToList();
            if (m_Assignments.Count == 0) return legacy;

            m_Assignments.Clear();
            Save(true);
            KnownWriteTimeTicks = GetFileWriteTimeUtc().Ticks;
            return legacy;
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
            m_NameSet = null;
            Save(true);
            KnownWriteTimeTicks = GetFileWriteTimeUtc().Ticks;
            NotifyChanged();
        }

        static DateTime GetFileWriteTimeUtc()
        {
            var path = GetFilePath();
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
    }

    /// <summary>
    /// 設定ファイル・割り当てファイルの外部更新を定期的に確認する。
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
            NameTagAssignments.ReloadIfChangedOnDisk();
        }
    }
}
