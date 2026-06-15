using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using XColumn.Models;

namespace XColumn.Views
{
    /// <summary>
    /// デバッグ情報の生成・エクスポート機能を提供する分割クラス。
    /// バグ報告用に、現在の環境情報（バージョン・OS・WebView2・各種設定・拡張機能・CSS）を
    /// JSONファイルへ出力します。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Event Handlers =====

        /// <summary>
        /// 「デバッグ情報をエクスポート」メニュークリック時の処理。
        /// 現在の環境情報を収集し、ユーザーが指定したパスへJSONとして書き出します。
        /// </summary>
        private void ExportDebugInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. システム情報の収集
                var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                var osVersion = Environment.OSVersion.ToString();
                string webView2Version;

                try
                {
                    // WebView2ランタイム未インストール時は例外になるため個別にハンドルする
                    webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch
                {
                    webView2Version = "Not Found / Error";
                }

                // 2. 現在の設定値(AppSettings)をメモリ上のUI状態から構築する。
                //    収集ロジックは SaveSettings と共通(CollectCurrentSettings)のため、
                //    設定項目が増えても自動的にエクスポート対象へ反映される。
                var currentSettings = new AppSettings();
                CollectCurrentSettings(currentSettings);

                // 3. エクスポート用オブジェクトの作成
                var exportData = new
                {
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    AppVersion = appVersion,
                    OSVersion = osVersion,
                    WebView2Version = webView2Version,
                    ActiveProfile = _activeProfileName,
                    Settings = currentSettings
                };

                // 4. 保存ダイアログの表示
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"XColumn_DebugInfo_{DateTime.Now:yyyyMMdd}.json",
                    DefaultExt = ".json",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = Properties.Resources.DebugInfo_SaveTitle
                };

                if (dlg.ShowDialog() == true)
                {
                    // 5. JSONシリアライズと保存
                    //    日本語等が \uXXXX へエスケープされ文字化けして見えるのを防ぐため、
                    //    UnicodeRanges.All を許可するエンコーダーを指定する。
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                    };

                    string jsonString = JsonSerializer.Serialize(exportData, options);
                    File.WriteAllText(dlg.FileName, jsonString);

                    // 6. 完了メッセージの表示
                    MessageWindow.Show(
                        Properties.Resources.Msg_DebugInfoExported,
                        Properties.Resources.Information,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // エラーメッセージは多言語リソース化（本文に例外メッセージを差し込む）
                MessageWindow.Show(
                    string.Format(Properties.Resources.Msg_Err_DebugExportFailed, ex.Message),
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}