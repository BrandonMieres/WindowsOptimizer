using System.Text.RegularExpressions;

namespace WindowsOptimizer.Core.Utilities;

public static partial class VariableExpander
{
    private static readonly Dictionary<string, string> EnvironmentVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        { "%LocalAppData%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
        { "%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
        { "%Temp%", Path.GetTempPath().TrimEnd('\\') },
        { "%WinDir%", Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows" },
        { "%SystemDrive%", Environment.GetEnvironmentVariable("SystemDrive") ?? "C:" },
        { "%ProgramData%", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) },
        { "%UserProfile%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }
    };

    private static readonly char[] DirectorySeparators = ['\\', '/'];

    public static string ExpandVariables(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        foreach (var kvp in EnvironmentVariables)
        {
            if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                path = Regex.Replace(path, Regex.Escape(kvp.Key), kvp.Value.Replace("\\", "\\\\"), RegexOptions.IgnoreCase);
                path = path.Replace("\\\\", "\\"); // Clean up double escapes if any
            }
        }
        return path;
    }

    /// <summary>
    /// Parses a path like "%LocalAppData%\Google\Chrome\User Data\*\Cache\*|RECURSE"
    /// Returns the expanded paths resolving wildcards.
    /// Note: Extremely simplified for now.
    /// </summary>
    public static IEnumerable<string> ExpandPaths(string pathPattern)
    {
        bool recurse = false;
        if (pathPattern.EndsWith("|RECURSE", StringComparison.OrdinalIgnoreCase))
        {
            recurse = true;
            pathPattern = pathPattern[..^8];
        }

        string expanded = ExpandVariables(pathPattern);

        // Very basic wildcard expansion (handles a single wildcard directory depth)
        // E.g., C:\Users\...\User Data\*\Cache\*
        return ResolveWildcards(expanded, recurse);
    }

    private static IEnumerable<string> ResolveWildcards(string pattern, bool recurse)
    {
        // For simplicity, if no wildcard is present, just return the path
        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            yield return pattern;
            yield break;
        }

        // Split by directory separator
        var parts = pattern.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries);
        
        string root = parts[0] + "\\";
        var results = new List<string> { root };

        for (int i = 1; i < parts.Length; i++)
        {
            var currentPart = parts[i];
            var newResults = new List<string>();

            foreach (var currentPath in results)
            {
                if (!Directory.Exists(currentPath)) continue;

                if (currentPart.Contains('*') || currentPart.Contains('?'))
                {
                    // Is this the last part (file pattern)?
                    if (i == parts.Length - 1)
                    {
                        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        try
                        {
                            newResults.AddRange(Directory.EnumerateFileSystemEntries(currentPath, currentPart, searchOption));
                        }
                        catch (UnauthorizedAccessException) { /* Ignore */ }
                    }
                    else
                    {
                        // Directory wildcard
                        try
                        {
                            newResults.AddRange(Directory.EnumerateDirectories(currentPath, currentPart, SearchOption.TopDirectoryOnly));
                        }
                        catch (UnauthorizedAccessException) { /* Ignore */ }
                    }
                }
                else
                {
                    newResults.Add(Path.Combine(currentPath, currentPart));
                }
            }
            results = newResults;
        }

        foreach (var res in results)
        {
            yield return res;
        }
    }
}
