using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
        /// プロファイル名を取得します。
        /// </summary>
        private string? GetTargetProfileName(object sender)
        {
            if (sender is FrameworkElement item && item.Tag is string name) return name;
            return null;
        }

        /// <summary>
        /// プロファイルの追加処理を行います。
        /// </summary>
        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var inputDlg = new InputWindow(Properties.Resources.Title_NewProfile, Properties.Resources.Prompt_NewProfile);
            inputDlg.Owner = this;

            if (inputDlg.ShowDialog() == true)
            {
                string newName = inputDlg.InputText?.Trim() ?? "";
                if (!IsValidProfileName(newName)) return;
            // 新規プロファイルの設定ファイルとデータフォルダを作成
                var newItem = new ProfileItem { Name = newName, IsActive = false };
                _profileNames.Add(newItem);
            // 必要に応じてデフォルト設定をコピー
                SaveAppConfig();

                // リソースを使用しメッセージを表示
                string msg = string.Format(Properties.Resources.Msg_ProfileCreated, newName);
                if (MessageWindow.Show(msg, Properties.Resources.Title_Created, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    PerformProfileSwitch(newName);
                }
            }
        }

        /// <summary>
        /// プロファイル変更時の処理
        /// </summary>
        private void SwitchProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (!string.IsNullOrEmpty(targetProfile) && targetProfile != _activeProfileName)
            {
                PerformProfileSwitch(targetProfile);
            }
        }

        /// <summary>
        /// 名前変更ボタンクリック時の処理
        /// </summary>
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            if (targetProfile == _activeProfileName)
            {
                SaveSettings(_activeProfileName);
            }

            var inputDlg = new InputWindow(Properties.Resources.Title_RenameProfile, Properties.Resources.Prompt_RenameProfile, targetProfile);
            inputDlg.Owner = this;

            if (inputDlg.ShowDialog() == true)
            {
                string newName = inputDlg.InputText?.Trim() ?? "";

                if (string.IsNullOrEmpty(newName)) return;
                if (newName == targetProfile) return;
                if (!IsValidProfileName(newName)) return;

                try
                {
                    string oldSettingsPath = GetProfilePath(targetProfile);
                    string newSettingsPath = GetProfilePath(newName);

                    // リソースを使用してメッセージを表示
                    if (File.Exists(newSettingsPath))
                    {
                        string msgExists = string.Format(Properties.Resources.Msg_Err_ProfileExists, newName);
                        MessageWindow.Show(msgExists, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 設定ファイルの名前を変更
                    if (File.Exists(oldSettingsPath)) File.Move(oldSettingsPath, newSettingsPath);
                    // ブラウザデータフォルダの名前を変更
                    string oldDataPath = Path.Combine(_userDataFolder, "BrowserData", targetProfile);
                    string newDataPath = Path.Combine(_userDataFolder, "BrowserData", newName);

                    // フォルダが存在する場合のみ移動
                    if (Directory.Exists(oldDataPath))
                    {
                        if (!Directory.Exists(newDataPath)) Directory.Move(oldDataPath, newDataPath);
                    }

                    var item = _profileNames.FirstOrDefault(p => p.Name == targetProfile);
                    if (item != null) item.Name = newName;

                    if (_activeProfileName == targetProfile) _activeProfileName = newName;

                    SaveAppConfig();

                    string msg = string.Format(Properties.Resources.Msg_ProfileRenamed, newName);
                    MessageWindow.Show(msg, Properties.Resources.Title_Complete, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    string msg = string.Format(Properties.Resources.Msg_Err_RenameFailed, ex.Message);
                    MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        /// <summary>
        /// プロファイルの複製処理を行います。
        /// </summary>
        private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            var inputWin = new InputWindow(Properties.Resources.Title_DuplicateProfile, Properties.Resources.Prompt_NewProfile, $"Copy of {targetProfile}");
            inputWin.Owner = this;

            if (inputWin.ShowDialog() == true)
            {
                string newName = inputWin.InputText?.Trim() ?? "";
                if (!IsValidProfileName(newName)) return;

                try
                {
                    bool isActiveProfile = (targetProfile == _activeProfileName);

                    if (isActiveProfile) SaveSettings(_activeProfileName);

                    string srcSettingsPath = GetProfilePath(targetProfile);
                    string destSettingsPath = GetProfilePath(newName);

                    if (File.Exists(srcSettingsPath)) File.Copy(srcSettingsPath, destSettingsPath);
                    else SaveAppSettingsToFile(newName, new AppSettings());

                    string srcDataPath = Path.Combine(_userDataFolder, "BrowserData", targetProfile);
                    string destDataPath = Path.Combine(_userDataFolder, "BrowserData", newName);

                    _profileNames.Add(new ProfileItem { Name = newName, IsActive = false });
                    SaveAppConfig();

                    if (isActiveProfile)
                    {
                        // ★修正: リソースを使用
                        string msgConfirm = string.Format(Properties.Resources.Msg_ConfirmCloneActiveProfile, targetProfile);

                        if (MessageWindow.Show(msgConfirm, Properties.Resources.Title_RestartConfirm, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            var info = new { SourcePath = srcDataPath, DestPath = destDataPath };
                            string json = JsonSerializer.Serialize(info);
                            File.WriteAllText(Path.Combine(_userDataFolder, "pending_clone.json"), json);

                            _activeProfileName = newName;
                            SaveAppConfig();

                            _isRestarting = true;
                            var exe = Process.GetCurrentProcess().MainModule?.FileName;
                            if (exe != null) Process.Start(exe);
                            System.Windows.Application.Current.Shutdown();
                        }
                        else
                        {
                            var added = _profileNames.FirstOrDefault(p => p.Name == newName);
                            if (added != null) _profileNames.Remove(added);
                            if (File.Exists(destSettingsPath)) File.Delete(destSettingsPath);
                            SaveAppConfig();
                        }
                    }
                    else
                    {
                        if (Directory.Exists(srcDataPath)) CopyDirectory(srcDataPath, destDataPath);

                        string msg = string.Format(Properties.Resources.Msg_ProfileCreated, newName);
                        if (MessageWindow.Show(msg, Properties.Resources.Title_Complete, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            PerformProfileSwitch(newName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    string msg = string.Format(Properties.Resources.Msg_Err_ProfileCloneFailed, ex.Message);
                    MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// プロファイルの削除処理を行います。
        /// </summary>
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            if (targetProfile == _activeProfileName)
            {
                string msg = string.Format(Properties.Resources.Msg_Err_CannotDeleteActive, targetProfile);
                MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmMsg = string.Format(Properties.Resources.Msg_ConfirmDeleteProfile, targetProfile);
            if (MessageWindow.Show(confirmMsg, Properties.Resources.Title_ConfirmDelete, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    string settingsPath = GetProfilePath(targetProfile);
                    if (File.Exists(settingsPath)) File.Delete(settingsPath);

                    string dataPath = Path.Combine(_userDataFolder, "BrowserData", targetProfile);
                    if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true);

                    var itemToRemove = _profileNames.FirstOrDefault(p => p.Name == targetProfile);
                    if (itemToRemove != null) _profileNames.Remove(itemToRemove);

                    SaveAppConfig();

                    string doneMsg = string.Format(Properties.Resources.Msg_ProfileDeleted, targetProfile);
                    MessageWindow.Show(doneMsg, Properties.Resources.Title_Complete, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    string msg = string.Format(Properties.Resources.Msg_Err_DeleteFailed, ex.Message);
                    MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 別窓で起動ボタンクリック時の処理
        /// </summary>
        private void LaunchNewWindow_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName,
                    Arguments = $"--profile \"{targetProfile}\"",
                    UseShellExecute = true
                };
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                string msg = string.Format(Properties.Resources.Msg_Err_LaunchFailed, ex.Message);
                MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// プロファイルを切り替え、アプリケーションを再起動します。
        /// </summary>
        /// <param name="targetProfileName"></param>
        private void PerformProfileSwitch(string targetProfileName)
        {
            SaveSettings(_activeProfileName);
            _activeProfileName = targetProfileName;
            SaveAppConfig();

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
                MessageWindow.Show(Properties.Resources.Msg_Err_ProfileNameInvalid, Properties.Resources.Title_Error);
                return false;
            }
            if (_profileNames.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageWindow.Show(string.Format(Properties.Resources.Msg_Err_ProfileExists, name), Properties.Resources.Title_Error);
                return false;
            }
            return true;
        }
        /// <summary>
        /// 設定フォルダのコピーを再帰的に行います。
        /// </summary>

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Name.Equals("lockfile", StringComparison.OrdinalIgnoreCase)) continue;
                CopyFileRobust(file.FullName, Path.Combine(destinationDir, file.Name));
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
            }
        }

        /// <summary>
        /// ファイルのコピーを堅牢に行います。通常のコピーで失敗した場合、
        /// </summary>
        private void CopyFileRobust(string src, string dest)
        {
            try
            {
                File.Copy(src, dest, true);
            }
            catch (IOException)
            {
                try
                {
                    using (var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }
                catch { }
            }
            catch { }
        }
    }
}