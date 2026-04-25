namespace Helper;

public static class ImageHelper
{
    public static async Task<byte[]> ReadBytesAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0) return [];
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
