using FaturaHatirlatici.Bot.BotServices;
using FaturaHatirlatici.Bot.Conversations;
using FaturaHatirlatici.Business.Services;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
//using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
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

    private async Task HandleStartAsync(ITelegramBotClient botClient, Message message, User from, CancellationToken cancellationToken)
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
    private async Task HandleAwaitingAmountAsync(ITelegramBotClient botClient, Update update, Message message, User from, CancellationToken cancellationToken)
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
    private async Task HandleAwaitingNameAsync(ITelegramBotClient botClient, Update update, Message message, User from, CancellationToken cancellationToken)
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
    private async Task HandleAwaitingDueDayAsync(ITelegramBotClient botClient, Update update, Message message, User from, CancellationToken cancellationToken)
    {
        var messageText = update.Message.Text;
        var state = _conversationService.GetConversation(from.Id);
        if (int.TryParse(messageText, out var result) && result <= 31 && result >= 1)
        {
            using var scope2 = _serviceScopeFactory.CreateScope();

            var bills = scope2.ServiceProvider.GetRequiredService<IBillService>();
            bool isThis = await bills.AddBillAsync(from.Id, state.Name!, state.Amount.Value, result);
            _conversationService.DeleteConversation(from.Id);
            if (isThis)
            {
                await botClient.SendMessage(chatId: message.Chat.Id, "✅ Saved..", cancellationToken: cancellationToken);

            }
            else
            {
                await botClient.SendMessage(chatId: message.Chat.Id, "Please /start first", cancellationToken: cancellationToken);

            }
        }
        else
            await botClient.SendMessage(chatId: message.Chat.Id, "Please enter a day between 1 and 31", cancellationToken: cancellationToken);
        return;
    }
    private async Task HandleListAsync(ITelegramBotClient botClient, Message message, User from, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var userService= scope.ServiceProvider.GetRequiredService<IUserService>();
        var billService = scope.ServiceProvider.GetRequiredService<IBillService>();
        bool isUser = await userService.IsRegisteredAsync(from.Id);

        if (!isUser) 
        {
            await botClient.SendMessage(chatId: message.Chat.Id,"Please use /start first",cancellationToken:cancellationToken);
            return;
        }
            var bills= await billService.GetBillsAsync(from.Id);
        if (bills.Count ==0)
        {
            await botClient.SendMessage(chatId: message.Chat.Id, "There is nothing here!!", cancellationToken: cancellationToken);
            return ;
        }
         
         
        var sb = new StringBuilder();
        sb.AppendLine(" Bill  list:");
        var cI = new CultureInfo("tr-TR");
        for (int i = 0; i < bills.Count; i++)
        {
            sb.AppendLine($"{i+1}. Title - {bills[i].Title}\nAmount - {bills[i].Amount.ToString("N2",cI)}\nDue Day - {bills[i].DueDay}");
        }

        await botClient.SendMessage(chatId:message.Chat.Id,sb.ToString(),cancellationToken:cancellationToken);

        
      }
    private async Task HandleDeleteAsync(ITelegramBotClient botClient, Message message, User from, CancellationToken cancellationToken)
    { 
    using var scope = _serviceScopeFactory.CreateScope();
    var user = scope.ServiceProvider.GetRequiredService<IBillService>();
        var parts = message.Text.Split(' ');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int num))
        { 
            await botClient.SendMessage(chatId:message.Chat.Id,"Usage /delete <num>",cancellationToken:cancellationToken);
            return;
        }
        var bills = await user.GetBillsAsync(from.Id);
        if (num < 1 || num > bills.Count)
        {
        await botClient.SendMessage(chatId: message.Chat.Id, $"No bill with number{num}", cancellationToken: cancellationToken);
            return;
        }
        var bill = bills[num - 1];
        await user.DeleteBillAsync(from.Id,bill.Id);
        await botClient.SendMessage(chatId: message.Chat.Id,$"Deleted {bill.Title}", cancellationToken: cancellationToken);

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
            await HandleStartAsync(botClient, message: message, from: from, cancellationToken);
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
        if(messageText =="/list")
        {
            await HandleListAsync(botClient, message, from, cancellationToken);
            return;
        }
        if (messageText.StartsWith( "/delete"))
        {
            await HandleDeleteAsync(botClient, message, from, cancellationToken);
            return;
        }
        var state = _conversationService.GetConversation(from.Id);
        if (state != null)
        {


            switch (state.ConversationStep)
            {
                case ConversationStep.AwaitingName:
                    await HandleAwaitingNameAsync(botClient, update, message, from, cancellationToken);
                    return;
                case ConversationStep.AwaitingAmount:
                    await HandleAwaitingAmountAsync(botClient, update, message, from, cancellationToken);
                    return;
                case ConversationStep.AwaitingDueDay:
                    await HandleAwaitingDueDayAsync(botClient, update, message, from, cancellationToken);
                    return;
                default:
                    break;
            }



        }



        await botClient.SendMessage(message.Chat.Id, messageText, cancellationToken: cancellationToken);

    }
}
