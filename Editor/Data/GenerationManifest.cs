// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System;
using System.Collections.Generic;

namespace MutouLab.ProjectBackupManager.Data
{
    /// <summary>
    /// 世代マニフェスト。バックアップ時点のファイル構成を記録する。
    /// </summary>
    [Serializable]
    internal class GenerationManifest
    {
        /// <summary>世代の一意識別子。</summary>
        public string id;

        /// <summary>バックアップ作成時刻（ISO 8601形式）。</summary>
        public string timestamp;

        /// <summary>バックアップ種別（"auto" または "manual"）。</summary>
        public string type;

        /// <summary>ユーザー向けラベル。</summary>
        public string label;

        /// <summary>バックアップ対象ファイルの合計サイズ（バイト）。</summary>
        public long totalSize;

        /// <summary>バックアップ対象ファイル数。</summary>
        public int fileCount;

        /// <summary>ファイルエントリ一覧。</summary>
        public List<FileEntry> files = new List<FileEntry>();

        /// <summary>
        /// 新しい世代マニフェストを作成する。
        /// </summary>
        /// <param name="backupType">バックアップ種別。</param>
        /// <param name="backupLabel">ユーザー向けラベル。</param>
        /// <returns>初期化されたマニフェスト。</returns>
        public static GenerationManifest Create(string backupType, string backupLabel)
        {
            return new GenerationManifest
            {
                id = Guid.NewGuid().ToString(),
                timestamp = DateTimeOffset.Now.ToString("o"),
                type = backupType,
                label = backupLabel,
                totalSize = 0,
                fileCount = 0,
                files = new List<FileEntry>()
            };
        }

        /// <summary>
        /// ファイルパスからエントリを検索する。
        /// </summary>
        /// <param name="relativePath">プロジェクトルートからの相対パス。</param>
        /// <returns>該当するエントリ。見つからなければnull。</returns>
        public FileEntry FindEntry(string relativePath)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].path == relativePath)
                    return files[i];
            }
            return null;
        }

        /// <summary>
        /// 全エントリのハッシュ値をセットとして返す。
        /// </summary>
        /// <returns>ハッシュ値のセット。</returns>
        public HashSet<string> CollectHashes()
        {
            var hashes = new HashSet<string>();
            for (int i = 0; i < files.Count; i++)
            {
                hashes.Add(files[i].hash);
            }
            return hashes;
        }
    }

    /// <summary>
    /// バックアップ対象の個別ファイル情報。
    /// </summary>
    [Serializable]
    internal class FileEntry
    {
        /// <summary>プロジェクトルートからの相対パス。</summary>
        public string path;

        /// <summary>ファイル内容のSHA-256ハッシュ値（16進数小文字）。</summary>
        public string hash;

        /// <summary>ファイルサイズ（バイト）。</summary>
        public long size;

        /// <summary>最終更新日時（ISO 8601形式）。</summary>
        public string lastModified;
    }
}
