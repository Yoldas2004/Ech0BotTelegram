# UpdateHandler Notları

## Bileşenlerin Anlamları

### `private`

Bir sınıf **üyesini** (field, metot, property) sadece tanımlandığı sınıf içinde erişilebilir kılar. Diğer sınıflar bu üyeyi göremez.

### `public`

Üyeyi veya sınıfı, ona erişebilen tüm kodlara açar.

### `readonly`

Field'a değer **sadece tanımlandığı satırda veya constructor içinde** atanabilir. Constructor bittikten sonra, uygulama çalışırken yeni bir değer atanamaz.

> [!warning] Dikkat `readonly` **referansı** kilitler, nesnenin içini kilitlemez.
> 
> - `_logger = baskaLogger;` → ❌ yasak
> - `_logger.LogError(...)` → ✅ serbest (nesneyi kullanmak onu değiştirmek değildir)

---

## Logger Field'ı

```cs
private readonly ILogger<UpdateHandler> _logger;
```

### `ILogger<UpdateHandler>`

.NET'in yerleşik loglama arayüzüdür. Generic parametre (`<UpdateHandler>`) logların hangi sınıftan geldiğini belirtir. Bu sayede hatanın veya bilginin hangi sınıfta oluştuğunu görebiliriz.

Logger bir DI örneği **değildir**, DI ile **verilen bir servistir**. Nesneyi biz `new` ile oluşturmayız, DI container constructor üzerinden verir.

### `_logger`

Field'ın adıdır. Alt çizgi (`_`), private field olduğunu belirten yaygın bir isimlendirme kuralıdır.

---

## Constructor

```cs
public UpdateHandler(ILogger<UpdateHandler> logger)
{
    _logger = logger;
}
```

Yukarıdaki yapı bir **yapıcı metot (constructor)**'dur.

- ==`UpdateHandler`==: Constructor'ın adıdır. Constructor her zaman bulunduğu sınıfın adını alır.
- ==`ILogger<UpdateHandler> logger`==: DI container'ın bize verdiği logger. Constructor parametresi olarak gelir.
- ==`_logger = logger;`==: Gelen logger'ı sınıfın field'ına kaydeder, böylece diğer metotlarda kullanabiliriz.

> [!tip] Neden client'ı constructor'dan almıyoruz? `ITelegramBotClient` zaten metotlara parametre olarak geliyor. Constructor'dan da alırsak aynı nesneyi iki yoldan almış oluruz. **Kural:** Metodun ihtiyacı parametreyle geliyorsa onu kullan, gelmiyorsa constructor'dan iste.

---

## HandleErrorAsync

```cs
public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
{
    _logger.LogError(exception, "Bot Error Source: {Source}", source);
    return Task.CompletedTask;
}
```

### Ne zaman çağrılır?

`IUpdateHandler` arayüzünden **uygulanan (implement)** metottur. İki durumda çağrılır:

1. **`PollingError`**: Telegram'a bağlanırken hata olursa (internet kesilmesi, geçersiz token vb.)
2. **`HandleUpdateError`**: `HandleUpdateAsync` exception fırlatırsa

İkinci durum önemli: Kodumuzdaki bir hata botu **çökertmez**, buraya düşer. Bu yüzden burada düzgün loglama şarttır, yoksa hatalar sessizce kaybolur.

### `Task`

Asenkron bir işin durumunu, ilerlemesini ve sonucunu temsil eden nesnedir.

### Neden `async` yok?

Metot `Task` dönüyor çünkü **interface bunu istiyor**. Ama içinde beklenecek (`await` edilecek) bir iş yok, loglama anında bitiyor. Gereksiz `async` eklersek derleyici uyarı verir ve boşuna bir state machine oluşur.

### Parametreler

#### `ITelegramBotClient botClient`

Telegram botunun client'ıdır. Handler içinde **kullanabilmemiz için** gelir (mesela hata olunca kullanıcıya "bir sorun oluştu" mesajı göndermek için).

#### `Exception exception`

`System` namespace'indeki, **tüm hataların temel sınıfıdır**. Hata mesajını (`Message`), stack trace'i ve iç hatayı (`InnerException`) taşır.

#### `HandleErrorSource source`

Telegram.Bot'tan gelen enum'dur. Hatanın kaynağını bildirir: `PollingError`, `HandleUpdateError`, `FatalError`.

#### `CancellationToken cancellationToken`

Dışarıdan gelen kapatma/durdurma sinyalini taşır. İşlemin güvenli şekilde durdurulmasını sağlar.

### `_logger.LogError(...)`

- `_logger` → `ILogger<UpdateHandler>` tipindeki field'ımız
- `LogError` → `ILogger` üzerinde tanımlı bir **extension metot**. `Microsoft.Extensions.Logging` **namespace**'inde bulunur.
- Sadece terminale yazmaz, hangi log hedefi ayarlandıysa oraya yazar (konsol, dosya vb.).

#### Parametre sırası

```cs
_logger.LogError(exception, "Bot Error Source: {Source}", source);
//               ↑ exception  ↑ şablon                    ↑ placeholder değeri
```

