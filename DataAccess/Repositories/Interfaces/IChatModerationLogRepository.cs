using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IChatModerationLogRepository
{
    Task InsertModerationLogAsync(ChatModerationLog log);
}
