using FaturaHatirlatici.Bot.Conversations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Bot.BotServices
{
    public class ConversationService:IConversationService
    {
        private readonly ConcurrentDictionary<long, ConversationState> _conversations = new ConcurrentDictionary<long, ConversationState>();
        

        public ConversationState? GetConversation(long telegramUserId)
        {
            var isThere = _conversations.TryGetValue(telegramUserId, out  var state   );

            if (!isThere)
            {
                return null;
            }
            return state ;
        }
        public void SaveConversation(long telegramUserId, ConversationState conversationState)
        {
         _conversations[telegramUserId] = conversationState;
        }
        public void DeleteConversation(long telegramUserId) 
        {
            _conversations.TryRemove(telegramUserId, out var state);
        }

    }
}
