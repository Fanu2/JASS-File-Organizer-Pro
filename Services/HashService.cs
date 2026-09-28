using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using JASS.FileOrganizer.Models;

namespace JASS.FileOrganizer.Services;

public sealed class HashService
{
    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(IEnumerable<string> paths, IProgress<int>? progress, CancellationToken token)
    {
        var candidates = new Dictionary<long, List<string>>();
        foreach (var p in paths)
        {
            token.ThrowIfCancellationRequested();
            try { var info = new FileInfo(p); if (!candidates.TryGetValue(info.Length, out var list)) candidates[info.Length] = list = []; list.Add(p); } catch { }
        }
        var candidatesList = candidates.Values.Where(x => x.Count > 1).SelectMany(x => x).ToList();
        var groups = new Dictionary<string, List<string>>();
        int done = 0;
        foreach (var p in candidatesList)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(p);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!groups.TryGetValue(hash, out var list)) groups[hash] = list = [];
                list.Add(p);
            }
            catch { }
            progress?.Report(candidatesList.Count == 0 ? 100 : ++done * 100 / candidatesList.Count);
        }
        return groups.Where(x => x.Value.Count > 1)
            .Select(x => new DuplicateGroup(x.Key, new FileInfo(x.Value[0]).Length, x.Value))
            .OrderByDescending(x => x.Size).ToList();
    }

    public async Task<List<VisualDuplicateGroup>> FindVisualDuplicatesAsync(IEnumerable<string> paths, IProgress<int>? progress, CancellationToken token)
    {
        var images = paths.Where(p => FileTypeClassifier.Classify(Path.GetExtension(p)) == "Image").ToList();
        var signatures = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int done = 0;
        foreach (var path in images)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var sig = await Task.Run(() => AverageHash(path), token);
                if (!signatures.TryGetValue(sig, out var list)) signatures[sig] = list = [];
                list.Add(path);
            }
            catch { }
            progress?.Report(images.Count == 0 ? 100 : ++done * 100 / images.Count);
        }
        // Exact aHash groups plus nearby signatures are kept separate for a conservative result.
        var groups = new List<VisualDuplicateGroup>();
        var remaining = signatures.ToList();
        for (int i = 0; i < remaining.Count; i++)
        {
            var bucket = new List<string>(remaining[i].Value);
            int maxDistance = 0;
            for (int j = i + 1; j < remaining.Count; j++)
            {
                var distance = HammingDistance(remaining[i].Key, remaining[j].Key);
                if (distance <= 5)
                {
                    bucket.AddRange(remaining[j].Value);
                    maxDistance = Math.Max(maxDistance, distance);
                }
            }
            var files = bucket.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count > 1) groups.Add(new VisualDuplicateGroup(remaining[i].Key, files, maxDistance));
        }
        return groups.OrderByDescending(g => g.Files.Count).ToList();
    }

    static string AverageHash(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit(); bmp.UriSource = new Uri(path); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.DecodePixelWidth = 8; bmp.DecodePixelHeight = 8; bmp.EndInit(); bmp.Freeze();
        var converted = new FormatConvertedBitmap(bmp, PixelFormats.Gray8, null, 0); converted.Freeze();
        var pixels = new byte[64]; converted.CopyPixels(pixels, 8, 0);
        double avg = pixels.Average(b => (double)b);
        ulong bits = 0; for (int i = 0; i < 64; i++) if (pixels[i] >= avg) bits |= 1UL << i;
        return bits.ToString("X16");
    }

    static int HammingDistance(string a, string b)
    {
        ulong x = Convert.ToUInt64(a, 16) ^ Convert.ToUInt64(b, 16); int n = 0;
        while (x != 0) { x &= x - 1; n++; }
        return n;
    }
}
