using FaturaHatirlatici.Data.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FaturaHatirlatici.Data.Entities
{
    public class Bill
    {
        public int Id { get; set; }
        public int BotUserId { get; set; }
        public BotUser BotUser { get; set; } = null!;
        [MaxLength(100)]
        public string Title { get; set; }= string.Empty ;
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DueDay { get; set; } 
        


    }
}
