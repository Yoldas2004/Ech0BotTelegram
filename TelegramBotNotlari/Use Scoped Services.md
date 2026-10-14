
# 🧩 DI Lifetime'ları ve Scope

> [!info] Özet **İlgili:** [[FaturaHatirlatici-Proje-Notlari]] · [[Ticket-2-Start-Kaydi]] · [[Ticket-3.3-Conversation-State]] **Tek cümle:** Bir servis, içine aldığı bağımlılıktan **daha uzun yaşayamaz**.

---

## 1. Üç Lifetime

| Lifetime      | Ne zaman oluşturulur?         | Ne kadar yaşar?               | Tipik kullanım                                                           |
| ------------- | ----------------------------- | ----------------------------- | ------------------------------------------------------------------------ |
| **Singleton** | İlk istendiğinde, **bir kez** | Uygulama kapanana kadar       | Client'lar (`TelegramBotClient`), logger, hafızada durum tutan servisler |
| **Scoped**    | Her **scope**'ta bir kez      | Scope kapanana kadar          | `DbContext` ve DbContext kullanan servisler                              |
| **Transient** | **Her istendiğinde** yeni     | İsteyen nesne yaşadığı sürece | Hafif, durumsuz yardımcılar                                              |

```cs
builder.Services.AddSingleton<IArayuz, Sinif>();
builder.Services.AddScoped<IArayuz, Sinif>();
builder.Services.AddTransient<IArayuz, Sinif>();
```

> [!note] `AddDbContext` varsayılan olarak **Scoped** kaydeder.

---

## 2. Scope nedir?

Scope, **bir iş biriminin** sınırıdır. Scope açılır, içinde servisler oluşturulur, iş biter, scope kapanır ve içindeki Scoped servisler **atılır** (dispose).

|Proje tipi|Scope'u kim açar?|
|---|---|
|**Web API**|Framework, **her HTTP isteği** için otomatik açar|
|**Worker Service** (bizim bot)|**Kimse!** HTTP isteği yok, scope'u **sen** açarsın|

> [!important] Restoran projesinde neden hiç scope açmadın? Web API'de her istek otomatik bir scope. Controller'a `IOrderService` inject ettiğinde arka planda zaten bir scope içindeydin. Botta böyle bir şey yok.

---

## 3. Captive Dependency (Esir Bağımlılık)

> [!danger] Kural Singleton bir servisin **constructor'ına** giren her şey, kaydedilen lifetime ne olursa olsun **fiilen Singleton olur**.

**Örnek:**

```
UpdateHandler (Singleton)
 └─ constructor'dan IUserService alırsa (Scoped)
     └─ FaturaDbContext (Scoped)
```

- Handler uygulama boyunca **tek** nesne, dolayısıyla `UserService` ve `DbContext` de tek kalır, hiç ölmez.
- `DbContext` **thread-safe değil**. İki kullanıcı aynı anda yazarsa patlar.
- Development ortamında DI bunu yakalar, uygulama açılırken hata verir: `Cannot consume scoped service '...' from singleton '...'`

### Hangisi hangisini alabilir?

|İçeren ↓ / İçerilen →|Singleton|Scoped|Transient|
|---|---|---|---|
|**Singleton**|✅|❌ captive|⚠️ fiilen singleton olur|
|**Scoped**|✅|✅|✅|
|**Transient**|✅|✅|✅|

> Kısaca: **Uzun yaşayan, kısa yaşayanı constructor'dan alamaz.**

---

## 4. Çözüm: Scope'u Kendin Aç (`IServiceScopeFactory`)

- `IServiceScopeFactory` kendisi **Singleton**, Singleton handler onu constructor'dan **güvenle** alabilir.
- Her iş için (bizde her update) **yeni bir scope** açarsın, servisi oradan istersin, iş bitince scope kapanır.

### Kalıp (farklı bir senaryo: her gece rapor gönderen bir Worker)

```cs
public class GeceRaporuWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;   // Singleton → güvenli

    public GeceRaporuWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Scope aç (using → iş bitince otomatik kapanır)
        using var scope = _scopeFactory.CreateScope();

        // 2. Scoped servisi scope'tan iste
        var raporServisi = scope.ServiceProvider.GetRequiredService<IRaporService>();

        // 3. İşini yap
        await raporServisi.GonderAsync(stoppingToken);

    }   // 4. Scope kapandı → RaporService ve DbContext atıldı
}
```

