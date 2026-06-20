using System.Text;
using System.Windows;
using XColumn.Models;

namespace XColumn.Views
{
    /// <summary>
    /// 現在のカラム構成（種別・自動更新間隔）から、無操作時に429へ達するまでの推定時間を算出する分割クラス。
    /// レート制限は「アカウント(プロファイル)×GraphQL Operation」ごとに別バケットである点を踏まえ、
    /// 同一バケットを共有するカラムの合算レートで見積もる。
    /// </summary>
    public partial class MainWindow
    {
        private const int RL429_WindowSeconds = 900; // Xの標準窓（15分）

        /// <summary>ツールメニューからの429到達推定。</summary>
        private void CheckRateLimit429_Click(object sender, RoutedEventArgs e)
        {
            string report = BuildRateLimit429Estimate();
            MessageWindow.Show(this, report, Properties.Resources.RL429_Title,
                               MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>カラムURLから主タイムラインの操作名を分類（IsPrimaryTimelineOperation のURL判定に準拠）。</summary>
        private static string ClassifyOperation(string? url)
        {
            url ??= "";
            if (url.Contains("/search")) return "SearchTimeline";
            if (url.Contains("/lists/") || url.Contains("/i/lists/")) return "ListLatestTweetsTimeline";
            if (url.Contains("/notifications")) return "NotificationsTimeline";
            if (url.Contains("/home")) return "HomeTimeline";
            return "UserTweets"; // ユーザーカラム等
        }

        /// <summary>操作ごとの推定上限（15分窓あたり／概算。実測で調整可）。</summary>
        private static int BudgetFor(string op) => op switch
        {
            "SearchTimeline" => 50,
            "NotificationsTimeline" => 1500,
            _ => 500, // List / Home / User（Userは推定）
        };

        private static string FormatHms(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds)) return "—";
            var ts = TimeSpan.FromSeconds(Math.Max(seconds, 0));
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
                                      : $"{ts.Minutes}:{ts.Seconds:00}";
        }

        private string BuildRateLimit429Estimate()
        {
            // 無操作前提：消費するのは「X上 & 自動更新ON & 間隔>0」のカラムのみ
            var targets = Columns
                .Where(c => !c.IsExternalSite && c.IsAutoRefreshEnabled && c.RefreshIntervalSeconds > 0)
                .ToList();

            if (targets.Count == 0)
                return Properties.Resources.RL429_NoTargets;

            // (プロファイル, 操作) でグループ化（レート制限はアカウント=プロファイル単位の別バケット）
            var groups = targets.GroupBy(c =>
            {
                string profile = string.IsNullOrEmpty(c.ProfileName) ? _activeProfileName : c.ProfileName!;
                string op = ClassifyOperation(c.Url);
                return (profile, op);
            });

            var sb = new StringBuilder();
            double? shortest = null;
            int windowMin = RL429_WindowSeconds / 60;

            foreach (var g in groups.OrderBy(g => g.Key.profile).ThenBy(g => g.Key.op))
            {
                int count = g.Count();
                int budget = BudgetFor(g.Key.op);
                // 合算レート（req/秒）。ジッターは無視＝最短（最悪）ケースで見積もる。
                double ratePerSec = g.Sum(c => 1.0 / c.RefreshIntervalSeconds);
                // 初回ロードで各カラム1消費 + 窓内の自動更新ぶん
                double reqInWindow = count + ratePerSec * RL429_WindowSeconds;

                sb.AppendLine($"[{g.Key.profile}] {g.Key.op}");
                sb.AppendLine(string.Format(Properties.Resources.RL429_Line_Count, count, budget, windowMin));
                sb.AppendLine(string.Format(Properties.Resources.RL429_Line_Consume, Math.Round(reqInWindow), windowMin));

                if (reqInWindow <= budget)
                {
                    double headroom = budget <= 0 ? 0 : (1.0 - reqInWindow / budget) * 100;
                    sb.AppendLine(string.Format(Properties.Resources.RL429_Line_Safe, Math.Round(headroom)));
                }
                else
                {
                    double usable = Math.Max(budget - count, 0);          // 初回ロードぶんを差し引いた残枠
                    double secToLimit = ratePerSec > 0 ? usable / ratePerSec : double.PositiveInfinity;
                    sb.AppendLine(string.Format(Properties.Resources.RL429_Line_Risk, FormatHms(secToLimit)));
                    shortest = shortest.HasValue ? Math.Min(shortest.Value, secToLimit) : secToLimit;
                }
                sb.AppendLine();
            }

            string header = shortest.HasValue
                ? string.Format(Properties.Resources.RL429_Header_Risk, FormatHms(shortest.Value))
                : Properties.Resources.RL429_Header_Safe;

            return header + "\n\n" + sb.ToString().TrimEnd() + "\n\n" + Properties.Resources.RL429_Footer;
        }
    }
}