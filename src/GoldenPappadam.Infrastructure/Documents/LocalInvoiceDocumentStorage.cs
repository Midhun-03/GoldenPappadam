namespace GoldenPappadam.Infrastructure.Documents;

/// <summary>
/// Keeps PDFs on the server's own disk, under a root folder given by configuration. Right for
/// development and a single office server; a cloud deployment registers an object-store
/// implementation instead. Files are not served statically: the only way to one is the
/// signed-in invoice endpoint.
/// </summary>
public class LocalInvoiceDocumentStorage(string rootPath) : IInvoiceDocumentStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task SaveInvoicePdfAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the target and then moved into place, so a crash half way through never
        // leaves a truncated PDF under the real name.
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temporary, content, ct);

        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<byte[]?> GetInvoicePdfAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);

        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task DeleteUnusedInvoicePdfAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Refuses any key that could reach outside the root folder.</summary>
    private string PathFor(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith('/') || key.Contains("..") ||
            key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')))
        {
            throw new ArgumentException($"'{key}' is not a valid document key.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{key}' is not a valid document key.", nameof(key));
        }

        return path;
    }
}
