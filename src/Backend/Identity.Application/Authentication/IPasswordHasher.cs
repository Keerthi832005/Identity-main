using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

public interface IPasswordHasher
{
    PasswordHash Hash(string password);
    bool Verify(string password, UserCredential? credential);
}
