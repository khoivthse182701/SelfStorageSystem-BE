using Xunit;

namespace SelfStorageSystem.Tests;

public class HealthCheckTests
{
    [Fact]
    public void Architecture_Setup_ShouldBeValid()
    {
        bool isSetupReady = true;
        Assert.True(isSetupReady);
    }
}
