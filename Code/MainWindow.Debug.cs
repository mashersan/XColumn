using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using XColumn.Models;

using MessageBox = System.Windows.MessageBox;

namespace XColumn
{
    /// <summary>
    /// デバッグ情報の生成・エクスポート機能を提供する分割クラス。
    /// バグ報告用に、現在の環境設定（設定、拡張機能、CSS、バージョン情報）をJSONファイルに出力します。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 「デバッグ情報をエクスポート」メニュークリック時の処理
        /// </summary>
        private void ExportDebugInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. システム情報の収集
                var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                var osVersion = Environment.OSVersion.ToString();
                string webView2Version = "Unknown";

                try
                {
                    webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch
                {
                    webView2Version = "Not Found / Error";
                }

                // 2. 現在の設定値（AppSettings）をメモリ上の状態から構築
                // ※MainWindow.Config.cs の SaveSettings ロジックと同様に、現在のUI状態を反映させます
                var currentSettings = new AppSettings
                {
                    // カラム構成
                    Columns = new List<ColumnData>(Columns),

                    // 拡張機能
                    Extensions = new List<ExtensionItem>(_extensionList),

                    // UI設定
                    StopTimerWhenActive = StopTimerWhenActive,
                    HideMenuInNonHome = _hideMenuInNonHome,
                    HideMenuInHome = _hideMenuInHome,
                    HideListHeader = _hideListHeader,
                    HideRightSidebar = _hideRightSidebar,

                    // 動作設定
                    UseSoftRefresh = _useSoftRefresh,
                    EnableWindowSnap = _enableWindowSnap,
                    DisableFocusModeOnMediaClick = _disableFocusModeOnMediaClick,

                    // カスタマイズ
                    CustomCss = _customCss,
                    AppVolume = _appVolume,
                    ColumnWidth = ColumnWidth,
                    UseUniformGrid = UseUniformGrid,
                    AddColumnToLeft = _addColumnToLeft,
                    AppFontFamily = _appFontFamily,
                    AppFontSize = _appFontSize,

                    // サーバー監視設定
                    ServerCheckIntervalMinutes = _serverCheckIntervalMinutes,
                };

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
                    Title = "デバッグ情報の保存"
                };

                if (dlg.ShowDialog() == true)
                {
                    // 5. JSONシリアライズと保存
                    // 日本語が文字化けしないようにエンコーダーを指定
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                    };

                    string jsonString = JsonSerializer.Serialize(exportData, options);
                    File.WriteAllText(dlg.FileName, jsonString);

                    MessageBox.Show(
                        "デバッグ情報を保存しました。\n\n" +
                        "※注意: このファイルにはカラムのURLや拡張機能のパスが含まれています。\n" +
                        "他者に共有する際は、必要に応じて内容を確認・編集してください。",
                        "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エクスポート中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}