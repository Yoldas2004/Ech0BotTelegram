# FaturaHatirlatici Bot: Proje Notları

> [!info] Bu dosya ne? Telegram fatura hatırlatma botunda şimdiye kadar yapılan her şeyin, alınan kararların ve öğrenilen kavramların özeti. Kod parçaları benim yazdığım kodlardır. Açık kalan review maddeleri ayrıca işaretlenmiştir.

---

## 0. Genel Bakış

### Proje

Kullanıcı Telegram'dan faturalarını ekler (ad, tutar, her ayın kaçında ödeneceği). Bot, son ödeme gününden X gün önce hatırlatma gönderir. Kullanıcı faturalarını listeleyebilir, silebilir ve "ödendi" olarak işaretleyebilir.

### Teknik Kararlar

|Konu|Karar|Neden|
|---|---|---|
|Proje tipi|.NET **Worker Service**|HTTP isteği beklemiyoruz, arka planda sürekli çalışan iş var|
|Telegram|**Telegram.Bot** NuGet paketi, **long polling**|Webhook için dışarıya açık bir sunucu gerekir, şimdilik gerek yok|
|Veritabanı|EF Core + **SQLite** (ileride)|Kurulumu basit, sonra MySQL'e geçilebilir|
|Katmanlar|`Bot` / `Business` / `Data`|Sorumlulukları ayırmak|
|Hatırlatma|`BackgroundService`|Kimse tetiklemeden her gün kendi çalışacak|
|Token|**User Secrets**|Asla repoya girmemeli|

### Yol Haritası

1. ==Kurulum + echo bot== ← **şu an buradayız**
2. Entity'ler + `/start` ile kullanıcı kaydı
3. `/ekle`: çok adımlı konuşma (asıl zor kısım)
4. `/liste`, `/sil`, `/odendi`
5. Hatırlatma servisi
6. Sağlamlaştırma: validasyon, saat dilimi, README, Docker

---

## 1. Proje Kurulumu

### Neden Web API değil de Worker?

- **Web API**, dışarıdan gelen HTTP isteklerini bekler. İstek gelmezse hiçbir şey yapmaz.
- **Bu bot** iki şeyi **kendiliğinden** yapıyor:
    - **Polling:** Bot, Telegram'a sürekli "yeni mesaj var mı?" diye soruyor. İsteği başlatan taraf biziz.
    - **Hatırlatma:** Her gün belli bir saatte faturaları kontrol edip mesaj atacak. Bunu kimse tetiklemiyor.

İkisi de "uygulama açık olduğu sürece arka planda çalışan iş" demek. Worker tam olarak bunun için var. Web API ile de yapılabilir, ama kullanmayacağımız bir HTTP katmanını taşımış oluruz.

> [!note] Ne zaman değişir? Webhook'a geçersek Telegram mesajları bize HTTP ile gönderir. O zaman bir endpoint gerekir ve ASP.NET Core ekleriz.

### Worker nasıl oluşturulur?

**Visual Studio (Türkçe):** Yeni proje → arama kutusuna ==**"Çalışan"**== yaz → **"Çalışan Hizmeti"** (Worker Service), C#.

> [!warning] Türkçe VS tuzağı Arama kutusuna "Worker" yazınca "Tam eşleşme yok" çıkar. Şablonun adı Türkçe'dir.

**CLI:**

```
dotnet new sln -n FaturaHatirlatici
dotnet new worker -n FaturaHatirlatici.Bot
dotnet sln add FaturaHatirlatici.Bot
```

### Şablonla gelen dosyalar

- `Program.cs`: Host'un kurulduğu, servislerin DI'a kaydedildiği yer. **İş yapmaz, bağımlılıkları kaydeder.**
- `Worker.cs`: `BackgroundService`'ten türeyen sınıf. `ExecuteAsync` metodu uygulama çalıştığı sürece yaşar.
- `appsettings.json`: Gizli olmayan ayarlar.
- `Properties/launchSettings.json`: Çalıştırma profili. Ortamı `Development` olarak ayarlar.

İlk kontrol: Proje hiç değiştirilmeden çalıştırılınca konsolda saniyede bir `Worker running at...` görünmeli.

### `.slnx` nedir?

Yeni .NET SDK'nın solution formatı (eski `.sln`'in XML hali). Sorun değil, sadece yeni bir SDK kullanıldığını gösterir. `.csproj` içindeki `TargetFramework` değerine bakılır. `net10.0` ise o da LTS, ona devam edilir.

### Yaşanan karışıklık: Yanlış proje / yanlış solution

- Yanlışlıkla iki proje oluştu: `EchoBot` ve `FaturaHatirlatici.Bot`. Kod `EchoBot`'ta yazılıyordu.
- Solution Explorer başlığında ==**"'EchoBot' çözümü (proje 2/2)"**== yazıyordu. Yani yanlış solution açıktı ve iki proje de onun içindeydi.
- Worker'lı **her projenin kendi** `Program.cs` ve `Worker.cs` dosyası olur.
- Karar: **FaturaHatirlatici.Bot** ile devam.

