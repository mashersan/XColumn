using System.Collections.Generic;

namespace XColumn.Models
{
    /// <summary>
    /// アプリケーション全体の設定データ（非暗号化）。
    /// プロファイルの一覧と、現在アクティブなプロファイルを管理します。
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 次回起動時に読み込むプロファイル名。
        /// </summary>
        public string ActiveProfile { get; set; } = "default";

        /// <summary>
        /// 存在するプロファイル名のリスト。
        /// </summary>
        public List<string> ProfileNames { get; set; } = new List<string> { "default" };

        // 追加: 言語設定 (デフォルトは日本語 "ja-JP")
        // 英語なら "en-US" などが入ります
        public string Language { get; set; } = "ja-JP";
    }
}