# TweetDesk

![TweetDesk Screenshot](http://masherhouse.com/wp-content/uploads/2025/11/TweetDesk_image.jpg) 
TweetDeck（旧）風のシンプルなマルチカラム型クライアントです。X (Twitter) のAPIを使用せず、`WebView2` を利用してカラムを表示します。

## ✨ 主な機能

* **マルチカラム表示**:
    * ホーム、通知、検索、リストをカラムとして自由に追加できます。
    * ドラッグ＆ドロップでカラムの順番を自由に入れ替えられます。
* **フォーカスモード**:
    * カラム内のツイート（ポスト）をクリックすると、そのツイートを単一表示する「フォーカスモード」に切り替わります。
    * フォーカスモードから戻る（または別のページに移動する）と、元のカラム一覧表示が復元されます。
* **カラムごとの自動更新**:
    * カラムごとに「手動更新」ボタンを搭載。
    * カラムごとに自動更新の「有効/無効」と「更新間隔（秒）」を設定できます。
    * 自動更新が有効なカラムには、次の更新までのカウントダウンが表示されます。
* **セッションの保存**:
    * 現在のカラム構成（並び順、URL、更新設定）をアプリ終了時に自動で保存し、次回起動時に復元します。
    * アプリのウィンドウサイズと位置、フォーカスモードの状態も保存・復元されます。

## 🖥️ 動作要件

* Windows 10 / 11
* [.NET 8.0 デスクトップ ランタイム](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 ランタイム](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * （通常、最新のWindowsにはプリインストールされています）

## 🚀 使い方

1.  [リリースページ](https://github.com/mashersan/TweetDesk/releases)から最新版の `.zip` をダウンロードします。
2.  任意のフォルダにZIPを展開します。
3.  `TweetDesk.exe` を実行します。
4.  上部のボタン（ホーム追加、検索追加など）でカラムを追加します。
5.  カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムを並べ替えます。
6.  各カラムのヘッダーにあるボタンで、以下の操作が可能です。
    * **↻**: そのカラムを手動で更新します。
    * **自動:**: 自動更新の有効/無効を切り替えます。
    * **(300)**: 更新間隔を秒単位で入力します。
    * **✖**: そのカラムを閉じます。

## 🛠️ ビルド方法 (開発者向け)

1.  このリポジトリをクローンします。
2.  Visual Studio 2022 で `TweetDesk.sln` を開きます。
3.  .NET 8.0 SDK がインストールされていることを確認します。
4.  NuGet パッケージ マネージャーから、以下のパッケージを復元（またはインストール）します。
    * `Microsoft.Web.WebView2`
    * `gong-wpf-dragdrop`
5.  ビルドして実行します。


## 更新履歴

- **v1.0.0 (2025/11/09)**
  - 初期リリース

---

## 📄 ライセンス (License)

このプロジェクトは、クリエイティブ・コモンズ 表示 - 非営利 4.0 国際 (CC BY-NC 4.0) ライセンスの下で公開されています。
このライセンスの主な内容は以下の通りです。詳細についてはライセンス条文の要約または全文をご確認ください。

[https://creativecommons.org/licenses/by-nc/4.0/deed.ja](https://creativecommons.org/licenses/by-nc/4.0/deed.ja)

### ✅ 可能なこと (Permissions)

* **共有 (Share)**: あらゆる媒体や形式で資料を複製し、再配布できます。
* **翻案 (Adapt)**: 資料をリミックス、改変、加工し、新しい作品のベースにできます。

### ⚠️ 守っていただきたい条件 (Conditions)

* **表示 (Attribution)**:
    適切なクレジット（作者名: 「マッシャー」または「Masher」、およびこのリポジトリへのリンク）を表示する必要があります。
* **非営利 (Non-Commercial)**:
    このプロジェクトおよびその派生物を、営利目的で使用することはできません。
    (例: 本ツールの販売、本ツールを組み込んだ有料サービスの提供など)

### 免責事項 (Disclaimer)

本ソフトウェアは「現状のまま」提供されており、作者はいかなる損害についても一切の責任を負いません。 ご利用は自己責任でお願いいたします。

---

### 📦 依存ライブラリのライセンス (Third-Party Licenses)

本ソフトウェアは、以下のサードパーティ製ライブラリを利用しており、これらのライブラリにはそれぞれのライセンスが適用されます。

#### 1. GongSolutions.WPF.DragDrop (gong-wpf-dragdrop)

* **ライセンス:** MIT License
* **Copyright (c) 2009-2024 Jan Karger, Steven Kirk**

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

#### 2. Microsoft.Web.WebView2

本ソフトウェアは、Microsoft Edge WebView2 ランタイムの機能に依存しています。
