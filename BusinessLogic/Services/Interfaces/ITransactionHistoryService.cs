using BusinessLogic.DTOs.Requests.Transactions;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Transactions;

namespace BusinessLogic.Services.Interfaces;

public interface ITransactionHistoryService
{
    Task<ServiceResult<PagedResult<TransactionHistoryItemDto>>> GetStudentHistoryAsync(
        int userId,
        TransactionHistoryQueryRequest request,
        CancellationToken ct = default);
}
