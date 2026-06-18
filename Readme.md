# XColumn

![XColumn Screenshot](http://masherhouse.com/wp-content/uploads/2025/11/TweetDesk_image.jpg)

TweetDeck（旧）風のシンプルなマルチカラム型クライアントです。X (Twitter) のAPIを使用せず、`WebView2` (Edgeレンダリングエンジン) を利用して複数のタイムラインを効率的に表示します。

## ✨ 主な機能

* **マルチカラム表示**: ホーム、通知、検索、リスト、ユーザーなどをカラムとして自由に追加・並べ替え可能。
* **プロファイル機能**: 複数のアカウントや用途（趣味用、仕事用など）ごとに、Cookieや設定が完全に分離された環境を切り替えられます。
* **多言語対応**: 日本語と英語の表示切り替えに対応しています。
* **Chrome拡張機能のサポート**: 広告ブロック（uBlock Origin等）やスタイル変更（Old Twitter Layout等）などの拡張機能をロードして使用できます。PC内のChromeから直接インポートすることも可能です。
* **NGワード機能**: 指定したキーワードを含むポストを非表示にできます。設定画面からの登録のほか、テキスト選択時の右クリックメニューからも追加可能です。
* **フォーカスモード**: ツイートや設定をクリックすると一時的に単一ビューに切り替わり、詳細確認や長文作成に集中できます。
* **柔軟なレイアウト**:
    * **等分割モード**: ウィンドウ幅に合わせて全カラムを自動リサイズ。
    * **固定幅モード**: 横スクロールで多数のカラムを表示。幅の微調整も可能。
* **カスタマイズ性**:
    * **テーマ切り替え**: ライト・ダーク・システム準拠の3パターンから外観を選択可能。
    * **カスタムCSS**: 全カラムに適用されるCSSを記述し、フォントや配色の微調整が可能。
    * **表示オプション**: 左側メニューの非表示、リストヘッダーの簡略化、右サイドバー（トレンド等）の非表示など、画面を広く使う設定が充実。
    * **RT非表示**: カラムごとにリポスト（RT）や「〇〇さんがいいねしました」等の表示を隠すことができます。

## 🛠️ その他の便利機能
* **休止モード（💤）＆ API制限時の自動休止／自動復帰**: カラム上部の💤ボタンで、重いページを一時的に軽量な待機画面へ置き換えてメモリを解放できます。さらに X のAPI制限（HTTP 429）を検知すると該当カラムを自動で休止し、制限が解除される時刻（待機画面に表示）に自動で復帰します。💤ボタンの再クリックで手動復帰も可能です。（この自動休止は設定でOFFにできます）
* **オフタイマー (自動終了)**: アプリがバックグラウンド（非アクティブ）の状態が指定時間続くと、自動でアプリを終了させることができます。
* **カラム設定の集約**: カラムごとの設定（RT非表示、自動更新など）を歯車メニュー内に格納し、カラム幅を狭くしてもレイアウトが崩れない省スペース設計を採用しています。
* **キーボードショートカット**: 矢印キーでのカラム移動、Ctrl + 数字キーでのジャンプ、PageUp/Downでのスクロール操作に対応。
* **アクティブ時タイマー停止**: アプリを操作している間は自動更新タイマーを止め、TLが勝手に流れるのを防ぎます。
* **アプリ内音量一括管理**: カラム内の動画や音声のボリュームを、ツールバーのスライダーで一括調整できます。
* **ウィンドウ・スナップ**: ウィンドウを移動する際、他のXColumnウィンドウや画面端にピタッと吸着します。
* **サーバー状態監視**: Xのサーバーダウンや制限を検知し、ステータスアイコンで通知します。
* **外部リンクの開き方を選択**: ツイート内の外部サイトへのリンクを、既定のブラウザ／PiPウィンドウ／アプリ内のフォーカスモードのいずれで開くか選べます。
* **自動更新タイミングの分散**: 複数カラムの自動更新が同時に集中してX のAPI制限（429）に陥らないよう、更新間隔を少しずつ分散させます（設定でON/OFF可）。
* **サイトを追加（試験的）**: X/Twitter以外の任意のWebページをカラムとして登録できます（設定の「試験的」タブでON）。

## 🖥️ 動作要件
* Windows 10 / 11
* [.NET 8.0 デスクトップ ランタイム](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 ランタイム](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * （通常、最新のWindowsにはプリインストールされています）

## 🚀 使い方

### ダウンロード
* [v1.48.0(2026.06.18)](https://github.com/mashersan/XColumn/releases)
### 基本操作
1.  `XColumn.exe` を実行します。
2.  **プロファイル**: 必要に応じて「新規」ボタンでプロファイルを作成します（デフォルトでも使用可能です）。
3.  **カラム追加**: メニューバーの **「ファイル」→「新規カラムを追加」** から必要なカラムを追加します。
4.  **並べ替え**: カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムの順序を入れ替えます。
5.  **設定メニュー**: 各カラムの歯車ボタン（⚙️）から、カラムごとの幅調整、自動更新間隔、ズーム倍率、RT/Rep非表示の設定を行えます。
6.  **言語設定**: メニューの **「ツール」→「設定」** から言語（Language）を変更できます（再起動後に適用されます）。

### 起動オプション（コマンドライン引数）
ショートカットの「リンク先」の末尾に引数を追加したり、ターミナルから実行することで、動作を変更できます。
* **`--profile "プロファイル名"`**: 指定したプロファイルで起動します。
    * 例: `XColumn.exe --profile "趣味用"`
* **`--enable-devtools`**: WebView2の開発者ツール (F12キー) を有効にします。
* **`--disable-gpu`**: GPUハードウェアアクセラレーションを無効にします（描画トラブル回避用）。

### カラム内のテーマ（背景色）を変更する方法
カラム内の表示（白・ダーク・漆黒など）は、X (Twitter) 公式の設定から変更することでアプリに反映されます。
1. ホーム画面などの左メニューから **「設定とプライバシー」** を選択します。
2. **「アクセシビリティ、表示、言語」** → **「表示」** を選択します。
3. **「背景」** セクションからお好みのテーマを選択してください。

### 拡張機能（広告ブロック等）の導入方法
Chromeウェブストアの拡張機能をXColumnで使用するには、以下のいずれかの方法で追加します。

**方法A：Chromeからインポート（推奨）**
1.  普段お使いのChromeブラウザで、目的の拡張機能をインストールしておきます。
2.  XColumnのメニュー **「ツール」→「拡張機能の管理...」** を開きます。
3.  **「Chromeからインポート...」** をクリックします。
4.  検出された拡張機能一覧から使用したいものにチェックを入れ、「インポート」を押します。
5.  アプリを再起動すると有効になります。

**方法B：フォルダから手動追加**
1.  拡張機能のフォルダ（`manifest.json`が含まれるフォルダ）を用意します。
2.  **「ツール」→「拡張機能の管理...」** → **「フォルダから追加...」** で選択します。

## 🛠️ ビルド方法 (開発者向け)

1.  このリポジトリをクローンします。
2.  Visual Studio 2022 で `XColumn.sln` を開きます。
3.  .NET 8.0 SDK がインストールされていることを確認します。
4.  NuGet パッケージ マネージャーからパッケージを復元します（WebView2, GongSolutions.WPF.DragDrop等）。
5.  ビルドして実行します。

## 更新履歴
### v1.48.0 (2026/06/18)
- 🚀 機能改善: X のAPI制限（HTTP 429）検知時の挙動を改善。従来は手動で復帰するまでカラムが休止し続けていましたが、
               応答内のリセット時刻（x-rate-limit-reset）を読み取り、制限が解除される時刻に自動でカラムを復帰させるようにしました。
               これにより、リスト等のカラムが制限で停止したまま戻らない問題を解消します。
- ✨ 機能追加: API制限による休止画面に、自動で再開する予定時刻を表示するようにしました。
- 🚀 機能改善: X/Twitter以外のサイトを表示しているカラムは、「ソフト更新」設定に関わらず自動更新時に通常の再読み込み（F5相当）を行うようにしました（ソフト更新のキー送信はX上でのみ有効なため）。

### v1.47.0 (2026/06/17)
- ✨ 機能追加: 外部サイトへのリンクを開く場所を選べる設定を追加。
                「既定のブラウザ」「PiPウィンドウ」「アプリ内のフォーカスモード」から選択できます。
- ✨ 機能追加(試験的): X/Twitter以外の任意のWebページをカラムとして登録できる「サイトを追加」機能を追加。
                        設定の「試験的」タブでONにすると、新規カラム追加メニューに項目が表示されます。
- 🚀 機能改善: 複数カラムの自動更新が同じタイミングに集中してX のAPI制限(HTTP 429)に陥るのを防ぐため、更新間隔を少しずつ分散（ジッター）する仕組みを追加。
                設定の「動作」タブでON/OFFを切り替えられます。
- ⚙ 設定追加: 「API制限(429)を無視する」設定を追加。ONにすると、API制限(HTTP 429)を検知してもカラムを自動で休止モードにしません。
- 🎨 UI改善: 設定画面を整理し、試験的な機能を専用の「試験的」タブに分離。

### v1.46.1 (2026/06/16)
- 🐛 バグ修正: 休止画面の本文で、改行タグ（<br>）がそのまま文字として表示される不具合を修正。

### v1.46.0 (2026/06/16)
- ✨ 機能追加: X (Twitter) 上のYouTube動画の再生に対応。
                「動画クリック時に自動でPiP」がONの場合はPiPウィンドウで全画面表示し、OFFかつ「メディアクリック時に遷移しない」がOFFの場合はフォーカスモードで全画面表示します。
- 🚀 機能改善: 「動画クリック時に自動でPiPを開く」設定を、アプリを再起動しなくても即座に反映するように改善。
- 🐛 バグ修正: マルチモニター環境で、カラムをドラッグ中に表示される影（ゴースト）が別のディスプレイに表示される不具合を修正。
- 🐛 バグ修正: DevTools有効時に、特定の箇所で右クリックするとコンテキストメニューが表示されない不具合を修正。
- 
### v1.45.0 (2026/06/15)
- ✨ 機能追加: X (Twitter) のAPI制限（HTTP 429）を検知した際、該当カラムを自動的に休止モード（💤）へ切り替える機能を追加。
                制限中の無駄なリクエストを止めてサーバー負荷とアカウントへの影響を抑えます。
                復帰は💤ボタンの再クリックで行えます。
- 🔧 内部変更: コードベースを MVVM アーキテクチャへ全面的に再編（名前空間と物理フォルダ構成の整理を含む）。
                アプリの動作・機能に変更はありません。

### v1.44.2 (2026/05/27)
- 🚀 機能追加: 拡張機能の管理ウィンドウに、インポートした拡張機能の保存先をエクスプローラーで表示する「フォルダを開く」ボタンを追加
- 🚀 機能改善: 拡張機能の管理ウィンドウから拡張機能を削除する際、インポート時に作成されたフォルダとその配下のファイルも完全に削除するように動作を変更
- 🐛 バグ修正: 拡張機能が新しいウィンドウで独自の設定画面などを開こうとした際、OSのアプリ選択ダイアログが表示されてしまうエラーを修正し、アプリ内の画面で安全に開くように改善

### v1.44.1 (2026/05/25)
- 🐛 バグ修正: 設定が正常に保存されない問題を修正

### v1.44.0 (2026/05/24)
- ⚙ 設定追加: PiPウィンドウを常に手前に表示する（最前面表示）オプションを追加
- 🚀 機能改善: 枠なしのPiPウィンドウの端をマウスで掴んでリサイズできるように操作性を改善し、デフォルトサイズを800x600に変更
- 🚀 機能改善: アプリ起動時およびプロファイル切り替え時、カラムを時間差で順次読み込むように変更し、多数のカラムが存在する場合のフリーズや読み込み失敗を防止
- 🐛 バグ修正: PiPウィンドウを変更したサイズや位置が保存されず、次回開いたときにリセットされてしまう問題を修正
- 🐛 バグ修正:「動画クリック時に自動でPiPで開く」および「カラムの上下2段表示」の設定が、アプリ再起動時にリセットされてしまう起動時の不具合を修正

### v1.43.0 (2026/05/23)
- ✨ 機能追加: 動画やGIFをクリックした際、拡張機能に頼らずWPFネイティブの最前面PiP（ピクチャー・イン・ピクチャー）ウィンドウで再生する機能を追加
- ⚙ 設定追加: 設定画面のその他→試験的機能に、動画クリック時に自動でPiPで開くオプションを追加
- 🚀 機能改善: X (Twitter) の複雑なSPA遷移（クリックイベント）をC#側で高精度にインターセプトし、確実なPiP切り替えを実現

### v1.42.0 (2026/05/23)
- 🚀 機能追加: リストを大量に使用するユーザー向けに、カラムの上下2段表示レイアウトを追加（等分割との併用や移動も可能）
- 🚀 機能改善: カラム移動後にフォーカスモードを起動すると元のカラムが手前に被る問題（Airspace問題）を修正し、安定性とパフォーマンスを向上
- 🚀 機能改善: アプリ内のハードコードされた日本語文字列を見直し、英語モードでの多言語表示を強化
- 💖 その他: ヘルプメニューに「ご支援・寄付」項目（Buy Me a Coffee / GitHub Sponsors）を追加

### v1.41.0 (2026/05/19)
- 🚀 機能追加: 複数アカウント（マルチプロファイル）に対応。試験的機能として別プロファイルのタイムラインを追加する機能を追加
- 🚀 機能改善: 試験的機能（別プロファイル追加）のON/OFF設定を追加し、メニュー表示を動的に切り替えるように改善
- 🚀 機能改善: マルチプロファイル環境におけるセッション誤爆を防止するため、カラムの所属プロファイルに応じたフォーカスモードの制御を強化
- 🐛 バグ修正: カラムヘッダーの自動更新アイコンがスクロール時に正しく変化しない問題を修正
- 🐛 バグ修正: スクロール検知や動画自動再生無効設定が、既存のカラムへ即時反映されない問題を修正

### v1.40.1 (2026/05/16)
- 🐛 バグ修正: フォーカスモード設定が即時反映されない問題を修正
- 🐛 バグ修正: カラムを追加したあとにフォーカスモードになると表示が崩れる事がある問題修正
- 🚀 機能改善: フォーカスモード遷移の動作を見直し、一部条件で画像が拡大表示されないことがある問題を修正

### v1.40.0 (2026/05/04)
- ✨ 機能追加:メモリ消費を大幅に削減できる「休止（💤）」ボタンをカラム上部に追加しました。
               クリックすると重いページを一時的に軽量な待機画面に置き換えてメモリを解放し、再度クリックで元のページに復帰します。
- 🐛 バグ修正: メニューからプロファイルを切り替えて再起動した際、選択したプロファイルではなくデフォルトのプロファイルが開いてしまう問題を修正しました。
- 🚀 機能改善: サーバー稼働状況（右上のステータスアイコン）の監視ロジックを見直し、バックグラウンドの軽微な通信エラーやキャンセルによって過敏に「接続不可」や「API制限中」と表示されてしまう問題を改善しました。

### 過去の更新内容は添付のtxtファイルをご参照ください。

### v1.0.0 (2025/11/09)
  - 初期リリース (TweetDeskとして)

---

## ⚠️ 免責事項 (重要)
本アプリケーションは個人が開発した非公式クライアントです。X (Twitter) のAPIを使用せず、Webサイトをブラウザコンポーネントで表示・制御しています。

* **機能の保証について**: Xの仕様変更により、予告なく機能しなくなる可能性があります。
* **責任の所在**: 本ソフトウェアの使用によって生じたあらゆる損害（アカウントの制限・凍結、データの消失、PCの不具合など）について、開発者は一切の責任を負いません。
* **自己責任**: 本アプリケーションのダウンロード、インストール、および使用は、**全てユーザー自身の責任において行ってください。**

## 📄 ライセンス (License)

このプロジェクトは **MIT ライセンス** の下で公開されています。

---
---

# XColumn (English)

A simple multi-column client inspired by (old) TweetDeck. It does not use the X (Twitter) API; instead, it uses `WebView2` (the Edge rendering engine) to efficiently display multiple timelines.

## ✨ Key Features

* **Multi-Column Display**: Freely add and reorder columns such as Home, Notifications, Search, Lists, and Users.
* **Profiles**: Switch between environments with completely separated cookies and settings for each account or purpose (e.g., hobby, work).
* **Multi-Language Support**: Switch the display between Japanese and English.
* **Chrome Extensions Support**: Load extensions such as ad blockers (uBlock Origin, etc.) and style modifiers (Old Twitter Layout, etc.). You can also import them directly from Chrome on your PC.
* **Muted Words**: Hide posts containing specified keywords. Add them from the Settings screen, or from the right-click menu when text is selected.
* **Focus Mode**: Click a tweet or settings to temporarily switch to a single view, so you can concentrate on details or composing long posts.
* **Flexible Layout**:
    * **Uniform-width mode**: Automatically resize all columns to fit the window width.
    * **Fixed-width mode**: Display many columns with horizontal scrolling. Fine width adjustment is also possible.
* **Customizability**:
    * **Theme switching**: Choose the appearance from three patterns: Light, Dark, or System.
    * **Custom CSS**: Write CSS applied to all columns to fine-tune fonts and colors.
    * **Display options**: Plenty of settings to use the screen more spaciously, such as hiding the left menu, simplifying list headers, and hiding the right sidebar (Trends, etc.).
    * **Hide RT**: Hide reposts (RT) and "‚óã liked" notices on a per-column basis.

## 🛠️ Other Useful Features
* **Suspend mode (💤) & auto-suspend / auto-resume on rate limit**: Use the 💤 button at the top of a column to temporarily replace heavy pages with a lightweight standby screen and free up memory. In addition, when an API rate limit (HTTP 429) is detected, the column is automatically suspended and then automatically resumes when the limit clears (the estimated resume time is shown on the standby screen). You can also resume manually with the 💤 button. (This auto-suspend can be turned off in Settings.)
* **Off-Timer (Auto Shutdown)**: Automatically shut down the app after it stays in the background (inactive) for a specified period.
* **Consolidated column settings**: Per-column settings (Hide RT, auto-refresh, etc.) are stored inside the gear menu — a space-saving design that keeps the layout from breaking even when a column is made narrow.
* **Keyboard shortcuts**: Move between columns with the arrow keys, jump with Ctrl + number keys, and scroll with PageUp/PageDown.
* **Pause timer while active**: While you are operating the app, the auto-refresh timer is paused to prevent the timeline from scrolling on its own.
* **In-app volume control**: Adjust the volume of videos and audio across all columns at once with the slider in the toolbar.
* **Window Snap**: When moving a window, it snaps neatly to other XColumn windows or to the screen edges.
* **Server status monitoring**: Detects X server outages or restrictions and notifies you with a status icon.
* **Choose how external links open**: Choose whether links to external sites in tweets open in your default browser, a PiP window, or in-app Focus Mode.
* **Staggered auto-refresh**: Spreads out refresh timing so multiple columns don't refresh at the same moment and hit X's API limit (429). Toggleable in Settings.
* **Add site (Experimental)**: Register any web page (not just X/Twitter) as a column (enable it in the "Experimental" tab in Settings).

## 🖥️ System Requirements
* Windows 10 / 11
* [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * (Typically pre-installed on the latest versions of Windows)

## 🚀 How to Use

### Download
* [v1.48.0(2026.06.18)](https://github.com/mashersan/XColumn/releases)
### Basic Operations
1.  Run `XColumn.exe`.
2.  **Profiles**: Create a new profile with the "New" button if needed (the Default profile is also available).
3.  **Add Columns**: Add the columns you need via **"File" → "Add New Column"** in the menu bar.
4.  **Reorder**: Drag and drop the handle at the top of a column (the bar displaying the URL) to change the column order.
5.  **Settings Menu**: Use the gear button (⚙️) on each column to adjust per-column width, auto-refresh interval, zoom factor, and Hide RT/Rep.
6.  **Language**: Change the language from **"Tools" → "Settings"** (applied after restart).

### Command Line Arguments
You can change the behavior by adding arguments to the end of the shortcut's "Target" field, or by running from a terminal.
* **`--profile "Profile Name"`**: Launch with the specified profile.
    * Example: `XColumn.exe --profile "Hobby"`
* **`--enable-devtools`**: Enable WebView2 Developer Tools (F12).
* **`--disable-gpu`**: Disable GPU hardware acceleration (to avoid rendering issues).

### How to Change the In-Column Theme (Background Color)
The in-column display (Light, Dark, Lights out, etc.) follows the official X (Twitter) settings and is reflected in the app.
1. From the left menu on Home, etc., select **"Settings and privacy"**.
2. Select **"Accessibility, display, and languages"** → **"Display"**.
3. Choose your preferred theme under the **"Background"** section.

### How to Install Extensions (Ad-blockers, etc.)
To use Chrome Web Store extensions in XColumn, add them using one of the following methods.

**Method A: Import from Chrome (Recommended)**
1.  Install the desired extension in your regular Chrome browser.
2.  Open **"Tools" → "Manage Extensions..."** in XColumn.
3.  Click **"Import from Chrome..."**.
4.  Check the extensions you want to use from the detected list and click "Import".
5.  Restart the app to apply.

**Method B: Manual Addition from Folder**
1.  Prepare the extension folder (the folder containing `manifest.json`).
2.  Select **"Tools" → "Manage Extensions..."** → **"Add from Folder..."**.

## 🛠️ Build Instructions (for Developers)

1.  Clone this repository.
2.  Open `XColumn.sln` in Visual Studio 2022.
3.  Make sure the .NET 8.0 SDK is installed.
4.  Restore packages from the NuGet Package Manager (WebView2, GongSolutions.WPF.DragDrop, etc.).
5.  Build and run.

## Update History
### v1.48.0 (2026/06/18)
- 🚀 Improved: Improved the behavior when an API rate limit (HTTP 429) is detected. Previously a column stayed suspended until you resumed it manually; now it reads the reset time (x-rate-limit-reset) from the response and automatically resumes the column when the limit clears. This resolves the issue where columns such as lists could remain stopped and not recover.
- ✨ New feature: The rate-limit suspend screen now shows the estimated time at which the column will automatically resume.
- 🚀 Improved: Columns displaying non-X/Twitter sites now perform a normal reload (equivalent to F5) on auto-refresh regardless of the "Soft Refresh" setting (the soft-refresh key press only works on X).

### v1.47.0 (2026/06/17)
- ✨ New feature: Added a setting to choose where links to external sites open.
                   You can select from "Default browser," "PiP window," or "in-app Focus Mode."
- ✨ New feature (Experimental): Added an "Add site" feature to register any web page (not just X/Twitter) as a column.
                                  Turn it on in the "Experimental" tab in Settings to show the item in the new-column menu.
- 🚀 Improved: Added a mechanism that staggers (jitters) refresh intervals so multiple columns don't refresh at the same moment and hit X's API limit (HTTP 429).
                You can toggle it in the "Behavior" tab in Settings.
- ⚙ Added setting: Added an "Ignore API limit (429)" option. When enabled, columns are not automatically switched to Suspend mode even when an API rate limit (HTTP 429) is detected.
- 🎨 UI: Reorganized the Settings screen and moved experimental features into a dedicated "Experimental" tab.

### v1.46.1 (2026/06/16)
- 🐛 Bug fix: Fixed an issue where a line-break tag (<br>) was shown as plain text in the suspend screen body.

### v1.46.0 (2026/06/16)
- ✨ New feature: Added playback support for YouTube videos on X (Twitter).
                   When "Auto-open PiP on video click" is ON, the video opens fullscreen in a PiP window; when it is OFF and "Don't navigate on media click" is also OFF, it opens fullscreen in Focus Mode.
- 🚀 Improved: The "Auto-open PiP on video click" setting now applies immediately without restarting the app.
- 🐛 Bug fix: Fixed an issue where the drag ghost of a column appeared on a different display in multi-monitor environments.
- 🐛 Bug fix: Fixed an issue where the context menu did not appear when right-clicking in certain areas while DevTools was enabled.
- 
### v1.45.0 (2026/06/15)
- ✨ New feature: Added automatic switching to Suspend mode (💤) for a column when an API rate limit (HTTP 429) is detected.
                   This halts unnecessary requests during the limit to reduce server load and impact on your account.
                   Click the 💤 button again to resume.
- 🔧 Internal: Refactored the codebase to an MVVM architecture (including namespace and physical folder restructuring).
                No changes to app behavior or features.

### v1.44.2 (2026/05/27)
- 🚀 New feature: Added an "Open Folder" button to the extension management window to show the saved location of imported extensions in Explorer.
- 🚀 Improved: Changed the behavior when removing an extension from the management window to completely delete the folder and its files created during import.
- 🐛 Bug fix: Fixed an error where the OS app selection dialog appeared when an extension tried to open its own settings page in a new window, so it now opens safely within the app.

### v1.44.1 (2026/05/25)
- 🐛 Bug fix: Fixed an issue where settings were not saved correctly.

### v1.44.0 (2026/05/24)
- ⚙ Added setting: Added an option to keep the PiP window always on top.
- 🚀 Improved: Improved usability by allowing the borderless PiP window to be resized by dragging its edges, and changed the default size to 800x600.
- 🚀 Improved: Changed column loading to sequential, time-staggered loading during app startup and profile switching, preventing freezes or load failures when many columns are present.
- 🐛 Bug fix: Fixed an issue where changes to the PiP window's size and position were not saved and were reset the next time it was opened.
- 🐛 Bug fix: Fixed a startup timing issue where the "Auto-open PiP on video click" and "2-tier column layout" settings were reset when the app restarted.

### v1.43.0 (2026/05/23)
- ✨ New feature: Added a feature to play videos and GIFs in a native WPF top-most PiP (Picture-in-Picture) window when clicked, without relying on extensions.
- ⚙ Added setting: Added an option under Settings → Other → Experimental Features to automatically open videos in PiP when clicked.
- 🚀 Improved: Accurately intercept X (Twitter)'s complex SPA transitions (click events) on the C# side to achieve reliable PiP switching.

### v1.42.0 (2026/05/23)
- 🚀 New feature: Added a 2-tier column layout for users who use many lists (can be combined with Uniform-width mode and moved freely).
- 🚀 Improved: Fixed the "Airspace" issue where the original column appeared on top when entering Focus Mode after moving a column, improving stability and performance.
- 🚀 Improved: Reviewed hardcoded Japanese strings within the app to enhance multi-language (English) display.
- 💖 Other: Added a "Support / Donate" item (Buy Me a Coffee / GitHub Sponsors) to the Help menu.

### v1.41.0 (2026/05/19)
- 🚀 New feature: Added support for multiple accounts (multi-profile). Added the ability to add timelines from other profiles as an experimental feature.
- 🚀 Improved: Added an ON/OFF setting for experimental features (adding other profiles) and improved the menu to toggle its visibility dynamically.
- 🚀 Improved: Strengthened Focus Mode control based on each column's profile to prevent session cross-contamination in multi-profile environments.
- 🐛 Bug fix: Fixed an issue where the auto-refresh icon in the column header did not change correctly when scrolling.
- 🐛 Bug fix: Fixed an issue where scroll detection and the video autoplay-disable setting were not immediately reflected in existing columns.

### v1.40.1 (2026/05/16)
- 🐛 Bug fix: Fixed an issue where Focus Mode settings were not immediately applied.
- 🐛 Bug fix: Fixed an issue where the display could become corrupted when entering Focus Mode after adding a column.
- 🚀 Improved: Revised the behavior of Focus Mode transitions and fixed an issue where images might not enlarge under certain conditions.

### v1.40.0 (2026/05/04)
- ✨ New feature: Added a "Suspend (💤)" button at the top of columns to drastically reduce memory consumption.
               Clicking it temporarily replaces heavy pages with a lightweight standby screen to free up memory, and clicking it again restores the original page.
- 🐛 Bug fix: Fixed an issue where restarting the app after switching profiles from the menu would open the default profile instead of the selected one.
- 🚀 Improved: Revised the monitoring logic for the server status indicator (top right), resolving an issue where it would too sensitively display "Disconnected" or "Rate Limited" due to minor background network errors or cancellations.

### For the full past update history, please refer to the attached .txt file.

### v1.0.0 (2025/11/09)
  - Initial release (as TweetDesk).

---

## ⚠️ Disclaimer (Important)
This application is an unofficial client developed by an individual. It does not use the X (Twitter) API but displays and controls the website using browser components.

* **No Warranty**: Features may stop working without notice due to changes in X's specifications.
* **Liability**: The developer assumes no responsibility for any damages (account restrictions/freezing, data loss, PC malfunctions, etc.) arising from the use of this software.
* **Use at Your Own Risk**: Download, installation, and use of this application are **entirely at the user's own risk.**

## 📄 License

This project is released under the **MIT License**.
