using UnityEditor;

namespace MutouLab.ProjectBackupManager.Data
{
    /// <summary>
    /// バックアップ設定。EditorPrefsを使用してプロジェクト単位で永続化する。
    /// </summary>
    internal static class BackupSettings
    {
        private static string Key(string name) => BackupConstants.PrefsPrefix + name;

        /// <summary>自動バックアップが有効かどうか。</summary>
        public static bool AutoBackupEnabled
        {
            get => EditorPrefs.GetBool(Key("AutoBackupEnabled"), true);
            set => EditorPrefs.SetBool(Key("AutoBackupEnabled"), value);
        }

        /// <summary>自動バックアップ間隔（分）。</summary>
        public static int IntervalMinutes
        {
            get => EditorPrefs.GetInt(Key("IntervalMinutes"), BackupConstants.DefaultIntervalMinutes);
            set => EditorPrefs.SetInt(Key("IntervalMinutes"), value);
        }

        /// <summary>保持する最大世代数。</summary>
        public static int MaxGenerations
        {
            get => EditorPrefs.GetInt(Key("MaxGenerations"), BackupConstants.DefaultMaxGenerations);
            set => EditorPrefs.SetInt(Key("MaxGenerations"), value);
        }

        /// <summary>バックアップストアの保存先パス（プロジェクトルート相対）。</summary>
        public static string StorePath
        {
            get => EditorPrefs.GetString(Key("StorePath"), BackupConstants.DefaultStorePath);
            set => EditorPrefs.SetString(Key("StorePath"), value);
        }

        /// <summary>追加の除外パターン（セミコロン区切り）。</summary>
        public static string ExcludePatterns
        {
            get => EditorPrefs.GetString(Key("ExcludePatterns"), "");
            set => EditorPrefs.SetString(Key("ExcludePatterns"), value);
        }

        /// <summary>
        /// 除外パターンを配列として取得する。
        /// </summary>
        /// <returns>除外パターンの配列。パターンが未設定の場合は空配列。</returns>
        public static string[] GetExcludePatternsArray()
        {
            string patterns = ExcludePatterns;
            if (string.IsNullOrEmpty(patterns))
                return System.Array.Empty<string>();
            return patterns.Split(';');
        }
    }
}
