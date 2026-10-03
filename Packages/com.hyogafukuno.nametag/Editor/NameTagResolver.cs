using System.Collections.Generic;
using UnityEditor;

namespace NameTag.Editor
{
    /// <summary>
    /// アセットに表示すべき名前タグを解決する。
    /// フォルダは自身のタグのみ。ファイルは自身のタグを優先し、なければ最も近い親フォルダのタグを継承する。
    /// </summary>
    [InitializeOnLoad]
    public static class NameTagResolver
    {
        public readonly struct Result
        {
            public readonly string TagName;
            /// <summary>タグの設定元アセットのパス(継承時は親フォルダ)。</summary>
            public readonly string SourcePath;
            public readonly bool Inherited;

            public Result(string tagName, string sourcePath, bool inherited)
            {
                TagName = tagName;
                SourcePath = sourcePath;
                Inherited = inherited;
            }

            public bool HasTag => !string.IsNullOrEmpty(TagName);
        }

        static readonly Dictionary<string, Result> s_Cache = new Dictionary<string, Result>();

        static NameTagResolver()
        {
            NameTagSettings.Changed += ClearCache;  // 割り当ての変更もこのイベントで通知される
            EditorApplication.projectChanged += ClearCache;
        }

        public static void ClearCache() => s_Cache.Clear();

        public static Result Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return default;
            if (s_Cache.TryGetValue(guid, out var cached)) return cached;

            var result = ResolveUncached(guid);
            s_Cache[guid] = result;
            return result;
        }

        static Result ResolveUncached(string guid)
        {
            var settings = NameTagSettings.instance;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return default;

            var own = NameTagAssignments.GetOwnTag(guid);
            if (settings.IsRegistered(own)) return new Result(own, path, false);

            if (AssetDatabase.IsValidFolder(path)) return default;

            // 最も近い親フォルダのタグを継承する
            var parent = GetParentPath(path);
            while (!string.IsNullOrEmpty(parent))
            {
                var parentTag = NameTagAssignments.GetOwnTag(AssetDatabase.AssetPathToGUID(parent));
                if (settings.IsRegistered(parentTag)) return new Result(parentTag, parent, true);
                parent = GetParentPath(parent);
            }
            return default;
        }

        static string GetParentPath(string path)
        {
            var index = path.LastIndexOf('/');
            return index > 0 ? path.Substring(0, index) : null;
        }
    }
}
