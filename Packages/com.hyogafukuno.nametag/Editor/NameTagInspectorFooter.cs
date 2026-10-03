using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NameTag.Editor
{
    /// <summary>
    /// インスペクタのエディタ一覧の末尾(Asset Labels の上)に名前タグ設定用のプルダウンを差し込む。
    /// 公開 API ではフッターに描画できないため、InspectorWindow の VisualElement ツリーに要素を追加している。
    /// </summary>
    [InitializeOnLoad]
    static class NameTagInspectorFooter
    {
        const string k_ElementName = "name-tag-inspector-footer";
        const string k_EditorsListClassName = "unity-inspector-editors-list";
        const double k_PollInterval = 0.25;

        static readonly Type s_InspectorWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
        static readonly PropertyInfo s_TrackerProperty = s_InspectorWindowType?.GetProperty(
            "tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static double s_NextPollTime;

        static NameTagInspectorFooter()
        {
            if (s_InspectorWindowType == null) return;

            EditorApplication.update += OnUpdate;
            Selection.selectionChanged += () => EditorApplication.delayCall += InjectAll;
            NameTagSettings.Changed += RefreshAll;
        }

        /// <summary>
        /// 設定変更時にフッターを描き直す。
        /// IMGUIContainer はインスペクタが再描画されるまで古い内容を表示し続けるため、
        /// Project Settings や外部からの変更後にインスペクタの表示が古いまま残ってしまう。
        /// </summary>
        static void RefreshAll()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll(s_InspectorWindowType))
            {
                if (((EditorWindow)window).rootVisualElement.Q(k_ElementName) is IMGUIContainer footer)
                {
                    // 継承元の表示行が増減して高さが変わるため、レイアウトからやり直す
                    footer.MarkDirtyLayout();
                }
                ((EditorWindow)window).Repaint();
            }
        }

        static void OnUpdate()
        {
            // インスペクタはアセット再インポートなどでも再構築されるため、定期的に差し込み状態を確認する
            if (EditorApplication.timeSinceStartup < s_NextPollTime) return;
            s_NextPollTime = EditorApplication.timeSinceStartup + k_PollInterval;
            InjectAll();
        }

        static void InjectAll()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll(s_InspectorWindowType))
            {
                Inject((EditorWindow)window);
            }
        }

        static void Inject(EditorWindow window)
        {
            var editorsList = window.rootVisualElement.Q(className: k_EditorsListClassName);
            if (editorsList == null) return;

            var footer = editorsList.Q(k_ElementName);
            if (footer == null)
            {
                footer = new IMGUIContainer(() => DrawFooter(window)) { name = k_ElementName };
                editorsList.Add(footer);
            }
            else if (editorsList.IndexOf(footer) != editorsList.childCount - 1)
            {
                footer.BringToFront();
            }
        }

        static void DrawFooter(EditorWindow window)
        {
            var guids = GetInspectedAssetGuids(window);
            if (guids.Count == 0) return;

            var settings = NameTagSettings.instance;

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                EditorGUILayout.Space(6);
                var lineRect = EditorGUILayout.GetControlRect(false, 1f);
                EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Name Tag", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Settings", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        NameTagSettingsProvider.Open();
                    }
                }

                var names = settings.Names.Where(NameTagSettings.IsValidName).Distinct().ToList();
                if (names.Count == 0)
                {
                    EditorGUILayout.HelpBox("名前が登録されていません。Project Settings > NameTag Settings で登録してください。", MessageType.Info);
                    EditorGUILayout.Space(4);
                    return;
                }

                DrawPopup(names, guids);
                DrawInheritanceInfo(guids);
                EditorGUILayout.Space(4);
            }
        }

        static void DrawPopup(List<string> names, List<string> guids)
        {
            var ownTags = guids.Select(NameTagAssignments.GetOwnTag).ToList();
            var first = ownTags[0];
            var mixed = ownTags.Any(t => t != first);

            // 選択肢: (なし) + 登録名。未登録名が設定されている場合は末尾に追加して表示する
            var options = new List<string> { "(なし)" };
            options.AddRange(names);
            var unregistered = !mixed && !string.IsNullOrEmpty(first) && !names.Contains(first);
            if (unregistered) options.Add($"{first} (未登録)");

            // 複数選択で値が混在している場合は -1 にして、どの項目を選んでも変更として扱われるようにする
            int currentIndex;
            if (mixed) currentIndex = -1;
            else if (string.IsNullOrEmpty(first)) currentIndex = 0;
            else if (unregistered) currentIndex = options.Count - 1;
            else currentIndex = names.IndexOf(first) + 1;

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup("Assignee", currentIndex, options.ToArray());
            EditorGUI.showMixedValue = false;
            if (!EditorGUI.EndChangeCheck()) return;
            if (unregistered && newIndex == options.Count - 1) return;

            var newTag = newIndex == 0 ? null : names[newIndex - 1];
            NameTagAssignments.SetTag(guids, newTag);
        }

        static void DrawInheritanceInfo(List<string> guids)
        {
            if (guids.Count != 1) return;

            var result = NameTagResolver.Resolve(guids[0]);
            if (!result.HasTag || !result.Inherited) return;

            EditorGUILayout.LabelField(
                $"親フォルダ「{result.SourcePath}」の名前タグ「{result.TagName}」を表示中",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>インスペクタに表示中のアセットの GUID を取得する(ロックされたインスペクタにも対応)。</summary>
        static List<string> GetInspectedAssetGuids(EditorWindow window)
        {
            var targets = GetInspectedTargets(window);
            var guids = new List<string>();
            foreach (var target in targets)
            {
                if (target == null) continue;

                var path = target is AssetImporter importer ? importer.assetPath : AssetDatabase.GetAssetPath(target);
                if (string.IsNullOrEmpty(path)) continue;

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid) && !guids.Contains(guid)) guids.Add(guid);
            }
            return guids;
        }

        static IEnumerable<Object> GetInspectedTargets(EditorWindow window)
        {
            if (s_TrackerProperty?.GetValue(window) is ActiveEditorTracker tracker)
            {
                // 先頭のエディタが選択中のアセット本体(またはそのインポーター)を表す
                var editors = tracker.activeEditors;
                return editors.Length > 0 ? editors[0].targets : Array.Empty<Object>();
            }
            return Selection.objects;
        }
    }
}
