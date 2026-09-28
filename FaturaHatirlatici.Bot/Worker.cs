
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace FaturaHatirlatici.Bot;

public class Worker(ILogger<Worker> logger, ITelegramBotClient botClient, IUpdateHandler handler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMe(cancellationToken: stoppingToken);
        logger.LogInformation("Bot Working {BotUserName}", me.Username);

        ReceiverOptions options = new()
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };
        botClient.StartReceiving(updateHandler: handler, receiverOptions: options, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
