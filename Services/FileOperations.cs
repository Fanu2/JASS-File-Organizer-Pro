using System.IO;
using Microsoft.VisualBasic.FileIO;
using JASS.FileOrganizer.Models;

namespace JASS.FileOrganizer.Services;

public sealed class FileOperations
{
    public void Move(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite: false);
    }
    public void Copy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
    }
    public void DeleteToRecycleBin(string path) => FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    public void Rename(string source, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Invalid file name.");
        var destination = Path.Combine(Path.GetDirectoryName(source)!, newName);
        if (File.Exists(destination)) throw new IOException("A file with that name already exists.");
        File.Move(source, destination);
    }
    public void ApplyOrganization(IEnumerable<OrganizationProposal> proposals, Action<string,string>? journal = null)
    {
        foreach (var p in proposals)
        {
            if (!File.Exists(p.Source) || File.Exists(p.Destination)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(p.Destination)!);
            File.Move(p.Source, p.Destination);
            journal?.Invoke(p.Source, p.Destination);
        }
    }
}