> [!tip] Ders
> 
> - Solution Explorer'ın başlığı hangi solution'ın açık olduğunu söyler, oraya bak.
> - "EchoBot" bir **görevin** adıydı (Ticket #1), projenin değil. Proje adı ne yaptığını anlatmalı. Avrupa'da işveren GitHub'a baktığında adından ne olduğunu anlamalı.
> - Yeniden adlandırmanın en ucuz olduğu an projenin başıdır.

> [!warning] Projeye özel olan şeyler Kod başka projeye taşınırsa bunlar **taşınmaz**, yeniden kurulmalı:
> 
> - **NuGet paketleri** (her `.csproj`'un kendi paket listesi var)
> - **User Secrets** (her `.csproj`'un kendi `UserSecretsId`'si var)

### Namespace

Proje adı namespace'i belirler: `FaturaHatirlatici.Bot`. Klasör açınca alt namespace oluşur: `Handlers/` klasörü → `FaturaHatirlatici.Bot.Handlers`.

**File-scoped namespace** (modern stil, şablon da böyle):

```cs
namespace FaturaHatirlatici.Bot.Handlers;
```

Süslü parantez ve bir girinti seviyesi kalkar.

**Implicit usings:** Proje `System`, `System.Collections.Generic`, `System.Linq` vb. using'leri otomatik ekler. Bunları elle yazmaya gerek yok. Gri görünen using'ler kullanılmıyordur, silinir.

---

## 2. Bot Token ve User Secrets

### Token nedir?

BotFather'dan alınan, botu kontrol etmeyi sağlayan anahtar. **Token'a sahip olan herkes botu kontrol edebilir.**

Yapısı (bu örnek **uydurma**):

```
123456789:ABCdefGhIJKlmNoPQRstuVWXyz12345678
```

- `123456789` → botun ID'si
- `:` → ayraç (token'ın **kendi parçası**, dokunulmaz)
- `ABCdef...` → gizli kısım

Üçü birlikte **tek bir string**'dir.

> [!danger] Token sızarsa Token bir chat'e, log'a veya commit'e düştüyse **sızmış sayılır**. BotFather'a git → `/revoke` → yeni token al. Yeni token'ı hiçbir yere yapıştırma, sadece secrets.json'a yaz.

### secrets.json formatı

Projeye sağ tık → ==**"Kullanıcı Gizli Anahtarlarını Yönet"**== (Manage User Secrets).

İki geçerli yazım (ikisi de kodda `Telegram:BotToken` ile okunur):

```json
{
  "Telegram": {
    "BotToken": "123456789:ABCdef..."
  }
}
```

```json
{
  "Telegram:BotToken": "123456789:ABCdef..."
}
```

- `"BotToken"` → **anahtar** (etiket, değişken adı gibi). Kodda bu isimle aranır.
- `"123456789:ABC..."` → **değer**. Token'ın **tamamı** buraya gelir.

> [!warning] Yapılan hata `86898833483":AAF...` → Sayılardan sonra fazladan `"` vardı. JSON bozulur. Token baştan sona tek çift tırnağın içinde olmalı.

### secrets.json nerede duruyor?

Proje klasöründe **değil**:

```
%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
```

`<UserSecretsId>`, `.csproj` içindeki değerdir. Git bu klasörü **hiç görmez**. Bu yüzden repo public olsa da token görünmez.

`UserSecretsId`'nin kendisi gizli değildir, sadece klasörün adıdır.

### `appsettings.json` vs `secrets.json`

||`appsettings.json`|`secrets.json`|
|---|---|---|
|Nerede|Proje klasöründe|Proje dışında (`%APPDATA%`)|
|Git'e girer mi|✅ Evet|❌ Asla|
|Ne yazılır|Gizli olmayan ayarlar (log seviyesi, hatırlatma saati)|Token, şifre, API key|

İkisi **birleşir**, kod hepsini `builder.Configuration` üzerinden tek yerden okur.

`appsettings.Development.json`: Sadece geliştirme ortamında `appsettings.json`'ın üzerine yazılan ayarlar.

> [!danger] Token şu durumlarda sızar
> 
> - `appsettings.json`'a veya koda yazılırsa
> - Log'a basılırsa ve log dosyası repoya girerse

---

## 3. Program.cs

```cs
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
builder.Services.AddSingleton<IUpdateHandler, UpdateHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

**Program.cs'in görevi:** Bağımlılıkları kaydetmek. İş yapmaz.

### 3.1 Token'ı okumak

```cs
var tokenString = builder.Configuration["Telegram:BotToken"];
```

- **Indexer** (`[...]`) ile normal bir config değeri okunur.
- `:` iç içe JSON seviyelerini ayırır (`Telegram` → `BotToken`).

> [!warning] Yapılan hata: `GetConnectionString` `builder.Configuration.GetConnectionString("BotToken")` aslında **`ConnectionStrings:BotToken`** anahtarına bakar. Token hep null gelir. Connection string **veritabanı bağlantısı** içindir. Normal config değeri indexer ile okunur.

### 3.2 Token kontrolü

```cs
if (string.IsNullOrWhiteSpace(tokenString))
{
    throw new InvalidOperationException("...");
}
```

- `IsNullOrWhiteSpace` → null, boş **ve** sadece boşluk karakterlerinden oluşan değerleri yakalar. `IsNullOrEmpty` boşlukları kaçırır.
- Bot token'sız **başlamamalı**. Hata en başta, anlamlı bir mesajla verilmeli.

**Neden `InvalidOperationException`?** Token bir metot parametresi değil. Sorun uygulamanın **başlangıç durumunun (config)** eksik olması. (Bkz. [[#4. Exception Notları]])

**Hata mesajı nasıl olmalı:** Altı ay sonra başka bir bilgisayarda projeyi açtığında sana ne yapacağını söylemeli:

- **Hangi anahtar** eksik (`Telegram:BotToken`)
- **Nasıl eklenir** (user secrets)

> [!tip] Örnek mesaj `'Telegram:BotToken' bulunamadı. Proje için user secrets'a ekleyin.` ❌ `"Token Bos"` → neyin eksik olduğunu söylemez.

### 3.3 Client'ı DI'a kaydetmek: Factory (lambda)

```cs
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tokenString));
//                             ↑ neyi istersen      ↑ ne verilecek
```

**Neden factory gerekli?** `TelegramBotClient`'ın constructor'ı token istiyor. DI, constructor'a hangi string'i vereceğini bilemez. Nesnenin **nasıl oluşturulacağını** DI'a biz söylüyoruz.

- `sp` → `IServiceProvider`. İçeride başka bir servise ihtiyaç olursa `sp.GetRequiredService<...>()` ile alınır. Burada gerekmiyor.
- İsim olarak `sp` (service provider) yaygındır. `sl` de çalışır ama okuyan ne olduğunu hemen anlamaz.
- Lambda **ilk kez istendiğinde bir kere** çalışır. Singleton olduğu için sonra hep aynı nesne döner.

**Genel desen (başka örnek):**

```cs
// ❌ Çalışmaz: DI constructor'a hangi string'i vereceğini bilemez
builder.Services.AddSingleton<SmsClient>();

// ✅ Factory ile
var apiKey = builder.Configuration["Sms:ApiKey"];
builder.Services.AddSingleton<ISmsClient>(sp => new SmsClient(apiKey));
```

### 3.4 Interface ile kaydetmek

Generic parametre **interface**, oluşturulan nesne **somut sınıf** olur. Worker `ITelegramBotClient` istediğinde DI ona bir `TelegramBotClient` verir.

**Neden?** Worker ve handler interface'e bağımlı olur. İleride test yazarken sahte (fake/mock) bir client verilebilir.

> [!warning] Yapılan hata: Interface'ten nesne oluşturmak `new ITelegramBotClient(tokenString)` → **derlenmez**. Interface bir sözleşmedir, `new` ile oluşturulamaz.

### 3.5 Handler kaydı

```cs
builder.Services.AddSingleton<IUpdateHandler, UpdateHandler>();
```

Factory gerekmez, çünkü `UpdateHandler`'ın constructor'ı sadece `ILogger` istiyor ve onu DI zaten biliyor. DI nesneyi kendisi oluşturabilir.

Lifetime'ın neden **Singleton** olduğu: [[#5. DI Lifetime'ları ve Captive Dependency]]

### 3.6 Worker kaydı

```cs
builder.Services.AddHostedService<Worker>();
```

Hosted service'ler **singleton** olarak kaydedilir. Uygulama başlarken çalışmaya başlar, kapanırken durdurulur.

> [!note] Stil `<IUpdateHandler, UpdateHandler>` → virgülden sonra boşluk. VS'te `Ctrl + K, Ctrl + D` belgeyi otomatik biçimlendirir.

---

## 4. Exception Notları

### Karar Kuralı

- Sorun **çağıranın verdiği değerde** mi? → `ArgumentException` (ve alt sınıfları)
- Sorun **nesnenin ya da sistemin o anki durumunda** mı? → `InvalidOperationException`

### ArgumentException

Bir metoda gönderilen parametrelerden biri geçersiz veya hatalı olduğunda fırlatılır.

**Alt sınıfları:**

- `ArgumentNullException` → değer null geldiğinde
- `ArgumentOutOfRangeException` → değer izin verilen aralığın dışında olduğunda

```cs
public class User
{
    public int Age { get; private set; }

    public void UpdateAge(int newAge)
    {
        if (newAge < 0 || newAge > 150)
        {
            throw new ArgumentOutOfRangeException(nameof(newAge), "Yaş 0-150 arasında olmalı.");
        }
        Age = newAge;
    }
}
```

> [!warning] Tuzak: Parametre sırası tipe göre değişir
> 
> - `ArgumentException(message, paramName)` → önce **mesaj**
> - `ArgumentNullException(paramName, message)` → önce **parametre adı**
> - `ArgumentOutOfRangeException(paramName, message)` → önce **parametre adı**

> [!warning] Tuzak: `nameof` tırnak içine yazılmaz `"nameof(x)"` yazarsan düz metin olarak basılır.

**Modern yöntem (.NET 8+), hazır kontrol metotları:**

```cs
ArgumentNullException.ThrowIfNull(user);
ArgumentException.ThrowIfNullOrWhiteSpace(name);
ArgumentOutOfRangeException.ThrowIfNegative(age);
ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 150);
```

### InvalidOperationException

Parametreler doğru olsa bile, nesnenin o anki durumu yapılan işlemi desteklemediğinde fırlatılır.

```cs
public enum OrderStatus { Pending, Approved }

public class Order
{
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public void Approve()
    {
        Status = OrderStatus.Approved;
    }

    public void AddProduct()
    {
        if (Status == OrderStatus.Approved)
        {
            throw new InvalidOperationException("Onaylanmış bir siparişe yeni ürün eklenemez.");
        }
        // ürün ekleme...
    }
}
```

> [!tip] Durumu `enum` ile tut String'de yazım hatası (`"Aproved"`) yaparsan derleyici uyarmaz. Enum'da derlenmez.

### Bu projedeki kullanım

Program başlarken token config'de yoksa → `InvalidOperationException`. Token bir metot parametresi değil, sorun uygulamanın başlangıç durumunun eksik olması.

---

## 5. DI Lifetime'ları ve Captive Dependency

### Üç lifetime

|Lifetime|Ne zaman oluşturulur|Tipik kullanım|
|---|---|---|
|**Singleton**|Uygulama boyunca **bir kez**|Client'lar, durumsuz (stateless) servisler, logger|
|**Scoped**|Her **scope**'ta bir kez (Web API'de: her HTTP isteği)|`DbContext`|
|**Transient**|**Her istendiğinde** yeni|Hafif, durumsuz servisler|

