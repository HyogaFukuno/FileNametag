using System;
using System.Collections.Generic;
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

        Dictionary<string, string> m_AssignmentMap;
        HashSet<string> m_NameSet;

        public IReadOnlyList<string> Names => m_Names;
        public IReadOnlyList<Assignment> Assignments => m_Assignments;

        void OnEnable()
        {
            // SettingsProvider から SerializedObject 経由で編集できるようにする
            hideFlags &= ~HideFlags.NotEditable;
        }

        /// <summary>登録済みの名前かどうか。</summary>
        public bool IsRegistered(string tagName)
        {
            EnsureCache();
            return !string.IsNullOrEmpty(tagName) && m_NameSet.Contains(tagName);
        }

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

            var index = m_Assignments.FindIndex(a => a.guid == guid);
            if (string.IsNullOrEmpty(tagName))
            {
                if (index >= 0) m_Assignments.RemoveAt(index);
            }
            else if (index >= 0)
            {
                m_Assignments[index].name = tagName;
            }
            else
            {
                m_Assignments.Add(new Assignment { guid = guid, name = tagName });
            }
            m_AssignmentMap = null;
        }

        /// <summary>存在しなくなったアセットへの割り当てを削除する。削除件数を返す。</summary>
        public int RemoveMissingAssignments()
        {
            var removed = m_Assignments.RemoveAll(a => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(a.guid)));
            if (removed > 0) m_AssignmentMap = null;
            return removed;
        }

        public void SaveAndNotify()
        {
            m_AssignmentMap = null;
            m_NameSet = null;
            Save(true);
            Changed?.Invoke();
        }

        void EnsureCache()
        {
            if (m_AssignmentMap != null && m_NameSet != null) return;

            m_NameSet = new HashSet<string>();
            foreach (var n in m_Names)
            {
                if (!string.IsNullOrEmpty(n)) m_NameSet.Add(n);
            }

            m_AssignmentMap = new Dictionary<string, string>();
            foreach (var a in m_Assignments)
            {
                if (!string.IsNullOrEmpty(a.guid)) m_AssignmentMap[a.guid] = a.name;
            }
        }
    }
}
