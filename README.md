# WinBridge

WinBridgeは、Windows 11の分散した設定を1か所から扱うための中継アプリです。安全に変更できる設定だけをアプリ内で変更し、それ以外はWindowsの正規設定画面へ案内します。

表示言語は日本語、英語、スペイン語、簡体字中国語、繁体字中国語に対応しています。「アプリ設定」の「表示言語」から固定するか、Windowsの表示言語に合わせて自動選択できます。未対応のWindows表示言語では英語を使用します。

すべての機能を無料で利用できます。開発支援は任意で、アプリの「アプリ設定」から
[Ko-fi](https://ko-fi.com/nioudachi)を開けます。支援の有無による機能差や特典はありません。

## 対応環境

- Windows 11 x64
- .NET 8 Desktop Runtime
- ビルドには .NET 8 SDK（または .NET 8をターゲットにできる新しいSDK）
- 管理者権限は通常不要

## プロジェクト構成

```text
WinBridge/
├─ Models/       設定値、モジュール定義、共通の操作結果
├─ Services/     Windows操作、JSON保存、ログ
├─ ViewModels/   画面の状態と操作
├─ Views/        WPF画面
├─ Resources/    モジュール定義JSON
├─ tests/        設定保存・移行・タイムアウト等の自動テスト
├─ App.xaml
└─ WinBridge.csproj
```

UIからWindowsコマンドやレジストリを直接操作せず、すべてService層を経由します。外部NuGetパッケージは使用していません。

## ビルドと起動

1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) をインストールします。
2. ターミナルでこのフォルダーを開きます。
3. 次を実行します。

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

ビルド済みアプリは通常 `bin\Release\net8.0-windows10.0.22621.0\win-x64\WinBridge.exe` から起動できます。

## 配布パッケージ

### Microsoft Store（MSIX）

Microsoft Storeへ提出するMSIXは、Partner Centerの「製品 ID の管理」に表示される
`Package/Identity/Name`、`Package/Identity/Publisher`、発行元表示名を指定して生成します。

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-msix.ps1 `
  -Version 1.1.5.0 `
  -PackageIdentityName '<Partner CenterのPackage/Identity/Name>' `
  -Publisher '<Partner CenterのPackage/Identity/Publisher>' `
  -PublisherDisplayName '<Partner Centerの発行元表示名>'
```

成果物は `WinBridge-msix-v1.1.5.0\WinBridge-v1.1.5.0-x64.msix` に作成されます。
提出用MSIXは署名せずに生成し、Microsoft Storeが提出後に署名します。

Microsoft Store版はパッケージ実行を自動判定し、エクスプローラーのレジストリを直接変更しません。
ファイル名拡張子と隠しファイルは、Windows標準の「フォルダー オプション」を開いて変更します。
MSIXマニフェストはパッケージ化されたWPFアプリに必要な `runFullTrust` だけを宣言し、
`unvirtualizedResources` やレジストリ仮想化の無効化は宣言しません。
再提出時の確認事項と認定メモ案は
[`packaging/msix/CERTIFICATION_NOTES.md`](packaging/msix/CERTIFICATION_NOTES.md) にあります。

### EXEインストーラーとポータブルZIP

Inno Setup 6または7をインストールしたWindows環境では、次のコマンドでポータブル版ZIP、ユーザー単位インストーラー、SHA-256一覧を同時に生成できます。

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1 `
  -Version 1.1.5 `
  -AllowUnsigned
```

コード署名証明書がある場合は、`-AllowUnsigned` の代わりに
`-SigningCertificateThumbprint <証明書の拇印>` を指定できます。署名を省略する場合でも、
成果物のファイル名は変わりません。

成果物はプロジェクト直下の `WinBridge-release-v1.1.5` に作成されます。

```text
WinBridge-v1.1.5-win-x64-portable.zip
WinBridge-v1.1.5-win-x64-Setup.exe
SHA256SUMS.txt
```

インストーラーは管理者権限を要求せず、`%LOCALAPPDATA%\Programs\WinBridge` にインストールします。スタートメニューとWindowsの「インストールされているアプリ」に登録され、アンインストーラーも作成されます。アプリ設定とログはアンインストール後も `%LOCALAPPDATA%\WinBridge` に残ります。

ダウンロード後の整合性は次のように確認できます。

```powershell
Get-FileHash .\WinBridge-v1.1.5-win-x64-portable.zip -Algorithm SHA256
Get-FileHash .\WinBridge-v1.1.5-win-x64-Setup.exe -Algorithm SHA256
```

