using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowのプロファイル管理機能（切り替え、作成、削除など）を管理する分割クラス。
    /// 複数のユーザー環境（Cookieや設定）を切り替えて使用するためのロジックを提供します。
    /// </summary>
    public partial class MainWindow
    {
        // 現在アクティブなプロファイル名(初期値 default)
        private string _activeProfileName = "default";
        // プロファイル名の一覧（UIバインディング用）
        private readonly ObservableCollection<ProfileItem> _profileNames = new ObservableCollection<ProfileItem>();

        /// <summary>
        /// プロファイル選択UIを初期化します。
        /// </summary>
        private void InitializeProfilesUI()
        {
            // アプリ設定からプロファイル情報を読み込み
            LoadAppConfig();
            ProfileComboBox.ItemsSource = _profileNames;

            // プロファイル名一覧を取得
            var activeItem = _profileNames.FirstOrDefault(p => p.IsActive);
            // 選択中のプロファイルを設定
            ProfileComboBox.SelectedItem = activeItem ?? _profileNames.FirstOrDefault();
        }

        /// <summary>
        /// プロファイルを新規作成し、切り替えます。
        /// </summary>
        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var inputDlg = new InputWindow("新規プロファイル", "プロファイル名を入力:");
            inputDlg.Owner = this;

            if (inputDlg.ShowDialog() == true)
            {
                string newName = inputDlg.InputText?.Trim() ?? "";

                if (!IsValidProfileName(newName)) return;
            // 新規プロファイルの設定ファイルとデータフォルダを作成
            var newItem = new ProfileItem { Name = newName!, IsActive = false };
                _profileNames.Add(newItem);
            // 必要に応じてデフォルト設定をコピー
                SaveAppConfig();

            // プロファイル選択UIを更新
                ProfileComboBox.SelectedItem = newItem;
            // ユーザーに通知
                MessageWindow.Show($"プロファイル「{newName}」を作成しました。\n新しいプロファイルに切り替えます（再起動）。", "作成完了");

                PerformProfileSwitch(newName);
            }
        }

        /// <summary>
        /// 別窓で起動ボタンクリック時の処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LaunchNewWindow_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProfileSelectionWindow(
                _profileNames,
                _activeProfileName,
                "別ウィンドウで起動",
                "新しいウィンドウで起動するプロファイルを選択してください:",
                "起動");

            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                string targetProfile = dlg.SelectedProfileName;
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (exePath != null) Process.Start(exePath, $"--profile \"{targetProfile}\"");
                }
                catch (Exception ex)
                {
                    MessageWindow.Show($"新しいウィンドウの起動に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 名前変更ボタンクリック時の処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProfileSelectionWindow(
                _profileNames,
                _activeProfileName,
                "プロファイル名の変更",
                "名前を変更するプロファイルを選択してください:",
                "選択");

            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                string targetProfileName = dlg.SelectedProfileName;

                if (targetProfileName == _activeProfileName)
                {
                    MessageWindow.Show(
                        $"現在使用中のプロファイル「{targetProfileName}」は名前変更できません。\n(※起動中のプロファイルは操作できません)",
                        "変更できません", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var inputDlg = new InputWindow("名前の変更", "新しいプロファイル名を入力してください:", targetProfileName);
                inputDlg.Owner = this;

                if (inputDlg.ShowDialog() == true)
                {
                    string newName = inputDlg.InputText?.Trim() ?? "";

                    if (string.IsNullOrEmpty(newName)) return;
                    if (newName == targetProfileName) return;
                    if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                    {
                        MessageWindow.Show("プロファイル名に使用できない文字が含まれています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    try
                    {
                        // 1. 設定ファイル (.dat) のリネーム
                        string oldSettingsPath = GetProfilePath(targetProfileName);
                        string newSettingsPath = GetProfilePath(newName);

                        if (System.IO.File.Exists(newSettingsPath))
                        {
                            MessageWindow.Show($"プロファイル「{newName}」は既に存在します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (System.IO.File.Exists(oldSettingsPath))
                        {
                            System.IO.File.Move(oldSettingsPath, newSettingsPath);
                        }

                        // 2. データフォルダのリネーム
                        string oldDataPath = System.IO.Path.Combine(_userDataFolder, "BrowserData", targetProfileName);
                        string newDataPath = System.IO.Path.Combine(_userDataFolder, "BrowserData", newName);

                        if (System.IO.Directory.Exists(oldDataPath))
                        {
                            if (!System.IO.Directory.Exists(newDataPath))
                            {
                                System.IO.Directory.Move(oldDataPath, newDataPath);
                            }
                        }

                        // 3. リスト更新
                        var item = _profileNames.FirstOrDefault(p => p.Name == targetProfileName);
                        if (item != null)
                        {
                            item.Name = newName;
                        }
                        SaveAppConfig();

                        MessageWindow.Show($"プロファイル名を「{newName}」に変更しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (System.Exception ex)
                    {
                        MessageWindow.Show($"名前の変更に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 「削除」ボタンクリック時の処理。
        /// プロファイルをリストから削除し、関連データも消去します。
        /// </summary>
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProfileSelectionWindow(
                _profileNames,
                _activeProfileName,
                "プロファイルの削除",
                "削除するプロファイルを選択してください:\n(※元に戻せません)",
                "削除");

            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                // 空白除去を確実に行う
                string targetProfileName = dlg.SelectedProfileName?.Trim() ?? "";

                if (string.IsNullOrEmpty(targetProfileName)) return;

                if (targetProfileName == _activeProfileName)
                {
                    MessageWindow.Show(
                        $"現在使用中のプロファイル「{targetProfileName}」は削除できません。",
                        "削除できません", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageWindow.Show(
                    $"プロファイル「{targetProfileName}」を本当に削除しますか？\n設定やカラム情報がすべて失われます。",
                    "完全削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 設定ファイル(.dat)のパス取得
                        string settingsPath = GetProfilePath(targetProfileName);

                        // ファイルが存在する場合、念のため読み取り専用属性を外して削除を試みる
                        if (File.Exists(settingsPath))
                        {
                            try
                            {
                                File.SetAttributes(settingsPath, FileAttributes.Normal);
                            }
                            catch { /* 無視 */ }
                        }

                        // File.Existsチェックをせずに削除実行
                        // (存在しない場合は例外が出ずスルーされるため安全)
                        File.Delete(settingsPath);

                        // データフォルダ削除
                        string dataPath = Path.Combine(_userDataFolder, "BrowserData", targetProfileName);
                        if (Directory.Exists(dataPath))
                        {
                            Directory.Delete(dataPath, true);
                        }

                        // メモリ上のリストから削除
                        var itemToRemove = _profileNames.FirstOrDefault(p => p.Name == targetProfileName);
                        if (itemToRemove != null)
                        {
                            _profileNames.Remove(itemToRemove);
                        }

                        // 構成ファイル(app_config.json)を更新
                        SaveAppConfig();

                        MessageWindow.Show($"プロファイル「{targetProfileName}」を削除しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageWindow.Show($"削除中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// プロファイルを実際に切り替える処理。
        /// 設定を保存し、次回の起動プロファイルを指定してアプリを再起動します。
        /// WebView2のデータフォルダを切り替えるには、プロセスごとの初期化が必要なためです。
        /// </summary>
        /// <param name="targetProfileName">切り替え先のプロファイル名</param>
        private void SwitchProfile_Click(object sender, RoutedEventArgs e)
        {
            // 選択中のプロファイル名を取得
            var selectedItem = ProfileComboBox.SelectedItem as ProfileItem;
            string? selectedProfile = selectedItem?.Name;

            // 入力値の検証
            if (string.IsNullOrEmpty(selectedProfile) || selectedProfile == _activeProfileName)
            {
                return;
            }

            PerformProfileSwitch(selectedProfile);
        }

        /// <summary>
        /// プロファイルを切り替えてアプリを再起動します。
        /// </summary>
        private void PerformProfileSwitch(string targetProfileName)
        {
            // 現在の設定を保存
            SaveSettings(_activeProfileName);

            // アクティブプロファイル名を更新して保存
            _activeProfileName = targetProfileName;
            SaveAppConfig();

            // アプリを再起動
            _isRestarting = true;
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null) Process.Start(exe);
            System.Windows.Application.Current.Shutdown();
        }


        /// <summary>
        /// プロファイル選択コンボボックスの選択項目が変更された際に発生するイベントハンドラ。
        /// 基本的にプロファイルの切り替えは「切り替え」ボタンで行うため、ここでは
        /// 即時の切り替え処理は行いませんが、選択状態の追跡やUIの更新が必要な場合に使用します。
        /// </summary>
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        /// <summary>
        /// 指定されたプロファイル名がファイル名として有効かどうかを検証します。
        /// </summary>
        /// <param name="name">検証対象のプロファイル名。</param>
        /// <returns>ファイル名に使用できない文字が含まれていなければ true、それ以外は false。</returns>
        private bool IsValidProfileName(string? name)
        {
            // 空文字・空白のみは禁止
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageWindow.Show("使用できない文字が含まれています。", "エラー");
                return false;
            }
            if (_profileNames.Any(p => p.Name == name))
            {
                MessageWindow.Show("その名前は既に使用されています。", "エラー");
                return false;
            }
            return true;
        }
    }
}