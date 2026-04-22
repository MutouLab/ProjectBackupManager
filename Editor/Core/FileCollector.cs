// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System.Collections.Generic;
using System.IO;
using MutouLab.ProjectBackupManager.Data;

namespace MutouLab.ProjectBackupManager.Core
{
    /// <summary>
    /// バックアップ対象ファイルの収集・フィルタリング。
    /// </summary>
    internal class FileCollector
    {
        private readonly string _projectRoot;

        /// <summary>
        /// FileCollectorを初期化する。
        /// </summary>
        /// <param name="projectRoot">Unityプロジェクトのルートディレクトリ。</param>
        public FileCollector(string projectRoot)
        {
            _projectRoot = projectRoot;
        }

        /// <summary>
        /// バックアップ対象の全ファイルパス（プロジェクトルート相対）を収集する。
        /// </summary>
        /// <returns>相対パスのリスト。</returns>
        public List<string> Collect()
        {
            var result = new List<string>();
            string[] excludePatterns = BackupSettings.GetExcludePatternsArray();

            // Assets/ 配下の全ファイル
            string assetsPath = Path.Combine(_projectRoot, BackupConstants.AssetsDirectory);
            if (Directory.Exists(assetsPath))
            {
                CollectDirectory(assetsPath, excludePatterns, result);
            }

            // パッケージマニフェスト
            AddIfExists(BackupConstants.PackagesManifestPath, result);
            AddIfExists(BackupConstants.VpmManifestPath, result);

            return result;
        }

        /// <summary>
        /// 指定ディレクトリ内のファイルを再帰的に収集する。
        /// </summary>
        private void CollectDirectory(string directory, string[] excludePatterns, List<string> result)
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                string relativePath = GetRelativePath(file);
                if (!IsExcluded(relativePath, excludePatterns))
                {
                    result.Add(relativePath);
                }
            }

            foreach (string subDir in Directory.GetDirectories(directory))
            {
                string dirName = Path.GetFileName(subDir);
                // 隠しディレクトリをスキップ
                if (dirName.StartsWith("."))
                    continue;

                string relativeDirPath = GetRelativePath(subDir);
                if (!IsExcluded(relativeDirPath, excludePatterns))
                {
                    CollectDirectory(subDir, excludePatterns, result);
                }
            }
        }

        /// <summary>
        /// ファイルが存在する場合にリストへ追加する。
        /// </summary>
        private void AddIfExists(string relativePath, List<string> result)
        {
            string fullPath = Path.Combine(_projectRoot, relativePath);
            if (File.Exists(fullPath))
            {
                result.Add(relativePath);
            }
        }

        /// <summary>
        /// 除外パターンに一致するかを判定する。
        /// </summary>
        private static bool IsExcluded(string relativePath, string[] excludePatterns)
        {
            for (int i = 0; i < excludePatterns.Length; i++)
            {
                string pattern = excludePatterns[i].Trim();
                if (string.IsNullOrEmpty(pattern))
                    continue;

                // 単純な前方一致・含有一致による除外判定
                if (relativePath.Contains(pattern))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 絶対パスをプロジェクトルート相対パスに変換する。
        /// </summary>
        private string GetRelativePath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string root = _projectRoot.Replace('\\', '/');
            if (!root.EndsWith("/"))
                root += "/";

            if (normalized.StartsWith(root))
                return normalized.Substring(root.Length);

            return normalized;
        }
    }
}
