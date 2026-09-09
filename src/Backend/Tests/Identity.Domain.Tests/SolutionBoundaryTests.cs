using Identity.Domain;

namespace Identity.Domain.Tests;

public sealed class SolutionBoundaryTests
{
    [Fact]
    public void DomainAssembly_IsOwnedByIdentitySolution()
    {
        var assemblyName = typeof(DomainAssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("Identity.Domain", assemblyName);
    }
}
