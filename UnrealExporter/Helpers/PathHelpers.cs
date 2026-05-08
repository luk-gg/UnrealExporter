using Spectre.Console;
using System.Text.RegularExpressions;
using Slugify;

public static class PathHelpers
{
    public static string Slugify(string input) => new SlugHelper().GenerateSlug(input);

    public static string NormalizePath(string path, bool allowEmpty = false)
    {
        if (!allowEmpty && string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.");

        path = path.Trim().Trim('"');
        path = Environment.ExpandEnvironmentVariables(path);

        try
        {
            var fullPath = Path.GetFullPath(path);
            fullPath = Path.GetFullPath(new Uri(fullPath).LocalPath)
                           .TrimEnd(Path.DirectorySeparatorChar);
            return fullPath;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid path: {path}. {ex.Message}", ex);
        }
    }

    // TODO: handle Base64? keys
    // Blade & Soul Revolution (Android)   78aeb4da56eb4ab89ea2eb61abd6c5a3
    // Sword Art Online: Fatal Bullet  h67GrjX2aGMgrAQeNwf9VmCYbt50ylJFeP3rIhbxh4e9bZXnqm8sbvEjWGOi6rgs
    // Street Fighter V    _aS4mfZK8M5s5KWC2Lz2VsFnGKI7azgl
    public static bool IsValidAesKey(string key, bool allowEmpty = false)
    {
        if (!allowEmpty && string.IsNullOrWhiteSpace(key))
            return false;
        key = key.Trim();
        return Regex.IsMatch(key, @"^0x[0-9a-fA-F]{64}$");
    }

    public static bool IsDirectoryWritable(string dirPath, out string? error)
    {
        error = null;
        bool created = false;

        if (!Directory.Exists(dirPath))
        {
            try
            {
                Directory.CreateDirectory(dirPath);
                created = true;
            }
            catch (Exception ex)
            {
                error = $"Could not create directory: {ex.Message}";
                return false;
            }
        }

        try
        {
            using FileStream fs = File.Create(
                Path.Combine(dirPath, Path.GetRandomFileName()),
                1,
                FileOptions.DeleteOnClose);
        }
        catch
        {
            error = "Directory is not writable";
            if (created) Directory.Delete(dirPath);
            return false;
        }

        if (created) Directory.Delete(dirPath);
        return true;
    }

    public static string GetValidFileName(string fileName, string? extension)
    {
        SlugHelper slugHelper = new SlugHelper();
        var safeFileName = slugHelper.GenerateSlug(fileName);
        if (!string.IsNullOrEmpty(extension) && !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            safeFileName += extension;
        return safeFileName;
    }
}