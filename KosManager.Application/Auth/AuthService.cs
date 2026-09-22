using KosManager.Domain;

namespace KosManager.Application.Auth;

public interface IJwtIssuer
{
    string Issue(User u);
}

public class AuthService(IUserRepository users, IPasswordHasher hasher, IJwtIssuer jwt)
{
    public async Task<User> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        if (await users.ByEmailAsync(email, ct) is not null)
            throw new ConflictException("email taken");
        var u = new User { Email = email, PasswordHash = hasher.Hash(password), Role = Roles.Penghuni };
        await users.AddAsync(u, ct);
        await users.SaveAsync(ct);
        return u;
    }

    public async Task<string> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var u = await users.ByEmailAsync(email, ct);
        if (u is null || !hasher.Verify(password, u.PasswordHash))
            throw new UnauthorizedException("email atau kata sandi salah");
        return jwt.Issue(u);
    }

    public async Task<User> MeAsync(int id, CancellationToken ct = default) =>
        await users.ByIdAsync(id, ct) ?? throw new NotFoundException("user tidak ada");
}

public class ConflictException(string message) : Exception(message);
public class UnauthorizedException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);
public class NotFoundException(string message) : Exception(message);
public class BadRequestException(string message) : Exception(message);
