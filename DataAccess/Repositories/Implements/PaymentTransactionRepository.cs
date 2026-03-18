using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<PaymentTransactionRepository> _logger;

    public PaymentTransactionRepository(SchoolManagementDbContext context, ILogger<PaymentTransactionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<PaymentTransaction?> GetByMoMoOrderIdAsync(string orderId)
    {
        return _context.PaymentTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(pt => pt.MoMoOrderId == orderId);
    }

    public Task<PaymentTransaction?> GetByIdAsync(long id)
    {
        return _context.PaymentTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(pt => pt.TransactionId == id);
    }

    public async Task CreateAsync(PaymentTransaction entity)
    {
        try
        {
            _context.PaymentTransactions.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "CreateAsync DB error — MoMoOrderId={MoMoOrderId}, StudentId={StudentId}", entity.MoMoOrderId, entity.StudentId);
            throw;
        }
    }

    public async Task UpdateAsync(PaymentTransaction entity)
    {
        try
        {
            _context.PaymentTransactions.Update(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "UpdateAsync DB error — TransactionId={TransactionId}", entity.TransactionId);
            throw;
        }
    }
}
