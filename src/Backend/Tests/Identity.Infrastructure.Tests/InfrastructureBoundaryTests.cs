using Identity.Infrastructure;

namespace Identity.Infrastructure.Tests;

public sealed class InfrastructureBoundaryTests
{
    [Fact]
    public void InfrastructureAssembly_HasExpectedIdentity()
    {
        var assemblyName = typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("Identity.Infrastructure", assemblyName);
    }
}
