

```cs
 using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace FaturaHatirlatici.Bot;

public class Worker(ILogger<Worker> logger, ITelegramBotClient botClient , IUpdateHandler handler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
       var me = await botClient.GetMe(cancellationToken:stoppingToken);
        logger.LogInformation("Bot Working {@me}",me.Username);
        
        ReceiverOptions options = new ()
        {
            AllowedUpdates = new[] {UpdateType.Message}
        };
        botClient.StartReceiving(updateHandler:handler,receiverOptions:options  , cancellationToken :stoppingToken);

       await Task.Delay(Timeout.Infinite,stoppingToken);
    }
}
```
Bu Kod Blogu .Net mimarisinde arka planda calisan uygulamalar olusturulurken kullanilan BackgroundService sinifinin kalbidir

### Gorevi ne 

.Net'in BackgroundService sinifindan alinan temel calisma metodudur 
Uygulama(bot) baslatildigi zaman otomatik olarak tetiklenerek arkaplanda calismaya baslar

### await botClient.GetMe Nedir

Telegram sunucusuna Ben Kimim? sorusunu sorar ve botun kendi bilgilerini geri donduren bir metoddur Burada telegrama basariyla baglanip baglanmadigini test etmek amaci ile burada kullaniyorum

### botClient.StartReceiving
bottan gelen mesajlari ve guncellemeleri surekli olarak dinlemek icin kullanilir 

```cs

botClient.StartReceiving(updateHandler:handler,receiverOptions:options, cancellationToken :stoppingToken);
```

----
### updateHandler:
Gelen yeni bilgilerin kod uygulamanizin hangi kod bloguna yonlendirilecegini bildirir
### receiverOptions:
Ayarladigimiz kurallari filtreler
### cancellationToken:
Disaridan gelen kapatma sinyallerini algilar ve uygular




 
