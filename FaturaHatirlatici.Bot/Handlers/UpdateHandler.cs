using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;
using FaturaHatirlatici.Business.Services;
using FaturaHatirlatici.Bot.BotServices;
using FaturaHatirlatici.Bot.Conversations;
using System.Globalization;
namespace FaturaHatirlatici.Bot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConversationService _conversationService;
    public UpdateHandler(ILogger<UpdateHandler> logger, IServiceScopeFactory serviceScopeFactory, IConversationService conversationService)
    {
        _conversationService = conversationService;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Bot Error Source:{Source}", source);
        return Task.CompletedTask;
    }

    private async Task HandleStartAsync(ITelegramBotClient botClient, Message message,User from, CancellationToken cancellationToken)
    {
        
        
            using var scope = _serviceScopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            bool isNewUser = await userService.RegisterAsync
                (
                  chatId: message.Chat.Id,
                  telegramUserId: from.Id,
                  name: from.FirstName,
                  userName: from.Username


                     );
            if (isNewUser)
            {
                await botClient.SendMessage(chatId: message.Chat.Id, "Welcome to EchoYol_Bot", cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId: message.Chat.Id, "Welcome back dear user", cancellationToken: cancellationToken);
            }
            return;
        
    }
    private async Task HandleAddAsync(ITelegramBotClient botClient, Message message, User from, CancellationToken cancellationToken)
    {
         

       

            using var scope = _serviceScopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            bool isUser = await userService.IsRegisteredAsync(from.Id);
            if (!isUser)
            {
                await botClient.SendMessage(chatId: message.Chat.Id, "Please /start first", cancellationToken: cancellationToken);
                return;
            }
            var stateConversation = new ConversationState();
            _conversationService.SaveConversation(from.Id, stateConversation);


            await botClient.SendMessage(chatId: message.Chat.Id, "What is the name on your invoice?", cancellationToken: cancellationToken);
            return;
        

    }
    private async Task HandleCancelAsync(ITelegramBotClient botClient, Message message, User from, CancellationToken cancellationToken)
    {
       

        await botClient.SendMessage(chatId: message.Chat.Id, "Good Bye " + message.Chat.Username, cancellationToken: cancellationToken);
        _conversationService.DeleteConversation(from.Id);
        
        return;


    }
    private async Task HandleAmountAsync(ITelegramBotClient botClient, Update update, Message message, User from, CancellationToken cancellationToken)
    {
        var messageText = update.Message.Text;
        var state = _conversationService.GetConversation(from.Id);
        CultureInfo trCulture = new CultureInfo("tr-TR");
        if (decimal.TryParse(messageText, NumberStyles.Number, trCulture, out decimal parsedAmount) && parsedAmount > 0 && !messageText.Contains('.'))
        {
            state.Amount = parsedAmount;
            state.ConversationStep = ConversationStep.AwaitingDueDay;
            _conversationService.SaveConversation(from.Id, state);



            await botClient.SendMessage(chatId: message.Chat.Id, "What is The Due Day", cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a valid amount etc[400,43]", cancellationToken: cancellationToken);
        }
        return;
    }
    private async Task HandleAwaitingNameAsync(ITelegramBotClient botClient,Update update, Message message, User from ,CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.Message.Text))
        {
            await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a valid name", cancellationToken: cancellationToken);
            return;
        }
        if (update.Message.Text.Length > 100)
        {
            await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a valid name less than 100", cancellationToken: cancellationToken);
            return;
        }
        if (update.Message.Text.StartsWith('/'))
        {
            await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a valid name", cancellationToken: cancellationToken);
            return;
        }
        var state = _conversationService.GetConversation(from.Id);
        state.Name = update.Message.Text; state.ConversationStep = ConversationStep.AwaitingAmount; _conversationService.SaveConversation(from.Id, state);
        await botClient.SendMessage(chatId: message.Chat.Id,
        "How Much Is It?", cancellationToken: cancellationToken);
        return;

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
            await HandleStartAsync(botClient, message:message,from:from, cancellationToken);
            return;
        }


            if (messageText == "/add")
        {
            await HandleAddAsync(botClient, message: message, from: from, cancellationToken);
            return;
        }

            if (messageText == "/cancel")
        {
           await HandleCancelAsync(botClient, message: message, from: from, cancellationToken);
            return;

           
        }
        var state = _conversationService.GetConversation(from.Id);
        if (state != null)
        {


            switch (state.ConversationStep)
            {
                case ConversationStep.AwaitingName:
                    HandleAwaitingNameAsync(botClient, update, message, from, cancellationToken);
                    return;
                case ConversationStep.AwaitingAmount:
                  
                case ConversationStep.AwaitingDueDay:
                    if (int.TryParse(messageText, out var result) && result <= 31 && result >= 1)
                    {
                        using var scope2 = _serviceScopeFactory.CreateScope();

                        var bills = scope2.ServiceProvider.GetRequiredService<IBillService>();
                        bool isThis = await bills.AddBillAsync(from.Id, state.Name!, state.Amount.Value, result);
                        _conversationService.DeleteConversation(from.Id);
                        if (isThis)
                        {
                            await botClient.SendMessage(chatId:message.Chat.Id, "✅ Saved..",cancellationToken:cancellationToken) ;

                        }
                        else
                        {
                            await botClient.SendMessage(chatId: message.Chat.Id, "Please /start first", cancellationToken: cancellationToken);

                        }
                    }
                    else
                        await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a day between 1 and 31", cancellationToken: cancellationToken);
                    return;
                default:
                    break;
            }
            


        }
      


        await botClient.SendMessage(message.Chat.Id, messageText, cancellationToken: cancellationToken);

    }
}
