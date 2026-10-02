using FaturaHatirlatici.Data.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Data.Entities
{
    public class Bill
    {
        public int Id { get; set; }
        public int BotUserId { get; set; }
        public BotUser BotUser { get; set; } 
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
         public decimal CurrencyTRY { get; set; }    
        public int DueDay { get; set; } 
        


    }
}
