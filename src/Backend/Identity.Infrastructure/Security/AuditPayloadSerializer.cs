using System.Text.Json;
using Identity.Application.Security;

namespace Identity.Infrastructure.Security;

internal sealed class AuditPayloadSerializer : IAuditPayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Serialize<TPayload>(TPayload payload) where TPayload : IAuditPayload
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload switch
        {
            MfaMethodAuditData value => JsonSerializer.Serialize(value, Options),
            DeviceAuditData value => JsonSerializer.Serialize(value, Options),
            _ => throw new InvalidOperationException(
                $"Audit payload type '{payload.GetType().Name}' is not approved for persistence."),
        };
    }
}
