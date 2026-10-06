using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Bot.Conversations
{
    public  class ConversationState
    {
        public ConversationStep ConversationStep { get; set; } =ConversationStep.AwaitingName;
        public string? Name { get; set; }
        public decimal? Amount { get; set; }
    }
}
