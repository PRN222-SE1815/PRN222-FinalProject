using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IAuthService
{
    Task<AuthUserDto?> LoginAsync(string username, string password);
}
