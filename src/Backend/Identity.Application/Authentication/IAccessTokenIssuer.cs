namespace Identity.Application.Authentication;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenRequest request);
    SigningMetadata GetSigningMetadata();
}
