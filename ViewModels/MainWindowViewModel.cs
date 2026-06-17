using System.Net;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using XColumn.Messages;
using XColumn.Properties;
using XColumn.Services;

namespace XColumn.ViewModels
{
    /// <summary>
    /// MainWindow のビューモデル。
    /// カラム追加・終了などの操作をコマンドとして提供します。
    /// 実際のカラム追加(ドメイン検証含む)は WeakReferenceMessenger 経由で View へ要求し、
    /// 入力ダイアログ等は IDialogService を介して表示します。
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        // ===== Constants (各種デフォルト/フォーマットURL。旧 MainWindow.Columns.cs の定数と同一) =====

        /// <summary>「ホーム」カラムの既定URL。</summary>
        private const string DefaultHomeUrl = "https://x.com/home";

        /// <summary>「通知」カラムの既定URL。</summary>
        private const string DefaultNotifyUrl = "https://x.com/notifications";

        /// <summary>「トレンド」カラムの既定URL。</summary>
        private const string DefaultTrendUrl = "https://x.com/explore/tabs/trending";

        /// <summary>「グローバルトレンド」カラムの既定URL。</summary>
        private const string DefaultGlobalTrendUrl = "https://x.com/i/jf/global-trending/home";

        /// <summary>「検索」カラムのURL書式（{0} = URLエンコード済みキーワード）。</summary>
        private const string SearchUrlFormat = "https://x.com/search?q={0}";

        /// <summary>「リスト」カラムのURL書式（{0} = リストID）。</summary>
        private const string ListUrlFormat = "https://x.com/i/lists/{0}";

        // ===== Fields =====

        /// <summary>ダイアログ表示サービス（DIで注入）。</summary>
        private readonly IDialogService _dialogService;

        // ===== Constructor =====

        /// <summary>
        /// 依存サービスを注入してビューモデルを初期化します。
        /// </summary>
        /// <param name="dialogService">入力/メッセージダイアログ表示用のサービス。</param>
        public MainWindowViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        // ===== Commands =====

        /// <summary>
        /// 「ホーム」カラムを追加します。
        /// </summary>
        [RelayCommand]
        private void AddHome() => RequestAddColumn(DefaultHomeUrl);

        /// <summary>
        /// 「通知」カラムを追加します。
        /// </summary>
        [RelayCommand]
        private void AddNotify() => RequestAddColumn(DefaultNotifyUrl);

        /// <summary>
        /// 「トレンド」カラムを追加します。
        /// </summary>
        [RelayCommand]
        private void AddTrend() => RequestAddColumn(DefaultTrendUrl);

        /// <summary>
        /// 「グローバルトレンド」カラムを追加します。
        /// </summary>
        [RelayCommand]
        private void AddGlobalTrend() => RequestAddColumn(DefaultGlobalTrendUrl);

        /// <summary>
        /// 「検索」カラムを追加します。キーワードを入力ダイアログで受け取ります。
        /// </summary>
        [RelayCommand]
        private void AddSearch()
        {
            var key = _dialogService.ShowInput(Resources.Prompt_Search, Resources.Prompt_SearchKeyword);
            if (!string.IsNullOrEmpty(key))
            {
                RequestAddColumn(string.Format(SearchUrlFormat, WebUtility.UrlEncode(key)));
            }
        }

        /// <summary>
        /// 「リスト」カラムを追加します。リストID または URL を入力ダイアログで受け取ります。
        /// </summary>
        [RelayCommand]
        private void AddList()
        {
            var input = _dialogService.ShowInput(Resources.Prompt_AddList, Resources.Prompt_AddListInput);
            if (string.IsNullOrEmpty(input)) return;

            if (input.StartsWith("http"))
            {
                // URLはそのまま追加要求（ドメイン検証・不正時のエラー表示は View 側の AddNewColumn に委譲）
                RequestAddColumn(input);
            }
            else if (long.TryParse(input, out _))
            {
                // 数値はリストURL形式へ変換
                RequestAddColumn(string.Format(ListUrlFormat, input));
            }
            else
            {
                // 数値でもURLでもない不正入力
                _dialogService.ShowMessage(Resources.Err_InputIdOrUrl, Resources.Common_Error);
            }
        }

        /// <summary>
        /// 「リスト(自動遷移)」カラムを追加します。
        /// このカラムは特殊フラグ(IsListAutoNav)を伴うため、View 側で生成します。
        /// </summary>
        [RelayCommand]
        private void AddListAuto() => WeakReferenceMessenger.Default.Send(new AddListAutoColumnRequest());

        /// <summary>
        /// 「ユーザー」カラムを追加します。ユーザーIDを入力ダイアログで受け取ります。
        /// </summary>
        [RelayCommand]
        private void AddUser()
        {
            var input = _dialogService.ShowInput(Resources.AddUser, Resources.UserId);
            if (string.IsNullOrEmpty(input)) return;

            // 先頭の @ を除去
            string userId = input.Trim().TrimStart('@');
            if (!string.IsNullOrEmpty(userId))
            {
                RequestAddColumn($"https://x.com/{userId}");
            }
        }

        /// <summary>
        /// 【試験的】X以外のサイトをカラムとして追加します。URLを入力ダイアログで受け取ります。
        /// </summary>
        [RelayCommand]
        private void AddSite()
        {
            var input = _dialogService.ShowInput(Resources.Prompt_AddSite, Resources.Prompt_AddSiteInput);
            if (string.IsNullOrWhiteSpace(input)) return;

            string url = input.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            WeakReferenceMessenger.Default.Send(new AddSiteColumnRequest(url));
        }

        /// <summary>
        /// アプリを終了します。
        /// </summary>
        [RelayCommand]
        private void Exit() => WeakReferenceMessenger.Default.Send(new CloseAppRequest());

        // ===== Private Methods =====

        /// <summary>
        /// 指定URLのカラム追加要求を WeakReferenceMessenger 経由で View へ送信します。
        /// </summary>
        /// <param name="url">追加するカラムが開く対象URL。</param>
        private void RequestAddColumn(string url)
            => WeakReferenceMessenger.Default.Send(new AddColumnRequest(url));
    }
}