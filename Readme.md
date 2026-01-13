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
* **オフタイマー (自動終了)**: アプリがバックグラウンド（非アクティブ）の状態が指定時間続くと、自動でアプリを終了させることができます。
* **カラム設定の集約**: カラムごとの設定（RT非表示、自動更新など）を歯車メニュー内に格納し、カラム幅を狭くしてもレイアウトが崩れない省スペース設計を採用しています。
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
3.  **カラム追加**: メニューバーの **「ファイル」→「新規カラムを追加」** から必要なカラムを追加します。
4.  **並べ替え**: カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムの順序を入れ替えます。
5.  **設定メニュー**: 各カラムの歯車ボタン（⚙️）から、カラムごとの幅調整、自動更新間隔、ズーム倍率、RT/Rep非表示の設定を行えます。
6.  **言語設定**: メニューの **「ツール」→「設定」** から言語（Language）を変更できます（再起動後に適用されます）。

### 別プロファイルを指定して起動する方法（コマンドライン引数）
ショートカットの「リンク先」の末尾に引数を追加したり、ターミナルから実行することで、特定のプロファイルで直接起動できます。
* **書式**: `XColumn.exe --profile "プロファイル名"`
* **例**: `XColumn.exe --profile "趣味用"`

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
- **v1.32.0 (2026/01/13)**
  - **機能改善**: フォーカスモード（詳細表示）を画面切り替え方式から、ウィンドウ全体を覆うモーダル（オーバーレイ）表示に刷新しました。
- **v1.31.0 (2026/01/05)**
  - **機能追加**: 各カラムの設定メニュー（歯車）に、表示倍率（ズーム）を変更する機能を追加しました。
  - **機能改善**: 設定メニューの入力欄（更新間隔・ズーム）でEnterキーを押した際、即座に値を反映するようにしました。
- **v1.30.1 (2026/01/02)**
  - **UI改善**: 設定画面のタブに「その他」を追加し、一部設定を移動しました。
- **v1.30.0 (2026/01/01)**
  - **機能追加**: 各カラムの設定メニュー（歯車）から、カラムごとの幅を個別に設定できるようになりました。
  - **機能追加**: 設定画面に「起動時に最新版をチェックする」オプションを追加しました。
  - **不具合修正**: サーバー監視の間隔設定が再起動後にリセットされてしまう問題を修正しました。
- **v1.29.3 (2025/12/30)**
  - **不具合修正**: カラム設定メニューから「RT非表示」「Rep非表示」を切り替えた際、変更が即座に画面に反映されない問題を修正しました。
- **v1.29.2 (2025/12/29)**
  - **機能改善**: カラムヘッダーのレイアウトを刷新しました。設定項目（RT非表示、自動更新など）を歯車メニュー内に集約し、カラム幅を狭くしてもボタンが隠れないようにしました。
  - **機能改善**: ウィンドウのタイトルバーに、現在のバージョンと使用中のプロファイル名を表示するようにしました。
- **v1.29.1 (2025/12/29)**
  - **不具合修正**: オフタイマーを使用する設定が正常に保存されない問題を修正しました。 
- **v1.29.0 (2025/12/29)**
  - **機能追加**: オフタイマー（自動終了）機能を実装しました。非アクティブ状態が一定時間続くとアプリを自動終了できます。
  - **機能改善**: カラムの自動更新間隔の入力欄でEnterキーを押した際、即座に設定を反映しタイマーをリセットするようにしました。
  - **不具合修正**: フォーカスモードで返信等の入力中にカーソルキー（←/→）での移動ができない問題を修正しました。
  - **不具合修正**: 旧IME使用時などに、入力開始直後に自動更新が走り、入力内容が勝手に確定されてしまう問題を（多分）修正しました。
