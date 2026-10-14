using FaturaHatirlatici.Data;
using FaturaHatirlatici.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaturaHatirlatici.Business.Services
{
    public class BillService:IBillService
    {
        private readonly FaturaDbContext _context;
        public BillService(FaturaDbContext context)
        {
            _context = context;
        }
        public async Task<bool> AddBillAsync(long telegramUserId, string title, decimal amount, int day)
        {
            var findUser = await _context.BotUsers.FirstOrDefaultAsync(x=>x.TelegramUserId == telegramUserId);
            if (findUser == null)
            {
                return false;
            }
            var billibilli = new Bill
            { 
                BotUserId = findUser.Id,
                Amount = amount,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                DueDay = day,

            };
            _context.Bills.Add(billibilli);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
