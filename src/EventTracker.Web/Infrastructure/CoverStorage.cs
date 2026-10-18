namespace EventTracker.Web.Infrastructure;

/// <summary>Stores event cover images under wwwroot/uploads/covers with random file names.</summary>
public sealed class CoverStorage(IWebHostEnvironment environment, ILogger<CoverStorage> logger)
{
    public const long MaxBytes = 3 * 1024 * 1024;
    private const string Folder = "/uploads/covers/";

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return null;

        if (file.Length > MaxBytes)
            return "Kapak görseli en fazla 3 MB olabilir.";

        if (!AllowedTypes.ContainsKey(file.ContentType))
            return "Yalnızca JPG, PNG veya WEBP görsel yüklenebilir.";

        return null;
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var folder = Path.Combine(environment.WebRootPath, "uploads", "covers");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():n}{AllowedTypes[file.ContentType]}";
        await using var stream = File.Create(Path.Combine(folder, fileName));
        await file.CopyToAsync(stream);

        return Folder + fileName;
    }

    /// <summary>Removes a cover that is no longer referenced. Only files in the covers folder are touched.</summary>
    public void Delete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(Folder, StringComparison.Ordinal))
            return;

        var fullPath = Path.Combine(environment.WebRootPath, "uploads", "covers", Path.GetFileName(path));
        try
        {
            File.Delete(fullPath);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Could not delete cover {Path}", fullPath);
        }
    }
}
