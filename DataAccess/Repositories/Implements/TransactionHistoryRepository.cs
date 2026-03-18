using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class TransactionHistoryRepository : ITransactionHistoryRepository
{
    private readonly SchoolManagementDbContext _context;

    public TransactionHistoryRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<int?> GetStudentIdByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return _context.Students
            .AsNoTracking()
            .Where(x => x.StudentId == userId)
            .Select(x => (int?)x.StudentId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<WalletTransactionHistoryRow>> GetWalletTransactionsAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? keyword,
        CancellationToken ct = default)
    {
        var query = _context.WalletTransactions
            .AsNoTracking()
            .Where(x => x.Wallet.StudentId == studentId);

        if (fromDateUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDateUtc.Value);
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDateUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Description != null && x.Description.Contains(keyword));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.WalletTransId)
            .Select(x => new WalletTransactionHistoryRow
            {
                WalletTransId = x.WalletTransId,
                CreatedAt = x.CreatedAt,
                Amount = x.Amount,
                TransactionType = x.TransactionType,
                BalanceBefore = x.BalanceBefore,
                BalanceAfter = x.BalanceAfter,
                Description = x.Description
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentTransactionHistoryRow>> GetPaymentTransactionsAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? status,
        string? keyword,
        CancellationToken ct = default)
    {
        var query = _context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == studentId);

        if (fromDateUtc.HasValue)
        {
            query = query.Where(x => (x.PaymentDate ?? x.CreatedAt) >= fromDateUtc.Value);
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(x => (x.PaymentDate ?? x.CreatedAt) <= toDateUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.MoMoOrderId.Contains(keyword)
                || (x.OrderInfo != null && x.OrderInfo.Contains(keyword))
                || (x.LocalMessage != null && x.LocalMessage.Contains(keyword)));
        }

        return await query
            .OrderByDescending(x => x.PaymentDate ?? x.CreatedAt)
            .ThenByDescending(x => x.TransactionId)
            .Select(x => new PaymentTransactionHistoryRow
            {
                TransactionId = x.TransactionId,
                CreatedAt = x.CreatedAt,
                PaymentDate = x.PaymentDate,
                PaymentMethod = x.PaymentMethod,
                MoMoOrderId = x.MoMoOrderId,
                Amount = x.Amount,
                Status = x.Status,
                LocalMessage = x.LocalMessage,
                OrderInfo = x.OrderInfo
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RegistrationOrderHistoryRow>> GetRegistrationOrderHistoryAsync(
        int studentId,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? status,
        string? keyword,
        CancellationToken ct = default)
    {
        var query = _context.RegistrationOrders
            .AsNoTracking()
            .Where(x => x.StudentId == studentId);

        if (fromDateUtc.HasValue)
        {
            query = query.Where(x => (x.PaidAt ?? x.CreatedAt) >= fromDateUtc.Value);
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(x => (x.PaidAt ?? x.CreatedAt) <= toDateUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.OrderStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.OrderCode.Contains(keyword)
                || (x.FailureReason != null && x.FailureReason.Contains(keyword)));
        }

        return await query
            .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
            .ThenByDescending(x => x.OrderId)
            .Select(x => new RegistrationOrderHistoryRow
            {
                OrderId = x.OrderId,
                CreatedAt = x.CreatedAt,
                PaidAt = x.PaidAt,
                OrderCode = x.OrderCode,
                OrderStatus = x.OrderStatus,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                FailureReason = x.FailureReason
            })
            .ToListAsync(ct);
    }
}
