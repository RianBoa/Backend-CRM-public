using backend.Domain.Users;

namespace backend.Auth;

public interface IJwtTokenService
{
    AuthTokenResult CreateAccessToken(User user, NUlid.Ulid sessionId);
}
