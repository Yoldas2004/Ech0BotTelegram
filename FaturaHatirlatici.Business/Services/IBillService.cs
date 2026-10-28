using FaturaHatirlatici.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Business.Services
{
    public interface IBillService
    {
        public Task<List<Bill>> GetBillsAsync(long telegramUserId);
        public Task<bool> AddBillAsync(long telegramUserId, string title,decimal amount,int day);
        public Task<bool> DeleteBillAsync(long telegramUserId, int billId);
    }
}
