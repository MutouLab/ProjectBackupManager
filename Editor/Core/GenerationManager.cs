// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System;
using System.Collections.Generic;
using System.IO;
using MutouLab.ProjectBackupManager.Data;
using UnityEngine;

namespace MutouLab.ProjectBackupManager.Core
{
    /// <summary>
    /// 世代マニフェストのCRUDとプルーニングを管理する。
    /// </summary>
    internal class GenerationManager
    {
        private readonly string _generationsRoot;
        private readonly ContentStore _contentStore;

        /// <summary>
        /// GenerationManagerを初期化する。
        /// </summary>
        /// <param name="storeRoot">ストアルートディレクトリのパス。</param>
        /// <param name="contentStore">コンテンツストアの参照。</param>
        public GenerationManager(string storeRoot, ContentStore contentStore)
        {
            _generationsRoot = Path.Combine(storeRoot, BackupConstants.GenerationsDirectory);
            _contentStore = contentStore;
        }

        /// <summary>
        /// 世代マニフェストディレクトリを作成する（存在しない場合）。
        /// </summary>
        public void EnsureInitialized()
        {
            if (!Directory.Exists(_generationsRoot))
                Directory.CreateDirectory(_generationsRoot);
        }

        /// <summary>
        /// 世代マニフェストを保存する。
        /// </summary>
        /// <param name="manifest">保存するマニフェスト。</param>
        public void Save(GenerationManifest manifest)
        {
            EnsureInitialized();
            string fileName = FormatFileName(manifest);
            string filePath = Path.Combine(_generationsRoot, fileName);
            string json = JsonUtility.ToJson(manifest, prettyPrint: true);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 指定IDの世代マニフェストを読み込む。
        /// </summary>
        /// <param name="generationId">世代ID。</param>
        /// <returns>マニフェスト。見つからなければnull。</returns>
        public GenerationManifest Load(string generationId)
        {
            if (!Directory.Exists(_generationsRoot))
                return null;

            foreach (string file in Directory.GetFiles(_generationsRoot, "*.json"))
            {
                string json = File.ReadAllText(file);
                var manifest = JsonUtility.FromJson<GenerationManifest>(json);
                if (manifest != null && manifest.id == generationId)
                    return manifest;
            }
            return null;
        }

        /// <summary>
        /// 全世代マニフェストを時系列順（新しい順）で取得する。
        /// </summary>
        /// <returns>マニフェストのリスト。</returns>
        public List<GenerationManifest> LoadAll()
        {
            var result = new List<GenerationManifest>();
            if (!Directory.Exists(_generationsRoot))
                return result;

            string[] files = Directory.GetFiles(_generationsRoot, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string json = File.ReadAllText(files[i]);
                    var manifest = JsonUtility.FromJson<GenerationManifest>(json);
                    if (manifest != null)
                        result.Add(manifest);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ProjectBackupManager] マニフェスト読み込み失敗: {files[i]}\n{e.Message}");
                }
            }

            // 新しい順にソート
            result.Sort((a, b) => string.Compare(b.timestamp, a.timestamp, StringComparison.Ordinal));
            return result;
        }

        /// <summary>
        /// 指定IDの世代を削除する。
        /// </summary>
        /// <param name="generationId">削除する世代ID。</param>
        /// <returns>削除に成功した場合true。</returns>
        public bool Delete(string generationId)
        {
            if (!Directory.Exists(_generationsRoot))
                return false;

            foreach (string file in Directory.GetFiles(_generationsRoot, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var manifest = JsonUtility.FromJson<GenerationManifest>(json);
                    if (manifest != null && manifest.id == generationId)
                    {
                        File.Delete(file);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ProjectBackupManager] マニフェスト削除失敗: {file}\n{e.Message}");
                }
            }
            return false;
        }

        /// <summary>
        /// 最大世代数を超えた古い世代を削除し、不要オブジェクトをGCする。
        /// </summary>
        /// <param name="maxGenerations">保持する最大世代数。</param>
        public void Prune(int maxGenerations)
        {
            var all = LoadAll();
            if (all.Count <= maxGenerations)
                return;

            // 古い世代を削除
            for (int i = maxGenerations; i < all.Count; i++)
            {
                Delete(all[i].id);
                Debug.Log($"[ProjectBackupManager] 世代を削除: {all[i].timestamp} ({all[i].type})");
            }

            CollectGarbage();
        }

        /// <summary>
        /// 未参照オブジェクトを削除する。
        /// 全残存マニフェストの参照ハッシュを収集し、それ以外のオブジェクトを削除する。
        /// </summary>
        public void CollectGarbage()
        {
            var remainingGenerations = LoadAll();
            var referencedHashes = new HashSet<string>();
            for (int i = 0; i < remainingGenerations.Count; i++)
            {
                var hashes = remainingGenerations[i].CollectHashes();
                referencedHashes.UnionWith(hashes);
            }

            int removed = _contentStore.RemoveUnreferenced(referencedHashes);
            if (removed > 0)
            {
                Debug.Log($"[ProjectBackupManager] 未参照オブジェクトを{removed}件削除");
            }
        }

        /// <summary>
        /// マニフェストファイル名を生成する。
        /// </summary>
        private static string FormatFileName(GenerationManifest manifest)
        {
            var dto = DateTimeOffset.Parse(manifest.timestamp);
            string timeStr = dto.ToString("yyyyMMdd_HHmmss");
            return $"{timeStr}_{manifest.type}.json";
        }
    }
}
