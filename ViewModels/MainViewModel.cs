using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using JASS.FileOrganizer.Models;
using JASS.FileOrganizer.Services;

namespace JASS.FileOrganizer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    readonly FileScanner scanner = new();
    readonly HashService hashService = new();
    public readonly FileOperations Operations = new();
    CancellationTokenSource? cts;
    string searchText = "", selectedType = "All", selectedFolder = "";
    string minSize = "", maxSize = "", daysOld = "";
    bool isBusy;

    public ObservableCollection<FileItem> Files { get; } = [];
    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = [];
    public ObservableCollection<string> SelectedDuplicateFiles { get; } = [];
    public ObservableCollection<VisualDuplicateGroup> VisualDuplicateGroups { get; } = [];
    public ObservableCollection<OperationRecord> History { get; } = [];
    public ObservableCollection<OrganizationProposal> OrganizationPlan { get; } = [];
    public ICollectionView FilesView { get; }
    public ObservableCollection<string> TypeFilters { get; } = ["All", "Images", "Videos", "Audio", "Documents", "PDF", "Archives", "Code", "Data", "Projects", "Other"];

    public string SelectedFolder { get => selectedFolder; set { selectedFolder = value; OnChanged(); } }
    public string SearchText { get => searchText; set { searchText = value; OnChanged(); FilesView.Refresh(); } }
    public string SelectedType { get => selectedType; set { selectedType = value; OnChanged(); FilesView.Refresh(); } }
    public string MinSize { get => minSize; set { minSize = value; OnChanged(); FilesView.Refresh(); } }
    public string MaxSize { get => maxSize; set { maxSize = value; OnChanged(); FilesView.Refresh(); } }
    public string DaysOld { get => daysOld; set { daysOld = value; OnChanged(); FilesView.Refresh(); } }
    public bool IsBusy { get => isBusy; private set { isBusy = value; OnChanged(); } }
    public string Status { get => status; private set { status = value; OnChanged(); } } string status = "Ready";
    public string Stats { get => stats; private set { stats = value; OnChanged(); } } string stats = "No folder scanned.";
    public string DashboardText => $"Files: {Files.Count:N0}\nTotal: {FileItem.FormatBytes(Files.Sum(x => x.Size))}\nImages: {Files.Count(x => x.Type == "Image"):N0}\nVideos: {Files.Count(x => x.Type == "Video"):N0}\nAudio: {Files.Count(x => x.Type == "Audio"):N0}\nDocuments/PDF: {Files.Count(x => x.Type is "Document" or "PDF"):N0}\nArchives: {Files.Count(x => x.Type == "Archive"):N0}\nCode: {Files.Count(x => x.Type == "Code"):N0}\nData: {Files.Count(x => x.Type == "Data"):N0}\nProjects: {Files.Count(x => x.Type == "Project"):N0}";

    public MainViewModel() { FilesView = CollectionViewSource.GetDefaultView(Files); FilesView.Filter = FilterFile; }

    bool FilterFile(object obj)
    {
        if (obj is not FileItem f) return false;
        if (SelectedType != "All")
        {
            var ok = SelectedType switch { "Images" => f.Type == "Image", "Videos" => f.Type == "Video", "Documents" => f.Type == "Document", "Archives" => f.Type == "Archive", "Projects" => f.Type == "Project", _ => f.Type.Equals(SelectedType.TrimEnd('s'), StringComparison.OrdinalIgnoreCase) };
            if (!ok) return false;
        }
        if (!string.IsNullOrWhiteSpace(SearchText) && !f.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !f.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return false;
        if (double.TryParse(MinSize, out var min) && f.Size < min * 1024 * 1024) return false;
        if (double.TryParse(MaxSize, out var max) && f.Size > max * 1024 * 1024) return false;
        if (int.TryParse(DaysOld, out var days) && f.Modified > DateTime.Now.AddDays(-days)) return false;
        return true;
    }

    public async Task ScanAsync(string folder)
    {
        cts?.Cancel(); cts = new(); IsBusy = true; Status = "Scanning..."; Files.Clear(); DuplicateGroups.Clear(); VisualDuplicateGroups.Clear(); OrganizationPlan.Clear();
        try
        {
            var result = await scanner.ScanAsync(folder, new Progress<int>(p => Status = $"Scanning... {p}%"), cts.Token);
            foreach (var f in result.Files.OrderBy(f => f.Name)) Files.Add(f);
            var largest = result.Files.OrderByDescending(f => f.Size).Take(10).Sum(f => f.Size);
            Stats = $"{Files.Count:N0} files • {FileItem.FormatBytes(result.TotalBytes)} • {result.FolderCount:N0} folders • Largest 10: {FileItem.FormatBytes(largest)}";
            SelectedFolder = folder; Status = $"Scan complete • {Files.Count:N0} files"; FilesView.Refresh(); OnChanged(nameof(DashboardText));
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        finally { IsBusy = false; }
    }

    public async Task FindDuplicatesAsync()
    {
        if (Files.Count == 0) return; IsBusy = true; Status = "Finding exact duplicates..."; DuplicateGroups.Clear();
        try { foreach (var g in await hashService.FindDuplicatesAsync(Files.Select(x => x.FullPath), new Progress<int>(p => Status = $"Hashing... {p}%"), cts?.Token ?? CancellationToken.None)) DuplicateGroups.Add(g); Status = $"Exact duplicate scan complete • {DuplicateGroups.Count} groups"; }
        finally { IsBusy = false; }
    }

    public async Task FindVisualDuplicatesAsync()
    {
        if (Files.Count == 0) return; IsBusy = true; Status = "Finding visually similar images..."; VisualDuplicateGroups.Clear();
        try { foreach (var g in await hashService.FindVisualDuplicatesAsync(Files.Select(x => x.FullPath), new Progress<int>(p => Status = $"Image analysis... {p}%"), cts?.Token ?? CancellationToken.None)) VisualDuplicateGroups.Add(g); Status = $"Visual analysis complete • {VisualDuplicateGroups.Count} groups"; }
        finally { IsBusy = false; }
    }

    public void BuildOrganizationPlan()
    {
        OrganizationPlan.Clear(); if (string.IsNullOrWhiteSpace(SelectedFolder)) return;
        foreach (var f in Files.Where(x => x.Type != "Unknown"))
        {
            var dest = Path.Combine(SelectedFolder, FileTypeClassifier.OrganizationFolder(f.Type), f.Name);
            if (!string.Equals(Path.GetFullPath(f.FullPath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) OrganizationPlan.Add(new OrganizationProposal(f.FullPath, dest, $"Organize {f.Type} files"));
        }
        Status = $"Organization plan ready • {OrganizationPlan.Count} proposed moves";
    }

    public void Cancel() => cts?.Cancel();
    public void AddHistory(string op, string src, string? dest, bool undoable) => History.Insert(0, new OperationRecord(DateTime.Now, op, src, dest, undoable));
    public void Rename(FileItem item, string newName) => Operations.Rename(item.FullPath, newName);
    public void Delete(FileItem item) => Operations.DeleteToRecycleBin(item.FullPath);

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
