using Hokai.Hosting;

namespace Hokai.Tests.Hosting;

public sealed class PlatformContextTests
{
    [Fact]
    public void Detect_ReturnsPopulatedContext()
    {
        var ctx = PlatformContext.Detect();

        Assert.NotNull(ctx.ExecutablePath);
        Assert.NotEmpty(ctx.ExecutablePath);
        Assert.NotEmpty(ctx.UserName);
        Assert.NotEmpty(ctx.HomeDirectory);
        Assert.NotNull(ctx.SudoUserName);
    }

    [Fact]
    public void DefaultContext_HasEmptyStrings()
    {
        var ctx = new PlatformContext();

        Assert.Equal("", ctx.ExecutablePath);
        Assert.Equal("", ctx.UserName);
        Assert.Equal("", ctx.HomeDirectory);
        Assert.Equal("", ctx.SudoUserName);
        Assert.False(ctx.IsElevated);
    }
}
