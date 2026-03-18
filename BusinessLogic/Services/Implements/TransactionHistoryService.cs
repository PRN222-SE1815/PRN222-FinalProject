using BusinessLogic.Constants;
using BusinessLogic.DTOs.Requests.Transactions;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Transactions;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class TransactionHistoryService : ITransactionHistoryService
{
    private const string SourceAll = "ALL";
    private const string SourceWallet = "WALLET";
    private const string SourcePayment = "PAYMENT";
    private const string SourceOrder = "ORDER";

    private readonly IUserRepository _userRepository;
    private readonly ITransactionHistoryRepository _transactionHistoryRepository;
    private readonly ILogger<TransactionHistoryService> _logger;

    public TransactionHistoryService(
        IUserRepository userRepository,
        ITransactionHistoryRepository transactionHistoryRepository,
        ILogger<TransactionHistoryService> logger)
    {
        _userRepository = userRepository;
        _transactionHistoryRepository = transactionHistoryRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<PagedResult<TransactionHistoryItemDto>>> GetStudentHistoryAsync(
        int userId,
        TransactionHistoryQueryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (request is null)
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.InvalidInput, "Yêu cầu không hợp lệ.");
            }

            if (request.FromDateUtc.HasValue && request.ToDateUtc.HasValue && request.FromDateUtc > request.ToDateUtc)
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.InvalidInput, "Khoảng thời gian không hợp lệ.");
            }

            var sourceType = string.IsNullOrWhiteSpace(request.SourceType)
                ? SourceAll
                : request.SourceType.Trim().ToUpperInvariant();

            if (sourceType is not (SourceAll or SourceWallet or SourcePayment or SourceOrder))
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.InvalidInput, "Nguồn giao dịch không hợp lệ.");
            }

            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
            var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user is null || !user.IsActive)
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.Forbidden, "Không thể xác thực người dùng.");
            }

            if (!string.Equals(user.Role, nameof(UserRole.STUDENT), StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.Forbidden, "Chỉ sinh viên được phép xem lịch sử giao dịch.");
            }

            var studentId = await _transactionHistoryRepository.GetStudentIdByUserIdAsync(userId, ct);
            if (!studentId.HasValue)
            {
                return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.NotFound, "Không tìm thấy thông tin sinh viên.");
            }

            var timelineItems = new List<TransactionHistoryItemDto>();

            if (sourceType is SourceAll or SourceWallet)
            {
                var walletRows = await _transactionHistoryRepository.GetWalletTransactionsAsync(
                    studentId.Value,
                    request.FromDateUtc,
                    request.ToDateUtc,
                    keyword,
                    ct);

                timelineItems.AddRange(walletRows.Select(x => new TransactionHistoryItemDto
                {
                    OccurredAt = x.CreatedAt,
                    SourceType = SourceWallet,
                    ReferenceCode = x.WalletTransId.ToString(),
                    TransactionType = x.TransactionType,
                    Status = null,
                    Amount = x.Amount,
                    BalanceBefore = x.BalanceBefore,
                    BalanceAfter = x.BalanceAfter,
                    Description = x.Description,
                    ReferenceId = x.WalletTransId
                }));
            }

            if (sourceType is SourceAll or SourcePayment)
            {
                var paymentRows = await _transactionHistoryRepository.GetPaymentTransactionsAsync(
                    studentId.Value,
                    request.FromDateUtc,
                    request.ToDateUtc,
                    status,
                    keyword,
                    ct);

                timelineItems.AddRange(paymentRows.Select(x => new TransactionHistoryItemDto
                {
                    OccurredAt = x.PaymentDate ?? x.CreatedAt,
                    SourceType = SourcePayment,
                    ReferenceCode = x.MoMoOrderId,
                    TransactionType = x.PaymentMethod,
                    Status = x.Status,
                    Amount = x.Amount,
                    BalanceBefore = null,
                    BalanceAfter = null,
                    Description = string.IsNullOrWhiteSpace(x.LocalMessage) ? x.OrderInfo : x.LocalMessage,
                    ReferenceId = x.TransactionId
                }));
            }

            if (sourceType is SourceAll or SourceOrder)
            {
                var orderRows = await _transactionHistoryRepository.GetRegistrationOrderHistoryAsync(
                    studentId.Value,
                    request.FromDateUtc,
                    request.ToDateUtc,
                    status,
                    keyword,
                    ct);

                timelineItems.AddRange(orderRows.Select(x => new TransactionHistoryItemDto
                {
                    OccurredAt = x.PaidAt ?? x.CreatedAt,
                    SourceType = SourceOrder,
                    ReferenceCode = x.OrderCode,
                    TransactionType = null,
                    Status = x.OrderStatus,
                    Amount = x.PaidAmount > 0m ? x.PaidAmount : x.TotalAmount,
                    BalanceBefore = null,
                    BalanceAfter = null,
                    Description = x.FailureReason,
                    ReferenceId = x.OrderId
                }));
            }

            var sortedItems = timelineItems
                .OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.ReferenceId ?? long.MinValue)
                .ToList();

            var totalCount = sortedItems.Count;
            var pagedItems = sortedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Success(new PagedResult<TransactionHistoryItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = pagedItems
            });
        }
        catch (OperationCanceledException)
        {
            return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.SystemError, "Yêu cầu đã bị hủy.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStudentHistoryAsync failed. UserId={UserId}", userId);
            return ServiceResult<PagedResult<TransactionHistoryItemDto>>.Fail(ErrorCodes.SystemError, "Đã xảy ra lỗi hệ thống.");
        }
    }
}
