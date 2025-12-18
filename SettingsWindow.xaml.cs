using ModernWpf.Controls.Primitives;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XColumn.Models;

// 曖昧さ回避
using Application = System.Windows.Application;

namespace XColumn
{
    /// <summary>
    /// 設定ウィンドウの相互作用ロジック。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }

        private AppConfig _appConfig;
        private string _appConfigPath;

        /// <summary>
        /// ウィンドウの初期化と現在の設定値の読み込み。
        /// </summary>
        public SettingsWindow(AppSettings currentSettings, AppConfig appConfig, string configPath)
        {
            InitializeComponent();

            // ModernWpfのモダンウィンドウスタイルを適用
            WindowHelper.SetUseModernWindowStyle(this, true);

            // Nullチェック (CS8602対策)
            if (currentSettings == null) currentSettings = new AppSettings();

            _appConfig = appConfig;
            _appConfigPath = configPath;

            // 言語設定の反映
            if (_appConfig.Language == "en-US")
                LanguageComboBox.SelectedIndex = 1;
            else
                LanguageComboBox.SelectedIndex = 0;

            // 設定のディープコピーを作成（キャンセル時に影響を与えないため）
            Settings = new AppSettings
            {
                WindowTop = currentSettings.WindowTop,
                WindowLeft = currentSettings.WindowLeft,
                WindowHeight = currentSettings.WindowHeight,
                WindowWidth = currentSettings.WindowWidth,
                WindowState = currentSettings.WindowState,
                Columns = currentSettings.Columns,
                Extensions = currentSettings.Extensions,
                IsFocusMode = currentSettings.IsFocusMode,
                FocusUrl = currentSettings.FocusUrl,
                StopTimerWhenActive = currentSettings.StopTimerWhenActive,
                SkippedVersion = currentSettings.SkippedVersion,
                ListAutoNavDelay = currentSettings.ListAutoNavDelay,

                // UI表示設定
                HideMenuInHome = currentSettings.HideMenuInHome,
                HideMenuInNonHome = currentSettings.HideMenuInNonHome,
                HideListHeader = currentSettings.HideListHeader,
                HideRightSidebar = currentSettings.HideRightSidebar,

                // カラム表示設定
                ColumnWidth = currentSettings.ColumnWidth,
                UseUniformGrid = currentSettings.UseUniformGrid,
                // カラムURL表示設定
                ShowColumnUrl = currentSettings.ShowColumnUrl,

                // カラム追加位置設定
                AddColumnToLeft = currentSettings.AddColumnToLeft,

                // フォント設定
                AppFontFamily = currentSettings.AppFontFamily,
                AppFontSize = currentSettings.AppFontSize,

                // 動作設定
                UseSoftRefresh = currentSettings.UseSoftRefresh,
                EnableWindowSnap = currentSettings.EnableWindowSnap,
                DisableFocusModeOnMediaClick = currentSettings.DisableFocusModeOnMediaClick,
                DisableFocusModeOnTweetClick = currentSettings.DisableFocusModeOnTweetClick,
                AppVolume = currentSettings.AppVolume,
                CustomCss = currentSettings.CustomCss,
                ServerCheckIntervalMinutes = currentSettings.ServerCheckIntervalMinutes,
                KeepUnreadPosition = currentSettings.KeepUnreadPosition,

                // NGワード設定
                NgWords = currentSettings.NgWords != null ? new List<string>(currentSettings.NgWords) : new List<string>(),

                // テーマ設定
                AppTheme = currentSettings.AppTheme
            };

            // テーマ設定の反映
            string currentTheme = Settings.AppTheme;
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag.ToString() == currentTheme)
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
            if (ThemeComboBox.SelectedItem == null) ThemeComboBox.SelectedIndex = 0; // Default System