表示された値がGitHub Releaseに添付された `SHA256SUMS.txt` と一致することを確認してください。
コード署名証明書を使わずに生成した場合、環境によってはWindowsが発行元の確認画面を表示することがあります。

## 自動テスト

外部テストライブラリに依存しない自動テストを同梱しています。

```powershell
dotnet run --project tests\WinBridge.Tests\WinBridge.Tests.csproj -c Release
```

設定Version移行、デバイスページ項目の追加・解除、全解除状態の維持、未来Versionの上書き防止、同時保存、バックアップ復旧、設定カタログURI、端末能力判定、機器依存設定の保存維持、コマンドタイムアウト、単一起動通知を検証します。Windowsの実設定は変更しません。

## 各モジュール

### 画面とスリープ

有効な電源プランの画面OFF・スリープ時間を読み込み、電源接続時とバッテリー使用時を変更します。集中作業、普段使い、省電力のプリセットがあります。プリセットを選ぶだけでは反映されず、「設定を適用」で初めて変更されます。

### Windows Update

更新確認、更新履歴、再起動、アクティブ時間、詳細オプションの正規設定画面を開きます。更新サービスの無効化や更新の強制停止は行いません。再起動待ちは公開された安定APIではないため、既知の標準レジストリキーが存在する場合だけ「必要な可能性があります」と表示します。

### スタートと検索

Windows検索、検索のアクセス許可、インデックス、スタート、既定のアプリ設定を開きます。Windows Searchサービスの状態を読み取り、よくある問題の安全な確認手順を表示します。自動再構築やサービス停止は行いません。

### エクスプローラー

EXEインストーラー版とポータブルZIP版では、ファイル名拡張子と隠しファイルの現在値を読み込み、変更します。直前のアプリ内変更は「元に戻す」が使えます（アプリ終了まで）。Microsoft Store版ではレジストリを直接変更せず、Windows標準の「フォルダー オプション」へ案内します。エクスプローラー再起動は確認後に実行し、終了処理に失敗しても再起動を試みます。

### デバイスと接続

現在のデバイスツリーをWindowsのConfiguration Manager APIで読み取り、問題コードが報告されているデバイス数を表示します。デバイスマネージャー、接続済みデバイス、オプションの更新をWindowsの正規画面で開けます。初期状態ではマウス、入力、入力マイク、プリンターとスキャナー、カメラ、Bluetoothの6項目を例として表示し、各項目を自由に外せます。追加候補には確認済みカタログのうち「デバイス」カテゴリだけを表示し、機器依存項目は対応機器を検出した場合だけ選べます。ドライバーやデバイスを直接変更・削除しません。

## 表示する機能と自分用

左下の「表示する機能」を開き、チェックを外すとホームとナビゲーションから隠せます。カードのドラッグ、または上下ボタンで並べ替えられます。「★ 自分用」を選ぶとホーム上部にもショートカットが表示されます。すべてを非表示にしても管理画面は残ります。

設定は `%LOCALAPPDATA%\WinBridge\settings.json` に保存されます。JSONが壊れている場合は、同じフォルダーへ日時付きの `settings.broken-*.json` として退避し、前回バックアップから復旧します。利用できるバックアップがない場合だけ初期設定へ戻します。

設定保存はアプリ内で1件ずつ順番に処理し、一意な一時ファイルへ完全に書き込んだ後で本体と入れ替えます。直前の正常な設定は `settings.backup.json` に保持され、メイン設定が破損した場合はバックアップから復旧します。新しいWinBridgeで作られた未知の設定Versionは、古いアプリから上書きしません。

## Windows設定を自由に追加・整理する

左下の「Windows設定を追加」を開くと、WinBridgeで検証済みの166件のWindows設定をカタログから選べます。設定名や「マウス」「音量」などの用途で検索し、「追加」を押すとホームと左メニューへ表示されます。バッテリー、タッチ、ペン、携帯回線、視線入力など24件の機器依存設定は、対応する機器や機能を検出した場合だけ表示します。一時的に利用できなくなっても、追加済み設定の保存情報は維持します。

追加後は、ドラッグまたは上下ボタンによる並べ替え、お気に入り登録、WinBridgeから外す操作ができます。「外す」はWinBridgeのショートカットを削除するだけで、Windowsの設定値や機能は変更しません。

「左メニューに固定」を外すと、ホームには残したまま左メニューだけを整理できます。固定した設定は左メニューでカテゴリ別の折りたたみツリーにまとめられ、項目数が増えても大分類を見失いません。

