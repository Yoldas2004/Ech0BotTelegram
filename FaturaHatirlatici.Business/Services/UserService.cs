using FaturaHatirlatici.Data;
using FaturaHatirlatici.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace FaturaHatirlatici.Business.Services
{
    public class UserService:IUserService
    {
        private readonly FaturaDbContext _faturaDbContext;
        public UserService(FaturaDbContext faturaDbContext)
        {
             _faturaDbContext = faturaDbContext;
        }
        public async Task<bool> IsRegisteredAsync(long telegramUserId)
        {
            bool isUseBot = await _faturaDbContext.BotUsers.AnyAsync(x => x.TelegramUserId == telegramUserId);
            if (!isUseBot) 
            {
                return false;
            }
            return true;
        }
        public async Task<bool> RegisterAsync(long chatId, long telegramUserId, string? name, string? userName) 
        {
           bool isThere = await  _faturaDbContext.BotUsers.AnyAsync(x=> x.TelegramUserId == telegramUserId);
            if (isThere)
            {
                return false;
            }
            BotUser user = new BotUser 
            {
                
                ChatId = chatId,
                FirstName = name,
                TelegramUserId = telegramUserId,
                 Username = userName,
                 CreatedAt = DateTime.UtcNow,
                 
            };
            _faturaDbContext.BotUsers.Add(user);
            await _faturaDbContext.SaveChangesAsync();
            return true;

        }
    }
}
