using System.Windows;
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

                // 動作設定
                UseSoftRefresh = currentSettings.UseSoftRefresh,
                AppVolume = currentSettings.AppVolume,
                CustomCss = currentSettings.CustomCss
            };

            // UIに反映
            HideMenuHomeCheckBox.IsChecked = Settings.HideMenuInHome;
            HideMenuNonHomeCheckBox.IsChecked = Settings.HideMenuInNonHome;
            HideListHeaderCheckBox.IsChecked = Settings.HideListHeader;
            HideRightSidebarCheckBox.IsChecked = Settings.HideRightSidebar;

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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // UIから設定を取得
            Settings.HideMenuInHome = HideMenuHomeCheckBox.IsChecked ?? false;
            Settings.HideMenuInNonHome = HideMenuNonHomeCheckBox.IsChecked ?? false;
            Settings.HideListHeader = HideListHeaderCheckBox.IsChecked ?? false;
            Settings.HideRightSidebar = HideRightSidebarCheckBox.IsChecked ?? false;

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