カタログは `Resources\SettingDefinitions.json` にあります。安全のため、読み込める対象は `ms-settings:` URIだけに制限しています。JSONから任意の実行ファイル、PowerShell、レジストリ処理を起動することはできません。

## 使用するWindows標準機能

- `powercfg.exe`: 現在値の取得、画面OFF時間、スリープ時間の変更
- `ms-settings:` URI: Windows Update、検索、スタート、既定のアプリ、ストレージ
- `control.exe`: インデックスのオプション、フォルダー オプション
- `sc.exe query WSearch`: Windows Searchサービスの読み取り
- `taskkill.exe` と `explorer.exe`: 確認後のエクスプローラー再起動
- `SHChangeNotify`: ファイル表示設定変更の通知
- `GetSystemPowerStatus`: バッテリー有無の判定
- `CfgMgr32.dll`: 現在存在するデバイスと問題コードの読み取り

コマンド引数は文字列連結せず、`ProcessStartInfo.ArgumentList` で渡します。

## レジストリ変更

EXEインストーラー版とポータブルZIP版で変更するのは、現在のユーザーの次の2値だけです。
Microsoft Store版はこれらの値を直接変更しません。

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
  HideFileExt  DWORD  0=拡張子を表示 / 1=非表示
  Hidden       DWORD  1=隠しファイルを表示 / 2=非表示
```

変更前の値はアプリがメモリに保持し、同じ起動中なら「元に戻す」で復元できます。手動で戻す場合は、エクスプローラーの「表示」から設定するか、WinBridgeで希望する状態を選び直して適用してください。

読み取り専用で、Windows Updateの `RebootRequired` キーの有無も確認します。このキーには書き込みません。

## 管理者権限

マニフェストは `asInvoker` で、アプリ全体の昇格を要求しません。初期版のどの操作もUAC昇格を明示的に要求しません。組織のポリシーなどでWindowsが変更を拒否した場合は、エラーとして表示します。

## 安定性とエラー表示

WinBridgeはユーザーセッションごとに1つだけ起動します。2回目の起動は新しいウィンドウを作らず、既存のWinBridgeを前面へ表示します。

`powercfg.exe` などのWindows標準コマンドは15秒でタイムアウトします。失敗時は画面下部に安全なメッセージを表示し、「詳細」から例外種類、終了コード、処理名、発生日時を確認できます。ユーザープロファイル名は技術情報から伏せ字にします。

## ログ

起動、終了、設定取得・変更、画面起動、エラーを `%LOCALAPPDATA%\WinBridge\Logs` に記録します。直近7ファイルを保持します。個人ファイルの内容、検索語、認証情報は記録しません。

## 動作確認

1. 起動し、5枚の機能モジュールカードが表示されることを確認します。
2. 「表示する機能」で非表示、再表示、並べ替えを行い、再起動後も保持されることを確認します。
3. 電源設定の現在値を読み込み、変更前の値を控えてから1項目ずつ適用・再取得します。
4. 各Windows設定ボタンが該当画面を開くことを確認します。
5. 拡張子・隠しファイルを切り替え、エクスプローラーへ反映されることと「元に戻す」を確認します。
6. 開いているファイル操作がない状態で、確認ダイアログからエクスプローラー再起動を試します。
7. `settings.json` を不正な内容にして再起動し、バックアップ後に初期状態で起動することを確認します。
8. 「Windows設定を追加」で設定を検索・追加し、ホームと左メニューへ反映されることを確認します。
9. 追加設定を並べ替え、お気に入り登録、取り外しし、再起動後も状態が保持されることを確認します。

## 現在の制限

- Windows 11以外は対象外です。
- Windowsのエディション、組織ポリシー、言語差により一部の設定URIやコマンドが利用できない場合があります。
- Windows Updateの最終確認日時は、安定して取得できる公開手段を採用できないため表示しません。
- 「元に戻す」はエクスプローラー表示設定の直前1回分で、アプリ終了後は保持しません。
- 電源設定の変更は現在有効なプランが対象です。独自の電源管理ソフトが後から値を上書きする場合があります。
- Windowsの実設定を書き換える自動テストは含みません。

## 今後の候補

優先度が高いのは、実機Windows 11での設定URI差異の検証、操作単位のお気に入り、エラー詳細表示パネル、永続的な変更履歴による「元に戻す」の強化、アクセシビリティの追加検証です。
