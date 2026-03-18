using System.Security.Claims;
using BusinessLogic.DTOs.Requests.AI;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.AI;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class IndexModel : PageModel
{
    private readonly IAIChatService _aiChatService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IAIChatService aiChatService, ILogger<IndexModel> logger)
    {
        _aiChatService = aiChatService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Purpose { get; set; } = "SCORE_SUMMARY";

    public long? ChatSessionId { get; set; }
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostStartSessionAsync(
        [FromBody] StartChatSessionRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _aiChatService.StartSessionAsync(
            userId, nameof(UserRole.STUDENT), request, ct);

        if (result.IsSuccess)
        {
            return new JsonResult(new { isSuccess = true, data = result.Data });
        }

        return BadRequest(new { isSuccess = false, errorCode = result.ErrorCode, message = result.Message });
    }

    public async Task<IActionResult> OnPostSendMessageAsync(
        [FromBody] SendChatMessageRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (request is null || request.ChatSessionId <= 0 || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { isSuccess = false, errorCode = "INVALID_INPUT", message = "Invalid request." });
        }

        var result = await _aiChatService.SendMessageAsync(
            userId, nameof(UserRole.STUDENT), request, ct);

        if (result.IsSuccess)
        {
            return new JsonResult(new { isSuccess = true, data = result.Data });
        }

        if (string.Equals(result.ErrorCode, "RATE_LIMITED", StringComparison.Ordinal))
        {
            return new JsonResult(new { isSuccess = false, errorCode = result.ErrorCode, message = result.Message })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
        }

        return BadRequest(new { isSuccess = false, errorCode = result.ErrorCode, message = result.Message });
    }

    public async Task<IActionResult> OnGetHistoryAsync(
        long chatSessionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var result = await _aiChatService.GetSessionHistoryAsync(
            userId, nameof(UserRole.STUDENT), chatSessionId, page, pageSize, ct);

        if (result.IsSuccess)
        {
            return new JsonResult(new { isSuccess = true, data = result.Data });
        }

        return BadRequest(new { isSuccess = false, errorCode = result.ErrorCode, message = result.Message });
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
