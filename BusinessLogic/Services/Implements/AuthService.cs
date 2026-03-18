using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using DataAccess.Repositories.Interfaces;

namespace BusinessLogic.Services.Implements;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthUserDto?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUsername = username.Trim();

        var user = await _userRepository.GetActiveUserByUsernameAsync(normalizedUsername);

        if (user == null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return new AuthUserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Role = user.Role,
            StudentId = user.Student?.StudentId,
            TeacherId = user.Teacher?.TeacherId
        };
    }
}
