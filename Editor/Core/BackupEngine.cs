// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MutouLab.ProjectBackupManager.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MutouLab.ProjectBackupManager.Core
{
    /// <summary>
    /// バックアップ作成・復元のオーケストレーション。
    /// </summary>
    internal class BackupEngine
    {
        private readonly string _projectRoot;
        private readonly ContentStore _contentStore;
        private readonly GenerationManager _generationManager;
        private readonly FileCollector _fileCollector;

        /// <summary>
        /// BackupEngineを初期化する。
        /// </summary>
        /// <param name="projectRoot">Unityプロジェクトのルートディレクトリ。</param>
        public BackupEngine(string projectRoot)
        {
            _projectRoot = projectRoot;
            string storeRoot = Path.Combine(projectRoot, BackupSettings.StorePath);
            _contentStore = new ContentStore(storeRoot);
            _generationManager = new GenerationManager(storeRoot, _contentStore);
            _fileCollector = new FileCollector(projectRoot);
        }

        /// <summary>世代マネージャへのアクセス。</summary>
        public GenerationManager Generations => _generationManager;

        /// <summary>バックグラウンドバックアップが実行中かどうか。</summary>
        public bool IsBackgroundBackupRunning { get; private set; }

        /// <summary>
        /// バックグラウンドスレッドでバックアップを作成する。
        /// メインスレッドでのブロッキングを最小限にし、UXを維持する。
        /// シーン保存とファイル収集のみメインスレッドで実行し、
        /// ハッシュ計算・ストア格納はバックグラウンドで行う。
        /// </summary>
        /// <param name="backupType">バックアップ種別。</param>
        /// <param name="label">ユーザー向けラベル。</param>
        /// <param name="onCompleted">完了時コールバック（メインスレッドで呼ばれる）。</param>
        public void CreateBackupInBackground(string backupType, string label, Action onCompleted = null)
        {
            if (IsBackgroundBackupRunning)
            {
                Debug.Log("[ProjectBackupManager] バックグラウンドバックアップ実行中のためスキップ");
                return;
            }

            IsBackgroundBackupRunning = true;

            try
            {
                _contentStore.EnsureInitialized();
                _generationManager.EnsureInitialized();

                // メインスレッド: 変更のあるシーンのみ保存
                SaveDirtyScenes();

                // メインスレッド: ファイルリスト収集と前回マニフェスト取得
                List<string> files = _fileCollector.Collect();
                var previousManifests = _generationManager.LoadAll();
                GenerationManifest previous = previousManifests.Count > 0 ? previousManifests[0] : null;

                // ローカルコピー（スレッド間で安全に参照するため）
                string projectRoot = _projectRoot;
                var contentStore = _contentStore;
                var generationManager = _generationManager;
                int maxGenerations = BackupSettings.MaxGenerations;

                // バックグラウンドスレッド: ハッシュ計算とストア格納
                Task.Run(() =>
                {
                    try
                    {
                        var manifest = GenerationManifest.Create(backupType, label);
                        int processedFiles = 0;
                        long totalSize = 0;

                        for (int i = 0; i < files.Count; i++)
                        {
                            string relativePath = files[i];
                            string absolutePath = Path.Combine(projectRoot, relativePath);

                            if (!File.Exists(absolutePath))
                                continue;

                            var fileInfo = new FileInfo(absolutePath);
                            string lastModified = fileInfo.LastWriteTimeUtc.ToString("o");
                            string hash;

                            FileEntry previousEntry = previous?.FindEntry(relativePath);
                            if (previousEntry != null &&
                                previousEntry.lastModified == lastModified &&
                                previousEntry.size == fileInfo.Length)
                            {
                                hash = previousEntry.hash;
                            }
                            else
                            {
                                hash = contentStore.Store(absolutePath);
                            }

                            var entry = new FileEntry
                            {
                                path = relativePath,
                                hash = hash,
                                size = fileInfo.Length,
                                lastModified = lastModified
                            };
                            manifest.files.Add(entry);
                            totalSize += fileInfo.Length;
                            processedFiles++;
                        }

                        manifest.fileCount = processedFiles;
                        manifest.totalSize = totalSize;
                        generationManager.Save(manifest);
                        generationManager.Prune(maxGenerations);

                        Debug.Log($"[ProjectBackupManager] バックグラウンドバックアップ完了: {processedFiles}ファイル, {FormatSize(totalSize)}");

                        // メインスレッドに完了を通知
                        EditorApplication.delayCall += () =>
                        {
                            IsBackgroundBackupRunning = false;
                            onCompleted?.Invoke();
                        };
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ProjectBackupManager] バックグラウンドバックアップエラー: {e.Message}\n{e.StackTrace}");
                        EditorApplication.delayCall += () => IsBackgroundBackupRunning = false;
                    }
                });
            }
            catch (Exception e)
            {
                IsBackgroundBackupRunning = false;
                Debug.LogError($"[ProjectBackupManager] バックアップ準備エラー: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// シーンの状態をディスクに書き出す。
        /// 既存シーン: saveAsCopy: true でdirtyフラグを維持したまま書き出す。
        /// 未保存シーン: 既存の.unityがあればそのパスに、なければプロジェクト名で保存する。
        /// </summary>
        private static void SaveDirtyScenes()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (string.IsNullOrEmpty(scene.path))
                {
                    string savePath = ResolveUntitledScenePath();
                    string saveDir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(saveDir) && !Directory.Exists(saveDir))
                        Directory.CreateDirectory(saveDir);

                    EditorSceneManager.SaveScene(scene, savePath);
                    Debug.Log($"[ProjectBackupManager] 未保存シーンを保存: {savePath}");
                }
                else if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene, scene.path, saveAsCopy: true);
                }
            }
        }

        /// <summary>
        /// 未保存シーンの保存先パスを決定する。
        /// Assets/配下に既存の.unityファイルがあればそのパスを返し、
        /// なければ Assets/Scenes/{プロジェクト名}.unity を返す。
        /// </summary>
        private static string ResolveUntitledScenePath()
        {
            // Assets/配下の既存シーンを検索
            string assetsDir = "Assets";
            if (Directory.Exists(assetsDir))
            {
                string[] existing = Directory.GetFiles(assetsDir, "*.unity", SearchOption.AllDirectories);
                if (existing.Length > 0)
                {
                    // 最終更新が最も新しいシーンを採用
                    string newest = existing[0];
                    DateTime newestTime = File.GetLastWriteTimeUtc(newest);
                    for (int i = 1; i < existing.Length; i++)
                    {
                        DateTime t = File.GetLastWriteTimeUtc(existing[i]);
                        if (t > newestTime)
                        {
                            newest = existing[i];
                            newestTime = t;
                        }
                    }
                    return newest.Replace('\\', '/');
                }
            }

            // 既存シーンがない場合はプロジェクト名で作成
            string projectName = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            return $"Assets/Scenes/{projectName}.unity";
        }

        /// <summary>
        /// 指定ファイルのみを復元する（選択復元）。
        /// </summary>
        /// <param name="generationId">復元元の世代ID。</param>
        /// <param name="filePaths">復元するファイルの相対パスリスト。</param>
        /// <returns>復元されたファイル数。</returns>
        public int RestoreFiles(string generationId, List<string> filePaths)
        {
            try
            {
                var manifest = _generationManager.Load(generationId);
                if (manifest == null)
                {
                    Debug.LogError($"[ProjectBackupManager] 世代が見つかりません: {generationId}");
                    return 0;
                }

                int restored = 0;
                for (int i = 0; i < filePaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "ファイル復元中",
                        $"{filePaths[i]} ({i + 1}/{filePaths.Count})",
                        (float)i / filePaths.Count);

                    FileEntry entry = manifest.FindEntry(filePaths[i]);
                    if (entry == null)
                    {
                        Debug.LogWarning($"[ProjectBackupManager] マニフェストにエントリがありません: {filePaths[i]}");
                        continue;
                    }

                    string destPath = Path.Combine(_projectRoot, entry.path);
                    if (_contentStore.Retrieve(entry.hash, destPath))
                        restored++;
                }

                AssetDatabase.Refresh();
                ReloadRestoredScenes(filePaths);
                Debug.Log($"[ProjectBackupManager] {restored}ファイルを復元");
                return restored;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectBackupManager] 復元エラー: {e.Message}\n{e.StackTrace}");
                return 0;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 完全ロールバックを実行する。
        /// Assets/全体をバックアップ時の状態に復元し、バックアップ後に追加されたファイルの一覧を返す。
        /// </summary>
        /// <param name="generationId">復元元の世代ID。</param>
        /// <param name="addedFiles">バックアップ後に追加されたファイルの相対パスリスト（出力）。</param>
        /// <returns>復元されたファイル数。失敗時は-1。</returns>
        public int Rollback(string generationId, out List<string> addedFiles)
        {
            addedFiles = new List<string>();

            try
            {
                var manifest = _generationManager.Load(generationId);
                if (manifest == null)
                {
                    Debug.LogError($"[ProjectBackupManager] 世代が見つかりません: {generationId}");
                    return -1;
                }

                // マニフェスト内のファイルパスをセット化
                var manifestPaths = new HashSet<string>();
                for (int i = 0; i < manifest.files.Count; i++)
                {
                    manifestPaths.Add(manifest.files[i].path);
                }

                // バックアップ後に追加されたファイルを検出
                var currentFiles = _fileCollector.Collect();
                for (int i = 0; i < currentFiles.Count; i++)
                {
                    // Assets/配下のファイルのみ対象（マニフェスト類はスキップ）
                    if (currentFiles[i].StartsWith(BackupConstants.AssetsDirectory + "/") &&
                        !manifestPaths.Contains(currentFiles[i]))
                    {
                        addedFiles.Add(currentFiles[i]);
                    }
                }

                // マニフェストのファイルを復元
                int restored = 0;
                for (int i = 0; i < manifest.files.Count; i++)
                {
                    var entry = manifest.files[i];
                    if (i % 50 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "ロールバック中",
                            $"{entry.path} ({i + 1}/{manifest.files.Count})",
                            (float)i / manifest.files.Count);
                    }

                    string destPath = Path.Combine(_projectRoot, entry.path);
                    if (_contentStore.Retrieve(entry.hash, destPath))
                        restored++;
                }

                // パッケージマニフェストの復元
                RestoreManifestFile(manifest, BackupConstants.PackagesManifestPath);
                RestoreManifestFile(manifest, BackupConstants.VpmManifestPath);

                AssetDatabase.Refresh();
                ReloadAllOpenScenes();
                Debug.Log($"[ProjectBackupManager] ロールバック完了: {restored}ファイルを復元");
                return restored;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectBackupManager] ロールバックエラー: {e.Message}\n{e.StackTrace}");
                return -1;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 指定されたファイルリストを削除する（ロールバック時のユーザー確認後に使用）。
        /// </summary>
        /// <param name="filePaths">削除するファイルの相対パスリスト。</param>
        public void DeleteFiles(List<string> filePaths)
        {
            for (int i = 0; i < filePaths.Count; i++)
            {
                string absolutePath = Path.Combine(_projectRoot, filePaths[i]);
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);

                    // .metaファイルも削除
                    string metaPath = absolutePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// マニフェストファイルを復元する。
        /// </summary>
        private void RestoreManifestFile(GenerationManifest manifest, string relativePath)
        {
            FileEntry entry = manifest.FindEntry(relativePath);
            if (entry != null)
            {
                string destPath = Path.Combine(_projectRoot, entry.path);
                _contentStore.Retrieve(entry.hash, destPath);
            }
        }

        /// <summary>
        /// バイト数を読みやすい文字列に変換する。
        /// </summary>
        internal static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        /// <summary>
        /// 復元対象にシーンファイルが含まれていた場合、該当する開いているシーンをリロードする。
        /// </summary>
        /// <param name="restoredPaths">復元されたファイルの相対パスリスト。</param>
        private static void ReloadRestoredScenes(List<string> restoredPaths)
        {
            var scenePathsToReload = new HashSet<string>();
            for (int i = 0; i < restoredPaths.Count; i++)
            {
                if (restoredPaths[i].EndsWith(".unity"))
                    scenePathsToReload.Add(restoredPaths[i]);
            }

            if (scenePathsToReload.Count == 0)
                return;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.IsValid() && scenePathsToReload.Contains(scene.path))
                {
                    EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                    Debug.Log($"[ProjectBackupManager] シーンをリロード: {scene.path}");
                    break;
                }
            }
        }

        /// <summary>
        /// 全ての開いているシーンをディスクからリロードする。
        /// </summary>
        private static void ReloadAllOpenScenes()
        {
            if (EditorSceneManager.sceneCount == 0)
                return;

            // 最初のシーンをSingleモードで開く（他のシーンをアンロード）
            var firstScene = EditorSceneManager.GetSceneAt(0);
            if (firstScene.IsValid() && !string.IsNullOrEmpty(firstScene.path))
            {
                EditorSceneManager.OpenScene(firstScene.path, OpenSceneMode.Single);
                Debug.Log($"[ProjectBackupManager] シーンをリロード: {firstScene.path}");
            }
        }
    }
}
