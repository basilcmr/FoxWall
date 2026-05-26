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

            // Simple translation of wildcard pattern to regex (* and ? support)
            string regexPattern = "^" + Regex.Escape(wildcardPattern)
                                              .Replace("\\*", ".*")
                                              .Replace("\\?", ".") + "$";
            try
            {
                return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
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

            if (!wildcardPath.Contains("*") && !wildcardPath.Contains("?"))
                return wildcardPath;

            try
            {
                string[] segments = wildcardPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                    return wildcardPath;

                // Handle drive letter prefix or UNC path prefix
                string currentPath = wildcardPath.StartsWith("\\\\") ? "\\\\" : "";
                if (wildcardPath.Contains(":") && segments[0].Contains(":"))
                {
                    currentPath = segments[0] + "\\";
                    string[] temp = new string[segments.Length - 1];
                    Array.Copy(segments, 1, temp, 0, temp.Length);
                    segments = temp;
                }

                return ResolveSegments(currentPath, segments, 0);
            }
            catch
            {
                return wildcardPath;
            }
        }

        private static string ResolveSegments(string basePath, string[] segments, int index)
        {
            if (index >= segments.Length)
            {
                if (System.IO.File.Exists(basePath))
                    return basePath;
                return "";
            }

            string segment = segments[index];
            if (segment.Contains("*") || segment.Contains("?"))
            {
                if (!System.IO.Directory.Exists(basePath))
                    return "";

                if (index == segments.Length - 1)
                {
                    string[] files = System.IO.Directory.GetFiles(basePath, segment);
                    if (files != null && files.Length > 0)
                        return files[0];
                }
                else
                {
                    string[] dirs = System.IO.Directory.GetDirectories(basePath, segment);
                    if (dirs != null)
                    {
                        foreach (string dir in dirs)
                        {
                            string resolved = ResolveSegments(dir, segments, index + 1);
                            if (!string.IsNullOrEmpty(resolved))
                                return resolved;
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
                        return nextPath;
                }
                else
                {
                    if (System.IO.Directory.Exists(nextPath))
                    {
                        string resolved = ResolveSegments(nextPath, segments, index + 1);
                        if (!string.IsNullOrEmpty(resolved))
                            return resolved;
                    }
                }
            }

            return "";
        }
    }
}
