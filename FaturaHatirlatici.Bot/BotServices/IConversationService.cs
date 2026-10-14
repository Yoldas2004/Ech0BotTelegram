using FaturaHatirlatici.Bot.Conversations;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Bot.BotServices
{
    public interface IConversationService
    {
        public ConversationState? GetConversation(long telegramUserId);
        public void SaveConversation(long telegramUserId,ConversationState state); 
        public void DeleteConversation(long telegramUserId);
    }
}
