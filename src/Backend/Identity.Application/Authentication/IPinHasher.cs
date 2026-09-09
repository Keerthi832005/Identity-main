using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

public interface IPinHasher
{
    PasswordHash Hash(string pin);
    bool Verify(string pin, UserCredential? credential);
}
