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
                HideListHeader = currentSettings.HideListHeader
            };

            // UIに反映
            HideMenuHomeCheckBox.IsChecked = Settings.HideMenuInHome;
            HideMenuNonHomeCheckBox.IsChecked = Settings.HideMenuInNonHome;
            HideListHeaderCheckBox.IsChecked = Settings.HideListHeader;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // UIから設定を取得
            Settings.HideMenuInHome = HideMenuHomeCheckBox.IsChecked ?? false;
            Settings.HideMenuInNonHome = HideMenuNonHomeCheckBox.IsChecked ?? false;
            Settings.HideListHeader = HideListHeaderCheckBox.IsChecked ?? false;

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