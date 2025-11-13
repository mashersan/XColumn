# XColumn

![XColumn Screenshot](http://masherhouse.com/wp-content/uploads/2025/11/TweetDesk_image.jpg) 
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
* **既定のブラウザで開く**:
    * ポスト内の外部リンクをクリックした際、アプリ内で開かずOSの「既定のブラウザ」（Chrome, Edgeなど）で開きます。
* **アップデート通知**:
    * 起動時に新しいバージョンがリリースされているか確認し、更新内容と共に通知します。

## 🖥️ 動作要件

* Windows 10 / 11
* [.NET 8.0 デスクトップ ランタイム](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 ランタイム](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * （通常、最新のWindowsにはプリインストールされています）

## 🚀 使い方

1.  [リリースページ](https://github.com/mashersan/XColumn/releases)から最新版の `.zip` をダウンロードします。
2.  任意のフォルダにZIPを展開します。
3.  `XColumn.exe` を実行します。
4.  上部のボタン（ホーム追加、検索追加など）でカラムを追加します。
5.  カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムを並べ替えます。
6.  各カラムのヘッダーにあるボタンで、以下の操作が可能です。
    * **↻**: そのカラムを手動で更新します。
    * **自動:**: 自動更新の有効/無効を切り替えます。
    * **(300)**: 更新間隔を秒単位で入力します。
    * **✖**: そのカラムを閉じます。

## 🛠️ ビルド方法 (開発者向け)

1.  このリポジトリをクローンします。
2.  Visual Studio 2022 で `XColumn.sln` を開きます。
3.  .NET 8.0 SDK がインストールされていることを確認します。
4.  NuGet パッケージ マネージャーから、以下のパッケージを復元（またはインストール）します。
    * `Microsoft.Web.WebView2`
    * `gong-wpf-dragdrop`
5.  ビルドして実行します。

## 更新履歴

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
  - 許可ドメインの検証メソッド `IsAllowedDomain` を追加
  - フォーカスモードの復元と遷移ロジックを改善
  - 設定読み込み時のエラーハンドリングを強化
  - デフォルトカラムのロードロジックを改善
- **v1.0.0 (2025/11/09)**
  - 初期リリース (TweetDeskとして)

---

## 📄 ライセンス (License)

このプロジェクトは **MIT ライセンス** の下で公開されています。
これは、ソフトウェアで最も広く使われている、非常に寛容な（ゆるい）ライセンスです。

### ひと目でわかるライセンス要約

#### ✅ 可能なこと (ほぼ何でもできます)

* **商用利用**: このアプリ（またはコード）を使って利益を上げても構いません。
* **改変**: 自由にコードを改造できます。
* **再配布**: 改造したかどうかにかかわらず、コピーを他の人に配っても構いません。
* **プライベート利用**: このコードを公開せずに、個人的な目的や社内のみで利用しても構いません。

#### ⚠️ 守っていただきたい義務 (たった1つだけです)

* **著作権表示の保持**:
    このソフトウェア（のコードや実行ファイル）を再配布する場合、必ず以下の**ライセンス条文（全文）**と**著作権表示（Copyright行）**を、ソフトウェアのコピーまたは重要な部分（例: Readmeやライセンスファイル）に**そのまま含めてください**。

#### 🚫 免責事項 (重要な注意点)

* このソフトウェアは「現状のまま（AS IS）」提供されます。
* このソフトウェアを使用したことによって生じたいかなる損害（PCが壊れた、データが消えた等）についても、**作者（マッシャー (Masher)）は一切の責任を負いません**。ご利用は自己責任でお願いいたします。

---

### MIT ライセンス条文 (全文)

Copyright (c) 2025 マッシャー (Masher)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

### 📦 依存ライブラリのライセンス (Third-Party Licenses)

本ソフトウェアは、以下のサードパーティ製ライブラリを利用しており、これらのライブラリも（本プロジェクトと互換性のある）MITライセンスの下で提供されています。

* **GongSolutions.WPF.DragDrop (gong-wpf-dragdrop)**
    * Copyright (c) 2009-2024 Jan Karger, Steven Kirk
    * ライセンス条文は上記MITライセンス条文（全文）に準じます。

* **Microsoft.Web.WebView2**
    * 本ソフトウェアは、Microsoft Edge WebView2 ランタイムの機能に依存しています。# XColumn

![XColumn Screenshot](http://masherhouse.com/wp-content/uploads/2025/11/TweetDesk_image.jpg) 
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
* **既定のブラウザで開く**:
    * ポスト内の外部リンクをクリックした際、アプリ内で開かずOSの「既定のブラウザ」（Chrome, Edgeなど）で開きます。
* **アップデート通知**:
    * 起動時に新しいバージョンがリリースされているか確認し、更新内容と共に通知します。

## 🖥️ 動作要件

* Windows 10 / 11
* [.NET 8.0 デスクトップ ランタイム](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
* [Microsoft Edge WebView2 ランタイム](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
    * （通常、最新のWindowsにはプリインストールされています）

## 🚀 使い方

1.  [リリースページ](https://github.com/mashersan/XColumn/releases)から最新版の `.zip` をダウンロードします。
2.  任意のフォルダにZIPを展開します。
3.  `XColumn.exe` を実行します。
4.  上部のボタン（ホーム追加、検索追加など）でカラムを追加します。
5.  カラム上部のハンドル（URLが表示されているバー）をドラッグ＆ドロップして、カラムを並べ替えます。
6.  各カラムのヘッダーにあるボタンで、以下の操作が可能です。
    * **↻**: そのカラムを手動で更新します。
    * **自動:**: 自動更新の有効/無効を切り替えます。
    * **(300)**: 更新間隔を秒単位で入力します。
    * **✖**: そのカラムを閉じます。

## 🛠️ ビルド方法 (開発者向け)

1.  このリポジトリをクローンします。
2.  Visual Studio 2022 で `XColumn.sln` を開きます。
3.  .NET 8.0 SDK がインストールされていることを確認します。
4.  NuGet パッケージ マネージャーから、以下のパッケージを復元（またはインストール）します。
    * `Microsoft.Web.WebView2`
    * `gong-wpf-dragdrop`
5.  ビルドして実行します。

## 更新履歴

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
  - 許可ドメインの検証メソッド `IsAllowedDomain` を追加
  - フォーカスモードの復元と遷移ロジックを改善
  - 設定読み込み時のエラーハンドリングを強化
  - デフォルトカラムのロードロジックを改善
- **v1.0.0 (2025/11/09)**
  - 初期リリース (TweetDeskとして)

---

## 📄 ライセンス (License)

このプロジェクトは **MIT ライセンス** の下で公開されています。
これは、ソフトウェアで最も広く使われている、非常に寛容な（ゆるい）ライセンスです。

### ひと目でわかるライセンス要約

#### ✅ 可能なこと (ほぼ何でもできます)

* **商用利用**: このアプリ（またはコード）を使って利益を上げても構いません。
* **改変**: 自由にコードを改造できます。
* **再配布**: 改造したかどうかにかかわらず、コピーを他の人に配っても構いません。
* **プライベート利用**: このコードを公開せずに、個人的な目的や社内のみで利用しても構いません。

#### ⚠️ 守っていただきたい義務 (たった1つだけです)

* **著作権表示の保持**:
    このソフトウェア（のコードや実行ファイル）を再配布する場合、必ず以下の**ライセンス条文（全文）**と**著作権表示（Copyright行）**を、ソフトウェアのコピーまたは重要な部分（例: Readmeやライセンスファイル）に**そのまま含めてください**。

#### 🚫 免責事項 (重要な注意点)

* このソフトウェアは「現状のまま（AS IS）」提供されます。
* このソフトウェアを使用したことによって生じたいかなる損害（PCが壊れた、データが消えた等）についても、**作者（マッシャー (Masher)）は一切の責任を負いません**。ご利用は自己責任でお願いいたします。

---

### MIT ライセンス条文 (全文)

Copyright (c) 2025 マッシャー (Masher)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

### 📦 依存ライブラリのライセンス (Third-Party Licenses)

本ソフトウェアは、以下のサードパーティ製ライブラリを利用しており、これらのライブラリも（本プロジェクトと互換性のある）MITライセンスの下で提供されています。

* **GongSolutions.WPF.DragDrop (gong-wpf-dragdrop)**
    * Copyright (c) 2009-2024 Jan Karger, Steven Kirk
    * ライセンス条文は上記MITライセンス条文（全文）に準じます。

* **Microsoft.Web.WebView2**
    * 本ソフトウェアは、Microsoft Edge WebView2 ランタイムの機能に依存しています。