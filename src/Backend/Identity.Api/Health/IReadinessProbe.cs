namespace Identity.Api.Health;

public interface IReadinessProbe
{
    Task<bool> IsReady(CancellationToken cancellationToken);
}
