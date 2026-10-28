using FaturaHatirlatici.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FaturaHatirlatici.Test
{
    public class BillServiceTest
    {
        [Fact]
        public void Test1()
        {
            int x1 = 2;
            int x2 = 2;

            int res = x1 + x2;
            Assert.Equal(4, res);

        }
        private static FaturaDbContext CreateContext()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            //diskte degil ramde veritabani aciyoruz ve isimiz bitince siliyoruz
            connection.Open();//baglantimizi aciyoruz kapandigi anda silinir

             var options = new DbContextOptionsBuilder<FaturaDbContext>().UseSqlite(connection).Options;
            //veritabanı bağlantı ayarlarını ve konfigürasyonunu oluşturup bunu
            //options adlı bir değişkene aktarmaya yarar.
            var faturaDbContext =new FaturaDbContext(options);
            //veritabanıyla konuşacak olan aracı nesneyi (bağlantıyı) hazır hale getirir.
            faturaDbContext.Database.EnsureCreated();
            /*EnsureCreated(): Model’e (BotUser, Bill ve aralarındaki ilişkiye) bakarak veritabanında tabloları oluşturur.
             * Veritabanı zaten varsa hiçbir şey yapmaz. Migration kullanmaz
             * , o yüzden sadece test ve deneme için uygundur.*/
            return faturaDbContext;
        }
    }
}
