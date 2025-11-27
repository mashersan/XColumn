using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XColumn.Models;

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
        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();

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

                // UI表示設定
                HideMenuInHome = currentSettings.HideMenuInHome,
                HideMenuInNonHome = currentSettings.HideMenuInNonHome,
                HideListHeader = currentSettings.HideListHeader,
                HideRightSidebar = currentSettings.HideRightSidebar,

                ColumnWidth = currentSettings.ColumnWidth,
                UseUniformGrid = currentSettings.UseUniformGrid,

                // フォント設定
                AppFontFamily = currentSettings.AppFontFamily,
                AppFontSize = currentSettings.AppFontSize,

                // 動作設定
                UseSoftRefresh = currentSettings.UseSoftRefresh,
                EnableWindowSnap = currentSettings.EnableWindowSnap,
                DisableFocusModeOnMediaClick = currentSettings.DisableFocusModeOnMediaClick, // 追加
                AppVolume = currentSettings.AppVolume,
                CustomCss = currentSettings.CustomCss,
                ServerCheckIntervalMinutes = currentSettings.ServerCheckIntervalMinutes
            };

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

            ColumnWidthSlider.Value = Settings.ColumnWidth;
            UseUniformGridCheckBox.IsChecked = Settings.UseUniformGrid;
            ColumnWidthSlider.IsEnabled = !Settings.UseUniformGrid;

            UseSoftRefreshCheckBox.IsChecked = Settings.UseSoftRefresh;
            EnableWindowSnapCheckBox.IsChecked = Settings.EnableWindowSnap;
            DisableFocusModeOnMediaClickCheckBox.IsChecked = Settings.DisableFocusModeOnMediaClick; // 追加

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
        /// フォントサイズを1増加
        /// </summary>
        private void FontSizeUp_Click(object sender, RoutedEventArgs e)
        {
            // 現在のフォントサイズを取得し、1増加させる
            if (int.TryParse(FontSizeTextBox.Text, out int size))
            {
                FontSizeTextBox.Text = (size + 1).ToString();
            }
            else
            {
                // 不正な値の場合、デフォルト値にリセット
                FontSizeTextBox.Text = "16";
            }
        }

        /// <summary>
        /// フォントサイズを1減少
        /// </summary>
        private void FontSizeDown_Click(object sender, RoutedEventArgs e)
        {
            //  現在のフォントサイズを取得し、1減少させる
            if (int.TryParse(FontSizeTextBox.Text, out int size))
            {
                // 0以下にはしない（0はデフォルトの意味だが、ボタン操作では直感的に1以上にする）
                if (size > 1)
                {
                    FontSizeTextBox.Text = (size - 1).ToString();
                }
                else if (size <= 0)
                {
                    FontSizeTextBox.Text = "14";
                }
            }
            else
            {
                FontSizeTextBox.Text = "14";
            }
        }
        /// <summary>
        /// OKボタンのクリック処理
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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

            Settings.UseSoftRefresh = UseSoftRefreshCheckBox.IsChecked ?? true;
            Settings.EnableWindowSnap = EnableWindowSnapCheckBox.IsChecked ?? true;
            Settings.DisableFocusModeOnMediaClick = DisableFocusModeOnMediaClickCheckBox.IsChecked ?? false; // 追加

            if (ServerCheckIntervalComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Tag.ToString(), out int interval))
            {
                Settings.ServerCheckIntervalMinutes = interval;
            }
            else
            {
                Settings.ServerCheckIntervalMinutes = 5;
            }

            Settings.CustomCss = CustomCssTextBox.Text;

            DialogResult = true;
            Close();
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