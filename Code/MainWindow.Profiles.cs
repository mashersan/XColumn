using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XColumn
{
    public partial class MainWindow
    {
        private string _activeProfileName = "default";
        private readonly ObservableCollection<string> _profileNames = new ObservableCollection<string>();

        private void InitializeProfilesUI()
        {
            LoadAppConfig();
            ProfileComboBox.ItemsSource = _profileNames;
            ProfileComboBox.SelectedItem = _activeProfileName;
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            string? newName = ShowInputWindow("新規プロファイル", "プロファイル名を入力:");
            if (!IsValidProfileName(newName)) return;

            _profileNames.Add(newName!);
            SaveAppConfig();

            ProfileComboBox.SelectedItem = newName;

            System.Windows.MessageBox.Show($"プロファイル「{newName}」を作成しました。\n新しいプロファイルに切り替えます（再起動）。", "作成完了");

            // 新規作成時は即座に切り替えを実行
            PerformProfileSwitch(newName!);
        }

        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            string? selectedProfile = ProfileComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedProfile)) return;

            if (selectedProfile == _activeProfileName)
            {
                System.Windows.MessageBox.Show("現在使用中のプロファイル名は変更できません。\n一度別のプロファイルに切り替えてください。", "制限");
                return;
            }

            string? newName = ShowInputWindow("名前の変更", $"「{selectedProfile}」の新しい名前:");
            if (!IsValidProfileName(newName)) return;

            try
            {
                string oldSet = GetProfilePath(selectedProfile);
                string newSet = GetProfilePath(newName!);
                if (File.Exists(oldSet)) File.Move(oldSet, newSet);

                string oldData = Path.Combine(_userDataFolder, "BrowserData", selectedProfile);
                string newData = Path.Combine(_userDataFolder, "BrowserData", newName!);
                if (Directory.Exists(oldData)) Directory.Move(oldData, newData);

                int index = _profileNames.IndexOf(selectedProfile);
                _profileNames[index] = newName!;
                ProfileComboBox.SelectedItem = newName;
                SaveAppConfig();

                System.Windows.MessageBox.Show("変更しました。", "完了");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"変更できませんでした: {ex.Message}", "エラー");
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            string? selectedProfile = ProfileComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedProfile)) return;

            if (selectedProfile == "default" && _profileNames.Count == 1)
            {
                System.Windows.MessageBox.Show("最後のプロファイルは削除できません。", "エラー");
                return;
            }

            if (selectedProfile == _activeProfileName)
            {
                System.Windows.MessageBox.Show("現在使用中のプロファイルは削除できません。", "制限");
                return;
            }

            if (System.Windows.MessageBox.Show($"プロファイル「{selectedProfile}」を削除しますか？\nこの操作は取り消せません。",
                "削除確認", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                string settingsPath = GetProfilePath(selectedProfile);
                if (File.Exists(settingsPath)) File.Delete(settingsPath);

                string dataPath = Path.Combine(_userDataFolder, "BrowserData", selectedProfile);
                if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true);

                _profileNames.Remove(selectedProfile);
                ProfileComboBox.SelectedItem = _activeProfileName;
                SaveAppConfig();

                System.Windows.MessageBox.Show("削除しました。", "完了");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"削除できませんでした: {ex.Message}", "エラー");
            }
        }

        private void SwitchProfile_Click(object sender, RoutedEventArgs e)
        {
            string? selectedProfile = ProfileComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedProfile) || selectedProfile == _activeProfileName)
            {
                System.Windows.MessageBox.Show("既にこのプロファイルを使用中です。", "通知");
                return;
            }

            if (System.Windows.MessageBox.Show($"「{selectedProfile}」に切り替えますか？\nアプリが再起動します。",
                "切替確認", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
            {
                PerformProfileSwitch(selectedProfile);
            }
            else
            {
                ProfileComboBox.SelectedItem = _activeProfileName;
            }
        }

        /// <summary>
        /// プロファイルを切り替えて再起動します。
        /// </summary>
        private void PerformProfileSwitch(string targetProfileName)
        {
            SaveSettings(_activeProfileName);

            _activeProfileName = targetProfileName;
            SaveAppConfig();

            // ★重要: 再起動フラグON (Closingイベントでの上書き保存を防ぐため)
            _isRestarting = true;

            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null) Process.Start(exe);
            System.Windows.Application.Current.Shutdown();
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private bool IsValidProfileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                System.Windows.MessageBox.Show("使用できない文字が含まれています。", "エラー");
                return false;
            }
            if (_profileNames.Contains(name))
            {
                System.Windows.MessageBox.Show("その名前は既に使用されています。", "エラー");
                return false;
            }
            return true;
        }
    }
}