using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Presentation.Hubs;

namespace Presentation.Realtime;

public sealed class SignalRRealtimeEventDispatcher : IRealtimeEventDispatcher
{
    private const int BatchSize = 100;

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRRealtimeEventDispatcher> _logger;

    public SignalRRealtimeEventDispatcher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRRealtimeEventDispatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task DispatchToUsersAsync(
        IReadOnlyCollection<int> userIds,
        string eventName,
        object payload,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            _logger.LogWarning("DispatchToUsersAsync skipped because event name is empty.");
            return;
        }

        var recipients = (userIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("DispatchToUsersAsync skipped because recipients are empty. EventName={EventName}", eventName);
            return;
        }

        foreach (var batch in recipients.Chunk(BatchSize))
        {
            try
            {
                var dispatchTasks = batch
                    .Select(userId => _hubContext.Clients.Group($"user:{userId}").SendAsync(eventName, payload, ct));

                await Task.WhenAll(dispatchTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "DispatchToUsersAsync failed for a batch. EventName={EventName}, BatchSize={BatchSize}",
                    eventName,
                    batch.Length);
            }
        }
    }

    public async Task DispatchToAllAsync(string eventName, object payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            _logger.LogWarning("DispatchToAllAsync skipped because event name is empty.");
            return;
        }

        try
        {
            await _hubContext.Clients.All.SendAsync(eventName, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DispatchToAllAsync failed. EventName={EventName}", eventName);
        }
    }
}
