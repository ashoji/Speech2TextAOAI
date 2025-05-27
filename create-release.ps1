# GitHubリリース用のzipファイル作成スクリプト
param(
    [string]$Version = "1.0.0"
)

Write-Host "Speech2TextAOAI リリース v$Version の作成を開始します..." -ForegroundColor Green

# プロジェクトをビルド
Write-Host "プロジェクトをビルドしています..." -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ビルドに失敗しました。" -ForegroundColor Red
    exit 1
}

# リリースディレクトリを作成
$releaseDir = "release\Speech2TextAOAI-v$Version"
if (Test-Path "release") {
    Remove-Item "release" -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

# 必要なファイルをコピー
Write-Host "リリースファイルをコピーしています..." -ForegroundColor Yellow

# 実行ファイルとDLL
Copy-Item "bin\Release\net8.0\Speech2TextAOAI.exe" $releaseDir
Copy-Item "bin\Release\net8.0\Speech2TextAOAI.dll" $releaseDir
Copy-Item "bin\Release\net8.0\Speech2TextAOAI.runtimeconfig.json" $releaseDir
Copy-Item "bin\Release\net8.0\Speech2TextAOAI.deps.json" $releaseDir

# 依存関係DLLをコピー
$dependencies = @(
    "Microsoft.Extensions.Configuration.dll",
    "Microsoft.Extensions.Configuration.Abstractions.dll",
    "Microsoft.Extensions.Configuration.FileExtensions.dll",
    "Microsoft.Extensions.Configuration.Json.dll",
    "Microsoft.Extensions.FileProviders.Abstractions.dll",
    "Microsoft.Extensions.FileProviders.Physical.dll",
    "Microsoft.Extensions.FileSystemGlobbing.dll",
    "Microsoft.Extensions.Primitives.dll",
    "System.IO.Pipelines.dll",
    "System.Text.Encodings.Web.dll",
    "System.Text.Json.dll"
)

foreach ($dll in $dependencies) {
    $source = "bin\Release\net8.0\$dll"
    if (Test-Path $source) {
        Copy-Item $source $releaseDir
    }
}

# runtimesフォルダがある場合はコピー
if (Test-Path "bin\Release\net8.0\runtimes") {
    Copy-Item "bin\Release\net8.0\runtimes" $releaseDir -Recurse
}

# 設定ファイル（テンプレート）をコピー
Copy-Item "appsettings.template.json" "$releaseDir\appsettings.template.json"

# READMEをコピー
Copy-Item "README.md" $releaseDir

# LICENSEファイルがあればコピー
if (Test-Path "LICENSE") {
    Copy-Item "LICENSE" $releaseDir
}

# セットアップ用のバッチファイルを作成
$setupBatch = @"
@echo off
echo Speech2TextAOAI セットアップ
echo.
echo 1. appsettings.template.json を appsettings.json にコピーしてください
echo 2. appsettings.json を編集してAzure OpenAIの設定を入力してください
echo    - Endpoint: Azure OpenAIのエンドポイントURL
echo    - ApiKey: Azure OpenAIのAPIキー
echo    - TranscriptionDeploymentName: 文字起こし用デプロイメント名
echo    - AnalysisDeploymentName: 分析用デプロイメント名
echo.
echo セットアップ完了後、以下のコマンドで実行できます：
echo Speech2TextAOAI.exe "音声ファイルのパス"
echo.
pause
"@

$setupBatch | Out-File -FilePath "$releaseDir\setup.bat" -Encoding UTF8

# 使用方法のテキストファイルを作成
$usageText = @"
Speech2TextAOAI v$Version 使用方法

【初回セットアップ】
1. appsettings.template.json を appsettings.json にコピー
2. appsettings.json を編集してAzure OpenAIの設定を入力
   - Endpoint: Azure OpenAIのエンドポイントURL
   - ApiKey: Azure OpenAIのAPIキー
   - TranscriptionDeploymentName: 文字起こし用デプロイメント名（例：gpt-4o-transcribe）
   - AnalysisDeploymentName: 分析用デプロイメント名（例：gpt-4）

【使用方法】
Speech2TextAOAI.exe "音声ファイルのパス"

例：
Speech2TextAOAI.exe "C:\path\to\audio.wav"

【対応音声形式】
- WAV (.wav)
- MP3 (.mp3)
- M4A (.m4a)
- FLAC (.flac)
- OGG (.ogg)
- WebM (.webm)

【出力ファイル】
- 音声ファイル名.txt: 文字起こし結果
- 音声ファイル名_ai.txt: AI分析結果

【大きなファイルについて】
25MBを超える音声ファイルは自動的に分割して処理されます。
分割処理にはFFmpegが必要です。

【注意事項】
- appsettings.json にはAPIキーが含まれるため、他人と共有しないでください
- 初回実行時は設定ファイルの確認を行ってください
"@

$usageText | Out-File -FilePath "$releaseDir\使用方法.txt" -Encoding UTF8

# zipファイルを作成
Write-Host "zipファイルを作成しています..." -ForegroundColor Yellow
$zipPath = "release\Speech2TextAOAI-v$Version.zip"
Compress-Archive -Path $releaseDir -DestinationPath $zipPath -Force

Write-Host "リリースファイルの作成が完了しました！" -ForegroundColor Green
Write-Host "作成されたファイル:" -ForegroundColor Cyan
Write-Host "  - $zipPath" -ForegroundColor White
Write-Host "  - $releaseDir\" -ForegroundColor White

# ファイルサイズを表示
$zipInfo = Get-Item $zipPath
Write-Host "zipファイルサイズ: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan

Write-Host "`nGitHubリリースページで以下の手順でリリースを作成してください:" -ForegroundColor Yellow
Write-Host "1. GitHubリポジトリページで 'Releases' をクリック" -ForegroundColor White
Write-Host "2. 'Create a new release' をクリック" -ForegroundColor White
Write-Host "3. Tag version: v$Version" -ForegroundColor White
Write-Host "4. Release title: Speech2TextAOAI v$Version" -ForegroundColor White
Write-Host "5. $zipPath をアップロード" -ForegroundColor White
Write-Host "6. 'Publish release' をクリック" -ForegroundColor White
