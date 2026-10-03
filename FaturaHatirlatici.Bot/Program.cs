using FaturaHatirlatici.Bot;
using FaturaHatirlatici.Bot.Handlers;
using FaturaHatirlatici.Business.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;

var builder = Host.CreateApplicationBuilder(args);
var tokenString = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(tokenString))
{
    throw new InvalidOperationException("'Telegram:BotToken' bulunamadı  secrets a bakin ");
}

var cString = builder.Configuration["ConnectionStrings:DefaultConnection"];
if (!string.IsNullOrWhiteSpace(cString))
{
    builder.Services.AddDbContext<FaturaHatirlatici.Data.FaturaDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    throw new InvalidOperationException("'ConnectionStrings:DefaultConnection' bulunamadı  ");
}
    

builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tokenString));
builder.Services.AddSingleton<IUpdateHandler,UpdateHandler>();
builder.Services.AddScoped<IBillService, BillService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IUserService, UserService>();

var host = builder.Build();
host.Run();
