using ModernWpf.Controls.Primitives;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// ウィンドウの初期化と現在の設定値の読み込み。
        /// </summary>
        /// <param name="currentSettings"></param>
        public SettingsWindow(AppSettings currentSettings, AppConfig appConfig, string configPath)
        {
            InitializeComponent();

            // ModernWpfのモダンウィンドウスタイルを適用
            WindowHelper.SetUseModernWindowStyle(this, true);

            _appConfig = appConfig;
            _appConfigPath = configPath;

            // 言語設定の反映
            if (_appConfig.Language == "en-US")
                LanguageComboBox.SelectedIndex = 1;
            else
                LanguageComboBox.SelectedIndex = 0;

            // AppSettingsのディープコピー
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

                // UI表示設定
                HideMenuInHome = currentSettings.HideMenuInHome,
                HideMenuInNonHome = currentSettings.HideMenuInNonHome,
                HideListHeader = currentSettings.HideListHeader,
                HideRightSidebar = currentSettings.HideRightSidebar,

                // カラム表示設定
                ColumnWidth = currentSettings.ColumnWidth,
                UseUniformGrid = currentSettings.UseUniformGrid,

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
            if (ThemeComboBox.SelectedItem == null) ThemeComboBox.SelectedIndex = 0;

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

            // フォーカスモード関連設定の反映
            DisableFocusModeOnMediaClickCheckBox.IsChecked = Settings.DisableFocusModeOnMediaClick;
            DisableFocusModeOnTweetClickCheckBox.IsChecked = Settings.DisableFocusModeOnTweetClick;

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

            CustomCssTextBox.Text = Settings.CustomCss;
        }

        private void UseUniformGridCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isUniform = UseUniformGridCheckBox.IsChecked ?? false;
            ColumnWidthSlider.IsEnabled = !isUniform;
        }

        /// <summary>
        /// OKボタンのクリック処理
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 言語設定の変更検出フラグ
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
            
            // 設定値の保存
            Settings.HideMenuInHome = HideMenuHomeCheckBox.IsChecked ?? false;
            Settings.HideMenuInNonHome = HideMenuNonHomeCheckBox.IsChecked ?? false;
            Settings.HideListHeader = HideListHeaderCheckBox.IsChecked ?? false;
            Settings.HideRightSidebar = HideRightSidebarCheckBox.IsChecked ?? false;
            Settings.AppFontFamily = FontFamilyComboBox.Text.Trim();
            if (int.TryParse(FontSizeTextBox.Text, out int size)) Settings.AppFontSize = size;
            else Settings.AppFontSize = 15;
            Settings.ColumnWidth = ColumnWidthSlider.Value;
            Settings.UseUniformGrid = UseUniformGridCheckBox.IsChecked ?? false;

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

            // カスタムCSS
            Settings.CustomCss = CustomCssTextBox.Text;

            // テーマ設定の保存
            if (ThemeComboBox.SelectedValue != null)
            {
                Settings.AppTheme = ThemeComboBox.SelectedValue.ToString()?? "System";
            }
            else
            {
                Settings.AppTheme = "System"; // 念のためのフォールバック
            }

            DialogResult = true;
            Close();

            // 言語変更時の再起動確認
            if (languageChanged)
            {
                // メッセージボックスを表示
                if (MessageWindow.Show(Properties.Resources.Msg_LanguageChanged_Restart,
                                       Properties.Resources.Settings_Title, // タイトル: 設定
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 再起動処理
                    // 現在のプロセスパスを取得して起動し、自分自身をシャットダウン
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