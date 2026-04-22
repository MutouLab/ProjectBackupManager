namespace MutouLab.ProjectBackupManager.Data
{
    /// <summary>
    /// バックアップシステム全体で使用する定数定義。
    /// </summary>
    internal static class BackupConstants
    {
        /// <summary>バックアップストアのデフォルト保存先（プロジェクトルート相対）。</summary>
        public const string DefaultStorePath = "BackupStore";

        /// <summary>コンテンツオブジェクト格納ディレクトリ名。</summary>
        public const string ObjectsDirectory = "objects";

        /// <summary>世代マニフェスト格納ディレクトリ名。</summary>
        public const string GenerationsDirectory = "generations";

        /// <summary>ストアメタデータファイル名。</summary>
        public const string StoreMetadataFile = "store.json";

        /// <summary>デフォルトの自動バックアップ間隔（分）。</summary>
        public const int DefaultIntervalMinutes = 10;

        /// <summary>デフォルトの最大世代保持数。</summary>
        public const int DefaultMaxGenerations = 12;

        /// <summary>EditorPrefsキーのプレフィックス。</summary>
        public const string PrefsPrefix = "MutouLab.ProjectBackupManager.";

        /// <summary>SHA-256ハッシュのディレクトリ分割に使用するプレフィックス長。</summary>
        public const int HashPrefixLength = 2;

        /// <summary>ハッシュ計算時のバッファサイズ（バイト）。</summary>
        public const int HashBufferSize = 81920;

        /// <summary>バックアップ対象のルートディレクトリ。</summary>
        public const string AssetsDirectory = "Assets";

        /// <summary>パッケージマニフェストのパス。</summary>
        public const string PackagesManifestPath = "Packages/manifest.json";

        /// <summary>VPMマニフェストのパス。</summary>
        public const string VpmManifestPath = "Packages/vpm-manifest.json";
    }
}
