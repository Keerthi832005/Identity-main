namespace Identity.Application.Security;

public interface IIdentifierHasher
{
    byte[] Hash(string identifier);
}
