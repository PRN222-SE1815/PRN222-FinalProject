using System.Security.Claims;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Chat;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;
using BusinessLogic.DTOs.Requests.Chat;

namespace Presentation.Pages.Chat;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly ILogger<IndexModel> _logger;
    private readonly IWebHostEnvironment _env;

    public IndexModel(IChatService chatService, ILogger<IndexModel> logger, IWebHostEnvironment env)
    {
        _chatService = chatService;
        _logger = logger;
        _env = env;
    }

    /// <summary>All chat rooms the current user belongs to.</summary>
    public List<ChatRoomDto> Rooms { get; set; } = new();

    /// <summary>Messages for the currently selected room (server-rendered initial load).</summary>
    public PagedResult<ChatMessageDto> Messages { get; set; } = new()
    {
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
        Items = Array.Empty<ChatMessageDto>()
    };

    /// <summary>The currently selected room (if any).</summary>
    public ChatRoomDto? SelectedRoom { get; set; }

    /// <summary>Current user's ID from claims.</summary>
    public int CurrentUserId { get; set; }

    public async Task<IActionResult> OnGetAsync(int? roomId)
    {
        CurrentUserId = GetUserId();

        if (CurrentUserId == 0)
            return RedirectToPage("/Account/Login");

        Rooms = await _chatService.GetMyRoomsAsync(CurrentUserId);

        if (roomId.HasValue)
        {
            SelectedRoom = await _chatService.GetRoomAsync(roomId.Value, CurrentUserId);
            if (SelectedRoom is not null)
            {
                Messages = await _chatService.GetRoomMessagesAsync(roomId.Value, CurrentUserId, null);
            }
        }
        else if (Rooms.Count > 0)
        {
            // Auto-select the first room
            SelectedRoom = Rooms[0];
            Messages = await _chatService.GetRoomMessagesAsync(SelectedRoom.RoomId, CurrentUserId, null);
        }

        return Page();
    }

    /// <summary>AJAX endpoint: get messages for a room (cursor-based paging).</summary>
    public async Task<IActionResult> OnGetMessagesAsync(int roomId, long? beforeMessageId)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var messages = await _chatService.GetRoomMessagesAsync(roomId, userId, beforeMessageId);
        return new JsonResult(messages);
    }

    /// <summary>AJAX endpoint: search users for new DM / group creation.</summary>
    public async Task<IActionResult> OnGetSearchUsersAsync(string? search)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var users = await _chatService.GetAvailableUsersForChatAsync(userId, search);
        return new JsonResult(users);
    }

    /// <summary>AJAX endpoint: upload a file attachment and return its URL.</summary>
    public async Task<IActionResult> OnPostUploadFileAsync(IFormFile? file)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        // 20 MB limit
        const long maxSize = 20 * 1024 * 1024;
        if (file.Length > maxSize)
            return BadRequest(new { error = "File exceeds the 20 MB limit." });

        // Allowed extensions
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".zip", ".mp4", ".mp3" };
        if (!allowed.Contains(ext))
            return BadRequest(new { error = $"File type '{ext}' is not allowed." });

        // Save to wwwroot/uploads/chat/
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "chat");
        Directory.CreateDirectory(uploadsDir);

        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var fileUrl = $"/uploads/chat/{uniqueName}";
        var fileType = ext.TrimStart('.');

        _logger.LogInformation("User {UserId} uploaded chat file {FileName} -> {FileUrl}", userId, file.FileName, fileUrl);

        return new JsonResult(new
        {
            fileUrl,
            fileType,
            fileSizeBytes = file.Length,
            originalName = file.FileName
        });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
