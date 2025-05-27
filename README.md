# Speech2TextAOAI

Azure OpenAI GPT-4o-transcribeを使用した音声文字起こし＆コールセンター分析アプリケーション

## 概要

このアプリケーションは、Azure OpenAI GPT-4o-transcribeを使用して音声ファイルを文字起こしし、その内容をAIで分析してコールセンターでの顧客対応を評価するツールです。

## 主な機能

- **Azure OpenAI GPT-4o-transcribeによる高精度文字起こし**
  - 様々な音声フォーマット対応（WAV、MP3、M4A、FLAC、OGG、WebM）
  - タイムスタンプ付き文字起こし
  - 多言語対応（主に日本語）
  - **25MB以上の大きなファイルの自動分割処理**

- **AI分析機能**
  - 通話内容の要約
  - 顧客の感情分析
  - 次のアクション提案
  - 重要ポイントの抽出

## 必要な環境

- .NET 8.0 以上
- **FFmpeg**（25MB以上の音声ファイル分割用）
- Azure OpenAI Service
  - GPT-4o-transcribe デプロイメント（文字起こし用）
  - GPT-4 または GPT-3.5-turbo デプロイメント（分析用）

## セットアップ

### 1. Azure OpenAI Serviceの設定

1. Azure Portal で Azure OpenAI Service を作成
2. 以下のモデルをデプロイ：
   - `gpt-4o-transcribe` - 音声文字起こし用
   - `gpt-4` または `gpt-3.5-turbo` - 分析用

### 2. 設定ファイルの準備

`appsettings.template.json` を `appsettings.json` にコピーして、Azure OpenAI の情報を設定：

```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://your-openai-resource.openai.azure.com",
      "ApiKey": "your-api-key-here",
      "TranscriptionDeploymentName": "gpt-4o-transcribe",
      "AnalysisDeploymentName": "gpt-4"
    }
  }
}
```

### 3. FFmpegのインストール（大きなファイル処理用）

25MB以上の音声ファイルを処理する場合は、FFmpegのインストールが必要です：

