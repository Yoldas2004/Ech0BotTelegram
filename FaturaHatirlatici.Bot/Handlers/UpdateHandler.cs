
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace FaturaHatirlatici.Bot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(ILogger<UpdateHandler> logger)
    {
        _logger = logger;
    }
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Bot Error Source:{Source}", source);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message == null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(update.Message.Text))
        {

            return;
        }
        await botClient.SendMessage(update.Message.Chat.Id, update.Message.Text, cancellationToken:cancellationToken);
    
    }
}
