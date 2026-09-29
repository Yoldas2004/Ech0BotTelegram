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
    }
}