            // システムフォント一覧の取得
            var fontList = new List<string>();
            foreach (var font in Fonts.SystemFontFamilies)
            {
                if (font.FamilyNames.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("ja-jp"), out string? jaName))
                {
                    fontList.Add(jaName);
                }
                else
                {
                    fontList.Add(font.Source);
                }
            }
            // フォント名でソート
            fontList.Sort();
            // コンボボックスに設定
            FontFamilyComboBox.ItemsSource = fontList;

            // UIコントロールへの値の反映
            HideMenuHomeCheckBox.IsChecked = Settings.HideMenuInHome;
            HideMenuNonHomeCheckBox.IsChecked = Settings.HideMenuInNonHome;
            HideListHeaderCheckBox.IsChecked = Settings.HideListHeader;
            HideRightSidebarCheckBox.IsChecked = Settings.HideRightSidebar;

            // フォント設定の反映
            FontFamilyComboBox.Text = Settings.AppFontFamily;
            FontSizeTextBox.Text = Settings.AppFontSize.ToString();

            // もし設定値が不正で選択されなかった場合、デフォルト(0番目)を選択
            if (ThemeComboBox.SelectedIndex < 0)
            {
                ThemeComboBox.SelectedIndex = 0;
            }

            // カラム表示設定の反映
            ColumnWidthSlider.Value = Settings.ColumnWidth;
            UseUniformGridCheckBox.IsChecked = Settings.UseUniformGrid;

            //  カラム幅スライダーの有効/無効設定
            ColumnWidthSlider.IsEnabled = !Settings.UseUniformGrid;

            // カラム追加位置設定の反映
            AddColumnToLeftCheckBox.IsChecked = Settings.AddColumnToLeft;

            // 動作設定の反映
            UseSoftRefreshCheckBox.IsChecked = Settings.UseSoftRefresh;
            EnableWindowSnapCheckBox.IsChecked = Settings.EnableWindowSnap;
            KeepUnreadPositionCheckBox.IsChecked = Settings.KeepUnreadPosition;
            ListAutoNavDelayTextBox.Text = Settings.ListAutoNavDelay.ToString();

            // フォーカスモード関連設定の反映
            DisableFocusModeOnMediaClickCheckBox.IsChecked = Settings.DisableFocusModeOnMediaClick;
            DisableFocusModeOnTweetClickCheckBox.IsChecked = Settings.DisableFocusModeOnTweetClick;

            // カラムURL表示設定の反映
            ShowColumnUrlCheckBox.IsChecked = Settings.ShowColumnUrl;

            // サーバー監視頻度の設定反映
            foreach (ComboBoxItem item in ServerCheckIntervalComboBox.Items)
            {
                if (int.TryParse(item.Tag.ToString(), out int val) && val == Settings.ServerCheckIntervalMinutes)
                {
                    ServerCheckIntervalComboBox.SelectedItem = item;
                    break;
                }
            }
            if (ServerCheckIntervalComboBox.SelectedItem == null)
            {
                ServerCheckIntervalComboBox.SelectedIndex = 2; // デフォルト(5分)
            }

            // NGワードリストの表示
            foreach (var word in Settings.NgWords)
            {
                NgWordListBox.Items.Add(word);
            }

            CustomCssTextBox.Text = Settings.CustomCss;
        }

        private void UseUniformGridCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isUniform = UseUniformGridCheckBox.IsChecked ?? false;
            ColumnWidthSlider.IsEnabled = !isUniform;
        }

        // --- NGワード関連イベント ---
        private void AddNgWordButton_Click(object sender, RoutedEventArgs e)
        {
            string word = NgWordInputBox.Text.Trim();
            if (!string.IsNullOrEmpty(word) && !NgWordListBox.Items.Contains(word))
            {
                NgWordListBox.Items.Add(word);
                NgWordInputBox.Text = "";
            }
        }

        private void DeleteNgWordButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = NgWordListBox.SelectedItems.Cast<string>().ToList();
            foreach (var item in selectedItems)
            {
                NgWordListBox.Items.Remove(item);
            }
        }
        // ---------------------------

        /// <summary>
        /// OKボタンのクリック処理
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            bool languageChanged = false;

            // 言語設定の保存
            var selectedItem = LanguageComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.Tag is string langCode)
            {
                if (_appConfig.Language != langCode)
                {
                    _appConfig.Language = langCode;
                    try
                    {
                        string json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_appConfigPath, json);
                        languageChanged = true;
                    }
                    catch { }
                }
            }

            // 画面の設定値をオブジェクトに保存
            Settings.HideMenuInHome = HideMenuHomeCheckBox.IsChecked ?? false;
            Settings.HideMenuInNonHome = HideMenuNonHomeCheckBox.IsChecked ?? false;
            Settings.HideListHeader = HideListHeaderCheckBox.IsChecked ?? false;
            Settings.HideRightSidebar = HideRightSidebarCheckBox.IsChecked ?? false;

            // フォント設定
            Settings.AppFontFamily = FontFamilyComboBox.Text.Trim();
            if (int.TryParse(FontSizeTextBox.Text, out int size))
            {
                Settings.AppFontSize = size;
            }
            else
            {
                Settings.AppFontSize = 15;
            }

            Settings.ColumnWidth = ColumnWidthSlider.Value;
            Settings.UseUniformGrid = UseUniformGridCheckBox.IsChecked ?? false;

            // カラムURL表示設定
            Settings.ShowColumnUrl = ShowColumnUrlCheckBox.IsChecked ?? true;

            // カラム追加位置設定
            Settings.AddColumnToLeft = AddColumnToLeftCheckBox.IsChecked ?? false;

            Settings.UseSoftRefresh = UseSoftRefreshCheckBox.IsChecked ?? true;
            Settings.KeepUnreadPosition = KeepUnreadPositionCheckBox.IsChecked ?? false;
            Settings.EnableWindowSnap = EnableWindowSnapCheckBox.IsChecked ?? true;

            // フォーカスモード関連設定
            Settings.DisableFocusModeOnMediaClick = DisableFocusModeOnMediaClickCheckBox.IsChecked ?? false;
            Settings.DisableFocusModeOnTweetClick = DisableFocusModeOnTweetClickCheckBox.IsChecked ?? false;

            if (ServerCheckIntervalComboBox.SelectedItem is ComboBoxItem selectedCombo &&
                int.TryParse(selectedCombo.Tag.ToString(), out int interval))
            {
                Settings.ServerCheckIntervalMinutes = interval;
            }
            else
            {
                Settings.ServerCheckIntervalMinutes = 5;
            }

            // NGワードの保存
            Settings.NgWords = NgWordListBox.Items.Cast<string>().ToList();

            // カスタムCSS
            Settings.CustomCss = CustomCssTextBox.Text;

            // --- テーマ設定の保存 ---
            if (ThemeComboBox.SelectedValue != null)
            {
                Settings.AppTheme = ThemeComboBox.SelectedValue.ToString() ?? "System";
            }
            else
            {
                Settings.AppTheme = "System";
            }

            // リスト自動ナビゲーション遅延時間の保存
            if (int.TryParse(ListAutoNavDelayTextBox.Text, out int delay))
            {
                Settings.ListAutoNavDelay = delay;
            }
            else
            {
                Settings.ListAutoNavDelay = 2000; // 変換失敗時はデフォルト値
            }


            DialogResult = true;
            Close();

            // 言語変更時の再起動確認
            if (languageChanged)
            {
                if (MessageWindow.Show(Properties.Resources.Msg_LanguageChanged_Restart,
                                       Properties.Resources.Settings_Title,
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 再起動処理
                    try
                    {
                        var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                        if (module != null)
                        {
                            System.Diagnostics.Process.Start(module.FileName);
                            Application.Current.Shutdown();
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// キャンセルボタンのクリック処理
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}