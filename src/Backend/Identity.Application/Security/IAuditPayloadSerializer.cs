namespace Identity.Application.Security;

public interface IAuditPayloadSerializer
{
    string Serialize<TPayload>(TPayload payload) where TPayload : IAuditPayload;
}
