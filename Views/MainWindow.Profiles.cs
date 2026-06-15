using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using XColumn.Models;
using XColumn.Services;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindow のプロファイル管理機能（切り替え・作成・名前変更・複製・削除）を管理する分割クラス。
    /// 複数のユーザー環境（CookieやWebViewデータ・設定）を切り替えて使用するためのロジックを提供します。
    /// 実際のファイル操作は IProfileService に委譲し、本クラスはダイアログ表示・再起動・UI更新を担当します。
    /// プロファイル切り替えはアプリの再起動を伴います。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Fields =====

        /// <summary>
        /// 現在アクティブなプロファイル名（初期値 "default"）。
        /// </summary>
        private string _activeProfileName = "default";

        /// <summary>
        /// プロファイル名の一覧（UIバインディング用）。
        /// </summary>
        private readonly ObservableCollection<ProfileItem> _profileNames = new ObservableCollection<ProfileItem>();

        /// <summary>
        /// プロファイルのファイル操作を担うサービス（DIコンテナから取得）。
        /// </summary>
        private readonly IProfileService _profileService = App.Current.Services.GetRequiredService<IProfileService>();

        // ===== Initialization =====

        /// <summary>
        /// プロファイル選択UIを初期化します（アプリ構成の読み込みと選択状態の設定）。
        /// </summary>
        private void InitializeProfilesUI()
        {
            // アプリ構成(app_config.json)からプロファイル情報を読み込み
            LoadAppConfig();
            ProfileComboBox.ItemsSource = _profileNames;

            // アクティブなプロファイルを初期選択（無ければ先頭）
            var activeItem = _profileNames.FirstOrDefault(p => p.IsActive);
            ProfileComboBox.SelectedItem = activeItem ?? _profileNames.FirstOrDefault();
        }

        // ===== Event Handlers (Profile Operations) =====

        /// <summary>
        /// 「新規プロファイル作成」の処理。名前を入力させ、作成後に切り替えるか確認します。
        /// </summary>
        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var inputDlg = new InputWindow(Properties.Resources.Title_NewProfile, Properties.Resources.Prompt_NewProfile)
            {
                Owner = this
            };

            if (inputDlg.ShowDialog() != true) return;

            string newName = inputDlg.InputText?.Trim() ?? "";
            if (!IsValidProfileName(newName)) return;

            // プロファイル一覧へ追加し、アプリ構成を保存（実データフォルダは初回起動時に生成される）
            _profileNames.Add(new ProfileItem { Name = newName, IsActive = false });
            SaveAppConfig();

            // 作成後、すぐ切り替えるか確認
            string msg = string.Format(Properties.Resources.Msg_ProfileCreated, newName);
            if (MessageWindow.Show(msg, Properties.Resources.Title_Created, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                PerformProfileSwitch(newName);
            }
        }

        /// <summary>
        /// 「このプロファイルに切り替える」の処理。
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
        /// 「名前変更」の処理。設定ファイル(.dat)とブラウザデータフォルダを併せてリネームします。
        /// </summary>
        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            // 対象がアクティブプロファイルなら、リネーム前に最新状態を保存しておく
            if (targetProfile == _activeProfileName)
            {
                SaveSettings(_activeProfileName);
            }

            var inputDlg = new InputWindow(Properties.Resources.Title_RenameProfile, Properties.Resources.Prompt_RenameProfile, targetProfile)
            {
                Owner = this
            };

            if (inputDlg.ShowDialog() != true) return;

            string newName = inputDlg.InputText?.Trim() ?? "";
            if (string.IsNullOrEmpty(newName)) return;
            if (newName == targetProfile) return;
            if (!IsValidProfileName(newName)) return;

            try
            {
                // リネーム先が既に存在する場合は中止
                if (_profileService.SettingsFileExists(newName))
                {
                    string msgExists = string.Format(Properties.Resources.Msg_Err_ProfileExists, newName);
                    MessageWindow.Show(msgExists, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 設定ファイル(.dat)とブラウザデータフォルダをまとめてリネーム
                _profileService.RenameProfile(targetProfile, newName);

                // UIリストとアクティブ名を更新
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

        /// <summary>
        /// 「複製」の処理。設定ファイルを複製し、ブラウザデータフォルダもコピーします。
        /// アクティブプロファイルの複製時は、WebViewによるファイルロックを避けるため
        /// 「予約コピー(pending_clone.json)＋再起動」方式を採ります。
        /// </summary>
        private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            var inputWin = new InputWindow(Properties.Resources.Title_DuplicateProfile, Properties.Resources.Prompt_NewProfile, $"Copy of {targetProfile}")
            {
                Owner = this
            };

            if (inputWin.ShowDialog() != true) return;

            string newName = inputWin.InputText?.Trim() ?? "";
            if (!IsValidProfileName(newName)) return;

            try
            {
                bool isActiveProfile = (targetProfile == _activeProfileName);

                // アクティブプロファイルなら、複製前に最新状態を保存
                if (isActiveProfile) SaveSettings(_activeProfileName);

                // 設定ファイル(.dat)を複製（コピー元が無ければ空設定を新規作成）
                _profileService.DuplicateProfileSettings(targetProfile, newName);

                _profileNames.Add(new ProfileItem { Name = newName, IsActive = false });
                SaveAppConfig();

                if (isActiveProfile)
                {
                    // アクティブプロファイルはWebViewがデータフォルダをロックしているため、
                    // その場でのコピーは失敗しやすい。再起動前(App起動時)にコピーを実行するよう予約する。
                    string msgConfirm = string.Format(Properties.Resources.Msg_ConfirmCloneActiveProfile, targetProfile);

                    if (MessageWindow.Show(msgConfirm, Properties.Resources.Title_RestartConfirm, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        // 起動時に処理されるコピー予約ファイルを書き出す
                        _profileService.SchedulePendingClone(targetProfile, newName);

                        _activeProfileName = newName;
                        SaveAppConfig();

                        // 新プロファイルを指定して再起動
                        RestartWithProfile(newName);
                    }
                    else
                    {
                        // キャンセル時は追加した分をロールバック（複製した設定ファイルも削除）
                        var added = _profileNames.FirstOrDefault(p => p.Name == newName);
                        if (added != null) _profileNames.Remove(added);
                        _profileService.DeleteProfile(newName);
                        SaveAppConfig();
                    }
                }
                else
                {
                    // 非アクティブプロファイルはロックされていないため、その場でコピー可能
                    _profileService.CopyBrowserData(targetProfile, newName);

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

        /// <summary>
        /// 「削除」の処理。設定ファイルとブラウザデータフォルダを削除します。
        /// アクティブプロファイルは削除できません。
        /// </summary>
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            string? targetProfile = GetTargetProfileName(sender);
            if (string.IsNullOrEmpty(targetProfile)) return;

            // 使用中のプロファイルは削除不可
            if (targetProfile == _activeProfileName)
            {
                string msg = string.Format(Properties.Resources.Msg_Err_CannotDeleteActive, targetProfile);
                MessageWindow.Show(msg, Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmMsg = string.Format(Properties.Resources.Msg_ConfirmDeleteProfile, targetProfile);
            if (MessageWindow.Show(confirmMsg, Properties.Resources.Title_ConfirmDelete, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // 設定ファイル(.dat)とブラウザデータフォルダを削除
                _profileService.DeleteProfile(targetProfile);

                // UIリストから除去
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

        /// <summary>
        /// 「別窓で起動」の処理。指定プロファイルで新しいアプリインスタンスを起動します。
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
        /// プロファイル選択コンボボックスの選択変更イベント。
        /// 切り替えは「切り替え」メニューで行うため、ここでは即時切り替えは行いません
        /// （将来的な選択状態追跡・UI更新のためのフックとして残しています）。
        /// </summary>
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // ===== Private Helpers =====

        /// <summary>
        /// イベント発生元(sender)の Tag からプロファイル名を取得します。
        /// </summary>
        /// <returns>取得できたプロファイル名。取得できなければ null。</returns>
        private string? GetTargetProfileName(object sender)
        {
            if (sender is FrameworkElement item && item.Tag is string name) return name;
            return null;
        }

        /// <summary>
        /// 現在のプロファイルを保存したうえでアクティブプロファイルを切り替え、アプリを再起動します。
        /// </summary>
        /// <param name="targetProfileName">切り替え先プロファイル名。</param>
        private void PerformProfileSwitch(string targetProfileName)
        {
            SaveSettings(_activeProfileName);
            _activeProfileName = targetProfileName;
            SaveAppConfig();

            RestartWithProfile(targetProfileName);
        }

        /// <summary>
        /// 指定プロファイルを引数に渡して新しいインスタンスを起動し、現在のインスタンスを終了します。
        /// （プロファイル切り替え・アクティブプロファイル複製で共通利用）
        /// </summary>
        /// <param name="profileName">起動時に渡すプロファイル名。</param>
        private void RestartWithProfile(string profileName)
        {
            // 終了時の二重保存を防ぐためフラグを立てる（MainWindow_Closing で参照）
            _isRestarting = true;

            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null)
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"--profile \"{profileName}\"",
                    UseShellExecute = true
                };
                Process.Start(processInfo);
            }

            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// 指定されたプロファイル名がファイル名として有効かどうかを検証します。
        /// 無効な場合はその場でエラーメッセージを表示します。
        /// </summary>
        /// <param name="name">検証対象のプロファイル名。</param>
        /// <returns>有効なら true、それ以外は false。</returns>
        private bool IsValidProfileName(string? name)
        {
            // 空文字・空白のみは禁止
            if (string.IsNullOrWhiteSpace(name)) return false;

            // ファイル名に使用できない文字を含む場合は不可
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageWindow.Show(Properties.Resources.Msg_Err_ProfileNameInvalid, Properties.Resources.Title_Error);
                return false;
            }

            // 既存プロファイルと重複（大文字小文字を無視）する場合は不可
            if (_profileNames.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageWindow.Show(string.Format(Properties.Resources.Msg_Err_ProfileExists, name), Properties.Resources.Title_Error);
                return false;
            }

            return true;
        }
    }
}