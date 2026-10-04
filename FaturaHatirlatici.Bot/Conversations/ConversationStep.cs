using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Bot.Conversations
{
    public enum  ConversationStep
    {
        AwaitingName=1,
        AwaitingAmount=2,
        AwaitingDueDay=3,

    }
}
