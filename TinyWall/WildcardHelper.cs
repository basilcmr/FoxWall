using System;
using System.Text.RegularExpressions;

namespace pylorak.TinyWall
{
    public static class WildcardHelper
    {
        public static bool IsWildcardMatch(string wildcardPattern, string text)
        {
            if (string.IsNullOrEmpty(wildcardPattern) || string.IsNullOrEmpty(text))
                return false;

            try
            {
                // Normalize paths by expanding environment variables, replacing slashes, and trimming
                string pattern = Environment.ExpandEnvironmentVariables(wildcardPattern).Replace('/', '\\').Trim();
                string target = Environment.ExpandEnvironmentVariables(text).Replace('/', '\\').Trim();

                // Simple translation of wildcard pattern to regex (* and ? support)
                string regexPattern = "^" + Regex.Escape(pattern)
                                                  .Replace("\\*", ".*")
                                                  .Replace("\\?", ".") + "$";

                return Regex.IsMatch(target, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string ResolveWildcardPath(string wildcardPath)
        {
            if (string.IsNullOrEmpty(wildcardPath))
                return wildcardPath;

            try
            {
                var resolved = ResolveAllWildcardPaths(wildcardPath);
                if (resolved != null && resolved.Count > 0)
                    return resolved[0];
            }
            catch
            {
                // Fallback
            }

            return wildcardPath;
        }

        public static System.Collections.Generic.List<string> ResolveAllWildcardPaths(string wildcardPath)
        {
            var results = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(wildcardPath))
                return results;

            // Expand environment variables
            string expandedPath = Environment.ExpandEnvironmentVariables(wildcardPath);

            if (!expandedPath.Contains("*") && !expandedPath.Contains("?"))
            {
                string normPath = expandedPath.Replace('/', '\\');
                if (System.IO.File.Exists(normPath))
                    results.Add(normPath);
                return results;
            }

            try
            {
                string[] segments = expandedPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                    return results;

                // Handle drive letter prefix or UNC path prefix
                string currentPath = expandedPath.StartsWith("\\\\") ? "\\\\" : "";
                if (expandedPath.Contains(":") && segments[0].Contains(":"))
                {
                    currentPath = segments[0] + "\\";
                    string[] temp = new string[segments.Length - 1];
                    Array.Copy(segments, 1, temp, 0, temp.Length);
                    segments = temp;
                }

                ResolveSegmentsRecursive(currentPath, segments, 0, results);
            }
            catch
            {
                // Ignore errors
            }

            return results;
        }

        private static void ResolveSegmentsRecursive(string basePath, string[] segments, int index, System.Collections.Generic.List<string> results)
        {
            // Safeguard against too many matches
            if (results.Count > 100)
                return;

            if (index >= segments.Length)
            {
                if (System.IO.File.Exists(basePath))
                {
                    if (!results.Contains(basePath))
                        results.Add(basePath);
                }
                return;
            }

            string segment = segments[index];
            if (segment.Contains("*") || segment.Contains("?"))
            {
                if (!System.IO.Directory.Exists(basePath))
                    return;

                if (index == segments.Length - 1)
                {
                    // Last segment: match files. If segment is "*", search all directories recursively to support directory rules
                    string[] files;
                    if (segment == "*")
                    {
                        files = System.IO.Directory.GetFiles(basePath, "*", System.IO.SearchOption.AllDirectories);
                    }
                    else
                    {
                        files = System.IO.Directory.GetFiles(basePath, segment);
                    }

                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            if (results.Count > 100)
                                break;
                            if (!results.Contains(file))
                                results.Add(file);
                        }
                    }
                }
                else
                {
                    // Directory segment: match directories and continue recursively
                    string[] dirs = System.IO.Directory.GetDirectories(basePath, segment);
                    if (dirs != null)
                    {
                        foreach (string dir in dirs)
                        {
                            ResolveSegmentsRecursive(dir, segments, index + 1, results);
                        }
                    }
                }
            }
            else
            {
                string nextPath = System.IO.Path.Combine(basePath, segment);
                if (index == segments.Length - 1)
                {
                    if (System.IO.File.Exists(nextPath))
                    {
                        if (!results.Contains(nextPath))
                            results.Add(nextPath);
                    }
                }
                else
                {
                    if (System.IO.Directory.Exists(nextPath))
                    {
                        ResolveSegmentsRecursive(nextPath, segments, index + 1, results);
                    }
                }
            }
        }
    }
}
