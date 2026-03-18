using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IUserService
{
    Task<CreateUserResultDto> CreateUserAsync(CreateUserRequestDto dto);
    Task<CreateUserResultDto> CreateStudentAsync(CreateStudentDto dto);
    Task<CreateUserResultDto> CreateTeacherAsync(CreateTeacherDto dto);
    Task<CreateUserResultDto> CreateAdminAsync(CreateUserDto dto);
    Task<UserDetailDto?> GetUserByIdAsync(int id);
    Task<PagedResultDto<UserListItemDto>> GetUsersAsync(UserFilterDto filter);
    Task UpdateUserAsync(int id, UpdateUserDto dto);
    Task ToggleUserStatusAsync(int id);
}
