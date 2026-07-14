namespace Hokai.Commands;

internal static class UriDisplayFormatter
{
    internal const int ColumnWidth = 50;

    internal static string Format(Uri uri)
    {
        var full = uri.ToString();

        if (full.Length <= ColumnWidth)
            return full;

        var trimmed = TrimQueryAndFragment(full);
        if (trimmed.Length <= ColumnWidth)
            return trimmed;

        return TruncateUri(uri);
    }

    private static string TrimQueryAndFragment(string uri)
    {
        var queryIndex = uri.IndexOf('?');
        var fragmentIndex = uri.IndexOf('#');
        var cutIndex = -1;

        if (queryIndex >= 0 && fragmentIndex >= 0)
            cutIndex = Math.Min(queryIndex, fragmentIndex);
        else if (queryIndex >= 0)
            cutIndex = queryIndex;
        else if (fragmentIndex >= 0)
            cutIndex = fragmentIndex;

        return cutIndex >= 0 ? uri[..cutIndex] : uri;
    }

    private static string TruncateUri(Uri uri)
    {
        var schemeLen = uri.Scheme.Length + 3; // "https://"
        var host = uri.Host;
        var portSuffix = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var path = uri.AbsolutePath;
        if (path.Length == 0)
            path = "/";

        var budget = ColumnWidth - schemeLen;

        var truncatedPath = MiddleTruncatePath(path, budget);
        var pathUsed = truncatedPath.Length;

        var hostBudget = Math.Max(1, budget - pathUsed);
        var truncatedHost = MiddleTruncateHost(host, portSuffix, hostBudget);

        var scheme = uri.Scheme + "://";
        var result = scheme + truncatedHost + truncatedPath;

        if (result.Length > ColumnWidth)
            result = result[..ColumnWidth];

        return result;
    }

    private static string MiddleTruncatePath(string path, int budget)
    {
        if (path == "/")
            return path;

        if (path.Length <= budget)
            return path;

        var segments = path.Split('/');
        var lastSegment = segments[^1];

        var prefix = "/.../";
        var fullSuffix = prefix + lastSegment;

        if (fullSuffix.Length <= budget)
            return fullSuffix;

        if (budget <= prefix.Length + 1)
            return path[..budget];

        var lastBudget = budget - prefix.Length;
        return prefix + lastSegment[^lastBudget..];
    }

    private static string MiddleTruncateHost(string host, string portSuffix, int budget)
    {
        var full = host + portSuffix;

        if (full.Length <= budget)
            return full;

        var portLen = portSuffix.Length;
        var hostBudget = budget - portLen;

        if (hostBudget < 5)
            return full[..budget];

        var suffix = Math.Max(1, hostBudget / 2);
        var prefix = hostBudget - suffix - 3; // 3 for "..."

        if (prefix < 1)
        {
            suffix = hostBudget - 4; // 1 + "..."
            prefix = 1;
        }

        if (suffix < 1)
            suffix = 1;

        return host[..prefix] + "..." + host[^suffix..] + portSuffix;
    }
}
