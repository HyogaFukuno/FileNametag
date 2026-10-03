using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NameTag.Editor
{
    /// <summary>
    /// Project Settings > NameTag Settings の画面。
    /// </summary>
    static class NameTagSettingsProvider
    {
        public const string SettingsPath = "Project/NameTag Settings";

        static SerializedObject s_SerializedObject;
        static bool s_ShowAssignments = true;
        static Vector2 s_AssignmentScroll;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "NameTag Settings",
                guiHandler = _ => DrawGUI(),
                keywords = new HashSet<string> { "Name", "Tag", "NameTag", "名前タグ", "担当" },
            };
        }

        public static void Open() => SettingsService.OpenProjectSettings(SettingsPath);

        static void DrawGUI()
        {
            // 他メンバーの変更を上書きしないよう、編集前に最新の設定を読み直す
            if (Event.current.type == EventType.Layout) NameTagSettings.ReloadIfChangedOnDisk();

            var settings = NameTagSettings.instance;
            if (s_SerializedObject == null || s_SerializedObject.targetObject != settings)
            {
                s_SerializedObject = new SerializedObject(settings);
            }

            s_SerializedObject.Update();

            EditorGUILayout.HelpBox(
                "名前タグとして使用できる名前を登録します。\n" +
                "フォルダ・ファイルを選択し、インスペクタ下部の「Name Tag」から割り当ててください。",
                MessageType.Info);

            EditorGUILayout.PropertyField(s_SerializedObject.FindProperty("m_Names"), new GUIContent("Names"), true);

            // リストの +/- や並べ替えは GUI.changed が立たない場合があるため、変更の有無は Apply の戻り値で判定する
            if (s_SerializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                settings.SaveAndNotify();
            }

            DrawNameValidation(settings);

            EditorGUILayout.Space(12);
            DrawAssignments(settings);
        }

        static void DrawNameValidation(NameTagSettings settings)
        {
            if (settings.Names.Any(n => !NameTagSettings.IsValidName(n)))
            {
                EditorGUILayout.HelpBox("空の名前があります。空の名前は選択肢に表示されません。", MessageType.Warning);
            }

            var duplicates = settings.Names
                .Where(NameTagSettings.IsValidName)
                .GroupBy(n => n)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            if (duplicates.Length > 0)
            {
                EditorGUILayout.HelpBox($"重複している名前があります: {string.Join(", ", duplicates)}", MessageType.Warning);
            }
        }

        static void DrawAssignments(NameTagSettings settings)
        {
            s_ShowAssignments = EditorGUILayout.Foldout(s_ShowAssignments, $"Assignments ({NameTagAssignments.Count})", true);
            if (!s_ShowAssignments) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField($"保存先: {NameTagAssignments.DirectoryPath}/<GUID>.txt", EditorStyles.miniLabel);
                if (NameTagAssignments.Count == 0)
                {
                    EditorGUILayout.LabelField("割り当てはありません。", EditorStyles.miniLabel);
                }

                string removeGuid = null;
                s_AssignmentScroll = EditorGUILayout.BeginScrollView(s_AssignmentScroll, GUILayout.MaxHeight(300));
                foreach (var a in NameTagAssignments.All.OrderBy(a => AssetDatabase.GUIDToAssetPath(a.Guid)))
                {
                    var exists = NameTagSettings.AssetExists(a.Guid);
                    var path = exists ? AssetDatabase.GUIDToAssetPath(a.Guid) : null;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var pathLabel = exists ? path : $"(Missing) {a.Guid}";
                        var nameLabel = settings.IsRegistered(a.TagName) ? a.TagName : $"{a.TagName} (未登録)";

                        if (GUILayout.Button(pathLabel, EditorStyles.label) && exists)
                        {
                            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
                        }
                        EditorGUILayout.LabelField(nameLabel, GUILayout.Width(140));
                        if (GUILayout.Button("解除", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            removeGuid = a.Guid;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                if (removeGuid != null)
                {
                    NameTagAssignments.SetTag(removeGuid, null);
                }

                if (GUILayout.Button("存在しないアセットの割り当てを削除", GUILayout.Width(240)))
                {
                    NameTagAssignments.RemoveMissing();
                }
            }
        }
    }
}