### Neden client Singleton?

`TelegramBotClient` içinde bir `HttpClient` tutar. Her seferinde yeni oluşturmak gereksizdir ve bağlantı kaynaklarını tüketir. Uygulama boyunca tek bir client yeterli ve güvenlidir.

### Neden handler Singleton? Captive Dependency

İlk tercih `AddScoped` oldu. Gerekçe: "Request geldikçe çalışması mantıklı." Ama bu projede bu mantık çalışmıyor.

1. Worker bir hosted service → **singleton**. Uygulama boyunca **bir kez** oluşturulur.
2. Worker handler'ı **constructor'dan** alıyor → handler da **o anda bir kez** oluşturulur ve Worker ile birlikte uygulama kapanana kadar yaşar.
3. Burada **HTTP request yok**. Polling kütüphanesi her update'te **aynı handler nesnesinin** metodunu çağırır.

> [!important] Captive Dependency (esir bağımlılık) Singleton'ın içine giren her şey, kaydedilen lifetime ne olursa olsun **fiilen singleton olur**.

- **Scoped:** Development ortamında DI bunu kontrol eder. Worker handler'ı istediği anda uygulama **başlarken patlar**: `Cannot consume scoped service ... from singleton`.
- **Transient:** Hata vermez ama **yanıltıcıdır**. "Her seferinde yeni" diye kaydedilir, oysa hep aynı nesne kullanılır.
- **Singleton:** ✅ Handler'ın içinde değişen bir durum (state) yok, sadece logger var. Logger da singleton-güvenlidir. Lifetime'ı **gerçekte ne olacağıyla aynı** kaydetmek en dürüst yaklaşımdır.

