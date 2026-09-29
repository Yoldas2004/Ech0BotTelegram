using FaturaHatirlatici.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Business.Services
{
    public interface IUserService
    {
        Task<bool> RegisterAsync(long chatId ,long telegramUserId, string? name , string? userName);

    }
}