- **Exception ilk parametre** olmalı. Yoksa stack trace kaybolur.
- **Structured logging:** Şablonda `$` kullanılmaz. `{Source}` placeholder'ı ayrı bir değerle doldurulur, böylece log sistemleri bu alanı ayrıca aranabilir hale getirir.
- Placeholder adları **PascalCase** yazılır: `{Source}`.

> [!warning] Yanlış `_logger.LogError($"Hata {source}", exception);` `$` placeholder'ı hemen string'e gömer ve exception mesaj parametresi olarak algılanır, stack trace kaybolur.

### `return Task.CompletedTask;`

**Zaten tamamlanmış** bir görev döndürür. Metot `Task` dönmek zorunda ama asenkron bir iş yapmadığı için "iş bitti" anlamında hazır `Task`'ı döneriz.

---

## HandleUpdateAsync

```cs
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
```

### Ne zaman çağrılır?

`IUpdateHandler` arayüzünden **uygulanan (implement)** metottur. Polling, Telegram'dan **yeni bir olay (update)** aldığında çağrılır.

> [!note] "Update" ne demek? Veri güncellemek değil, **Telegram'dan gelen olay** demek. Mesaj, mesaj düzenleme, butona tıklama, botun bir gruba eklenmesi gibi her şey bir update'tir.

### `Update update`

Telegram.Bot'tan gelen bir model (veri taşıyıcı) sınıfıdır. Telegram sunucularından bota gelen her türlü olayı taşıyan pakettir.

### Guard Clause'lar

Önce istemediğimiz durumları eleyip `return;` ile metottan **erken çıkıyoruz**. Bu desene **guard clause** denir. İç içe `if` yerine kodu düz ve okunur tutar.

> [!note] `return;` burada değer döndürmez `return` genelde değer döndürür. Ama `void` veya `async Task` metotlarda `return;` sadece **metottan çıkar**.

#### 1. if

```cs
if (update.Message == null)
{
    return;
}
```

`update.Message` null ise gelen olay **mesaj türünde değildir** (mesaj düzenleme, buton tıklaması, botun gruba eklenmesi vb.). İşlemi sonlandır.

> [!warning] Dikkat Fotoğraf, sticker, animasyon **da bir Message'dır**, sadece `Text`'leri null olur. Onları 2. if yakalar.

#### 2. if

```cs
if (string.IsNullOrWhiteSpace(update.Message.Text))
{
    return;
}
```

Mesajın metni yoksa (fotoğraf, sticker vb.), boşsa veya sadece boşluk karakterlerinden oluşuyorsa işlemi sonlandır.

### `async` / `await`

- `async`: Metodun içinde `await` kullanılabileceğini belirtir.
- `await`: Asenkron işlemin bitmesini bekler. Önemli noktalar:
    - Beklerken **thread'i bloklamaz**, thread başka işlere bakabilir.
    - İşlem hata verirse exception'ı **bizim koda taşır**.

> [!warning] await unutulursa İşlem arka planda başıboş çalışır ve hata olursa **sessizce kaybolur**.

### `SendMessage`

Botun mesaj göndermesini sağlar. `ITelegramBotClient` üzerinde tanımlı bir **extension metot**tur, Telegram.Bot kütüphanesinin içindedir.

**Neden böyle yapılmış?** `ITelegramBotClient` arayüzünün asıl görevi HTTP isteği atmaktır. Her istek için bunu elle yapmamak adına, bizim yerimize isteği hazırlayan yardımcı (extension) metotlar yazılmıştır.

#### Parametreler

|Parametre|Açıklama|
|---|---|
|==`update.Message.Chat.Id`==|Mesajın geldiği **chat'in** ID'si. Mesaj buraya gönderilir.|
|==`update.Message.Text`==|Kullanıcının gönderdiği metni **verir**. Echo olduğu için aynısını geri gönderiyoruz.|
|==`cancellationToken: cancellationToken`==|Named argument ile iptal sinyali|

> [!warning] `Chat.Id` ≠ `Message.Id`
> 
> - `Message.Id` → mesajın numarası ("bu sohbetteki 57. mesaj")
> - `Chat.Id` → sohbetin kimliği Mesaj göndermek için **Chat.Id** gerekir.

#### `cancellationToken: cancellationToken`, Named Argument

- **Soldaki:** `SendMessage` metodunun kabul ettiği parametrenin resmi adı
- **Sağdaki:** `HandleUpdateAsync`'in imzasından bize ulaşan değişkenin kendisi

`SendMessage`'ın metinden sonra birçok opsiyonel parametresi var (parse mode, butonlar vb.). `cancellationToken` listenin sonunda olduğu için onu **adıyla** veriyoruz ve aradaki parametreleri atlıyoruz.

#### Neden cancellationToken gönderiyoruz?

`SendMessage` internet üzerinden yapılan ve süresi belli olmayan asenkron bir işlemdir. Mesaj gönderilirken:

- Uygulama kapatılırsa (Ctrl+C),
- Sunucu yeniden başlatılırsa,
- Botun çalışması durdurulursa,

token iptal sinyalini taşır. İşlem sonuna kadar beklenmez, **güvenli şekilde iptal edilir** (`OperationCanceledException`). Böylece uygulama kapanırken takılı kalmaz.

> [!warning] Token metin değildir `cancellationToken.ToString()` kullanıcıya gönderilecek bir şey değildir. Token bir **sinyaldir**, kendi özel parametresine verilir.