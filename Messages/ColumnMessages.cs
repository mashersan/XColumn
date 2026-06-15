namespace XColumn.Messages
{
    /// <summary>
    /// 「指定URLの新規カラムを追加してほしい」という要求メッセージ。
    /// ViewModel から送信し、View(MainWindow) が受信して実際の追加処理を行います。
    /// </summary>
    public sealed class AddColumnRequest
    {
        /// <summary>追加するカラムが開く対象URL。</summary>
        public string Url { get; }

        /// <summary>
        /// 指定URLのカラム追加要求を生成します。
        /// </summary>
        /// <param name="url">追加するカラムが開く対象URL。</param>
        public AddColumnRequest(string url) => Url = url;
    }

    /// <summary>
    /// 「リスト自動遷移カラム(IsListAutoNav=true)を追加してほしい」という要求メッセージ。
    /// 特殊フラグ付きカラムの生成のため、URL文字列では表現できない要求です。
    /// このメッセージ自体に付随データはなく、型の存在そのものが要求を表します。
    /// </summary>
    public sealed class AddListAutoColumnRequest
    {
    }

    /// <summary>
    /// 「アプリを終了してほしい」という要求メッセージ。
    /// このメッセージ自体に付随データはなく、型の存在そのものが要求を表します。
    /// </summary>
    public sealed class CloseAppRequest
    {
    }
}