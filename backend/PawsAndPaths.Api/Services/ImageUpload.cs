namespace PawsAndPaths.Api.Services;

public static class ImageUpload
{
    public const int MaximumBytes = 2 * 1024 * 1024;

    public static async Task<(byte[]? Data, string? ContentType, string? Error)> ReadAsync(
        IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0) return (null, null, "Choose a photo to upload.");
        if (photo.Length > MaximumBytes) return (null, null, "The photo must be 2 MB or smaller.");
        await using var stream = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(stream, cancellationToken);
        var data = stream.ToArray();
        var contentType = Detect(data);
        return contentType is null
            ? (null, null, "Use a JPEG, PNG, or WebP photo.")
            : (data, contentType, null);
    }

    private static string? Detect(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "image/png";
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
