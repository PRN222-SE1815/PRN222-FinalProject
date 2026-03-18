namespace BusinessLogic.Services.Interfaces;

public interface IRealtimeEventDispatcher
{
    Task DispatchToUsersAsync(IReadOnlyCollection<int> userIds, string eventName, object payload, CancellationToken ct = default);
    Task DispatchToAllAsync(string eventName, object payload, CancellationToken ct = default);
}
