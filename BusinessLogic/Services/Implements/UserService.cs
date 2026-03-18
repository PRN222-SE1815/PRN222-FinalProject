using System.Security.Cryptography;
using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;

namespace BusinessLogic.Services.Implements;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<CreateUserResultDto> CreateStudentAsync(CreateStudentDto dto)
    {
        var student = new Student
        {
            StudentCode = dto.StudentCode.Trim(),
            ProgramId = dto.ProgramId,
            CurrentSemesterId = dto.CurrentSemesterId
        };

        return CreateUserAsync(dto, UserRole.STUDENT, student, null);
    }

    public Task<CreateUserResultDto> CreateTeacherAsync(CreateTeacherDto dto)
    {
        var teacher = new Teacher
        {
            TeacherCode = dto.TeacherCode.Trim(),
            Department = string.IsNullOrWhiteSpace(dto.Department) ? null : dto.Department.Trim()
        };

        return CreateUserAsync(dto, UserRole.TEACHER, null, teacher);
    }

    public Task<CreateUserResultDto> CreateAdminAsync(CreateUserDto dto)
    {
        return CreateUserAsync(dto, UserRole.ADMIN, null, null);
    }

    public async Task<CreateUserResultDto> CreateUserAsync(CreateUserRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Role))
        {
            throw new ArgumentException("Role is required.", nameof(dto));
        }

        if (!Enum.TryParse<UserRole>(dto.Role.Trim(), true, out var role))
        {
            throw new ArgumentException("Invalid role.", nameof(dto));
        }

        Student? student = null;
        Teacher? teacher = null;

        if (role == UserRole.STUDENT)
        {
            if (string.IsNullOrWhiteSpace(dto.StudentCode))
            {
                throw new ArgumentException("Student code is required.", nameof(dto));
            }

            student = new Student
            {
                StudentCode = dto.StudentCode.Trim(),
                ProgramId = dto.ProgramId,
                CurrentSemesterId = dto.CurrentSemesterId
            };
        }
        else if (role == UserRole.TEACHER)
        {
            if (string.IsNullOrWhiteSpace(dto.TeacherCode))
            {
                throw new ArgumentException("Teacher code is required.", nameof(dto));
            }

            teacher = new Teacher
            {
                TeacherCode = dto.TeacherCode.Trim(),
                Department = string.IsNullOrWhiteSpace(dto.Department) ? null : dto.Department.Trim()
            };
        }

        var baseDto = new CreateUserDto
        {
            Username = dto.Username,
            FullName = dto.FullName,
            Email = dto.Email,
            Password = dto.Password
        };

        return await CreateUserAsync(baseDto, role, student, teacher);
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetUserWithDetailsAsync(id);
        if (user == null)
        {
            return null;
        }

        return new UserDetailDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            StudentCode = user.Student?.StudentCode,
            ProgramId = user.Student?.ProgramId,
            CurrentSemesterId = user.Student?.CurrentSemesterId,
            TeacherCode = user.Teacher?.TeacherCode,
            Department = user.Teacher?.Department
        };
    }

    public async Task<PagedResultDto<UserListItemDto>> GetUsersAsync(UserFilterDto filter)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : filter.PageSize;
        var role = string.IsNullOrWhiteSpace(filter.Role) ? null : filter.Role.Trim();
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();

        var (users, totalCount) = await _userRepository.GetUsersAsync(role, search, page, pageSize);

        var items = users.Select(user => new UserListItemDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive
        }).ToList();

        return new PagedResultDto<UserListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetUserByIdAsync(id);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            throw new ArgumentException("Full name is required.", nameof(dto));
        }

        user.FullName = dto.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        if (user.Role == UserRole.STUDENT.ToString())
        {
            if (!string.IsNullOrWhiteSpace(dto.StudentCode))
            {
                var trimmedCode = dto.StudentCode.Trim();
                if (await _userRepository.StudentCodeExistsAsync(trimmedCode, user.UserId))
                {
                    throw new InvalidOperationException("Student code already exists.");
                }

                if (user.Student == null)
                {
                    throw new InvalidOperationException("Student profile not found.");
                }

                user.Student.StudentCode = trimmedCode;
            }
        }
        else if (user.Role == UserRole.TEACHER.ToString())
        {
            if (!string.IsNullOrWhiteSpace(dto.TeacherCode))
            {
                var trimmedCode = dto.TeacherCode.Trim();
                if (await _userRepository.TeacherCodeExistsAsync(trimmedCode, user.UserId))
                {
                    throw new InvalidOperationException("Teacher code already exists.");
                }

                if (user.Teacher == null)
                {
                    throw new InvalidOperationException("Teacher profile not found.");
                }

                user.Teacher.TeacherCode = trimmedCode;
            }

            if (user.Teacher != null)
            {
                user.Teacher.Department = string.IsNullOrWhiteSpace(dto.Department) ? null : dto.Department.Trim();
            }
        }

        await _userRepository.UpdateUserAsync(user);
    }

    public async Task ToggleUserStatusAsync(int id)
    {
        var user = await _userRepository.GetUserByIdAsync(id);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);
    }

    private async Task<CreateUserResultDto> CreateUserAsync(
        CreateUserDto dto,
        UserRole role,
        Student? student,
        Teacher? teacher)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
        {
            throw new ArgumentException("Username is required.", nameof(dto));
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            throw new ArgumentException("Full name is required.", nameof(dto));
        }

        if (await _userRepository.UsernameExistsAsync(dto.Username.Trim()))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        if (student != null)
        {
            if (await _userRepository.StudentCodeExistsAsync(student.StudentCode.Trim()))
            {
                throw new InvalidOperationException("Student code already exists.");
            }
        }

        if (teacher != null)
        {
            if (await _userRepository.TeacherCodeExistsAsync(teacher.TeacherCode.Trim()))
            {
                throw new InvalidOperationException("Teacher code already exists.");
            }
        }

        var temporaryPassword = string.IsNullOrWhiteSpace(dto.Password)
            ? GenerateTemporaryPassword()
            : dto.Password;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

        var user = new User
        {
            Username = dto.Username.Trim(),
            FullName = dto.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            Role = role.ToString(),
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateUserWithProfileAsync(user, student, teacher);

        return new CreateUserResultDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Role = user.Role,
            TemporaryPassword = temporaryPassword
        };
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<char> buffer = stackalloc char[12];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }
        return new string(buffer);
    }
}
