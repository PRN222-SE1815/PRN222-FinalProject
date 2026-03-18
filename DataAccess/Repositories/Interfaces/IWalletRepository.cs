using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IWalletRepository
{
    Task<StudentWallet?> GetWalletForUpdateAsync(int studentId);
    Task UpdateWalletBalanceAsync(int walletId, decimal delta);
    Task AddWalletTransactionAsync(WalletTransaction entity);
}
