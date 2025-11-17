using System.Collections.Generic;
using System.Windows;

namespace XColumn.Models
{
    /// <summary>
    /// プロファイルごとに保存される設定データ（暗号化対象）。
    /// ウィンドウ位置、カラム構成、フォーカス状態などを保持します。
    /// </summary>
    public class AppSettings
    {
        public List<ColumnData> Columns { get; set; } = new List<ColumnData>();

        // ウィンドウ位置・サイズ
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowHeight { get; set; } = 800;
        public double WindowWidth { get; set; } = 1200;
        public WindowState WindowState { get; set; } = WindowState.Normal;

        // フォーカスモードの状態
        public bool IsFocusMode { get; set; } = false;
        public string? FocusUrl { get; set; } = null;

        /// <summary>
        /// ユーザーがスキップした最新バージョンのタグ名（例: "1.2.0"）。
        /// </summary>
        public string SkippedVersion { get; set; } = "0.0.0";
    }
}