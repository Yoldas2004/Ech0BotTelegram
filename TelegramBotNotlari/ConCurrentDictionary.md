`ConcurrentDictionary<TKey, TValue>`, C# thread-safe (eşzamanlı) sözlük veri yapısıdır. Çoklu iş parçacıklarının (threads) aynı anda veri okuyup yazabileceği senaryolarda standart `Dictionary` yerine tercih edilir.

Farklı işlemler için kullanılan **TryGetValue**, **TryRemove** ve **Indexer (köşeli parantez `[]`)** metotlarının kullanım senaryoları ve özellikleri:

### 1. `TryGetValue(TKey key, out TValue value)`

Bir anahtarın sözlükte bulunup bulunmadığını kontrol eder ve varsa değerini döndürür. Thread-safe bir okuma işlemidir.

- **Neden `ContainsKey` + Indexer yerine kullanılır?**
    
    Standart sözlükte `if (dict.ContainsKey(key)) { var val = dict[key]; }` yazılabilir. Ancak çoklu iş parçacığı ortamında, `ContainsKey` kontrolü yapıldıktan hemen sonra başka bir thread o anahtarı silebilir. `TryGetValue` bu iki adımı tek ve güvenli (atomic) bir işlem olarak gerçekleştirir.
    
- **Geri Dönüş Değeri:** Anahtar bulunursa `true`, bulunamazsa `false` döner.
    

C#

```
using System;
using System.Collections.Concurrent;

var dict = new ConcurrentDictionary<string, int>();
dict["elma"] = 5;

// Güvenli Okuma
if (dict.TryGetValue("elma", out int miktar))
{
    Console.WriteLine($"Elma miktarı: {miktar}");
}
else
{
    Console.WriteLine("Elma anahtarı bulunamadı.");
}
```

### 2. `TryRemove(TKey key, out TValue value)`

Bir anahtarı ve ona karşılık gelen değeri sözlükten güvenli bir şekilde siler ve silinen değeri `out` parametresi ile verir.

- **Özellik:** Silme işlemi atomiktir. Başka bir thread aynı anahtarı silmeye veya değiştirmeye çalışsa bile çakışma yaşanmaz.
    
- **Geri Dönüş Değeri:** Anahtar bulunup başarıyla silindiyse `true`, anahtar sözlükte yoksa `false` döner.
    

C#

```
// Güvenli Silme ve Değeri Alma
if (dict.TryRemove("elma", out int silinenMiktar))
{
    Console.WriteLine($"'elma' başarıyla silindi. Silinen değer: {silinenMiktar}");
}
else
{
    Console.WriteLine("Silinecek anahtar bulunamadı.");
}
```

### 3. Indexer ile Yazma (`dict[key] = value`)

Sözlüğe köşeli parantez erişimi ile değer atamak için kullanılır.

- **Davranış:** Klasik sözlük ile aynı şekilde çalışır.
    
    - Eğer `key` sözlükte **yoksa**, yeni bir eleman olarak **ekler**.
        
    - Eğer `key` sözlükte **zaten varsa**, var olan değeri **üzerine yazar (overwrite)**.
        
- **Thread-Safety:** Indexer atama işlemi thread-safe'dir, ancak **koşullu eklemeler** için her zaman ideal değildir.
    

C#

```
// Ekleme veya Üzerine Yazma
dict["muz"] = 10; // "muz" yoksa ekler, varsa değerini 10 yapar.
dict["muz"] = 15; // "muz" zaten var olduğu için değerini 15 olarak günceller.
```

> **Önemli Not:** Eğer amacınız bir değeri _sadece sözlükte yoksa eklemek_ veya _mevcut değere göre güncellemek_ ise indexer yerine `GetOrAdd` veya `AddOrUpdate` metotlarını kullanmak daha güvenlidir.

### Özet Karşılaştırma Tablosu

|**Metot / İşlem**|**İşlevi**|**Anahtar Var İse**|**Anahtar Yok İse**|
|---|---|---|---|
|**`TryGetValue`**|Güvenli Okuma|`true` döner, `out` ile değeri verir|`false` döner|
|**`TryRemove`**|Güvenli Silme|Elemanı siler, `true` döner ve değeri verir|`false` döner|
|**`dict[key] = val`**|Doğrudan Yazma|Değerin üzerine yazar|Yeni eleman ekler|