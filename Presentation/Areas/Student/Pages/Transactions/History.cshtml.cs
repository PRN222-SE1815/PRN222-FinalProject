using System.Security.Claims;
using BusinessLogic.DTOs.Requests.Transactions;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.DTOs.Responses.Transactions;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.Transactions;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class HistoryModel : PageModel
{
    private readonly ITransactionHistoryService _transactionHistoryService;
    private readonly ILogger<HistoryModel> _logger;

    public HistoryModel(ITransactionHistoryService transactionHistoryService, ILogger<HistoryModel> logger)
    {
        _transactionHistoryService = transactionHistoryService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SourceType { get; set; } = "ALL";

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public PagedResult<TransactionHistoryItemDto> Result { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public int TotalPages => Result.PageSize > 0
        ? (int)Math.Ceiling((double)Result.TotalCount / Result.PageSize)
        : 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            return RedirectToPage("/Account/Login");
        }

        try
        {
            var request = new TransactionHistoryQueryRequest
            {
                FromDateUtc = FromDate.HasValue
                    ? DateTime.SpecifyKind(FromDate.Value.Date, DateTimeKind.Local).ToUniversalTime()
                    : null,
                ToDateUtc = ToDate.HasValue
                    ? DateTime.SpecifyKind(ToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime()
                    : null,
                SourceType = SourceType,
                Status = Status,
                Keyword = Keyword,
                Page = PageNumber,
                PageSize = PageSize
            };

            var serviceResult = await _transactionHistoryService.GetStudentHistoryAsync(userId, request, ct);
            if (serviceResult.IsSuccess && serviceResult.Data is not null)
            {
                Result = serviceResult.Data;
                PageNumber = Result.Page;
                PageSize = Result.PageSize;
            }
            else
            {
                ErrorMessage = serviceResult.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading transaction history for UserId={UserId}", userId);
            ErrorMessage = "Đã xảy ra lỗi khi tải lịch sử giao dịch.";
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
