using DataAccess.Repositories.Models;

namespace DataAccess.Repositories.Interfaces;

public interface ITransactionHistoryRepository
{
    Task<int?> GetStudentIdByUserIdAsync(int userId, CancellationToken ct = default);

    Task<IReadOnlyList<WalletTransactionHistoryRow>> GetWalletTransactionsAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? keyword,
        CancellationToken ct = default);

    Task<IReadOnlyList<PaymentTransactionHistoryRow>> GetPaymentTransactionsAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? status,
        string? keyword,
        CancellationToken ct = default);

    Task<IReadOnlyList<RegistrationOrderHistoryRow>> GetRegistrationOrderHistoryAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? status,
        string? keyword,
        CancellationToken ct = default);
}
