using System.IO;

namespace JASS.FileOrganizer.Models;

public sealed class FileItem
{
    public string FullPath { get; init; } = "";
    public string Name => Path.GetFileName(FullPath);
    public string Extension => Path.GetExtension(FullPath).ToLowerInvariant();
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public DateTime Created { get; init; }
    public DateTime Accessed { get; init; }
    public FileAttributes Attributes { get; init; }
    public bool IsDirectory { get; init; }
    public string Type => IsDirectory ? "Folder" : FileTypeClassifier.Classify(Extension);
    public string SizeText => FormatBytes(Size);
    public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm");
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm");
    public string AttributesText => Attributes.ToString();

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int i = 0;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return $"{value:0.##} {units[i]}";
    }
}

public static class FileTypeClassifier
{
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".heic" };
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpeg", ".mpg" };
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma" };
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt", ".xls", ".xlsx", ".csv", ".ppt", ".pptx" };
    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz" };
    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".py", ".js", ".ts", ".tsx", ".jsx", ".java", ".cpp", ".c", ".h", ".hpp", ".html", ".css", ".json", ".xml", ".yaml", ".yml", ".sql", ".ps1", ".sh" };
    private static readonly HashSet<string> Data = new(StringComparer.OrdinalIgnoreCase)
        { ".db", ".sqlite", ".sqlite3", ".parquet", ".feather", ".ndjson", ".jsonl", ".dbf", ".bak" };
    private static readonly HashSet<string> Projects = new(StringComparer.OrdinalIgnoreCase)
        { ".csproj", ".sln", ".vcxproj", ".pyproj", ".props", ".targets", ".slnx", ".gitignore", ".gitattributes", ".proj" };

    public static string Classify(string ext)
    {
        if (Images.Contains(ext)) return "Image";
        if (Videos.Contains(ext)) return "Video";
        if (Audio.Contains(ext)) return "Audio";
        if (Documents.Contains(ext)) return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Document";
        if (Archives.Contains(ext)) return "Archive";
        if (Code.Contains(ext)) return "Code";
        if (Data.Contains(ext)) return "Data";
        if (Projects.Contains(ext)) return "Project";
        return string.IsNullOrWhiteSpace(ext) ? "Unknown" : "Other";
    }

    public static string OrganizationFolder(string type) => type switch
    {
        "Image" => "Images", "Video" => "Videos", "Audio" => "Audio", "PDF" => "PDF",
        "Document" => "Documents", "Archive" => "Archives", "Code" => "Code",
        "Data" => "Data", "Project" => "Projects", _ => "Other"
    };
}

public sealed record DuplicateGroup(string Hash, long Size, List<string> Files)
{
    public string SizeText => FileItem.FormatBytes(Size);
    public string CountText => $"{Files.Count} files";
    public string ReclaimableText => FileItem.FormatBytes(Size * Math.Max(0, Files.Count - 1));
}

public sealed record VisualDuplicateGroup(string Signature, List<string> Files, int MaxHammingDistance = 0)
{
    public string CountText => $"{Files.Count} visually similar images";
    public string SimilarityText => $"Similarity: {Math.Max(0, 100 - (MaxHammingDistance * 100 / 64)):0}%";
};

public sealed record OperationRecord(DateTime Time, string Operation, string Source, string? Destination, bool Undoable);

public sealed record OrganizationProposal(string Source, string Destination, string Reason)
{
    public string FileName => Path.GetFileName(Source);
};
