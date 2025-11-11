using System.Windows;

// 名前空間を XColumn に変更
namespace XColumn
{
    /// <summary>
    /// ユーザーにテキスト入力を促すためのシンプルなダイアログウィンドウ (InputWindow.xaml の分離コード)。
    /// </summary>
    public partial class InputWindow : Window
    {
        /// <summary>
        /// ユーザーが入力して「OK」を押したテキストを取得します。
        /// キャンセルされた場合、この値は null になります。
        /// </summary>
        public string? InputText { get; private set; }

        /// <summary>
        /// InputWindow のコンストラクタ。
        /// </summary>
        /// <param name="title">ウィンドウのタイトルバーに表示するテキスト</param>
        /// <param name="prompt">ユーザーに表示する説明文（例: "キーワードを入力してください"）</param>
        public InputWindow(string title, string prompt)
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;
        }

        /// <summary>
        /// ウィンドウがロードされた時、自動的にテキストボックスにフォーカスを当てます。
        /// </summary>
        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            InputTextBox.Focus(); // ユーザーがすぐ入力できるようにフォーカス
        }

        /// <summary>
        /// OKボタンが押された時の処理。
        /// </summary>
        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text; // 入力内容をプロパティに保存
            this.DialogResult = true;      // 親ウィンドウ(MainWindow)に「OK」が押されたことを通知
            this.Close();                  // ダイアログを閉じる
        }
    }
}