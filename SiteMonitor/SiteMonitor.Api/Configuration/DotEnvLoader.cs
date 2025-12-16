using System;
using System.IO;

namespace SiteMonitor.Api.Configuration;

internal static class DotEnvLoader
{
    public static void Load()
    {
        try
        {
            var current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                var envPath = Path.Combine(current, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var rawLine in File.ReadAllLines(envPath))
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var separatorIndex = line.IndexOf('=');
                        if (separatorIndex <= 0)
                        {
                            continue;
                        }

                        var key = line[..separatorIndex].Trim();
                        var value = line[(separatorIndex + 1)..].Trim();
                        Environment.SetEnvironmentVariable(key, value);
                    }

                    break;
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }
        catch
        {
            // Ignore dotenv errors; environment variables can be set elsewhere.
        }
    }
}
