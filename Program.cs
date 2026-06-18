using Discord;
using Discord.WebSocket;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElephantGun;

[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Field names are expected to match environment variable names")]
public class Config
{
    [Required]
    public required string DISCORD_TOKEN { get; init; }

    [Required]
    public ulong TARGET_USER_ID { get; init; }

    [Required]
    public ulong BOT_OWNER_ID { get; init; }

    [Required]
    public ulong JACK_USER_ID { get; init; }

    [Required]
    public required string OPENAI_API_URL { get; init; }

    [Required]
    public required string OPENAI_MODEL { get; init; }

    [Required]
    public required string OPENAI_API_KEY { get; init; }

    public string? OPENAI_IMAGE_API_URL { get; init; }

    public double ELEPHANT_FACT_CHANCE { get; init; } = 0.15;
}

public static class Program
{
    private static DiscordSocketClient? _client;
    private static Config _config = null!;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(300)
    };
    private static readonly Random _random = new();

    public static async Task Main(string[] args)
    {
        DotEnv.Load();
        
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build().Get<Config>();
        if (config == null)
        {
            Console.Error.WriteLine("Failed to load configuration from environment variables.");
            Environment.Exit(1);
        }
        
        Validator.ValidateObject(config, new ValidationContext(config), validateAllProperties: true);
        
        _config = config;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        });

        _client.Log += Log;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, _config.DISCORD_TOKEN);
        await _client.StartAsync();

        // Keep the bot running
        await Task.Delay(-1);
    }

    private static Task Log(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private static Task ReadyAsync()
    {
        Console.WriteLine($"{_client?.CurrentUser} is connected!");
        return Task.CompletedTask;
    }

    private static async Task MessageReceivedAsync(SocketMessage message)
    {
        // Ignore messages in the bot-spam channel
        if (message.Channel is SocketTextChannel textChannel && textChannel.Name == "bot-spam")
        {
            return;
        }

        // Ignore the bot's own messages
        if (message.Author.Id == _client?.CurrentUser?.Id)
        {
            return;
        }

        // Check if the message is from the bot owner for command execution
        if (message.Author.Id == _config.BOT_OWNER_ID)
        {
            if (message.Content.StartsWith("phapxecute "))
            {
                var command = message.Content.Substring("phapxecute ".Length);
                var output = await ExecuteCommandAsync(command);
                var sanitizedOutput = output.Replace("```", "\\`\\`\\`");
                await message.Channel.SendMessageAsync($"```{sanitizedOutput}```");
                return;
            }
        }

        // Check if the message is from the target user or contains "elephant" or mentions "php"
        var contentLower = message.Content.ToLower();
        var isTargetUser = message.Author.Id == _config.TARGET_USER_ID;
        if (isTargetUser ||
            contentLower.Contains("elephant") ||
            contentLower.Contains("php"))
        {
            // Offload reaction and AI work so the gateway handler returns immediately
            _ = Task.Run(async () =>
            {
                try
                {
                    // React with custom shut emoji, falling back to a default emoji if it fails
                    try
                    {
                        await message.AddReactionAsync(Emote.Parse("<:shut:1403433783830646835>"));
                    }
                    catch
                    {
                        await message.AddReactionAsync(new Emoji("🤫"));
                    }

                    // Randomly ping Jack with an AI-generated elephant fact when the target user speaks
                    if (isTargetUser && _random.NextDouble() < _config.ELEPHANT_FACT_CHANCE)
                    {
                        Console.WriteLine($"[ElephantFact] Triggered for message {message.Id} from target user {message.Author.Id}.");
                        var fact = await GenerateElephantFactAsync();
                        Console.WriteLine($"[ElephantFact] Generated fact: {fact}");

                        using var imageStream = await GenerateElephantImageAsync(fact);
                        var pingMessage = $"<@{_config.JACK_USER_ID}> {fact}";

                        if (imageStream != null)
                        {
                            Console.WriteLine("[ElephantFact] Attaching generated image to ping.");
                            await message.Channel.SendFileAsync(imageStream, "elephant.png", pingMessage);
                        }
                        else
                        {
                            Console.WriteLine("[ElephantFact] No image generated; sending text-only ping.");
                            await message.Channel.SendMessageAsync(pingMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MessageHandler] Unhandled error processing message {message.Id}: {ex.Message}");
                }
            });
        }
    }

    private static async Task<string> ExecuteCommandAsync(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = output;
            if (!string.IsNullOrEmpty(error))
            {
                result += "\n" + error;
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }

    private static async Task<string> GenerateElephantFactAsync()
    {
        try
        {
            var chatApiUrl = GetChatApiUrl();
            Console.WriteLine($"[ElephantFact] Requesting fact from {chatApiUrl} using model {_config.OPENAI_MODEL}.");

            var request = new ChatCompletionRequest
            {
                Model = _config.OPENAI_MODEL,
                Messages =
                [
                    new ChatMessage { Role = "system", Content = "You generate short, random, interesting elephant facts. Respond with ONLY the fact — one or two sentences. No reasoning, no preamble, no explanation." },
                    new ChatMessage { Role = "user", Content = "Tell me a random elephant fact." }
                ],
                MaxTokens = 2000,
                Temperature = 0.9
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, chatApiUrl);
            requestMessage.Headers.Add("Authorization", $"Bearer {_config.OPENAI_API_KEY}");
            requestMessage.Content = content;

            using var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ElephantFact] Raw fact response: {responseJson}");

            var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);
            var fact = completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            // Strip special/control tokens like <|channel|> and refuse to use garbage output
            if (!string.IsNullOrWhiteSpace(fact))
            {
                fact = System.Text.RegularExpressions.Regex.Replace(fact, @"<\|[^|]+\|>", string.Empty).Trim();
            }

            if (!string.IsNullOrWhiteSpace(fact))
            {
                Console.WriteLine($"[ElephantFact] Parsed fact: {fact}");
                return fact;
            }

            Console.WriteLine("[ElephantFact] Fact response was empty or contained only special tokens; using fallback.");
            return GetFallbackElephantFact();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ElephantFact] Failed to generate elephant fact: {ex.Message}");
            return GetFallbackElephantFact();
        }
    }

    private static string NormalizeBaseUrl(string url)
    {
        return url.TrimEnd('/');
    }

    private static string GetChatApiUrl()
    {
        return $"{NormalizeBaseUrl(_config.OPENAI_API_URL)}/v1/chat/completions";
    }

    private static string GetImageApiUrl()
    {
        if (!string.IsNullOrWhiteSpace(_config.OPENAI_IMAGE_API_URL))
        {
            return _config.OPENAI_IMAGE_API_URL;
        }

        return $"{NormalizeBaseUrl(_config.OPENAI_API_URL)}/v1/images/generations";
    }

    private static async Task<Stream?> GenerateElephantImageAsync(string fact)
    {
        try
        {
            var imageApiUrl = GetImageApiUrl();
            var prompt = $"A sloppy AI-generated Facebook-style image depicting: {fact}";
            Console.WriteLine($"[ElephantImage] Requesting image from {imageApiUrl} with prompt: {prompt}");

            var request = new ImageGenerationRequest
            {
                Model = "flux-2-klein-4b",
                Prompt = prompt,
                N = 1,
                Size = "512x512"
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, imageApiUrl);
            requestMessage.Headers.Add("Authorization", $"Bearer {_config.OPENAI_API_KEY}");
            requestMessage.Content = content;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            using var response = await _httpClient.SendAsync(requestMessage, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            Console.WriteLine($"[ElephantImage] Raw image response: {responseJson}");

            var imageResponse = JsonSerializer.Deserialize<ImageGenerationResponse>(responseJson);
            var imageData = imageResponse?.Data?.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(imageData?.Url))
            {
                Console.WriteLine($"[ElephantImage] Downloading image from URL: {imageData.Url}");
                using var imageStream = await _httpClient.GetStreamAsync(imageData.Url, cts.Token);
                var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream, cts.Token);
                memoryStream.Position = 0;
                Console.WriteLine($"[ElephantImage] Downloaded {memoryStream.Length} bytes.");
                return memoryStream;
            }

            if (!string.IsNullOrWhiteSpace(imageData?.B64Json))
            {
                var bytes = Convert.FromBase64String(imageData.B64Json);
                Console.WriteLine($"[ElephantImage] Decoded base64 image ({bytes.Length} bytes).");
                return new MemoryStream(bytes);
            }

            Console.WriteLine("[ElephantImage] Image response contained no URL or base64 data.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ElephantImage] Failed to generate elephant image: {ex.Message}");
            return null;
        }
    }

    private static string GetFallbackElephantFact()
    {
        string[] facts =
        [
            "Elephants are the largest land animals on Earth.",
            "An elephant's trunk has more than 40,000 muscles.",
            "Elephants can communicate through vibrations in the ground.",
            "A group of elephants is called a herd.",
            "Elephants can recognize themselves in a mirror."
        ];
        return facts[_random.Next(facts.Length)];
    }

    private class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = [];
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; } = new();
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class ImageGenerationRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("n")]
        public int N { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;
    }

    private class ImageGenerationResponse
    {
        [JsonPropertyName("data")]
        public List<ImageData> Data { get; set; } = [];
    }

    private class ImageData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }
    }
} 