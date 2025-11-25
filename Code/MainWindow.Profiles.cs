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
            // プロファイル名入力ダイアログを表示
            string? newName = ShowInputWindow("新規プロファイル", "プロファイル名を入力:");
            // 入力値の検証
            if (!IsValidProfileName(newName)) return;
            // 新規プロファイルの設定ファイルとデータフォルダを作成
            var newItem = new ProfileItem { Name = newName!, IsActive = false };
            _profileNames.Add(newItem);
            // 必要に応じてデフォルト設定をコピー
            SaveAppConfig();

            // プロファイル選択UIを更新
            ProfileComboBox.SelectedItem = newItem;
            // ユーザーに通知
            System.Windows.MessageBox.Show($"プロファイル「{newName}」を作成しました。\n新しいプロファイルに切り替えます（再起動）。", "作成完了");

            // 新規プロファイルに切り替え
            PerformProfileSwitch(newName!);
        }

        /// <summary>
        /// 「名前変更」ボタンクリック時の処理。
        /// 現在選択中のプロファイル（ただしアクティブでないもの）の名前を変更します。
        /// </summary>
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            // 選択中のプロファイル名を取得
            var selectedItem = ProfileComboBox.SelectedItem as ProfileItem;
            string? selectedProfile = selectedItem?.Name;

            // 入力値の検証
            if (string.IsNullOrEmpty(selectedProfile)) return;

            // アクティブプロファイルは名前変更不可
            if (selectedProfile == _activeProfileName)
            {
                System.Windows.MessageBox.Show("現在使用中のプロファイル名は変更できません。\n一度別のプロファイルに切り替えてください。", "制限");
                return;
            }

            // 名前変更ダイアログを表示
            string? newName = ShowInputWindow("名前の変更", $"「{selectedProfile}」の新しい名前:");
            // 入力値の検証
            if (!IsValidProfileName(newName)) return;

            // プロファイル名の変更処理
            try
            {
                // 設定ファイルとデータフォルダの名前を変更
                string oldSet = GetProfilePath(selectedProfile);
                string newSet = GetProfilePath(newName!);
                // ファイル/フォルダの移動
                if (File.Exists(oldSet)) File.Move(oldSet, newSet);

                // データフォルダの移動
                string oldData = Path.Combine(_userDataFolder, "BrowserData", selectedProfile);
                string newData = Path.Combine(_userDataFolder, "BrowserData", newName!);

                // フォルダの移動
                if (Directory.Exists(oldData)) Directory.Move(oldData, newData);

                // プロファイル名リストを更新
                int index = _profileNames.IndexOf(selectedItem!);

                // プロファイル名を更新
                if (index >= 0)
                {
                    _profileNames[index] = new ProfileItem { Name = newName!, IsActive = false };
                    ProfileComboBox.SelectedItem = _profileNames[index];
                }
                // アプリ設定を保存
                SaveAppConfig();

                // ユーザーに通知
                System.Windows.MessageBox.Show("変更しました。", "完了");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"変更できませんでした: {ex.Message}\n(ブラウザがフォルダをロックしている可能性があります)", "エラー");
            }
        }

        /// <summary>
        /// 「削除」ボタンクリック時の処理。
        /// プロファイルをリストから削除し、関連データも消去します。
        /// </summary>
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            // 選択中のプロファイル名を取得
            var selectedItem = ProfileComboBox.SelectedItem as ProfileItem;
            string? selectedProfile = selectedItem?.Name;

            // 入力値の検証
            if (string.IsNullOrEmpty(selectedProfile)) return;

            // 最後のプロファイルは削除不可
            if (selectedProfile == "default" && _profileNames.Count == 1)
            {
                System.Windows.MessageBox.Show("最後のプロファイルは削除できません。", "エラー");
                return;
            }

            // アクティブプロファイルは削除不可
            if (selectedProfile == _activeProfileName)
            {
                System.Windows.MessageBox.Show("現在使用中のプロファイルは削除できません。", "制限");
                return;
            }

            // 削除確認ダイアログを表示
            if (System.Windows.MessageBox.Show($"プロファイル「{selectedProfile}」を削除しますか？\nこの操作は取り消せません。",
                "削除確認", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            // プロファイル削除処理
            try
            {
                // 設定ファイルを削除
                string settingsPath = GetProfilePath(selectedProfile);
                if (File.Exists(settingsPath)) File.Delete(settingsPath);

                // データフォルダを削除
                string dataPath = Path.Combine(_userDataFolder, "BrowserData", selectedProfile);
                if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true);

                // プロファイル名リストを更新
                _profileNames.Remove(selectedItem!);

                // アプリ設定を保存
                ProfileComboBox.SelectedItem = _profileNames.FirstOrDefault(p => p.IsActive);
                SaveAppConfig();

                // ユーザーに通知
                System.Windows.MessageBox.Show("削除しました。", "完了");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"削除できませんでした: {ex.Message}", "エラー");
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
                System.Windows.MessageBox.Show("既にこのプロファイルを使用中です。", "通知");
                return;
            }

            // 切替確認ダイアログを表示
            if (System.Windows.MessageBox.Show($"「{selectedProfile}」に切り替えますか？\nアプリが再起動します。",
                "切替確認", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
            {
                PerformProfileSwitch(selectedProfile);
            }
            else
            {
                ProfileComboBox.SelectedItem = _profileNames.FirstOrDefault(p => p.IsActive);
            }
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
                System.Windows.MessageBox.Show("使用できない文字が含まれています。", "エラー");
                return false;
            }
            if (_profileNames.Any(p => p.Name == name))
            {
                System.Windows.MessageBox.Show("その名前は既に使用されています。", "エラー");
                return false;
            }
            return true;
        }
    }
}