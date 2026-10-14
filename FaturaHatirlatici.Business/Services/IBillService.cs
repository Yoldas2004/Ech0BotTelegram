using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Business.Services
{
    public interface IBillService
    {
        public Task<bool> AddBillAsync(long telegramUserId, string title,decimal amount,int day);
    }
}