```
İş geldi
  └─ Singleton (Worker / Handler)
       └─ scope aç  ← using var scope = ...CreateScope()
            └─ Scoped servis
                 └─ DbContext
       └─ scope kapanır → hepsi atılır
```

### Kalıbın 4 parçası

|Parça|Neden|
|---|---|
|`IServiceScopeFactory` constructor'da|Singleton olduğu için captive sorunu yok|
|`using var scope`|`using` olmazsa scope kapanmaz, DbContext'ler birikir (memory leak)|
|`GetRequiredService<T>()`|Servis kayıtlı değilse **hemen ve net** hata verir|
|Her iş için **yeni** scope|Her update kendi DbContext'iyle çalışır, birbirine karışmaz|

### `GetRequiredService` vs `GetService`

||Kayıtlı değilse|
|---|---|
|`GetService<T>()`|`null` döner, hata sonra, belirsiz bir yerde `NullReferenceException` olarak çıkar|
|`GetRequiredService<T>()`|**Hemen** açıklayıcı bir exception: "No service for type ... has been registered"|

> Kural: Servisin **mutlaka** olması gerekiyorsa `GetRequiredService`.

---

## 5. Lifetime Seçerken Sorulacak 3 Soru

```
1. DbContext (ya da Scoped bir şey) kullanıyor mu?
   └─ Evet → Scoped
2. Uygulama boyunca HATIRLAMASI gereken bir verisi var mı?
   └─ Evet → Singleton (ve o veri thread-safe olmalı!)
3. Pahalı bir kaynak mı tutuyor (HttpClient, bağlantı)?
   └─ Evet → Singleton
Hiçbiri → Scoped ya da Transient (genelde Scoped güvenli varsayılan)
```

> [!warning] Singleton + veri = thread safety Singleton servis aynı anda birçok kullanıcıdan çağrılır. İçinde **değişen** bir veri tutuyorsa (sözlük, liste), bu veri thread-safe olmalı: `ConcurrentDictionary`, `lock` vb.

---

## 6. Bu Projede

|Servis|Lifetime|Neden|
|---|---|---|
|`ITelegramBotClient`|Singleton|İçinde `HttpClient` var, tek client yeterli|
|`IUpdateHandler` → `UpdateHandler`|Singleton|Worker (Singleton) onu constructor'dan alıyor, zaten fiilen tek|
|`Worker` (HostedService)|Singleton|Hosted service'ler her zaman Singleton|
|`FaturaDbContext`|Scoped|`AddDbContext` varsayılanı, thread-safe değil|
|`IUserService` → `UserService`|Scoped|DbContext kullanıyor|
|`IBillService` → `BillService`|Scoped|DbContext kullanıyor|
|`IConversationService` → `ConversationService`|**?**|👉 **Kendin doldur** (bölüm 5'teki 3 soruyu sor)|

---

## 7. Tuzaklar

|Tuzak|Sonuç|Doğrusu|
|---|---|---|
|Scoped servisi Singleton'ın constructor'ına almak|Captive dependency, açılışta hata|`IServiceScopeFactory` ile scope aç|
|`CreateScope()`'u `using`'siz kullanmak|Scope kapanmaz, DbContext birikir|`using var scope = ...`|
|`GetService` + null kontrolü unutmak|Belirsiz `NullReferenceException`|`GetRequiredService`|
|Servisi kaydetmeyi unutmak|`No service for type ... registered`|`Program.cs`'e ekle, **kaydet**, build al|
|Hafızada durum tutan servisi Scoped yapmak|Her scope'ta yeni ve **boş** nesne, veri kaybolur|Singleton|
|Singleton'da normal `Dictionary`|Eşzamanlı yazmada bozulma|`ConcurrentDictionary`|
|Kendi iç verisini constructor'dan istemek|DI onu bulamaz, açılışta hata|Alanda `new` ile oluştur|

---

## 8. Mülakat Soruları (AI'sız cevapla)

1. Singleton, Scoped ve Transient arasındaki fark nedir? Her biri için bir örnek ver.
   Singleton =Hafizada Tutar
   Scope = Dbde 
   Transient =Service LifeTime
2. Captive dependency nedir? Nasıl fark edersin, nasıl çözersin?
3. Web API'de neden scope açmana gerek yok da Worker Service'te var?
4. `using var scope` yazmasaydın ne olurdu?
5. `GetService` ile `GetRequiredService` arasındaki fark?
6. Hafızada veri tutan bir servisi Scoped kaydedersen ne olur?