#### Windowsの場合：
1. [FFmpeg公式サイト](https://ffmpeg.org/download.html)からダウンロード
2. 解凍後、`ffmpeg.exe`をPATHに追加
3. または、パッケージマネージャーを使用：
   ```powershell
   # Chocolateyを使用する場合
   choco install ffmpeg
   
   # Scoopを使用する場合
   scoop install ffmpeg
   ```

#### 動作確認：
```powershell
ffmpeg -version
```

### 4. ビルドと実行

```powershell
# プロジェクトをビルド
dotnet build

# 実行
dotnet run -- "C:\path\to\your\audio.wav"
```

## 使用方法

### 基本的な使用方法

```powershell
Speech2TextAOAI.exe "音声ファイルのパス"
```

### 実行例

```powershell
Speech2TextAOAI.exe "C:\recordings\customer_call.wav"
```

### 大きなファイルの処理

25MB以上の音声ファイルは自動的に分割されて処理されます：

```powershell
# 大きなファイルの例（自動分割）
Speech2TextAOAI.exe "C:\recordings\large_meeting.wav"
```

処理の流れ：
1. ファイルサイズをチェック（25MB超過の場合）
2. FFmpegを使用して適切なサイズに分割
3. 各分割ファイルを順次文字起こし
4. 結果をまとめて出力

### 出力ファイル

実行すると以下のファイルが生成されます：

1. `{音声ファイル名}.txt` - 文字起こし結果（タイムスタンプ付き）
2. `{音声ファイル名}_ai.txt` - AI分析結果

### 文字起こし結果の例

#### 通常ファイルの場合：
```
[00:05] こんにちは、サポートセンターの田中と申します。
[00:12] お客様のお困りごとについてお聞かせください。
[00:18] 先週購入した商品が届かないんです。
```

#### 分割処理されたファイルの場合：
```
=== 分割 1/3 ===
[00:05] こんにちは、サポートセンターの田中と申します。
[00:12] お客様のお困りごとについてお聞かせください。

=== 分割 2/3 ===
[10:15] それでは注文番号を教えていただけますでしょうか。
[10:22] 注文番号は ABC123 です。

=== 分割 3/3 ===
[20:30] 確認いたしました。明日中にお届けいたします。
[20:35] ありがとうございました。
```

### AI分析結果の例

```
【要約】
お客様が先週購入した商品が届かないという問い合わせ。配送状況の確認と対応が必要。

【お客様の感情】
- 感情の状態：困惑・軽い不満
- 具体的な理由：期待していた商品が届かず、不安を感じている

【次のアクション】
- 注文番号の確認
- 配送業者への問い合わせ
- 配送状況のお客様への報告
- 必要に応じて再発送の手配

【重要なポイント】
- 迅速な状況確認が必要
- お客様への定期的な進捗報告
- 配送遅延の原因究明
```

## 対応音声フォーマット

- WAV (推奨)
- MP3
- M4A
- FLAC
- OGG
- WebM

## リリース作成手順

### 開発者向け：GitHubリリースの作成

1. **リリーススクリプトの実行**
   ```powershell
   # バージョン番号を指定してリリースファイルを作成
   .\create-release.ps1 -Version "1.2.0"
   ```

2. **作成されるファイル**
   - `release\Speech2TextAOAI-v1.2.0.zip` - リリース用zipファイル
   - `release\Speech2TextAOAI-v1.2.0\` - リリース用フォルダ

3. **含まれるファイル**
   - 実行ファイル（Speech2TextAOAI.exe）
   - 必要なDLLファイル
   - appsettings.template.json（設定テンプレート）
   - README.md
   - 使用方法.txt
   - setup.bat（セットアップ用バッチファイル）

4. **GitHubでのリリース作成**
   1. GitHubリポジトリページで「Releases」をクリック
   2. 「Create a new release」をクリック
   3. Tag version: `v1.2.0`
   4. Release title: `Speech2TextAOAI v1.2.0`
   5. リリースノートを記述
   6. 作成された `Speech2TextAOAI-v1.2.0.zip` をアップロード
   7. 「Publish release」をクリック

### 注意事項

- `appsettings.json`は機密情報を含むため、リリースには含まれません
- ユーザーは`appsettings.template.json`をコピーして設定する必要があります
- `.gitignore`により`appsettings.json`は自動的にGitから除外されます

## トラブルシューティング

### よくある問題

1. **"Azure OpenAIの設定が不正です"エラー**
   - `appsettings.json` の設定を確認
   - APIキーとエンドポイントが正しいか確認

2. **"音声を認識できませんでした"エラー**
   - 音声ファイルのフォーマットを確認
   - ファイルが破損していないか確認
   - GPT-4o-transcribe のデプロイメント名を確認

3. **音声ファイルのサイズ制限**
   - ~~Azure OpenAI は音声ファイルのサイズに制限があります~~
   - ~~大きなファイルは事前に分割することを検討してください~~
   - **25MB以上のファイルは自動的に分割処理されます**
   - FFmpegが正しくインストールされていることを確認してください

4. **FFmpegが見つからないエラー**
   - FFmpegがインストールされていることを確認
   - PATHにFFmpegが追加されていることを確認
   - `ffmpeg -version` コマンドで動作確認

### デバッグ方法

詳細なログが必要な場合は、以下のようにデバッグモードで実行：

```powershell
dotnet run --configuration Debug -- "音声ファイルのパス"
```

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 変更履歴

### v1.0.0
- Azure OpenAI GPT-4o-transcribe による文字起こし機能
- タイムスタンプ付き文字起こし
- AI分析機能
- 複数音声フォーマット対応
- **25MB以上の大きなファイルの自動分割機能**
- FFmpegを使用した音声ファイル分割
- 分割ファイルの順次処理とマージ
- エラーハンドリングの改善