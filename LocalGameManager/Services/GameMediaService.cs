using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LocalGameManager.Services;

public sealed class GameMediaService(AppPaths paths)
{
    public async Task DeleteAsync(long gameId, long mediaId, CancellationToken cancellationToken = default)
    {
        await using var db = new LibraryDbContext(paths);
        var media = await db.GameMedia.FirstOrDefaultAsync(x => x.Id == mediaId && x.GameId == gameId, cancellationToken);
        if (media is null) return;
        db.GameMedia.Remove(media);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ImportAsync(long gameId, string filePath, MediaKind kind, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        using var source = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var thumbnail = CreateThumbnail(frame);
        await using var db = new LibraryDbContext(paths);
        if (kind is MediaKind.Cover or MediaKind.Icon)
        {
            var existing = await db.GameMedia.Where(media => media.GameId == gameId && media.Kind == kind).ToListAsync(cancellationToken);
            db.GameMedia.RemoveRange(existing);
        }
        var sort = await db.GameMedia.Where(media => media.GameId == gameId && media.Kind == kind).Select(media => (int?)media.SortOrder).MaxAsync(cancellationToken) ?? -1;
        db.GameMedia.Add(new GameMedia { GameId = gameId, Kind = kind, SortOrder = sort + 1, MimeType = GuessMimeType(filePath), Width = frame.PixelWidth, Height = frame.PixelHeight, Content = bytes, ThumbnailContent = thumbnail });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static byte[] CreateThumbnail(BitmapFrame frame)
    {
        var scale = Math.Min(1d, 360d / Math.Max(frame.PixelWidth, frame.PixelHeight));
        BitmapSource image = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame;
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
    private static string GuessMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp", _ => "image/png" };
}
