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
* **キーボードショートカット**: 矢印キーでのカラム移動、Ctrl + 数字キーでのジャンプ、PageUp/Downでのスクロール操作に対応。
* **アクティブ時タイマー停止**: アプリを操作している間は自動更新タイマーを止め、TLが勝手に流れるのを防ぎます。
* **アプリ内音量一括管理**: カラム内の動画や音声のボリュームを、ツールバーのスライダーで一括調整できます。
* **ウィンドウ・スナップ**: ウィンドウを移動する際、他のXColumnウィンドウや画面端にピタッと吸着します。
* **サーバー状態監視**: Xのサーバーダウンや制限を検知し、ステータスアイコンで通知します。

## 🖥️ 動作要件

* Windows 10 / 11
* [.NET 8.0 デスクトップ ランタイム](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 ランタイム](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * （通常、最新のWindowsにはプリインストールされています）

## 🚀 使い方

### 基本操作
1.  `XColumn.exe` を実行します。
2.  **プロファイル**: 必要に応じて「新規」ボタンでプロファイルを作成します（デフォルトでも使用可能です）。
3.  メニューバーの **「ファイル」→「新規カラムを追加」** から必要なカラムを追加します。
4.  **並べ替え**: カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムの順序を入れ替えます。
5.  **言語設定**: メニューの **「ツール」→「設定」** から言語（Language）を変更できます（再起動後に適用されます）。

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
- **v1.22.1 (2025/12/10)**
  - **UI修正**: 設定ウィンドウのOK,キャンセルボタンの幅を少し大きくしました。
  - **UI修正**: NGワード設定画面の文字の重なりを修正しました
- **v1.22.0 (2025/12/10)**
  - **機能追加**: 右クリックメニューに「コピー」・「貼り付け」・「切り取り」・「Googleで検索」を追加しました。
 　　　　　　　　 「貼り付け」及び「切り取り」は入力欄でのみ有効です。
  - **機能追加**: カラム上部のURL部分の表示/非表示を切り替える設定を追加しました。
- **v1.21.0 (2025/12/09)**
  - **機能追加**: NGワード機能を追加しました。設定画面の「ミュート」タブ、またはテキスト選択時の右クリックメニューから登録できます。
  - **機能改善**: プロファイルの複製機能を追加しました。
  - **機能改善**: プロファイルメニューの構造を見直し、各プロファイル名の下に操作メニュー（切り替え、複製など）を配置しました。
- **v1.20.0 (2025/12/09)**
  - **機能追加**: 多言語対応（日本語 / 英語）を追加しました。設定画面から切り替え可能です。
- **v1.19.0 (2025/12/09)**
  - **機能追加**: PC内のChromeブラウザにインストールされている拡張機能をスキャンし、簡単にインポートする機能を追加しました。（拡張機能の管理 > Chromeからインポート）
  - **ドキュメント**: Readmeの拡張機能導入手順を更新しました。
- **v1.18.3 (2025/12/08)**
  - **仕様変更**: カラムへのジャンプショートカットキーを正式に「数字キー」から「Ctrl + 数字キー」に変更しました。
  - **不具合修正**: 自動更新頻度などの設定を変更しても、アプリ再起動時に保存・復元されない問題を修正しました。
  - **不具合修正**: 日本語入力(IME)での変換操作中に、カーソルキー入力がアプリのショートカットとして誤検知され、入力フォーカスが外れてしまう問題に対応（仮）。
  - **改善**: 内部スクリプトの管理構造をリファクタリングし、保守性と安定性を向上させました。
- **v1.18.2 (2025/12/04)**
  - **不具合修正**: 一部環境でカラムの自動更新のカウントダウンタイマーが正しく動作しない問題を修正しました。
- **v1.18.1 (2025/12/03)**
  - **不具合修正**: 一部環境で未読位置保持設定が正しく動作しない問題を修正しました。
- **v1.18.0 (2025/12/03)**
  - **設定追加**: 未読位置保持設定を追加しました。（設定 > カラム > 未読位置を保持）
- **v1.17.2 (2025/12/01)**
  - **不具合修正**: 設定「ポスト/画像クリック時に遷移しない」をOFFにしても、フォーカスモードに遷移しない問題を修正しました。
- **v1.17.1 (2025/12/01)**
  - **操作性改善**: 画像ビューア（メディア表示）での閲覧中や、テキスト入力中にカラム移動ショートカットが暴発しないよう挙動を改善しました。
- **v1.17.0 (2025/12/01)**
  - **UI刷新**: ModernWpfUIを導入し、Windows 11ライクなモダンなデザインに刷新しました。
  - **UI改善**: 設定画面からアプリのテーマ（ライト/ダーク/システム設定）を切り替えられるようになりました。
  - **UI改善**: 設定項目をタブ（表示、カラム、動作、カスタムCSS）に分類し、レイアウトを見やすく整理しました。
  - **操作性改善**: クリックによるカラム選択・フォーカス移動機能を追加し、レイアウト崩れを修正しました。
- **v1.16.0 (2025/11/30)**
  - **機能追加**: キーボードショートカットによるナビゲーション機能の追加
    - 矢印キー（←→）: カラムのフォーカス移動
    - 数字キー（1-9）: 指定した順序のカラムをフォーカス
    - PageUp/Down: アクティブなカラムのスクロール
  - **不具合修正**: 一部の拡張機能が正しく動作しない問題を修正しました。
- **v1.15.0 (2025/11/29)**
  - **UI刷新**: 画面上部のツールバーを標準的なメニューバー（ファイル、プロファイル、ツール、ヘルプ）に変更し、より広く画面を使えるようにしました。
  - **不具合修正**: プロファイルの名前変更や削除時に、特定の条件下でエラーが発生したりファイルが完全に削除されない問題を修正しました。
  - **機能追加**: デバッグ情報をエクスポートする機能を追加しました。（ヘルプ > デバッグ情報をエクスポート）
- **v1.14.2 (2025/11/29)**
  - **不具合修正**: フォーカスモードに遷移した際に、cssカスタマイズが適用されない問題を修正しました。 
  - **設定追加**: ポスト内のリンクをクリックした際に、フォーカスモードを使用しない設定をを追加しました。
- **v1.14.1 (2025/11/29)**
  - **不具合修正**: Rep非表示機能が正常に動作しない問題を修正しました。
- **v1.14.0 (2025/11/29)**
  - **機能追加**: トレンド（Explore）カラム内のトレンドワードやハッシュタグをクリックした際、そのワードで検索した新しいカラムを自動的に追加する機能を追加しました。
  - **設定追加**: 新しいカラムを追加する位置を「右端（末尾）」か「左端（先頭）」か選択できる設定を追加しました。（設定 > 一般 > カラムのレイアウト）
  - **UI改善**: カラムのドラッグ時に、他のカラムの間に挿入される位置を示すガイドラインを表示するようにしました。
- **v1.13.0 (2025/11/28)**
  - **機能追加**: 「カラムを追加」ボタンにトレンドを追加しました。
  - **機能追加**: カラムヘッダーに戻るボタン「←」を追加しました。
- **v1.12.0 (2025/11/28)**
  - **機能追加**: カラムごとに「リプライ（返信）」を非表示にする機能を追加しました。
  - **UI改善**: カラムヘッダーを2行レイアウトに変更し、URLと操作ボタンを分離して使いやすくしました。
- **v1.11.6 (2025/11/27)**
  - **不具合修正**: 検索結果からフォーカスモード中に移行した後、元のカラムに戻れなくなる問題を修正しました。
  - **UI改善**: フォーカスモード中に左側メニュー等が表示されないようにしました。
- **v1.11.5 (2025/11/27)**
  - **操作性改善**: 検索結果カラム戻るボタン非表示に変更
  - **UI改善**: 画面上部の「アクティブ時停止」を「アクティブ時自動更新停止」に文言変更し、機能を分かりやすくしました。
- **v1.11.4 (2025/11/27)**
  - **設定追加**: メディアクリック時にフォーカスモードに遷移するか選択する設定を追加しました。
  - **不具合修正**: バージョンアップ通知で「いいえ」を押した時にそのバージョンをスキップしなかった問題を修正しました。
- **v1.11.3 (2025/11/27)**
  - **操作性改善**: タッチパッドでの横スクロールの挙動（方向・滑らかさ）を修正しました。
  - **不具合修正**: フォーカスモード終了時に表示設定（メニュー非表示など）が再適用されない問題を修正しました。
  - **機能追加**: カラムヘッダーの右クリックメニューに削除機能を追加しました。
- **v1.11.2 (2025/11/27)**
  - **不具合修正**: Home、End、PageUP、PageDownキー押下時の挙動を修正
- **v1.11.1 (2025/11/26)**
  - **不具合修正**: カラム幅を指定した際に、投稿欄などでカーソルキー（←→↑↓）が効かない問題を修正しました。
  - **操作性改善**: タッチパッドでの横スクロールの挙動を改善しました。
- **v1.11.0 (2025/11/26)**
  - **サーバー稼働状況モニター**: X(Twitter)サーバーへの接続状態を常時監視し、ステータスバーに表示する機能を追加。クリックで障害情報サイト(Downdetector)へアクセス可能です。
  - **UI改善**: ツールバーのボタンを機能ごとにドロップダウンメニューに整理し、画面を広く使えるようにしました。
- **v1.10.1 (2025/11/26)**
  - **スナップ機能の強化**: 複数ウィンドウをスナップさせている際、一方をアクティブにするともう一方のウィンドウも連動して最前面に表示されるように改善しました。
- **v1.10.0 (2025/11/26)**
  - **ウィンドウスナップ機能**: ウィンドウ移動時に画面端や他のウィンドウに吸着する機能を追加。吸着したウィンドウ同士の追従移動にも対応。
  - **設定項目の追加**: スナップ機能の有効/無効を選択画面から切り替え可能に。
- **v1.9.0 (2025/11/25)**
  - **フォント設定機能**: フォントの種類とサイズ(px)を設定画面から変更可能に。サイズ入力用の増減ボタンを追加。
  - **RT非表示機能**: カラムごとにリポスト（リツイート）を非表示にする設定を追加。
  - **操作性改善**:
    - タッチパッドでの横スクロールに仮対応。
    - テキストボックス内でのカーソル移動の不具合を修正。
- **v1.8.0 (2025/11/25)**
  - **音量コントロール**: ツールバーに音量スライダーを追加。
  - **レイアウト設定の強化**:
    - カラム幅のカスタマイズ（スライダー）
    - 等分割モードと固定幅モード（横スクロール）の切り替え
  - **カスタムCSS**: ユーザー独自のCSSを適用可能に。
  - 拡張機能の削除、無効時の処理見直し
  - 拡張機能「Old Twitter Layout(2025)」の設定ページを開けるように修正
  - フォーカスモードの遷移ロジック見直し、改善
- **v1.7.0 (2025/11/22)**
  - **設定項目の追加**: 自動更新時にページリロードを行うか、ソフト更新を行うかを選択可能に。
    - マウスオーバーかつスクロール中は自動更新（トップ戻り）を防ぐ機能を追加。
- **v1.6.0 (2025/11/22)**
  - **画面表示オプション機能を追加**: 設定画面から以下の表示をカスタマイズ可能になりました。
    - 左側メニューの非表示（ホーム画面 / それ以外のカラムで個別に設定可）
    - リストヘッダー（画像や詳細情報）の簡易表示
- **v1.5.1 (2025/11/19)**
  - **Chrome拡張機能の設定画面機能を追加**: インストール済み拡張機能のオプションページを開けるようになりました。
  - 拡張機能管理画面のUI調整。
- **v1.5.0 (2025/11/18)**
  - **Chrome拡張機能のサポートを追加**: 広告ブロッカーなどを導入可能になりました。
  - ソースコードの大幅な整理とドキュメントコメントの追加。
  - 安定性向上のため、WebView2パッケージのバージョンを安定版に変更。
- **v1.4.2 (2025/11/17)**
  - アプリがアクティブ（操作中）の時に自動更新を一時停止するかどうかを選択できる設定を追加。
  - アプリが非アクティブから復帰した際、タイマーをリセットせず前回の残り時間から再開するように挙動を改善。
- **v1.4.1 (2025/11/17)**
  - 自動更新をONにした直後にカウントダウンが始まらない不具合を修正
  - UI上で現在使用中のプロファイル名が判別しやすいよう改善
  - カラムから直接リポストしようとした際に、フォーカスモードに移行せず、正常にリポストできるよう修正
- **v1.4.0 (2025/11/17)**
  - 複数プロファイル機能を追加（設定だけでなく、Xアカウントのログイン状態も切り替え可能）
  - プロファイルの新規作成、名前変更、削除機能を追加
  - アプリがアクティブ（フォアグラウンド）の時は自動更新を停止し、非アクティブ（バックグラウンド）時に再開するよう変更
  - コードのリファクタリング（機能ごとのファイル分割）
- **v1.3.1 (2025/11/13)**
  - マルチディスプレイ環境において、セカンダリディスプレイ上にウィンドウを配置した際に、ウィンドウの位置とサイズが正しく保存・復元されない不具合を修正
  - （開発）マルチディスプレイ対応（System.Windows.Formsの参照）に伴うビルドエラーを修正
- **v1.3.0 (2025/11/13)**
  - ポスト内の外部リンクをクリックした際、アプリ内ではなく既定のブラウザで開くよう修正
  - アップデート確認ダイアログに、GitHubリリースの更新内容を表示するよう改善
- **v1.2.0 (2025/11/11)**
  - アプリ名を `TweetDesk` から `XColumn` に変更
  - フォーカスモードから戻る際、元のタイムライン表示に戻るよう修正
  - フォーカスモード中に返信画面に移動してもフォーカスが解除されないよう修正
  - 起動時のアップデート確認機能を追加
- **v1.1.0 (2025/11/09)**
  - DPAPI による設定ファイルの暗号化と復号を実装
  - 設定ファイルの拡張子を `.json` から `.dat` に変更
  - フォーカスモードの復元と遷移ロジックを改善
  - 設定読み込み時のエラーハンドリングを強化
  - デフォルトカラムのロードロジックを改善
- **v1.0.0 (2025/11/09)**
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

A simple multi-column client inspired by (old) TweetDeck. It does not use the X (Twitter) API, but instead utilizes `WebView2` (Edge rendering engine) to efficiently display multiple timelines.

## ✨ Key Features

* **Multi-Column Display**: Freely add and rearrange columns for Home, Notifications, Search, Lists, Users, etc.
* **Profiles**: Switch between completely isolated environments (Cookies/Settings) for multiple accounts or purposes (e.g., Personal, Work).
* **Multi-language Support**: Supports switching between Japanese and English.
* **Chrome Extensions Support**: Load extensions like Ad-blockers (uBlock Origin) or style modifiers (Old Twitter Layout). You can also import them directly from Chrome on your PC.
* **NG Word Functionality**: Mute posts containing specific keywords. Words can be registered via Settings or the right-click menu.
* **Focus Mode**: Clicking on a tweet or settings temporarily switches to a single view, allowing you to focus on details or drafting long posts.
* **Flexible Layout**:
    * **Uniform Grid**: Automatically resizes all columns to fit within the window width.
    * **Fixed Width**: Displays many columns with horizontal scrolling. Width can be fine-tuned.
* **Customizability**:
    * **Themes**: Choose from Light, Dark, or System default.
    * **Custom CSS**: Apply global CSS to all columns to tweak fonts or colors.
    * **Display Options**: Hide left menu, simplify list headers, hide right sidebar (Trends), etc.
    * **Hide RTs**: Option to hide Reposts (RTs) or "liked by..." notifications per column.

## 🛠️ Other Useful Features
* **Keyboard Shortcuts**: Arrow keys to move between columns, Ctrl + Number keys to jump, PageUp/Down to scroll.
* **Pause on Active**: Automatically pauses auto-refresh timers while you are interacting with the app to prevent the timeline from flowing away.
* **Global Volume Control**: Adjust the volume of videos/audio in all columns at once via the toolbar slider.
* **Window Snapping**: Windows snap to other XColumn windows or screen edges when moved.
* **Server Status Monitor**: Detects X server outages or limits and notifies you via a status icon.

## 🖥️ System Requirements

* Windows 10 / 11
* [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
    * (Usually pre-installed on modern Windows)

## 🚀 How to Use

### Basic Operations
1.  Run `XColumn.exe`.
2.  **Profiles**: Create a new profile if needed (Default is also available).
3.  Add columns via **"File" -> "Add New Column"** in the menu bar.
4.  **Reorder**: Drag and drop the handle (bar displaying the URL) at the top of a column to change its order.
5.  **Language**: You can change the language from **"Tools" -> "Settings"** (Requires restart).

### How to Install Extensions (Ad-blockers, etc.)
To use Chrome Web Store extensions in XColumn:

**Method A: Import from Chrome (Recommended)**
1.  Install the desired extension in your regular Chrome browser.
2.  Open **"Tools" -> "Manage Extensions..."** in XColumn.
3.  Click **"Import from Chrome..."**.
4.  Check the extensions you want to use and click "Import".
5.  Restart the app to apply.

**Method B: Manual Addition from Folder**
1.  Prepare the extension folder (containing `manifest.json`).
2.  Select **"Tools" -> "Manage Extensions..."** -> **"Add from Folder..."**.

## ⚠️ Disclaimer
This application is an unofficial client developed by an individual. It does not use the X (Twitter) API but controls the website using browser components.

* **No Warranty**: Features may stop working without notice due to changes in X's specifications.
* **Liability**: The developer assumes no responsibility for any damages (account restrictions/freezing, data loss, PC malfunctions, etc.) arising from the use of this software.
* **Use at Your Own Risk**: Download, installation, and use of this application are entirely at the user's own risk.

## 📄 License

This project is released under the **MIT License**.