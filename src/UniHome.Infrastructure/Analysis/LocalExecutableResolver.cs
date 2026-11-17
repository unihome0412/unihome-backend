namespace UniHome.Infrastructure.Analysis;

internal static class LocalExecutableResolver
{
    public static string? Resolve(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var candidate = configuredPath.Trim();

        if (Path.IsPathRooted(candidate))
        {
            return File.Exists(candidate) ? candidate : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var file = Path.Combine(directory, candidate);

            if (File.Exists(file))
            {
                return file;
            }

            if (OperatingSystem.IsWindows() && File.Exists(file + ".exe"))
            {
                return file + ".exe";
            }
        }

        return null;
    }
}
