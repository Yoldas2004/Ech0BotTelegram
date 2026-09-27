using FaturaHatirlatici.Bot;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);
var tokenString = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(tokenString))
{
    throw new InvalidOperationException("'Telegram:BotToken' bulunamadı  secrets a bakin ");
}
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tokenString));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
