using System.IO;
using System.Text;
using System.Text.Json;
using JASS.FileOrganizer.Models;

namespace JASS.FileOrganizer.Services;

public static class ReportService
{
    public static void ExportCsv(IEnumerable<FileItem> items, string path)
    {
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("FullPath,Name,Type,Extension,Size,Modified");
        foreach (var f in items)
            sw.WriteLine($"\"{f.FullPath.Replace("\"","\"\"")}\",\"{f.Name.Replace("\"","\"\"")}\",{f.Type},{f.Extension},{f.Size},\"{f.Modified:O}\"");
    }

    public static void ExportJson(IEnumerable<FileItem> items, string path)
    {
        var data = items.Select(f => new
        {
            f.FullPath, f.Name, f.Type, f.Extension, f.Size, f.Modified
        });
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ExportHtml(IEnumerable<FileItem> items, string path)
    {
        var sb = new StringBuilder("""
<!doctype html><html><head><meta charset="utf-8"><title>JASS File Organizer Report</title>
<style>body{font-family:Segoe UI,Arial;margin:30px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:7px;text-align:left}th{background:#eee}</style>
</head><body><h1>JASS File Organizer Pro Report</h1><table><tr><th>Name</th><th>Type</th><th>Size</th><th>Modified</th><th>Path</th></tr>
""");
        foreach (var f in items)
            sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(f.Name)}</td><td>{f.Type}</td><td>{f.SizeText}</td><td>{f.ModifiedText}</td><td>{System.Net.WebUtility.HtmlEncode(f.FullPath)}</td></tr>");
        sb.Append("</table></body></html>");
        File.WriteAllText(path, sb.ToString());
    }
}
