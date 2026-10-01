
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;
using FaturaHatirlatici.Business.Services;

namespace FaturaHatirlatici.Bot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    public UpdateHandler(ILogger<UpdateHandler> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Bot Error Source:{Source}", source);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }
        if (update.Message.Text is not { } messageText) 
        {
            return;
        }
        if (message.From is not { } from) { return; }
        if (messageText == "/start")
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            bool isNewUser = await userService.RegisterAsync
                (
                  chatId : message.Chat.Id,
                  telegramUserId: from.Id,
                  name: from.FirstName,
                  userName:from.Username
                  
                 
                     );
            if (isNewUser) 
            {
               await botClient.SendMessage(chatId:message.Chat.Id,"Welcome to EchoYol_Bot",cancellationToken:cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId:message.Chat.Id,"Welcome back dear user",cancellationToken:cancellationToken);
            }
           return;
        }
        if (update.Message == null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(update.Message.Text))
        {

            return;
        }
        await botClient.SendMessage(message.Chat.Id, messageText, cancellationToken:cancellationToken);
    
    }
}
