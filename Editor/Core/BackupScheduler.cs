using System;
using MutouLab.ProjectBackupManager.Data;
using UnityEditor;
using UnityEngine;

namespace MutouLab.ProjectBackupManager.Core
{
    /// <summary>
    /// EditorApplication.updateベースの自動バックアップスケジューラ。
    /// </summary>
    [InitializeOnLoad]
    internal static class BackupScheduler
    {
        private const string LastBackupTimeKey = "MutouLab.ProjectBackupManager.LastBackupTime";

        /// <summary>バックアップ実行時に発火するイベント。</summary>
        public static event Action OnBackupCompleted;

        private static bool _isPlayMode;

        static BackupScheduler()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// 前回のバックアップ時刻を取得する。
        /// </summary>
        public static DateTimeOffset LastBackupTime
        {
            get
            {
                string stored = SessionState.GetString(LastBackupTimeKey, "");
                if (DateTimeOffset.TryParse(stored, out var result))
                    return result;
                return DateTimeOffset.MinValue;
            }
            private set => SessionState.SetString(LastBackupTimeKey, value.ToString("o"));
        }

        /// <summary>
        /// 次回の自動バックアップ予定時刻を取得する。
        /// </summary>
        public static DateTimeOffset NextBackupTime
        {
            get
            {
                if (LastBackupTime == DateTimeOffset.MinValue)
                    return DateTimeOffset.Now;
                return LastBackupTime.AddMinutes(BackupSettings.IntervalMinutes);
            }
        }

        private static void OnUpdate()
        {
            if (!BackupSettings.AutoBackupEnabled)
                return;

            if (_isPlayMode)
                return;

            if (DateTimeOffset.Now < NextBackupTime)
                return;

            ExecuteAutoBackup();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            _isPlayMode = state == PlayModeStateChange.EnteredPlayMode;
        }

        private static bool _isBackupRunning;

        /// <summary>
        /// 自動バックアップをバックグラウンドで実行する。
        /// </summary>
        private static void ExecuteAutoBackup()
        {
            if (_isBackupRunning)
                return;

            _isBackupRunning = true;
            LastBackupTime = DateTimeOffset.Now;

            string projectRoot = GetProjectRoot();
            var engine = new BackupEngine(projectRoot);
            engine.CreateBackupInBackground("auto", "自動バックアップ", () =>
            {
                _isBackupRunning = false;
                OnBackupCompleted?.Invoke();
            });
        }

        /// <summary>
        /// Unityプロジェクトのルートディレクトリを取得する。
        /// </summary>
        internal static string GetProjectRoot()
        {
            // Application.dataPath は "ProjectRoot/Assets" を返す
            return System.IO.Path.GetDirectoryName(Application.dataPath);
        }
    }
}
