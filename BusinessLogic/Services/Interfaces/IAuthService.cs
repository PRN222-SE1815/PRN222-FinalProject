using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface IAuthService
{
    Task<AuthUserDto?> LoginAsync(string username, string password);
}
