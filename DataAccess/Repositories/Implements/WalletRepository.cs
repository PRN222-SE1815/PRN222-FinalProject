using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class WalletRepository : IWalletRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<WalletRepository> _logger;

    public WalletRepository(SchoolManagementDbContext context, ILogger<WalletRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<StudentWallet?> GetWalletForUpdateAsync(int studentId)
    {
        // TODO: lock at service via transaction isolation.
        return _context.StudentWallets
            .AsTracking()
            .SingleOrDefaultAsync(w => w.StudentId == studentId);
    }

    public async Task UpdateWalletBalanceAsync(int walletId, decimal delta)
    {
        try
        {
            var wallet = await _context.StudentWallets
                .SingleOrDefaultAsync(w => w.WalletId == walletId);

            if (wallet == null)
            {
                _logger.LogWarning("UpdateWalletBalanceAsync wallet not found — WalletId={WalletId}", walletId);
                return;
            }

            wallet.Balance += delta;
            wallet.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "UpdateWalletBalanceAsync DB error — WalletId={WalletId}", walletId);
            throw;
        }
    }

    public async Task AddWalletTransactionAsync(WalletTransaction entity)
    {
        try
        {
            _context.WalletTransactions.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "AddWalletTransactionAsync DB error — WalletId={WalletId}", entity.WalletId);
            throw;
        }
    }
}
