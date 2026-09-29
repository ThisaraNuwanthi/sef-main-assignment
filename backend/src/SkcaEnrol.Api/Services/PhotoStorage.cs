namespace SkcaEnrol.Api.Services;

public class StorageOptions
{
    public const string Section = "Storage";

    // Relative paths resolve from the app folder. Kept outside wwwroot on purpose:
    // photos are only served through an endpoint that checks ownership.
    public string UploadsPath { get; set; } = "uploads";
}

public interface IPhotoStorage
{
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);
    void Delete(string? relativePath);
    string? GetFullPath(string? relativePath);
}

/// <summary>Saves uploaded photos to local disk. Files are named by us, never by the user.</summary>
public class LocalPhotoStorage(Microsoft.Extensions.Options.IOptions<StorageOptions> options, IWebHostEnvironment env) : IPhotoStorage
{
    // Trailing separator so the "inside uploads" check below cannot match a sibling like "uploads-evil".
    private readonly string _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.UploadsPath)) + Path.DirectorySeparatorChar;

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        // Extension comes from the detected image type, not the uploaded filename.
        var extension = PhotoValidator.DetectExtension(file) ?? throw new InvalidOperationException("Validate the photo first.");
        var relative = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}");
        var full = Path.Combine(_root, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var stream = File.Create(full);
        await file.CopyToAsync(stream, ct);
        return relative;
    }

    public void Delete(string? relativePath)
    {
        var full = GetFullPath(relativePath);
        if (full is not null && File.Exists(full)) File.Delete(full);
    }

    public string? GetFullPath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        // Guard against "../" tricks: the resolved path must stay inside the uploads folder.
        return full.StartsWith(_root, StringComparison.Ordinal) ? full : null;
    }
}

/// <summary>Checks an uploaded photo is a real, small JPEG or PNG.</summary>
public static class PhotoValidator
{
    public const long MaxBytes = 2 * 1024 * 1024; // 2 MB is plenty for a profile photo

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Please choose a photo.";
        if (file.Length > MaxBytes) return "Photo must be 2 MB or smaller.";
        if (file.ContentType is not ("image/jpeg" or "image/png")) return "Photo must be a JPEG or PNG image.";
        // The Content-Type header is set by the client and can lie, so also check the file's first bytes.
        if (DetectExtension(file) is null) return "The file content is not a valid JPEG or PNG image.";
        return null;
    }

    /// <summary>Reads the file's "magic bytes": JPEG starts FF D8 FF, PNG starts 89 50 4E 47.</summary>
    public static string? DetectExtension(IFormFile file)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = file.OpenReadStream();
        var read = stream.Read(header);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return ".jpg";
        if (read >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return ".png";
        return null;
    }
}
