namespace Raft.Hosting;

public static class DotEnv
{
    public static Dictionary<string, string?> Load()
    {
        var values = new Dictionary<string, string?>();
        string? path = FindFile();
        if (path == null)
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }
            int separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }
            string key = trimmed[..separator].Trim().Replace("__", ":");
            values[key] = trimmed[(separator + 1)..].Trim();
        }
        return values;
    }

    private static string? FindFile()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        return null;
    }
}
