using Identity.Application;

namespace Identity.Application.Tests;

public sealed class ApplicationBoundaryTests
{
    [Fact]
    public void ApplicationAssembly_HasExpectedIdentity()
    {
        var assemblyName = typeof(ApplicationAssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("Identity.Application", assemblyName);
    }
}