- **v1.28.0 (2025/12/22)**
  - **機能追加**: カラム追加メニューに「ユーザー」を追加しました。
  - **機能追加**: 設定画面に、X (Web) の「表示設定」「自動再生設定」へ直接アクセスするリンクを追加しました。
  - **不具合修正**: Chrome拡張機能の名前が正しく表示されない問題を修正しました。
- **v1.27.0 (2025/12/21)**
  - **機能追加**: カラムの「クリア」機能（🧹ボタン）を実装しました。
    - **メモリ最適化**: X (Twitter) のようなSPAを長時間表示し続けると、WebView2内にDOM要素やスクリプトの状態が蓄積されメモリを圧迫しますが、
                        本機能による再ナビゲーションでこれらを解放し、動作を軽量化します。 
- **v1.26.0 (2025/12/19)**
  - **機能追加**:「新規カラムを追加」メニューに「グローバルトレンド」を追加しました。
  - **仕様変更**: グローバルトレンドカラムのメニュー表示設定が「ホーム以外」の設定に従うよう修正しました。
- **v1.25.3 (2025/12/19)**
  - **不具合修正**: 画像表示から戻った際、意図しない位置（過去に閲覧した画像の位置など）へスクロールが復元されてしまう問題を修正しました。
  - **機能改善**: スクロール位置の保持タイミングを改善し、タイムラインのトップ（一番上）にいる状態をより確実に保存するようにしました。
- **v1.25.2 (2025/12/18)**
  - **機能追加**: 設定画面に「リスト自動遷移の待機時間」の設定項目を追加しました。
  - **機能改善**: リスト自動遷移のロジックを刷新し、ウィンドウサイズが小さい場合や特定条件下で正常に遷移しない問題を改善しました。
  - **UI修正**: 設定画面の「OK/キャンセル」ボタンのサイズを微調整（拡大）し、操作性を向上させました。
- **v1.25.1 (2025/12/18)**
  - **不具合修正**: 拡張機能の設定ページで保存せずに戻ろうとした際、確認ダイアログによってアプリ全体が操作不能（フリーズ）になる問題を修正しました。
  - **機能改善**: 「拡張機能の管理」ウィンドウのレイアウトを修正しました。ウィンドウ幅を広げた際に操作ボタンがバラけず、正しく整列されるようになりました。
- **v1.25.0 (2025/12/16)**
  - **機能追加**: ESCキーでカラムの「戻る」操作やフォーカスモードの終了ができるようになりました。
  - **機能改善**: カラム遷移から戻った際のスクロール位置ズレを補正する機能を追加しました。
  - **不具合修正**: 設定ページなどを開いたまま終了すると、次回起動時にカラムが消滅する問題を修正しました。
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

A simple multi-column client inspired by (old) TweetDeck.

## ✨ Key Features
* **Multi-Column Display**: Home, Notifications, Search, Lists, etc.
* **Profiles**: Isolated environments for multiple accounts.
* **Chrome Extensions Support**: Load Ad-blockers or style modifiers.
* **Focus Mode**: Detailed view for tweets and settings.
* **Customizability**: Themes, Custom CSS, and various display options.

