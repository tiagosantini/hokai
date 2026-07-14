using Hokai.Commands;

namespace Hokai.Tests.Commands;

public sealed class UriDisplayFormatterTests
{
    [Fact]
    public void Format_ShortUri_ReturnsUnchanged()
    {
        var uri = new Uri("https://example.com/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Equal("https://example.com/health", result);
    }

    [Fact]
    public void Format_ExactlyFifty_ReturnsUnchanged()
    {
        // "https://" (8) + 38 char host + "/health" (7) = 53, need 50 => 8 + 35 + 7 = 50
        var uri = new Uri("https://abcdefghijklmnopqrstuvwxyz0123456789abc.com/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void Format_OverFifty_TruncatesToMaxWidth()
    {
        var uri = new Uri("https://verybigendpoint-withwaytoomanywordsinit.com/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.True(result.Length <= UriDisplayFormatter.ColumnWidth);
    }

    [Fact]
    public void Format_LongHostname_PreservesSchemeAndPath()
    {
        var uri = new Uri("https://verybigendpoint-withwaytoomanywordsinit.com/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.StartsWith("https://", result);
        Assert.EndsWith("/health", result);
    }

    [Fact]
    public void Format_LongHostname_ContainsEllipsis()
    {
        var uri = new Uri("https://verybigendpoint-withwaytoomanywordsinit.com/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Contains("...", result);
    }

    [Fact]
    public void Format_LongPathWithShortHost_PreservesHostAndPathSuffix()
    {
        var uri = new Uri("https://example.com/this/is/a/very/long/path/that/exceeds/the/column/width/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.StartsWith("https://", result);
        Assert.Contains("example.com", result);
        Assert.EndsWith("/health", result);
    }

    [Fact]
    public void Format_LongHostAndPath_PreservesBoth()
    {
        var uri = new Uri("https://very-long-hostname-that-takes-much-space.com/some/nested/route/health.txt");

        var result = UriDisplayFormatter.Format(uri);

        Assert.StartsWith("https://", result);
        Assert.Contains("...", result);
        Assert.True(result.Length <= UriDisplayFormatter.ColumnWidth);
    }

    [Fact]
    public void Format_WithQueryAndFragment_DropsThemWhenNeeded()
    {
        var uri = new Uri("https://example.com/health?tracking=verylongparametervalue#section-name");

        var result = UriDisplayFormatter.Format(uri);

        Assert.DoesNotContain("?", result);
        Assert.DoesNotContain("#", result);
    }

    [Fact]
    public void Format_ShortUriWithQuery_KeepsQuery()
    {
        var uri = new Uri("https://ex.io/h?t=1");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Contains("?", result);
    }

    [Fact]
    public void Format_NonDefaultPort_IsPreserved()
    {
        var uri = new Uri("https://example.com:8443/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Equal("https://example.com:8443/health", result);
    }

    [Fact]
    public void Format_LongHostWithPort_PreservesPort()
    {
        var uri = new Uri("https://verybigendpoint-withwaytoomanywordsinit.com:8443/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.EndsWith("/health", result);
        Assert.Contains(":8443", result);
    }

    [Fact]
    public void Format_Ipv4Address_IsPreserved()
    {
        var uri = new Uri("https://192.168.1.100:9090/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Equal("https://192.168.1.100:9090/health", result);
    }

    [Fact]
    public void Format_LongIpv4Address_MiddleTruncates()
    {
        var uri = new Uri("https://192.168.1.100:9090/api/v2/status/check/health");

        var result = UriDisplayFormatter.Format(uri);

        Assert.StartsWith("https://", result);
        Assert.Contains(":9090", result);
        Assert.EndsWith("/health", result);
    }

    [Fact]
    public void Format_RootPath_IsPreserved()
    {
        var uri = new Uri("https://example.com/");

        var result = UriDisplayFormatter.Format(uri);

        Assert.EndsWith("/", result);
    }

    [Fact]
    public void Format_PercentEncodedPath_PreservesCharacters()
    {
        var uri = new Uri("https://example.com/api/v1/%E6%97%A5%E6%9C%AC%E8%AA%9E");

        var result = UriDisplayFormatter.Format(uri);

        Assert.Contains("api", result);
        Assert.Contains("v1", result);
    }

    [Fact]
    public void Format_ResultDoesNotExceedColumnWidth()
    {
        var uris = new[]
        {
            new Uri("https://a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y.z.com/health"),
            new Uri("https://example.com/" + new string('x', 100)),
            new Uri("https://a" + new string('b', 60) + ".com/health"),
            new Uri("https://example.com/health?q=" + new string('x', 200)),
        };

        foreach (var uri in uris)
        {
            var result = UriDisplayFormatter.Format(uri);
            Assert.True(result.Length <= UriDisplayFormatter.ColumnWidth,
                $"URI {uri} produced result of length {result.Length}: {result}");
        }
    }
}
