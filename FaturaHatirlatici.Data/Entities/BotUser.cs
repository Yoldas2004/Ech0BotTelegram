using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Data.Entities
{
    public  class BotUser
    {
        public int Id  { get; set; }
        public long TelegramUserId { get; set; }
        public long ChatId { get; set; }
        public string? FirstName { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    
    }
}
