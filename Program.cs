using Discord;
using Discord.WebSocket;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ElephantGun;

public class Config
{
    [Required]
    [ConfigurationKeyName("DISCORD_TOKEN")]
    public string DiscordToken { get; set; }

    [Required]
    [ConfigurationKeyName("TARGET_USER_ID")]
    public ulong TargetUserId { get; set; }

    [Required]
    [ConfigurationKeyName("BOT_OWNER_ID")]
    public ulong BotOwnerId { get; set; }
}

public static class Program
{
    private static DiscordSocketClient? _client;
    private static Config _config = null!;

    public static async Task Main(string[] args)
    {
        Console.Write(Environment.CurrentDirectory);
        DotEnv.Load();
        
        Console.WriteLine(Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
        
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

        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
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
        // Check if the message is from the bot owner for command execution
        if (message.Author.Id == _config.BotOwnerId)
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
        if (message.Author.Id == _config.TargetUserId ||
            contentLower.Contains("elephant") ||
            contentLower.Contains("php"))
        {
            // React with custom shut emoji
            await message.AddReactionAsync(Emote.Parse("<:shut:1403433783830646835>"));
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
} 