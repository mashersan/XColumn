using System.Windows;

namespace TweetDesk
{
    /// <summary>
    /// ユーザーにテキスト入力を促すためのシンプルなダイアログウィンドウ
    /// </summary>
    public partial class InputWindow : Window
    {
        /// <summary>
        /// ユーザーが入力してOKを押したテキスト
        /// (★修正 CS8618) nullになる可能性があるため '?' を追加
        /// </summary>
        public string? InputText { get; private set; }

        /// <summary>
        /// InputWindow のコンストラクタ
        /// </summary>
        /// <param name="title">ウィンドウのタイトル</param>
        /// <param name="prompt">ユーザーに表示する説明文</param>
        public InputWindow(string title, string prompt)
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;
            // InputText は null のまま（コンストラクタ終了時）でOK
        }

        /// <summary>
        /// ウィンドウがロードされた時、テキストボックスにフォーカスを当てる
        /// (★修正 CS8622)
        /// </summary>
        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
        }

        /// <summary>
        /// OKボタンが押された時
        /// (★修正 CS8622)
        /// </summary>
        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }
    }
}