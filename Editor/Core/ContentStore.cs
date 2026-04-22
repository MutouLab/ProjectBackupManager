// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System.IO;
using System.Security.Cryptography;
using MutouLab.ProjectBackupManager.Data;
using UnityEngine;

namespace MutouLab.ProjectBackupManager.Core
{
    /// <summary>
    /// コンテンツアドレッサブルファイルストア。
    /// SHA-256ハッシュをキーとしてファイルを格納し、重複排除を実現する。
    /// </summary>
    internal class ContentStore
    {
        private readonly string _objectsRoot;

        /// <summary>
        /// ContentStoreを初期化する。
        /// </summary>
        /// <param name="storeRoot">ストアルートディレクトリのパス。</param>
        public ContentStore(string storeRoot)
        {
            _objectsRoot = Path.Combine(storeRoot, BackupConstants.ObjectsDirectory);
        }

        /// <summary>
        /// ストアディレクトリを作成する（存在しない場合）。
        /// </summary>
        public void EnsureInitialized()
        {
            if (!Directory.Exists(_objectsRoot))
                Directory.CreateDirectory(_objectsRoot);
        }

        /// <summary>
        /// ファイルをストアに格納し、ハッシュ値を返す。
        /// 同一ハッシュのオブジェクトが既に存在する場合はコピーをスキップする。
        /// </summary>
        /// <param name="sourceFilePath">格納するファイルの絶対パス。</param>
        /// <returns>ファイル内容のSHA-256ハッシュ値（16進数小文字）。</returns>
        public string Store(string sourceFilePath)
        {
            string hash = ComputeHash(sourceFilePath);

            if (!Exists(hash))
            {
                string destPath = GetObjectPath(hash);
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                File.Copy(sourceFilePath, destPath, overwrite: false);
            }

            return hash;
        }

        /// <summary>
        /// ストアからファイルを復元する。
        /// </summary>
        /// <param name="hash">復元対象のハッシュ値。</param>
        /// <param name="destPath">復元先の絶対パス。</param>
        /// <returns>復元に成功した場合true。</returns>
        public bool Retrieve(string hash, string destPath)
        {
            string objectPath = GetObjectPath(hash);
            if (!File.Exists(objectPath))
            {
                Debug.LogError($"[ProjectBackupManager] オブジェクトが見つかりません: {hash}");
                return false;
            }

            string destDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(objectPath, destPath, overwrite: true);
            return true;
        }

        /// <summary>
        /// 指定ハッシュのオブジェクトが存在するかを確認する。
        /// </summary>
        /// <param name="hash">確認するハッシュ値。</param>
        /// <returns>存在する場合true。</returns>
        public bool Exists(string hash)
        {
            return File.Exists(GetObjectPath(hash));
        }

        /// <summary>
        /// 参照されていないオブジェクトを削除する。
        /// </summary>
        /// <param name="referencedHashes">現在参照されているハッシュ値のセット。</param>
        /// <returns>削除されたオブジェクト数。</returns>
        public int RemoveUnreferenced(System.Collections.Generic.HashSet<string> referencedHashes)
        {
            int removed = 0;

            if (!Directory.Exists(_objectsRoot))
                return removed;

            foreach (string prefixDir in Directory.GetDirectories(_objectsRoot))
            {
                foreach (string objectFile in Directory.GetFiles(prefixDir))
                {
                    string fileName = Path.GetFileName(objectFile);
                    string dirName = Path.GetFileName(prefixDir);
                    string hash = dirName + fileName;

                    if (!referencedHashes.Contains(hash))
                    {
                        File.Delete(objectFile);
                        removed++;
                    }
                }

                // 空のプレフィックスディレクトリを削除
                if (Directory.GetFiles(prefixDir).Length == 0 &&
                    Directory.GetDirectories(prefixDir).Length == 0)
                {
                    Directory.Delete(prefixDir);
                }
            }

            return removed;
        }

        /// <summary>
        /// ファイルのSHA-256ハッシュを計算する。
        /// </summary>
        /// <param name="filePath">対象ファイルの絶対パス。</param>
        /// <returns>SHA-256ハッシュ値（16進数小文字）。</returns>
        public static string ComputeHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       BackupConstants.HashBufferSize))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                var sb = new System.Text.StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// ハッシュ値からオブジェクトファイルのパスを組み立てる。
        /// </summary>
        private string GetObjectPath(string hash)
        {
            string prefix = hash.Substring(0, BackupConstants.HashPrefixLength);
            string remainder = hash.Substring(BackupConstants.HashPrefixLength);
            return Path.Combine(_objectsRoot, prefix, remainder);
        }
    }
}
