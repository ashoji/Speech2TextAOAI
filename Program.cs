using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

class Program
{
    private static IConfiguration? _configuration;
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25MB
    
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("使用方法: Speech2TextAOAI.exe <音声ファイルパス>");
            Console.WriteLine("例: Speech2TextAOAI.exe C:\\path\\to\\audio.wav");
            return;
        }

        string audioFilePath = args[0];
        
        if (!File.Exists(audioFilePath))
        {
            Console.WriteLine($"エラー: ファイルが見つかりません: {audioFilePath}");
            return;
        }

        // 設定ファイルを読み込み
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();        try
        {
            Console.WriteLine("Azure OpenAI GPT-4o-transcribeによる音声ファイルの文字起こしを開始しています...");
            
            // ファイルサイズをチェックして分割が必要かどうか判定
            var fileInfo = new FileInfo(audioFilePath);
            string transcriptionText;
            
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                Console.WriteLine($"ファイルサイズが25MBを超えています ({fileInfo.Length / (1024 * 1024):F1}MB)。分割して処理します...");
                transcriptionText = await ProcessLargeAudioFileAsync(audioFilePath);
            }
            else
            {
                Console.WriteLine($"ファイルサイズ: {fileInfo.Length / (1024 * 1024):F1}MB");
                transcriptionText = await TranscribeAudioWithOpenAIAsync(audioFilePath);
            }
            
            // 文字起こし結果をファイルに保存
            string textFilePath = Path.ChangeExtension(audioFilePath, ".txt");
            await File.WriteAllTextAsync(textFilePath, transcriptionText, Encoding.UTF8);
            Console.WriteLine($"文字起こし完了: {textFilePath}");
            
            Console.WriteLine("AI分析を開始しています...");
            
            // AI分析を実行
            string analysisResult = await AnalyzeWithOpenAIAsync(transcriptionText);
            
            // AI分析結果をファイルに保存
            string baseFileName = Path.GetFileNameWithoutExtension(audioFilePath);
            string directory = Path.GetDirectoryName(audioFilePath) ?? "";
            string aiFilePath = Path.Combine(directory, $"{baseFileName}_ai.txt");
            await File.WriteAllTextAsync(aiFilePath, analysisResult, Encoding.UTF8);
            Console.WriteLine($"AI分析完了: {aiFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラーが発生しました: {ex.Message}");
        }
    }

    static async Task<string> TranscribeAudioWithOpenAIAsync(string audioFilePath)
    {
        var endpoint = _configuration?["Azure:OpenAI:Endpoint"];
        var apiKey = _configuration?["Azure:OpenAI:ApiKey"];
        var deploymentName = _configuration?["Azure:OpenAI:TranscriptionDeploymentName"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException("Azure OpenAIの設定が不正です。appsettings.jsonを確認してください。");
        }

        // エンドポイントからベースURLを抽出
        var baseEndpoint = endpoint.Contains("/openai/deployments") 
            ? endpoint.Substring(0, endpoint.IndexOf("/openai/deployments"))
            : endpoint.TrimEnd('/');
        
        var requestUrl = $"{baseEndpoint}/openai/deployments/{deploymentName}/audio/transcriptions?api-version=2024-06-01";

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            
            // マルチパートフォームデータを作成
            using var formData = new MultipartFormDataContent();
            
            // 音声ファイルを読み込み
            var audioBytes = await File.ReadAllBytesAsync(audioFilePath);
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetContentType(audioFilePath));
              formData.Add(audioContent, "file", Path.GetFileName(audioFilePath));
            formData.Add(new StringContent("ja"), "language");
            formData.Add(new StringContent("json"), "response_format");
            
            Console.WriteLine("音声ファイルをAzure OpenAIに送信中...");
            var response = await httpClient.PostAsync(requestUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"音声認識に失敗しました: {response.StatusCode} - {responseContent}");
            }            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var transcriptionText = jsonResponse.GetProperty("text").GetString();
            
            return transcriptionText ?? "音声を認識できませんでした。";
        }
        catch (Exception ex)
        {
            throw new Exception($"音声認識中にエラーが発生しました: {ex.Message}", ex);
        }
    }

    static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".webm" => "audio/webm",
            _ => "audio/wav"
        };
    }

    static async Task<string> AnalyzeWithOpenAIAsync(string transcriptionText)
    {
        var endpoint = _configuration?["Azure:OpenAI:Endpoint"];
        var apiKey = _configuration?["Azure:OpenAI:ApiKey"];
        var deploymentName = _configuration?["Azure:OpenAI:AnalysisDeploymentName"];
        var systemPrompt = _configuration?["AI:Prompts:SystemPrompt"];
        var userPromptTemplate = _configuration?["AI:Prompts:UserPromptTemplate"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException("Azure OpenAIの設定が不正です。appsettings.jsonを確認してください。");
        }

        if (string.IsNullOrEmpty(systemPrompt) || string.IsNullOrEmpty(userPromptTemplate))
        {
            throw new InvalidOperationException("AIプロンプトの設定が不正です。appsettings.jsonを確認してください。");
        }

        // エンドポイントからベースURLを抽出
        var baseEndpoint = endpoint.Contains("/openai/deployments") 
            ? endpoint.Substring(0, endpoint.IndexOf("/openai/deployments"))
            : endpoint.TrimEnd('/');
        
        var requestUrl = $"{baseEndpoint}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-02-15-preview";

        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = string.Format(userPromptTemplate, transcriptionText) }
            },
            max_tokens = 1000,
            temperature = 0.3
        };

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                return $"AI分析中にエラーが発生しました: {response.StatusCode} - {responseContent}";
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var analysisResult = jsonResponse
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return analysisResult ?? "分析結果を取得できませんでした。";
        }
        catch (Exception ex)
        {            return $"AI分析中にエラーが発生しました: {ex.Message}";
        }
    }

    /// <summary>
    /// 25MBを超える大きな音声ファイルを分割して処理する
    /// </summary>
    static async Task<string> ProcessLargeAudioFileAsync(string audioFilePath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"audio_split_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            // FFmpegを使用してファイルを分割
            var splitFiles = await SplitAudioFileAsync(audioFilePath, tempDirectory);
            
            if (splitFiles.Count == 0)
            {
                throw new Exception("音声ファイルの分割に失敗しました。");
            }

            var allTranscriptions = new List<string>();
            
            Console.WriteLine($"{splitFiles.Count}個のファイルに分割されました。順次処理を開始します...");
            
            for (int i = 0; i < splitFiles.Count; i++)
            {
                var splitFile = splitFiles[i];
                Console.WriteLine($"分割ファイル {i + 1}/{splitFiles.Count} を処理中: {Path.GetFileName(splitFile)}");
                
                try
                {
                    var transcription = await TranscribeAudioWithOpenAIAsync(splitFile);
                    allTranscriptions.Add($"=== 分割 {i + 1}/{splitFiles.Count} ===\n{transcription}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"分割ファイル {i + 1} の処理中にエラーが発生しました: {ex.Message}");
                    allTranscriptions.Add($"=== 分割 {i + 1}/{splitFiles.Count} ===\n[処理エラー: {ex.Message}]");
                }
            }
            
            return string.Join("\n\n", allTranscriptions);
        }
        finally
        {
            // 一時ファイルをクリーンアップ
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"一時ファイルの削除中にエラーが発生しました: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// FFmpegを使用して音声ファイルを分割する
    /// </summary>
    static async Task<List<string>> SplitAudioFileAsync(string audioFilePath, string outputDirectory)
    {
        var splitFiles = new List<string>();
        
        try
        {
            // まず音声ファイルの長さを取得
            var duration = await GetAudioDurationAsync(audioFilePath);
            if (duration <= 0)
            {
                throw new Exception("音声ファイルの長さを取得できませんでした。");
            }

            // 約20MBになるように分割間隔を計算（余裕を持って）
            var fileInfo = new FileInfo(audioFilePath);
            var segmentDuration = (int)(duration * 20.0 * 1024 * 1024 / fileInfo.Length); // 20MBあたりの秒数
            segmentDuration = Math.Max(segmentDuration, 60); // 最低60秒
            segmentDuration = Math.Min(segmentDuration, 600); // 最大10分

            Console.WriteLine($"音声長: {duration:F1}秒、分割間隔: {segmentDuration}秒");

            var segmentCount = (int)Math.Ceiling(duration / segmentDuration);
            
            for (int i = 0; i < segmentCount; i++)
            {
                var startTime = i * segmentDuration;
                var outputFile = Path.Combine(outputDirectory, $"segment_{i:D3}.wav");
                
                var ffmpegArgs = $"-i \"{audioFilePath}\" -ss {startTime} -t {segmentDuration} -acodec pcm_s16le -ar 16000 -ac 1 \"{outputFile}\"";
                
                var result = await RunFFmpegAsync(ffmpegArgs);
                if (result && File.Exists(outputFile))
                {
                    splitFiles.Add(outputFile);
                    Console.WriteLine($"分割ファイル作成: {Path.GetFileName(outputFile)}");
                }
                else
                {
                    Console.WriteLine($"分割ファイル {i + 1} の作成に失敗しました。");
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"音声ファイルの分割中にエラーが発生しました: {ex.Message}", ex);
        }

        return splitFiles;
    }

    /// <summary>
    /// FFmpegを使用して音声ファイルの長さを取得する
    /// </summary>
    static async Task<double> GetAudioDurationAsync(string audioFilePath)
    {
        try
        {
            var ffprobeArgs = $"-v quiet -show_entries format=duration -of csv=p=0 \"{audioFilePath}\"";
            var result = await RunFFmpegCommandAsync("ffprobe", ffprobeArgs);
            
            if (double.TryParse(result.Trim(), out var duration))
            {
                return duration;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"音声ファイルの長さ取得中にエラーが発生しました: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// FFmpegコマンドを実行する
    /// </summary>
    static async Task<bool> RunFFmpegAsync(string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FFmpegの実行中にエラーが発生しました: {ex.Message}");
            Console.WriteLine("FFmpegがインストールされていることを確認してください。");
            return false;
        }
    }

    /// <summary>
    /// FFmpegコマンドを実行して結果を取得する
    /// </summary>
    static async Task<string> RunFFmpegCommandAsync(string command, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return output;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"コマンド実行エラー: {error}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"{command}の実行中にエラーが発生しました: {ex.Message}", ex);
        }
    }
}
