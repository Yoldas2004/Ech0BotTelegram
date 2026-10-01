using FaturaHatirlatici.Data.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Data.Entities
{
    public class Bill
    {
        public int Id { get; set; }
        public long TelegramUserId { get; set; }
        public string? Title { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Currency Currency { get; set; } =  Currency.TRY;
        public DateTime DueTime { get; set; } 
        public bool IsPaid { get; set; }
        public ReminderType ReminderType { get; set; }
        public DateTime LastReminderAt { get; set; }


    }
}
