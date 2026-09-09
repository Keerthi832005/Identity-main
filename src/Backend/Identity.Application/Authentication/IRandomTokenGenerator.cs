namespace Identity.Application.Authentication;

public interface IRandomTokenGenerator
{
    string Generate();
}
