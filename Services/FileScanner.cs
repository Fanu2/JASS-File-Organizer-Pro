using System.IO;
using JASS.FileOrganizer.Models;

namespace JASS.FileOrganizer.Services;

public sealed class ScanResult
{
    public List<FileItem> Files { get; } = [];
    public List<string> EmptyFolders { get; } = [];
    public long TotalBytes { get; set; }
    public int FolderCount { get; set; }
}

public sealed class FileScanner
{
    public async Task<ScanResult> ScanAsync(string root, IProgress<int>? progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            var result = new ScanResult();
            var stack = new Stack<string>();
            stack.Push(root);
            int visited = 0;
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var dir = stack.Pop();
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(dir); } catch { continue; }
                bool hasAny = false;
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    hasAny = true;
                    try
                    {
                        if (Directory.Exists(entry)) { result.FolderCount++; stack.Push(entry); }
                        else if (File.Exists(entry))
                        {
                            var info = new FileInfo(entry);
                            result.Files.Add(new FileItem { FullPath = info.FullName, Size = info.Length, Modified = info.LastWriteTime, Created = info.CreationTime, Accessed = info.LastAccessTime, Attributes = info.Attributes });
                            result.TotalBytes += info.Length;
                        }
                    }
                    catch { }
                }
                if (!hasAny) result.EmptyFolders.Add(dir);
                visited++;
                if (visited % 20 == 0) progress?.Report(Math.Min(99, visited % 100));
            }
            progress?.Report(100);
            return result;
        }, token);
    }
}
