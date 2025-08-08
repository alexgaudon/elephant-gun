using Discord;
using Discord.WebSocket;

namespace ElephantGun;

public class Program
{
    private static DiscordSocketClient? _client;
    private static ulong TARGET_USER_ID;
    private static ulong BOT_OWNER_ID;

    public static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        var targetUserIdStr = Environment.GetEnvironmentVariable("TARGET_USER_ID");
        var botOwnerIdStr = Environment.GetEnvironmentVariable("BOT_OWNER_ID");
        
        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("DISCORD_TOKEN environment variable is required!");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(targetUserIdStr) || !ulong.TryParse(targetUserIdStr, out TARGET_USER_ID))
        {
            Console.Error.WriteLine("TARGET_USER_ID environment variable is required and must be a valid ulong!");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(botOwnerIdStr) || !ulong.TryParse(botOwnerIdStr, out BOT_OWNER_ID))
        {
            Console.Error.WriteLine("BOT_OWNER_ID environment variable is required and must be a valid ulong!");
            Environment.Exit(1);
        }

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        });

        _client.Log += Log;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, token);
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
        // Check if the message is from the target user or contains "elephant"
        if (message.Author.Id == TARGET_USER_ID || 
            message.Content.ToLower().Contains("elephant"))
        {
            // React with elephant emoji
            await message.AddReactionAsync(new Emoji("🐘"));
            
            // React with gun emoji
            await message.AddReactionAsync(new Emoji("🔫"));
        }
    }
} 