## Exception Notları: ArgumentException vs InvalidOperationException

### Karar Kuralı

- Sorun **çağıranın verdiği değerde** mi? → `ArgumentException` (ve alt sınıfları)
- Sorun **nesnenin ya da sistemin o anki durumunda** mı? → `InvalidOperationException`

---

### ArgumentException

Bir metoda gönderilen parametrelerden biri geçersiz veya hatalı olduğunda fırlatılır.

**Ne zaman kullanılır:** Dışarıdan gelen bir değer (parametre) kurallara uymadığında.

**Alt sınıfları:**

- `ArgumentNullException` → değer null geldiğinde
- `ArgumentOutOfRangeException` → değer izin verilen aralığın dışında olduğunda

**Örnek:**

csharp

```csharp
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

⚠️ **Tuzak: parametre sırası tipe göre değişir**

- `ArgumentException(message, paramName)` → önce mesaj
- `ArgumentNullException(paramName, message)` → önce parametre adı
- `ArgumentOutOfRangeException(paramName, message)` → önce parametre adı

⚠️ **Tuzak:** `nameof` tırnak içine yazılmaz. `"nameof(x)"` yazarsan düz metin olur.

**Modern yöntem (.NET 8+), hazır kontrol metotları:**

csharp

```csharp
ArgumentNullException.ThrowIfNull(user);
ArgumentException.ThrowIfNullOrWhiteSpace(name);
ArgumentOutOfRangeException.ThrowIfNegative(age);
ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 150);
```

---

### InvalidOperationException

Metoda gönderilen parametreler doğru olsa bile, nesnenin o anki durumu yapılan işlemi desteklemediğinde fırlatılır.

**Ne zaman kullanılır:** Nesnenin verileri veya o anki çalışma durumu bu metodun çalışmasına uygun olmadığında.

**Örnek:**

csharp

```csharp
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

💡 Durumu string yerine `enum` ile tut. String'de yazım hatası yaparsan (`"Aproved"`) derleyici uyarmaz, enum'da derlenmez.

---

### Gerçek Kullanım: EchoBot / FaturaHatirlatici

Program başlarken bot token'ı config'de yoksa → `InvalidOperationException`.  
Sebep: Token bir metot parametresi değil. Sorun, uygulamanın başlangıç durumunun (config) eksik olması.

