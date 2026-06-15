using System.Windows;

namespace XColumn.Views
{
    /// <summary>
    /// ユーザーにテキスト入力を促すためのシンプルなダイアログウィンドウ。
    /// </summary>
    public partial class InputWindow : Window
    {
        // ===== Properties =====

        /// <summary>
        /// ユーザーが入力して「OK」を押したテキストを取得します。
        /// </summary>
        public string? InputText { get; private set; }

        // ===== Constructor =====

        /// <summary>
        /// タイトル・説明文・初期値を指定して入力ダイアログを初期化します。
        /// </summary>
        /// <param name="title">ウィンドウのタイトル。</param>
        /// <param name="prompt">説明文。</param>
        /// <param name="defaultText">テキストボックスの初期値（省略可）。</param>
        public InputWindow(string title, string prompt, string defaultText = "")
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;

            // 初期値をセット
            InputTextBox.Text = defaultText;

            // 入力しやすいように全選択状態にする
            if (!string.IsNullOrEmpty(defaultText))
            {
                InputTextBox.SelectAll();
            }
        }

        // ===== Event Handlers =====

        /// <summary>
        /// ウィンドウがロードされた時、入力欄に自動的にフォーカスを当てます。
        /// </summary>
        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
        }

        /// <summary>
        /// 「OK」ボタンクリック時の処理。入力テキストを確定してウィンドウを閉じます。
        /// </summary>
        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }
    }
}