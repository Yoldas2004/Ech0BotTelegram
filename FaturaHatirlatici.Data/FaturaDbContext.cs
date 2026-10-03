using FaturaHatirlatici.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Data
{
    public  class FaturaDbContext :DbContext
    {
        public FaturaDbContext(DbContextOptions<FaturaDbContext> options): base(options)
        {
            
        }
     public DbSet<BotUser> BotUsers { get; set; }
        public DbSet<Bill> Bills { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<BotUser>().HasIndex(x=>x.TelegramUserId).IsUnique();
            modelBuilder.Entity<Bill>().HasOne(x=> x.BotUser).WithMany().HasForeignKey(x=>x.BotUserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Bill>().Property(x => x.Title).HasMaxLength(100);//Bir stringi nvarchara dondurur ve sinirlar
        }
    }
}
