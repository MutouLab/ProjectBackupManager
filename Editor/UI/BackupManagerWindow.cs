// Copyright (c) 2026 MutouLab. Licensed under the MIT License.
// See LICENSE.md in the project root for full license text.
using System;
using System.Collections.Generic;
using MutouLab.ProjectBackupManager.Core;
using MutouLab.ProjectBackupManager.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MutouLab.ProjectBackupManager.UI
{
    /// <summary>
    /// Project Backup Managerのメインエディタウィンドウ。
    /// UIとロジックを分離し、このクラスはイベント中継と表示更新のみを担当する。
    /// </summary>
    internal class BackupManagerWindow : EditorWindow
    {
        // UI要素参照
        private Toggle _autoBackupToggle;
        private IntegerField _intervalField;
        private IntegerField _maxGenerationsField;
        private Button _storePathBrowseBtn;
        private Label _storePathResolved;
        private Button _backupNowBtn;
        private Label _lastBackupLabel;
        private Label _nextBackupLabel;
        private Label _storeSizeLabel;
        private ScrollView _generationList;
        private Label _noGenerationsLabel;
        private VisualElement _restorePanel;
        private Label _restoreHeader;
        private ScrollView _fileTree;
        private Button _restoreSelectedBtn;
        private Button _restoreAllBtn;
        private Button _rollbackBtn;

        // タブUI要素
        private Button _tabListBtn;
        private Button _tabSettingsBtn;
        private Button _tabHelpBtn;
        private VisualElement _tabListContent;
        private VisualElement _tabSettingsContent;
        private VisualElement _tabHelpContent;
        private Label _helpText;

        // 状態
        private BackupEngine _engine;
        private string _selectedGenerationId;
        private GenerationManifest _selectedManifest;
        private readonly HashSet<string> _selectedFilePaths = new HashSet<string>();

        [MenuItem("MutouLab/Project Backup Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<BackupManagerWindow>();
            window.titleContent = new GUIContent("Backup Manager");
            window.minSize = new Vector2(780, 560);
            window.maxSize = new Vector2(780, 560);
        }

        public void CreateGUI()
        {
            // UXML/USSのロード
            string packagePath = "Packages/com.mutoulab.project-backup-manager/Editor/UI/";
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(packagePath + "BackupManagerWindow.uxml");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath + "BackupManagerWindow.uss");

            if (visualTree == null)
            {
                Debug.LogError("[ProjectBackupManager] UXMLの読み込みに失敗しました");
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            // 要素バインド
            BindElements();

            // 初期値設定
            LoadSettings();

            // イベント登録
            RegisterCallbacks();

            // エンジン初期化
            _engine = new BackupEngine(BackupScheduler.GetProjectRoot());

            // 世代一覧を表示
            RefreshGenerationList();

            // ステータス更新タイマー
            rootVisualElement.schedule.Execute(UpdateStatus).Every(5000);

            // スケジューラのイベント購読
            BackupScheduler.OnBackupCompleted += OnAutoBackupCompleted;
        }

        private void OnDestroy()
        {
            BackupScheduler.OnBackupCompleted -= OnAutoBackupCompleted;
        }

        /// <summary>
        /// UXMLの名前付き要素をフィールドにバインドする。
        /// </summary>
        private void BindElements()
        {
            _autoBackupToggle = rootVisualElement.Q<Toggle>("auto-backup-toggle");
            _intervalField = rootVisualElement.Q<IntegerField>("interval-field");
            _maxGenerationsField = rootVisualElement.Q<IntegerField>("max-generations-field");
            _storePathBrowseBtn = rootVisualElement.Q<Button>("store-path-browse-btn");
            _backupNowBtn = rootVisualElement.Q<Button>("backup-now-btn");
            _lastBackupLabel = rootVisualElement.Q<Label>("last-backup-label");
            _nextBackupLabel = rootVisualElement.Q<Label>("next-backup-label");
            _storeSizeLabel = rootVisualElement.Q<Label>("store-size-label");
            _generationList = rootVisualElement.Q<ScrollView>("generation-list");
            _noGenerationsLabel = rootVisualElement.Q<Label>("no-generations-label");
            _restorePanel = rootVisualElement.Q<VisualElement>("restore-panel");
            _restoreHeader = rootVisualElement.Q<Label>("restore-header");
            _fileTree = rootVisualElement.Q<ScrollView>("file-tree");
            _restoreSelectedBtn = rootVisualElement.Q<Button>("restore-selected-btn");
            _restoreAllBtn = rootVisualElement.Q<Button>("restore-all-btn");
            _rollbackBtn = rootVisualElement.Q<Button>("rollback-btn");

            // タブ要素
            _tabListBtn = rootVisualElement.Q<Button>("tab-list-btn");
            _tabSettingsBtn = rootVisualElement.Q<Button>("tab-settings-btn");
            _tabHelpBtn = rootVisualElement.Q<Button>("tab-help-btn");
            _tabListContent = rootVisualElement.Q<VisualElement>("tab-list-content");
            _tabSettingsContent = rootVisualElement.Q<VisualElement>("tab-settings-content");
            _tabHelpContent = rootVisualElement.Q<VisualElement>("tab-help-content");
            _storePathResolved = rootVisualElement.Q<Label>("store-path-resolved");
            _helpText = rootVisualElement.Q<Label>("help-text");
            _helpText.text = GetHelpContent();
        }

        /// <summary>
        /// 設定値をUIに反映する。
        /// </summary>
        private void LoadSettings()
        {
            _autoBackupToggle.value = BackupSettings.AutoBackupEnabled;
            _intervalField.value = BackupSettings.IntervalMinutes;
            _maxGenerationsField.value = BackupSettings.MaxGenerations;
            UpdateStorePathHint(BackupSettings.StorePath);
        }

        /// <summary>
        /// UIイベントコールバックを登録する。
        /// </summary>
        private void RegisterCallbacks()
        {
            // タブ切り替え
            _tabListBtn.clicked += () => SwitchTab(0);
            _tabSettingsBtn.clicked += () => SwitchTab(1);
            _tabHelpBtn.clicked += () => SwitchTab(2);

            // 設定
            _autoBackupToggle.RegisterValueChangedCallback(evt =>
                BackupSettings.AutoBackupEnabled = evt.newValue);

            _intervalField.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Max(1, evt.newValue);
                BackupSettings.IntervalMinutes = value;
                if (value != evt.newValue)
                    _intervalField.SetValueWithoutNotify(value);
            });

            _maxGenerationsField.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Clamp(evt.newValue, 1, 999);
                BackupSettings.MaxGenerations = value;
                if (value != evt.newValue)
                    _maxGenerationsField.SetValueWithoutNotify(value);
            });

            _storePathBrowseBtn.clicked += OnStorePathBrowseClicked;

            _backupNowBtn.clicked += OnBackupNowClicked;
            _restoreSelectedBtn.clicked += OnRestoreSelectedClicked;
            _restoreAllBtn.clicked += OnRestoreAllClicked;
            _rollbackBtn.clicked += OnRollbackClicked;
        }

        /// <summary>
        /// タブを切り替える。
        /// </summary>
        /// <param name="tabIndex">0: バックアップ一覧, 1: 詳細設定, 2: 使い方</param>
        private void SwitchTab(int tabIndex)
        {
            var contents = new[] { _tabListContent, _tabSettingsContent, _tabHelpContent };
            var buttons = new[] { _tabListBtn, _tabSettingsBtn, _tabHelpBtn };

            for (int i = 0; i < contents.Length; i++)
            {
                contents[i].style.display = i == tabIndex ? DisplayStyle.Flex : DisplayStyle.None;
                if (i == tabIndex)
                    buttons[i].AddToClassList("tab-btn-active");
                else
                    buttons[i].RemoveFromClassList("tab-btn-active");
            }
        }

        private static string GetHelpContent()
        {
            return
@"[ 基本操作 ]
「今すぐバックアップ」ボタンで現在のAssetsフォルダとシーンの状態を保存します。
バックアップはバックグラウンドで実行されるため、作業中のエディタ操作はブロックされません。

[ 自動バックアップ ]
詳細設定タブで有効にすると、指定間隔で自動的にバックアップを取得します。
未保存のシーン（Untitled）も自動的に保存されるため、
Unityの予期せぬ終了によるデータ紛失を防止できます。

[ 復元操作の違い ]
- 選択を復元
  チェックを入れたファイルのみをバックアップ時の状態に戻します。
  他のファイルは影響を受けません。

- 全て復元
  バックアップに含まれる全ファイルを書き戻します。
  バックアップ後に追加されたファイルはそのまま残ります。

- 完全ロールバック
  全ファイルを書き戻した上で、バックアップ後に追加された
  ファイルを検出し、削除するか確認します。
  プロジェクトをバックアップ時点の完全な状態に戻します。

[ 保存先 ]
バックアップデータはプロジェクトルート直下の BackupStore フォルダに
保存されます。詳細設定タブから変更可能です。

[ 世代管理 ]
古いバックアップは「最大世代数」を超えると自動的に削除されます。
同一内容のファイルは重複保存されず、ストア容量を節約します。";
        }

        /// <summary>
        /// ストアパスのフルパスをヒントラベルに表示する。
        /// </summary>
        private void UpdateStorePathHint(string relativePath)
        {
            string projectRoot = BackupScheduler.GetProjectRoot();
            string fullPath = System.IO.Path.Combine(projectRoot, relativePath);
            _storePathResolved.text = fullPath;
        }

        // ---- イベントハンドラ（Core層に委譲） ----

        private void OnStorePathBrowseClicked()
        {
            string projectRoot = BackupScheduler.GetProjectRoot();
            string oldStorePath = BackupSettings.StorePath;
            string oldFull = System.IO.Path.Combine(projectRoot, oldStorePath);
            string selected = EditorUtility.OpenFolderPanel("バックアップ保存先を選択", oldFull, "");
            if (string.IsNullOrEmpty(selected))
                return;

            // 新しいパスを決定
            string newStorePath;
            if (selected.StartsWith(projectRoot))
                newStorePath = selected.Substring(projectRoot.Length).TrimStart('/', '\\');
            else
                newStorePath = selected;

            // 同じパスなら何もしない
            string newFull = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, newStorePath));
            if (string.Equals(System.IO.Path.GetFullPath(oldFull), newFull, System.StringComparison.OrdinalIgnoreCase))
                return;

            // 既存データの移動
            if (System.IO.Directory.Exists(oldFull))
            {
                bool migrate = EditorUtility.DisplayDialog(
                    "バックアップデータの移動",
                    $"既存のバックアップデータを新しい保存先に移動しますか？\n\n移動元: {oldFull}\n移動先: {newFull}",
                    "移動する", "移動しない");

                if (migrate)
                {
                    try
                    {
                        if (System.IO.Directory.Exists(newFull))
                        {
                            EditorUtility.DisplayDialog(
                                "エラー",
                                "移動先のフォルダが既に存在します。\n別のフォルダを選択してください。",
                                "OK");
                            return;
                        }
                        System.IO.Directory.Move(oldFull, newFull);
                        Debug.Log($"[ProjectBackupManager] バックアップデータを移動しました: {oldFull} → {newFull}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ProjectBackupManager] バックアップデータの移動に失敗しました: {ex.Message}");
                        EditorUtility.DisplayDialog("エラー", $"データの移動に失敗しました:\n{ex.Message}", "OK");
                        return;
                    }
                }
            }

            BackupSettings.StorePath = newStorePath;
            UpdateStorePathHint(newStorePath);

            // エンジンを新しいパスで再初期化
            _engine = new BackupEngine(projectRoot);
            RefreshGenerationList();
            UpdateStatus();
        }

        private void OnBackupNowClicked()
        {
            _backupNowBtn.SetEnabled(false);
            _backupNowBtn.text = "バックアップ中...";
            _engine.CreateBackupInBackground("manual", "手動バックアップ", () =>
            {
                _backupNowBtn.SetEnabled(true);
                _backupNowBtn.text = "今すぐバックアップ";
                RefreshGenerationList();
                UpdateStatus();
            });
        }

        private void OnAutoBackupCompleted()
        {
            // メインスレッドで実行
            EditorApplication.delayCall += () =>
            {
                RefreshGenerationList();
                UpdateStatus();
            };
        }

        private void OnGenerationSelected(string generationId)
        {
            _selectedGenerationId = generationId;
            _selectedManifest = _engine.Generations.Load(generationId);
            _selectedFilePaths.Clear();

            if (_selectedManifest != null)
            {
                RefreshFileTree();
            }

            // カードの選択状態を更新
            _generationList.Query<VisualElement>(className: "generation-card").ForEach(card =>
            {
                string cardId = (string)card.userData;
                if (cardId == generationId)
                    card.AddToClassList("generation-card-selected");
                else
                    card.RemoveFromClassList("generation-card-selected");
            });
        }

        private void OnGenerationDeleteClicked(string generationId)
        {
            if (!EditorUtility.DisplayDialog(
                    "世代削除",
                    "このバックアップを削除しますか？",
                    "削除", "キャンセル"))
                return;

            _engine.Generations.Delete(generationId);

            if (_selectedGenerationId == generationId)
            {
                _selectedGenerationId = null;
                _selectedManifest = null;
                _fileTree.Clear();
                _restoreHeader.text = "復元";
            }

            // 削除した世代のオブジェクトをGC
            _engine.Generations.CollectGarbage();
            RefreshGenerationList();
        }

        private void OnRestoreSelectedClicked()
        {
            if (_selectedManifest == null || _selectedFilePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("復元", "復元するファイルを選択してください。", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "選択復元",
                    $"{_selectedFilePaths.Count}件のファイルを復元しますか？\n現在のファイルは上書きされます。",
                    "復元", "キャンセル"))
                return;

            var filePaths = new List<string>(_selectedFilePaths);
            _engine.RestoreFiles(_selectedGenerationId, filePaths);
        }

        private void OnRestoreAllClicked()
        {
            if (_selectedManifest == null) return;

            if (!EditorUtility.DisplayDialog(
                    "全ファイル復元",
                    $"この世代の{_selectedManifest.fileCount}件全てのファイルを復元しますか？\n現在のファイルは上書きされます。",
                    "復元", "キャンセル"))
                return;

            var allPaths = new List<string>();
            for (int i = 0; i < _selectedManifest.files.Count; i++)
                allPaths.Add(_selectedManifest.files[i].path);

            _engine.RestoreFiles(_selectedGenerationId, allPaths);
        }

        private void OnRollbackClicked()
        {
            if (_selectedManifest == null) return;

            if (!EditorUtility.DisplayDialog(
                    "完全ロールバック",
                    $"Assets/全体をこの世代の状態に戻します。\nこの操作は取り消せません。続行しますか？",
                    "ロールバック", "キャンセル"))
                return;

            int restored = _engine.Rollback(_selectedGenerationId, out var addedFiles);

            if (restored < 0) return;

            // バックアップ後に追加されたファイルの処理
            if (addedFiles.Count > 0)
            {
                string fileListStr = "";
                int displayCount = Mathf.Min(addedFiles.Count, 20);
                for (int i = 0; i < displayCount; i++)
                    fileListStr += addedFiles[i] + "\n";
                if (addedFiles.Count > displayCount)
                    fileListStr += $"... 他{addedFiles.Count - displayCount}件";

                if (EditorUtility.DisplayDialog(
                        "追加ファイルの処理",
                        $"バックアップ後に追加された{addedFiles.Count}件のファイルがあります。削除しますか？\n\n{fileListStr}",
                        "削除する", "残す"))
                {
                    _engine.DeleteFiles(addedFiles);
                }
            }

            RefreshGenerationList();
        }

        // ---- UI更新 ----

        /// <summary>
        /// 世代一覧を再構築する。
        /// </summary>
        private void RefreshGenerationList()
        {
            _generationList.Clear();
            var generations = _engine.Generations.LoadAll();

            bool hasGenerations = generations.Count > 0;
            _noGenerationsLabel.style.display = hasGenerations ? DisplayStyle.None : DisplayStyle.Flex;
            _generationList.style.display = hasGenerations ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < generations.Count; i++)
            {
                var gen = generations[i];
                var card = CreateGenerationCard(gen);
                _generationList.Add(card);
            }
        }

        /// <summary>
        /// 世代カードUIを生成する。
        /// </summary>
        private VisualElement CreateGenerationCard(GenerationManifest gen)
        {
            var card = new VisualElement();
            card.AddToClassList("generation-card");
            card.userData = gen.id;

            if (gen.id == _selectedGenerationId)
                card.AddToClassList("generation-card-selected");

            // 情報エリア
            var info = new VisualElement();
            info.AddToClassList("generation-info");

            var timestamp = new Label();
            timestamp.AddToClassList("generation-timestamp");
            if (DateTimeOffset.TryParse(gen.timestamp, out var dto))
                timestamp.text = dto.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            else
                timestamp.text = gen.timestamp;

            var detail = new Label();
            detail.AddToClassList("generation-detail");
            detail.AddToClassList(gen.type == "auto" ? "generation-type-auto" : "generation-type-manual");
            string typeLabel = gen.type == "auto" ? "自動" : "手動";
            detail.text = $"{typeLabel} | {gen.fileCount}ファイル | {BackupEngine.FormatSize(gen.totalSize)}";

            info.Add(timestamp);
            info.Add(detail);

            // 操作ボタン
            var actions = new VisualElement();
            actions.AddToClassList("generation-actions");

            var selectBtn = new Button(() => OnGenerationSelected(gen.id)) { text = "詳細" };
            selectBtn.AddToClassList("card-btn");

            var deleteBtn = new Button(() => OnGenerationDeleteClicked(gen.id)) { text = "削除" };
            deleteBtn.AddToClassList("card-btn");
            deleteBtn.AddToClassList("card-delete-btn");

            actions.Add(selectBtn);
            actions.Add(deleteBtn);

            card.Add(info);
            card.Add(actions);

            return card;
        }

        /// <summary>
        /// 復元パネルのファイルツリーを更新する。
        /// </summary>
        private void RefreshFileTree()
        {
            if (DateTimeOffset.TryParse(_selectedManifest.timestamp, out var dto))
                _restoreHeader.text = $"復元 — {dto.ToLocalTime():yyyy/MM/dd HH:mm:ss}";

            _fileTree.Clear();

            for (int i = 0; i < _selectedManifest.files.Count; i++)
            {
                var entry = _selectedManifest.files[i];
                var fileRow = new VisualElement();
                fileRow.AddToClassList("file-entry");

                var toggle = new Toggle();
                string entryPath = entry.path;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                        _selectedFilePaths.Add(entryPath);
                    else
                        _selectedFilePaths.Remove(entryPath);
                });

                var pathLabel = new Label(entry.path);
                pathLabel.AddToClassList("file-entry-path");

                var sizeLabel = new Label(BackupEngine.FormatSize(entry.size));
                sizeLabel.AddToClassList("file-entry-size");

                fileRow.Add(toggle);
                fileRow.Add(pathLabel);
                fileRow.Add(sizeLabel);
                _fileTree.Add(fileRow);
            }
        }

        /// <summary>
        /// ステータスバーを更新する。
        /// </summary>
        private void UpdateStatus()
        {
            var lastTime = BackupScheduler.LastBackupTime;
            _lastBackupLabel.text = lastTime == DateTimeOffset.MinValue
                ? "前回: ---"
                : $"前回: {lastTime.ToLocalTime():HH:mm:ss}";

            if (BackupSettings.AutoBackupEnabled)
            {
                var nextTime = BackupScheduler.NextBackupTime;
                _nextBackupLabel.text = $"次回: {nextTime.ToLocalTime():HH:mm:ss}";
            }
            else
            {
                _nextBackupLabel.text = "次回: 無効";
            }

            _storeSizeLabel.text = $"ストア: {GetStoreSize()}";
        }

        /// <summary>
        /// ストアの合計サイズを取得する。
        /// </summary>
        private string GetStoreSize()
        {
            string storePath = System.IO.Path.Combine(
                BackupScheduler.GetProjectRoot(), BackupSettings.StorePath);
            if (!System.IO.Directory.Exists(storePath))
                return "0 B";

            long totalSize = 0;
            foreach (var file in System.IO.Directory.GetFiles(storePath, "*", System.IO.SearchOption.AllDirectories))
            {
                try
                {
                    totalSize += new System.IO.FileInfo(file).Length;
                }
                catch
                {
                    // アクセス不可ファイルはスキップ
                }
            }
            return BackupEngine.FormatSize(totalSize);
        }
    }
}
