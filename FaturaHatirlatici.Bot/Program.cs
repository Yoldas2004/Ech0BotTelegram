using FaturaHatirlatici.Bot;
using FaturaHatirlatici.Bot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;

var builder = Host.CreateApplicationBuilder(args);
var tokenString = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(tokenString))
{
    throw new InvalidOperationException("'Telegram:BotToken' bulunamadı  secrets a bakin ");
}
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tokenString));
builder.Services.AddSingleton<IUpdateHandler,UpdateHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
