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

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(s_SerializedObject.FindProperty("m_Names"), new GUIContent("Names"), true);
            if (EditorGUI.EndChangeCheck())
            {
                s_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
                settings.SaveAndNotify();
            }

            DrawNameValidation(settings);

            EditorGUILayout.Space(12);
            DrawAssignments(settings);
        }

        static void DrawNameValidation(NameTagSettings settings)
        {
            if (settings.Names.Any(string.IsNullOrWhiteSpace))
            {
                EditorGUILayout.HelpBox("空の名前があります。空の名前は選択肢に表示されません。", MessageType.Warning);
            }

            var duplicates = settings.Names
                .Where(n => !string.IsNullOrEmpty(n))
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
            s_ShowAssignments = EditorGUILayout.Foldout(s_ShowAssignments, $"Assignments ({settings.Assignments.Count})", true);
            if (!s_ShowAssignments) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (settings.Assignments.Count == 0)
                {
                    EditorGUILayout.LabelField("割り当てはありません。", EditorStyles.miniLabel);
                }

                string removeGuid = null;
                s_AssignmentScroll = EditorGUILayout.BeginScrollView(s_AssignmentScroll, GUILayout.MaxHeight(300));
                foreach (var a in settings.Assignments.OrderBy(a => AssetDatabase.GUIDToAssetPath(a.guid)))
                {
                    var path = AssetDatabase.GUIDToAssetPath(a.guid);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var pathLabel = string.IsNullOrEmpty(path) ? $"(Missing) {a.guid}" : path;
                        var nameLabel = settings.IsRegistered(a.name) ? a.name : $"{a.name} (未登録)";

                        if (GUILayout.Button(pathLabel, EditorStyles.label) && !string.IsNullOrEmpty(path))
                        {
                            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
                        }
                        EditorGUILayout.LabelField(nameLabel, GUILayout.Width(140));
                        if (GUILayout.Button("解除", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            removeGuid = a.guid;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                if (removeGuid != null)
                {
                    settings.SetTag(removeGuid, null);
                    settings.SaveAndNotify();
                }

                if (GUILayout.Button("存在しないアセットの割り当てを削除", GUILayout.Width(240)))
                {
                    if (settings.RemoveMissingAssignments() > 0) settings.SaveAndNotify();
                }
            }
        }
    }
}