## 🖥️ System Requirements
* Windows 10 / 11
* [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
* (Typically pre-installed on the latest versions of Windows)

## 🚀 How to Use

### Basic Operations
1.  Run `XColumn.exe`.
2.  **Profiles**: Create a new profile if needed (Default is also available).
3.  **Add Columns**: Add columns via **"File" -> "Add New Column"** in the menu bar.
4.  **Reorder**: Drag and drop the handle (bar displaying the URL) at the top of a column to change its order.
5.  **Column Settings**: Use the gear icon (⚙️) on each column to adjust column width, auto-refresh intervals, zoom factor, and display filters.
6.  **Language**: You can change the language from **"Tools" -> "Settings"** (Requires restart).

### Command Line Arguments
Launch with a specific profile directly:
* **Format**: `XColumn.exe --profile "Profile Name"`

### Changing Background Theme
The in-column display (Light, Dark, Lights out) follows X (Twitter) official settings.
1. Open **"Settings and privacy"** from the left menu on Home.
2. Select **"Accessibility, display, and languages"** -> **"Display"**.
3. Choose your preferred theme under the **"Background"** section.

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


## 🛠️ Update History
- **v1.32.0 (2026/01/13)**
  - **Improvement**: Refreshed Focus Mode (detailed view) from a screen-switching method to a modal (overlay) display that covers the entire window.
- **v1.31.0 (2026/01/05)**
  - **New Feature**: Added a "Zoom" setting to the column settings menu, allowing you to adjust the display scale.
  - **Improvement**: Pressing Enter in the settings input boxes (Refresh Interval, Zoom) now immediately applies the changes.
- **v1.30.1 (2026/01/02)**
  - **UI Improvements**: Added an "Other" tab to the Settings screen and moved some settings.
- **v1.30.0 (2026/01/01)**
  - **New Feature**: Added the ability to set individual column widths from the column settings menu.
  - **New Feature**: Added an option to toggle "Check for updates on startup" in the settings.
  - **Fix**: Resolved an issue where the server check interval setting was not saved across restarts.
- **v1.29.3 (2025/12/30)**
  - **Bug Fix**: Fixed an issue where toggling "Hide RT" or "Hide Rep" from the column settings menu would not immediately reflect the changes on the screen.
- **v1.29.2 (2025/12/29)**
  - **Feature Improvement**: The column header layout has been revamped. Settings (hide RT, auto-update, etc.) have been consolidated into the gear menu, so the buttons are not hidden even when the column width is narrowed.
  - **Feature Improvement**: The window title bar now displays the current version and the name of the profile being used.
- **v1.29.1 (2025/12/29)**
  - **Bug Fix**: Fixed an issue where settings using the Off Timer were not saved correctly.
- **v1.29.0 (2025/12/29)**
  - **New Feature**: Added an Off-Timer (Auto Shutdown) function.
  - **Improvement**: Pressing Enter in the auto-refresh interval box now immediately applies the setting and resets the timer.
  - **Fix**: Resolved an issue where arrow keys would trigger column navigation while typing in Focus Mode.
  - **Fix**: Fixed an issue where auto-refresh would interrupt input when using legacy IME.
- **v1.28.0 (2025/12/22)**
  - **New Feature**: Added "User..." to the Add Column menu.
  - **New Feature**: Added links to X (Web) Display/Autoplay settings within the app settings.
  - **Improvement**: Implemented a policy to prevent automatic video playback.
  - **Fix**: Resolved an issue where some Chrome extensions (e.g., Tampermonkey) were not displaying their correct names.
  - **Fix**: Fixed an issue where clicking settings links would unintentionally add a Home column.
  - **Fix**: Resolved an issue where column settings would reset if the app was closed while viewing a settings page.
- **v1.25.3 (2025/12/19)**
  - **Fix**: Resolved an issue where returning from an image view would occasionally scroll back to the position of a previously viewed image.
  - **Improvement**: Improved the timing of scroll position preservation to more reliably save the state when returning to the top of the timeline.
- **v1.25.2 (2025/12/18)**
  - **Feature Added**: Added a "List Auto-Transition Wait Time" setting to the Settings screen.
  - **Feature Improvement**: Revised the logic for list auto-transitions to resolve issues with small window sizes and certain conditions where transitions would not occur correctly.
  - **UI Fix**: Slightly tweaked (enlarged) the size of the "OK/Cancel" buttons on the Settings screen to improve usability.
- **v1.25.1 (2025/12/18)**
  - **Fix**: Resolved an issue where the app became unresponsive when closing extension settings without saving.
  - **Improvement**: Fixed the layout of the "Manage Extensions" window for better resizing behavior.

## ⚠️ Disclaimer
This is an unofficial client. Use at your own risk. The developer is not responsible for any account restrictions or data loss.

## 📄 License
Released under the **MIT License**.