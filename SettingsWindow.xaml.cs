using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using XColumn.Models;

namespace XColumn
{
 /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
 /// </summary>
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }

        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();

            // 設定のコピーを作成
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
                AppVolume = currentSettings.AppVolume,
                CustomCss = currentSettings.CustomCss
            };

            // --- システムフォント一覧の取得 ---
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
            fontList.Sort();
            FontFamilyComboBox.ItemsSource = fontList;

            // --- UIに反映 ---
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
            CustomCssTextBox.Text = Settings.CustomCss;
        }

        private void UseUniformGridCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isUniform = UseUniformGridCheckBox.IsChecked ?? false;
            ColumnWidthSlider.IsEnabled = !isUniform;
        }

        // --- フォントサイズ変更ボタンの処理 (新規追加) ---

        private void FontSizeUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(FontSizeTextBox.Text, out int size))
            {
                FontSizeTextBox.Text = (size + 1).ToString();
            }
            else
            {
                FontSizeTextBox.Text = "16"; // 数値でない場合はデフォルト+1
            }
        }

        private void FontSizeDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(FontSizeTextBox.Text, out int size))
            {
                // 0以下にはしない（0はデフォルトの意味だが、ボタン操作では直感的に1以上にする）
                if (size > 1) FontSizeTextBox.Text = (size - 1).ToString();
                else if (size <= 0) FontSizeTextBox.Text = "14"; // 0から下げるなら一旦14くらいにする
            }
            else
            {
                FontSizeTextBox.Text = "14";
            }
        }
        // ----------------------------------------------

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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
            Settings.CustomCss = CustomCssTextBox.Text;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}