> [!note] İleride Handler'a veritabanı (`DbContext`, scoped) gerektiğinde bu sorun gerçekten karşımıza çıkacak. Çözüm: Her update için ayrı bir scope açmak (`IServiceScopeFactory`).

---

## 6. UpdateHandler

```cs
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
        _logger.LogError(exception, "Bot Error Source{Source}", source);
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
        await botClient.SendMessage(update.Message.Chat.Id, update.Message.Text, cancellationToken: cancellationToken);
    }
}
```

> [!todo] Açık küçük düzeltme `"Bot Error Source{Source}"` → `"Bot Error Source: {Source}"`. Yoksa log'da `SourcePollingError` diye bitişik görünür.

### 6.1 Dosya ve sınıf düzeni

- Her sınıf **kendi dosyasında** durur, dosya adı sınıf adıyla aynı olur.
- Handler'lar `Handlers/` klasöründe. İleride `/ekle`, `/liste` gibi komutların handler'ları da orada toplanacak.

### 6.2 `IUpdateHandler`'ı biz oluşturmuyoruz

Interface **Telegram.Bot kütüphanesinin içinde** hazır gelir (`Telegram.Bot.Polling` namespace'i).

> [!warning] Yapılan hata: Kendi `IUpdateHandler`'ını yazmak Polling başlatılırken kütüphane **kendi** `IUpdateHandler`'ını ister. Aynı isimde kendi yazdığın interface'i tanımaz. İsimleri aynı olsa da **farklı tiplerdir**.

**Metot imzalarını otomatik getirmek:** `IUpdateHandler` üzerine gel → `Ctrl + .` → ==**"Arabirimi uygula"**== (Implement interface).

### 6.3 Bileşenlerin Anlamları

#### `private`

Bir sınıf **üyesini** (field, metot, property) sadece tanımlandığı sınıf içinde erişilebilir kılar.

#### `public`

Üyeyi veya sınıfı, ona erişebilen tüm kodlara açar.

#### `internal`

Sadece **aynı proje (assembly)** içinden erişilebilir. DI aynı projede olduğu için çalışır. Ama sınıf başka bir projeden (örneğin test projesinden) kullanılmak istenirse engel olur. Bu yüzden `public` seçildi.

#### `readonly`

Field'a değer **sadece tanımlandığı satırda veya constructor içinde** atanabilir.

> [!warning] Dikkat `readonly` **referansı** kilitler, nesnenin içini kilitlemez.
> 
> - `_logger = baskaLogger;` → ❌ yasak
> - `_logger.LogError(...)` → ✅ serbest (nesneyi kullanmak onu değiştirmek değildir) Bu fark mülakatlarda sık sorulur.

### 6.4 Logger Field'ı

```cs
private readonly ILogger<UpdateHandler> _logger;
```

- ==`ILogger<UpdateHandler>`==: .NET'in yerleşik loglama arayüzü. Generic parametre logların **hangi sınıftan** geldiğini belirtir. Log'da mesajın yanında `FaturaHatirlatici.Bot.Handlers.UpdateHandler` yazar.
- Logger bir DI örneği **değildir**, DI ile **verilen bir servistir**.
- ==`_logger`==: Field'ın adı. Alt çizgi, private field olduğunu belirten yaygın bir isimlendirme kuralıdır.

### 6.5 Constructor

```cs
public UpdateHandler(ILogger<UpdateHandler> logger)
{
    _logger = logger;
}
```

- Constructor her zaman **bulunduğu sınıfın adını** alır.
- `logger`'ı DI container verir, nesneyi biz `new` ile oluşturmayız.
- Gelen logger field'a kaydedilir, böylece diğer metotlarda kullanılabilir.

> [!tip] Neden client'ı constructor'dan almıyoruz? `ITelegramBotClient` zaten metotlara **parametre olarak geliyor**. Constructor'dan da alırsak aynı nesneyi iki yoldan almış oluruz: Gereksiz tekrar ve "hangisini kullanacağım?" karışıklığı. **Kural:** Metodun ihtiyacı parametreyle geliyorsa onu kullan, gelmiyorsa constructor'dan iste. Bu yüzden constructor'da sadece logger var.

### 6.6 HandleErrorAsync

#### Ne zaman çağrılır?

`IUpdateHandler`'dan **uygulanan (implement)** metottur. İki durumda çağrılır, hangisi olduğunu `source` söyler:

1. **`PollingError`**: Telegram'a bağlanırken hata olursa (internet kesilmesi, geçersiz token vb.)
2. **`HandleUpdateError`**: `HandleUpdateAsync` exception fırlatırsa

İkincisi önemli: Kodumuzdaki bir hata botu **çökertmez**, buraya düşer. Bu yüzden burada düzgün loglama şarttır, yoksa hatalar sessizce kaybolur.

#### Neden `async` yok?

Metot `Task` dönüyor çünkü **interface bunu istiyor**. Ama içinde `await` edilecek bir iş yok, loglama anında bitiyor. Gereksiz `async` → derleyici uyarısı + boşuna bir state machine.

#### Parametreler

- ==`ITelegramBotClient botClient`==: Handler içinde **kullanabilmek için** gelir (örneğin hata olunca kullanıcıya "bir sorun oluştu" mesajı göndermek için). Hatanın hangi bottan geldiğini söylemek için değil.
- ==`Exception exception`==: `System` namespace'indeki, **tüm hataların temel sınıfı**. Hata mesajını (`Message`), stack trace'i ve iç hatayı (`InnerException`) taşır.
- ==`HandleErrorSource source`==: Telegram.Bot'tan gelen enum. Hatanın kaynağı: `PollingError`, `HandleUpdateError`, `FatalError`.
- ==`CancellationToken cancellationToken`==: Kapatma/durdurma sinyali.

#### `_logger.LogError(...)`

- `LogError`, `ILogger` üzerinde tanımlı bir **extension metot**tur. `Microsoft.Extensions.Logging` **namespace**'indedir (class değil).
- Sadece terminale yazmaz, hangi log hedefi ayarlandıysa oraya yazar (konsol, dosya vb.).

```cs
_logger.LogError(exception, "Bot Error Source: {Source}", source);
//               ↑ exception  ↑ şablon (tırnakta $ yok)   ↑ placeholder değeri
```

- **Exception ilk parametre** olmalı. Yoksa stack trace kaybolur.
- **Structured logging:** Şablonda `$` kullanılmaz. `{Source}` placeholder'ı ayrı bir değerle doldurulur. Log sistemleri bu alanı ayrıca aranabilir hale getirir.
- Placeholder adları **PascalCase** yazılır, okunabilirlik için araya ayraç konur: `Source: {Source}`.
- **Mesaj olanı anlatmalı.** "Loglama başarısız" yazılırsa okuyan kişi loglama sisteminin bozulduğunu düşünür. Oysa başarısız olan bot tarafındaki bir işlem.

> [!warning] Yapılan hatalar (sırasıyla)
> 
> 1. `throw new NotImplementedException();` silinmemişti → her hatada log'a yazıp **yeni hata fırlatıyordu**.
> 2. `_logger.LogError($"Exception Hatasi{source}", exception);` → exception **mesaj parametresi** olarak algılandı, stack trace kayboldu.
> 3. `$` interpolation → placeholder hemen string'e gömüldü, structured logging çalışmadı.
> 4. `async` var ama `await` yok → derleyici uyarısı.

#### `return Task.CompletedTask;`

**Zaten tamamlanmış** bir görev döndürür. Metot `Task` dönmek zorunda ama asenkron bir iş yapmadığı için "iş bitti" anlamında hazır `Task` döneriz.

### 6.7 HandleUpdateAsync

#### Ne zaman çağrılır?

`IUpdateHandler`'dan **uygulanan** metottur. Polling, Telegram'dan **yeni bir olay (update)** aldığında çağrılır.

> [!note] "Update" ne demek? Veri güncellemek değil, **Telegram'dan gelen olay** demek. Mesaj, mesaj düzenleme, butona tıklama, botun bir gruba eklenmesi gibi her şey bir update'tir. Bu yüzden içeride önce "bu update mesaj mı, metni var mı?" diye kontrol edilir.

#### `Update update`

Telegram.Bot'tan gelen bir model (veri taşıyıcı) sınıf. Telegram sunucularından bota gelen her türlü olayı taşıyan paket.

#### Guard Clause'lar

İstemediğimiz durumları önce eleyip `return;` ile metottan **erken çıkıyoruz**. Bu desene **guard clause** denir. İç içe `if` yerine kodu düz ve okunur tutar.

> [!note] `return;` burada değer döndürmez `void` veya `async Task` metotlarda `return;` sadece **metottan çıkar**.

**1. if:** `update.Message` null ise gelen olay **mesaj türünde değildir** (mesaj düzenleme, buton tıklaması vb.).

```cs
if (update.Message == null)
{
    return;
}
```

> [!warning] Dikkat Fotoğraf, sticker, animasyon **da bir Message'dır**, sadece `Text`'leri null olur. Onları 2. if yakalar.

**2. if:** Metin yoksa (fotoğraf, sticker vb.), boşsa veya sadece boşluklardan oluşuyorsa çık.

```cs
if (string.IsNullOrWhiteSpace(update.Message.Text))
{
    return;
}
```

> [!warning] Guard'ın görevi sadece elemek Bir ara 2. guard'ın içine `SendMessage` konmuştu. Guard sessizce çıkar, iş yapmaz.

#### `async` / `await`

- `async`: Metodun içinde `await` kullanılabileceğini belirtir.
- `await`: Asenkron işlemin bitmesini bekler.
    - Beklerken **thread'i bloklamaz**, thread başka işlere bakabilir.
    - İşlem hata verirse exception'ı **bizim koda taşır**.
- `HandleErrorAsync`'in aksine burada **gerçekten beklenecek bir iş** (mesaj gönderimi) var, bu yüzden `async`.

> [!danger] `await` unutulursa İşlem arka planda başıboş çalışır ve hata olursa **sessizce kaybolur**. (Önceki projelerde de tekrar eden bir hata.)

#### `SendMessage`

Botun mesaj göndermesini sağlar. `ITelegramBotClient` üzerinde tanımlı bir **extension metot**, Telegram.Bot kütüphanesinin içinde.

**Neden böyle yapılmış?** `ITelegramBotClient`'ın asıl görevi HTTP isteği atmak. Her istek için bunu elle yapmamak adına, isteği bizim yerimize hazırlayan yardımcı (extension) metotlar yazılmış.

> [!warning] Kütüphane sürümü Telegram.Bot son sürümlerde API'sini değiştirdi. İnternetteki eski örneklerde geçen `SendTextMessageAsync` **artık yok**, yerine `SendMessage` var. Her zaman **kurulu sürümün** kendi GitHub örneklerine bak.

**Parametreler:**

|Sıra|Metodun beklediği|Verilen|
|---|---|---|
|1|Mesajın gideceği **chat ID**|==`update.Message.Chat.Id`==|
|2|Gönderilecek **metin**|==`update.Message.Text`==|
|Son|İptal sinyali|==`cancellationToken: cancellationToken`==|

`update.Message.Text` metni "yazdırmaz", **kullanıcının gönderdiği metni verir**. Echo olduğu için aynısını geri gönderiyoruz.

> [!warning] `Chat.Id` ≠ `Message.Id`
> 
> - `Message.Id` → mesajın numarası ("bu sohbetteki 57. mesaj"). Sadece belirli bir mesaja **alıntılayarak cevap** vermek için gerekir.
> - `Chat.Id` → sohbetin kimliği. Mesaj **göndermek** için bu gerekir.

#### Named Argument: `cancellationToken: cancellationToken`

- **Soldaki:** `SendMessage`'ın parametresinin resmi adı
- **Sağdaki:** `HandleUpdateAsync`'in imzasından gelen değişkenin kendisi

`SendMessage`'ın metinden sonra birçok opsiyonel parametresi var (parse mode, butonlar vb.). `cancellationToken` listenin sonunda. Onu **adıyla** veriyoruz ve aradaki parametreleri atlıyoruz.

```cs
// Genel desen
Metot(birinci, ikinci, parametreAdi: deger);
```

#### Neden cancellationToken gönderiyoruz?

`SendMessage` internet üzerinden yapılan, süresi belli olmayan bir işlem. Mesaj gönderilirken uygulama kapatılırsa (Ctrl+C), sunucu yeniden başlatılırsa ya da bot durdurulursa, token iptal sinyalini taşır. İşlem sonuna kadar beklenmez, **güvenli şekilde iptal edilir** (`OperationCanceledException`). Uygulama kapanırken takılı kalmaz.

> [!danger] Token metin değildir `cancellationToken.ToString()` kullanıcıya gönderilecek bir şey değil. Gönderilirse kullanıcıya `System.Threading.CancellationToken` gibi anlamsız bir yazı gider. Token bir **sinyaldir**, kendi özel parametresine verilir.

> [!warning] Placeholder sadece logger'da çalışır `"Basarisiz {cancellationToken}"` gibi bir şablon `SendMessage`'a verilirse olduğu gibi yazılır. `{...}` placeholder'larını sadece logger anlar.

#### HandleUpdateAsync'te yapılan hatalar (toplu)

|Hata|Sonuç|Doğrusu|
|---|---|---|
|`SendMessage` await edilmedi|Hata sessizce kaybolur|`async` + `await`|
|`return Task.FromCanceled(cancellationToken);`|Token iptal edilmemişse **exception fırlatır** → her mesajda hata|`async` metotta bir şey döndürmeye gerek yok|
|En altta `throw new NotImplementedException();`|Mesaj olmayan her update hata fırlatır|Görmezden gel (`return;`)|
|`update.Message.Id`|Yanlış hedef|`update.Message.Chat.Id`|
|Sabit metin gönderildi|Echo olmuyor|`update.Message.Text`|
|`Text` null kontrolü yoktu|Fotoğraf/sticker'da patlar|2. guard|
|`cancellationToken` iletilmedi|İptal sinyali işlemez|Named argument|

---

## 7. Worker

```cs
using FaturaHatirlatici.Bot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace FaturaHatirlatici.Bot;

public class Worker(ILogger<Worker> logger, ITelegramBotClient botClient , IUpdateHandler handler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
       var me = await botClient.GetMe(cancellationToken:stoppingToken);
        logger.LogInformation($"Bot Calisiyo @me{me}");

        ReceiverOptions options = new ()
        {
            AllowedUpdates =Array.Empty<UpdateType>()
        };
        botClient.StartReceiving(updateHandler:handler,receiverOptions:options  , cancellationToken :stoppingToken);

       await Task.Delay(Timeout.Infinite,stoppingToken);
    }
}
```

> [!todo] Bu kod review'da "Request changes" aldı. Açık maddeler aşağıda.

### 7.1 `BackgroundService` ve `ExecuteAsync`

- `BackgroundService`, `IHostedService`'i uygulayan hazır bir temel sınıftır.
- `ExecuteAsync` uygulama başlarken çağrılır ve **uygulama açık olduğu sürece** yaşar.
- `stoppingToken`: Uygulama kapanırken (Ctrl+C) iptal edilen token.
- Şablondaki `while` + `Task.Delay(1000)` döngüsü **demo koddu**, silindi.

### 7.2 Primary Constructor

```cs
public class Worker(ILogger<Worker> logger, ITelegramBotClient botClient, IUpdateHandler handler) : BackgroundService
```

Sınıf adının yanındaki parantez bir **primary constructor**dır (C# 12). Parametreler sınıfın her yerinde doğrudan kullanılabilir.

||Primary constructor|Klasik constructor|
|---|---|---|
|Yazım|Kısa|Uzun (field + ctor)|
|`readonly` garantisi|❌ Parametreler sınıf içinde yeniden atanabilir|✅ `private readonly` field|
|Kullanım|`logger`|`_logger`|

Worker'da **primary constructor** tercih edildi. `UpdateHandler`'da klasik constructor kullanılıyor.

### 7.3 `GetMe`

```cs
var me = await botClient.GetMe(cancellationToken: stoppingToken);
```

Botun kendi bilgisini (`User` nesnesi) getirir. İki işe yarar:

- Başlangıçta bot adını loglamak
- **Token kontrolü:** Token yanlışsa hata burada, en başta çıkar

### 7.4 `ReceiverOptions` ve `AllowedUpdates`

Polling ayarlarıdır. `AllowedUpdates`, Telegram sunucusundan **hangi update türlerinin** isteneceğini belirler.

> [!warning] Boş dizi = hepsi `AllowedUpdates = Array.Empty<UpdateType>()` Telegram API'de **"tüm update türleri"** anlamına gelir. Yani ayar hiçbir şey yapmıyor. Handler sadece mesajları işliyorsa sunucudan sadece onları istemek gerekir. Böylece gereksiz trafik de gelmez.

### 7.5 Polling'i başlatmak: İki yol

||`StartReceiving`|`ReceiveAsync`|
|---|---|---|
|Davranış|Polling'i arka planda başlatır, **hemen döner**|Polling'i başlatır, **token iptal edilene kadar bekler**|
|`ExecuteAsync`'i açık tutmak için|Ek bir bekleme gerekir (`Task.Delay(Timeout.Infinite, stoppingToken)`)|Kendisi bekler, `await` etmek yeter|

Seçilen yol: `StartReceiving` + `Task.Delay(Timeout.Infinite, stoppingToken)`. Çalışır, geçerli bir desen.

> [!question] Açık soru (PR açıklamasına yazılacak) `ReceiveAsync` önerilmişti. Neden `StartReceiving` + sonsuz bekleme seçildi? Mülakatta da "neden böyle yaptın?" diye sorulur.

`Task.Delay(Timeout.Infinite, stoppingToken)`: Sonsuza kadar bekler. Uygulama kapanırken token iptal edilince bekleme biter ve `ExecuteAsync` sonlanır.

### 7.6 Worker review'u: Açık maddeler

- 🔴 **Log satırı:** `$` interpolation yine var (UpdateHandler'da düzeltilen kural). `{me}` bütün `User` nesnesini basıyor ve `@me` düz metin olarak yazılıyor. Loglanması gereken: botun **kullanıcı adı**, structured logging ile (`{BotName}` gibi).
- 🟡 **`AllowedUpdates`:** Boş dizi = hepsi. Sadece ihtiyaç duyulan türü iste.
- ❓ **`StartReceiving` vs `ReceiveAsync`:** Seçimin gerekçesi PR açıklamasına.
- 🟡 **Kullanılmayan using:** `FaturaHatirlatici.Bot.Handlers` → sil.
- 🟡 **Format:** Virgül ve `:` etrafındaki boşluklar tutarsız. Commit'ten önce `Ctrl + K, Ctrl + D`.

---

## 8. Git ve GitHub

### 8.1 Repoyu başlatmak

Solution klasöründe (`.slnx`'in olduğu yer):

```
git init -b main
git remote add origin https://github.com/Yoldas2004/<repo-adı>.git
```

### 8.2 `.gitignore` ve README

```
dotnet new gitignore
Set-Content README.md "# FaturaHatirlatici"
```

- `.gitignore` → `bin/` ve `obj/` (derleme çıktıları) repoya girmemeli.
- `git add` sadece **var olan** dosyaları ekler. `fatal: pathspec 'README.md' did not match any files` = dosya henüz oluşturulmamış.

> [!warning] PowerShell tuzağı `echo "..." > README.md` kullanma. Eski PowerShell dosyayı **UTF-16** kaydeder, GitHub'da bozuk karakterler görünür. `Set-Content` kullan veya editörle oluştur.

> [!note] GitHub'da README/lisans ile repo oluşturulduysa İlk push reddedilir. Önce: `git pull origin main --allow-unrelated-histories`

### 8.3 Doğru ilk akış

1. `main`'e **sadece** `.gitignore` + README → commit → push
2. `git switch -c feature/1-echo-bot`
3. Kod bu branch'te commit'lenir → push
4. PR açılır → PR'da yapılan işin tamamı görünür

### 8.4 Push'tan önce kontroller

```
git status          # eklenecek dosyalara bak
git ls-files        # Git'in takip ettiği dosyalar
git grep "<token'ın ilk birkaç karakteri>"   # sonuç boş dönmeli
git log --oneline --all --graph              # hangi commit hangi branch'te
```

`git ls-files` listesinde olması gerekenler: `Program.cs`, `Worker.cs`, `Handlers/UpdateHandler.cs`, `.csproj`, `.slnx`, `.gitignore`, `README.md`. **Olmaması gerekenler:** `bin/`, `obj/` ile başlayan satırlar.

`appsettings.json` ve `appsettings.Development.json` açılıp içlerinde token olmadığı kontrol edilmeli.

### 8.5 Yaşanan durum: Her şey `main`'e girdi

```
* 72bc1ed (HEAD -> feature/1-echo-bot, origin/main, origin/feature/1-echo-bot, main) chore
```

- Tek bir commit var ve kod da ilk commit'le `main`'e girmiş. Branch aynı commit'i gösteriyor.
- `git status` → `nothing to commit, working tree clean`: Ya her şey commit'lendi ya da yanlış branch'e commit'lendi. Ayırt etmek için `git log --oneline --all --graph`.
- Push çıktısında `Total 0 (delta 0)` → GitHub'a **hiç yeni commit gitmedi**. Branch zaten var olan bir commit'i gösteriyor.
- Bu durumda PR açılırsa GitHub **"There isn't anything to compare"** der.

**Çözüm:** `main` geri alınmıyor. Kalan düzeltmeler (Worker review + `.slnx` yeniden adlandırma) `feature/1-echo-bot`'ta commit'lenir, PR bunları gösterir.

> [!danger] Force push `main`'i geri almak için force push gerekir. Tek kişilik repoda yapılabilir ama **şirkette `main`'e force push yasaktır**. Bu alışkanlık edinilmemeli.

### 8.6 Commit mesajları

Mesaj **ne yapıldığını** anlatmalı. Sadece `chore` yetersiz.

|Önek|Anlamı|Örnek|
|---|---|---|
|`feat:`|Yeni özellik|`feat: add echo bot with update handler`|
|`fix:`|Hata düzeltme|`fix: use structured logging in worker`|
|`chore:`|Kod dışı bakım işi|`chore: initial commit`|
|`refactor:`|Davranışı değiştirmeyen düzenleme|`refactor: rename solution file`|

### 8.7 Yeniden adlandırmalar (açık iş)

- **Solution:** `git mv EchoBot.slnx FaturaHatirlatici.slnx` → VS'i yeni `.slnx` ile aç, projenin yüklendiğini kontrol et → **ayrı commit**.
- **Repo:** `Ech0BotTelegram` (o yerine **sıfır**). GitHub → **Settings → Repository name**. GitHub eski linki otomatik yönlendirir. Yerelde: `git remote set-url origin <yeni-url>`.

---

## 9. Pull Request (PR)

### PR nedir?

"Branch'imde yaptığım değişiklikleri `main`'e almak istiyorum, önce bir bakar mısınız?" isteği.

Şirketlerde kimse doğrudan `main`'e kod yazmaz:

1. Ayrı bir branch'te çalışırsın (`feature/1-echo-bot`)
2. Bitince PR açarsın. PR, `main`'e göre **nelerin değiştiğini** (diff) satır satır gösterir.
3. Senior satırlara yorum yazar (**review**)
4. Düzeltmeleri **aynı branch'e** push edersin, PR otomatik güncellenir
5. Senior **approve** eder, sen **merge** edersin. Kod ancak o zaman `main`'e girer.

**Neden var:** `main` her zaman çalışan, kontrol edilmiş kod olarak kalır. Hatalar birleşmeden önce yakalanır. Her değişikliğin neden yapıldığı PR açıklamasında kayıtlı kalır.

### Nasıl açılır?

Branch push edilince GitHub'da sarı kutuda ==**"Compare & pull request"**== butonu çıkar. Push çıktısı da linki verir:

```
remote: Create a pull request for 'feature/1-echo-bot' on GitHub by visiting:
remote:      https://github.com/Yoldas2004/<repo>/pull/new/feature/1-echo-bot
```

Çıkmazsa: **Pull requests → New pull request** → base: `main`, compare: `feature/1-echo-bot`.

### PR açıklama şablonu

```markdown
## Ne yapıldı
...

## Nasıl test edildi
- [ ] Echo çalışıyor (mesaj atınca aynısı geri geliyor)
- [ ] Token hiçbir dosyada, commit'te ya da log'da görünmüyor
- [ ] Ctrl+C ile uygulama hata fırlatmadan kapanıyor

## Notlar / sorular
...
```

---

## 10. Çalışma Düzeni (Şirket İçi)

### 📋 Ticket'lar

Sadece **hedef + kabul kriterleri** verilir. Adım adım talimat yok. Nasıl yapılacağı araştırılıp çözülür.

### ❓ Soru sorma formatı

> **Ne yapmaya çalışıyorum / Ne denedim / Ne oldu (tam hata mesajı)**

"Nasıl yapılır" sorusunun cevabı "dokümana bak" olabilir. Önce kendin denemen beklenir.

### 🌿 Git akışı

- Her ticket için branch: `feature/<no>-<kisa-ad>`
- Anlamlı commit mesajları
- İş bitince PR, açıklamada **Ne yapıldı / Nasıl test edildi / Notlar**
- PR linki gönderilir, review PR'ın **tamamına tek seferde** yapılır

### 🔁 Review

Yorumlar düzeltilir → aynı PR'a push → **approve** → sen **merge** edersin. Approve olmadan merge yok.

Review işaretleri:

- 🔴 Değişiklik gerekli
- 🟡 Öneri / düzeltme
- ❓ Soru, gerekçe iste

### ✅ Definition of Done

Kabul kriterleri sağlanmış + build uyarısız + token/secret yok + PR approve edilmiş + merge edilmiş.

### Şirketteki gerçek hayatla farkı

- Bu kadar sık kontrol olmaz. Ticket alınır, günlerce kendin uğraşırsın, bitince tek PR açarsın.
- Bu kadar ipucu verilmez. Araştırma ve dokümantasyon okuma senin işin.
- Review asenkrondur. Yorumlar GitHub'da PR'a yazılır, cevap birkaç saat sürebilir.

---

## 11. Ticket #1: Kurulum + Echo Bot

### Görev

Kullanıcının yazdığı mesajı aynen geri gönderen bot.

### Kabul kriterleri

- [x] "merhaba" yazınca bot "merhaba" diye cevap veriyor
- [ ] Token hiçbir dosyada, commit'te ya da log'da görünmüyor → push öncesi kontrol edilecek
- [ ] Ctrl+C ile uygulama hata fırlatmadan kapanıyor → test edilecek

### Cevaplanacak sorular

- [ ] Neden şimdilik webhook yerine polling? (bir cümle)
- [x] Client neden singleton? → Bkz. [[#Neden client Singleton?]]
- [ ] Worker'da neden `StartReceiving` + `Task.Delay` (ya da `ReceiveAsync`)?

### Durum

- [x] Proje kurulumu (Worker Service, .NET, `.slnx`)
- [x] Yanlış proje/solution karışıklığı çözüldü, FaturaHatirlatici.Bot ile devam
- [x] NuGet: `Telegram.Bot`
- [x] User Secrets: `Telegram:BotToken`
- [x] `Program.cs` ✅ kapandı
- [x] `UpdateHandler.cs` ✅ bitti (tek küçük düzeltme: `Source: {Source}`)
- [ ] `Worker.cs` → review düzeltmeleri bekliyor
- [x] Git repo, `.gitignore`, README, `feature/1-echo-bot` branch'i push edildi
- [ ] `EchoBot.slnx` → `FaturaHatirlatici.slnx` (ayrı commit)
- [ ] Repo adı: `Ech0BotTelegram` → düzelt
- [ ] PR aç, review al, merge et
- [ ] Eski (chat'e yapıştırılan) token `/revoke` edildi mi? → kontrol et

### Sıradaki adımlar (sırayla)

1. `feature/1-echo-bot` branch'inde Worker düzeltmeleri → commit
2. `.slnx` yeniden adlandırma → ayrı commit
3. Push → PR aç → linki gönder

---

## 12. Tekrar Eden Hatalar ve Dersler

|Hata|Nerede görüldü|Kural|
|---|---|---|
|`await` unutmak|`SendMessage`|Asenkron çağrı her zaman `await` edilir|
|`$` interpolation ile log|`HandleErrorAsync`, `Worker`|Log'da `$` yok, placeholder + ayrı değer|
|`NotImplementedException` bırakmak|İki handler metodu|Otomatik gelen `throw` satırları silinir|
|`Message.Id` / `Chat.Id` karışıklığı|`SendMessage`|Göndermek için `Chat.Id`|
|Placeholder'ı logger dışında kullanmak|`SendMessage` metni|`{...}` sadece logger'da çalışır|
|Token'ı metin gibi kullanmak|`cancellationToken.ToString()`|Token bir sinyal, named argument ile verilir|
|Yanlış proje / solution'da çalışmak|EchoBot|Solution Explorer başlığına bak|
|Kendi `IUpdateHandler`'ını yazmak|Handler|Kütüphanenin interface'ini kullan|
|Interface'ten `new`|`Program.cs`|Kayıt tipi interface, nesne somut sınıf|
|`GetConnectionString` ile config okumak|`Program.cs`|Normal değer indexer ile okunur|
|Token'ı chat'e yapıştırmak|secrets.json|Sızan token `/revoke` edilir|
|Kodu ilk commit'le `main`'e atmak|Git|`main`'e sadece `.gitignore` + README, kod branch'te|