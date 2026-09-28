using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using JASS.FileOrganizer.Models;
using JASS.FileOrganizer.Services;
using JASS.FileOrganizer.ViewModels;
using Forms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace JASS.FileOrganizer.Views;

public partial class MainWindow : Window
{
    readonly MainViewModel vm = new(); FileItem? selected;
    public MainWindow() { InitializeComponent(); DataContext = vm; }

    async void SelectFolder_Click(object sender, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog { Description = "Select a folder to scan" }; if (d.ShowDialog() == Forms.DialogResult.OK) await vm.ScanAsync(d.SelectedPath); }
    async void Scan_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(vm.SelectedFolder)) { SelectFolder_Click(sender, e); return; } await vm.ScanAsync(vm.SelectedFolder); }
    async void Duplicates_Click(object sender, RoutedEventArgs e) => await vm.FindDuplicatesAsync();
    async void VisualDuplicates_Click(object sender, RoutedEventArgs e) => await vm.FindVisualDuplicatesAsync();
    void Dashboard_Click(object sender, RoutedEventArgs e) => WpfMessageBox.Show(vm.DashboardText, "JASS File Intelligence Dashboard");

    void VisualGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VisualGroupsList.SelectedItem is not VisualDuplicateGroup group) return;
        VisualGroupInfo.Text = $"{group.Files.Count} files\n{group.SimilarityText}\nSignature: {group.Signature}";
        VisualFilesList.ItemsSource = group.Files;
        VisualFilesList.SelectedIndex = 0;
    }

    void VisualFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VisualFilesList.SelectedItem is not string path) return;
        VisualPreview.Source = null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.UriSource = new Uri(path); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.DecodePixelWidth = 700; bmp.EndInit(); bmp.Freeze();
            VisualPreview.Source = bmp;
        }
        catch { }
    }

    void VisualOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (VisualFilesList.SelectedItem is not string path) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
    }

    void VisualOpenSelected_Click(object sender, RoutedEventArgs e)
    {
        if (VisualFilesList.SelectedItem is not string path) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }
    void History_Click(object sender, RoutedEventArgs e) { if (vm.History.Count == 0) WpfMessageBox.Show("No operations recorded yet.", "History"); else WpfMessageBox.Show(string.Join(Environment.NewLine, vm.History.Take(20).Select(h => $"{h.Time:yyyy-MM-dd HH:mm:ss}  {h.Operation}  {h.Source}")), "Recent Operations"); }

    void BatchRename_Click(object sender, RoutedEventArgs e)
    {
        var items = FilesGrid.SelectedItems.OfType<FileItem>().ToList();
        if (items.Count == 0) { WpfMessageBox.Show("Select one or more files first.", "Batch Rename"); return; }
        var pattern = Microsoft.VisualBasic.Interaction.InputBox("Pattern: use {n}, {name}, {ext}\nExample: Photo_{n}", "Batch Rename", "File_{n}");
        if (string.IsNullOrWhiteSpace(pattern)) return;
        try
        {
            int n = 1;
            foreach (var item in items)
            {
                var ext = Path.GetExtension(item.Name);
                var baseName = Path.GetFileNameWithoutExtension(item.Name);
                var newBase = pattern.Replace("{n}", n.ToString("000")).Replace("{name}", baseName).Replace("{ext}", ext.TrimStart('.'));
                if (Path.GetExtension(newBase).Length == 0) newBase += ext;
                vm.Rename(item, newBase); vm.AddHistory("Rename", item.FullPath, Path.Combine(Path.GetDirectoryName(item.FullPath)!, newBase), true); n++;
            }
            if (!string.IsNullOrWhiteSpace(vm.SelectedFolder)) _ = vm.ScanAsync(vm.SelectedFolder);
        }
        catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Batch Rename failed"); }
    }

    void PlanOrganization_Click(object sender, RoutedEventArgs e) { vm.BuildOrganizationPlan(); if (vm.OrganizationPlan.Count == 0) WpfMessageBox.Show("No organization moves are currently proposed.", "Organization Planner"); }
    async void ApplyPlan_Click(object sender, RoutedEventArgs e)
    {
        if (vm.OrganizationPlan.Count == 0) { WpfMessageBox.Show("Build an organization plan first.", "Organization"); return; }
        if (WpfMessageBox.Show($"Apply {vm.OrganizationPlan.Count} proposed moves?\n\nFiles are moved into type-based subfolders.", "Confirm Organization", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Warning) != WpfMessageBoxResult.Yes) return;
        try
        {
            var plan = vm.OrganizationPlan.ToList();
            vm.Operations.ApplyOrganization(plan, (s,d) => vm.AddHistory("Move", s, d, true));
            vm.OrganizationPlan.Clear(); await vm.ScanAsync(vm.SelectedFolder);
        }
        catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Organization failed"); }
    }

    void DuplicateGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        vm.SelectedDuplicateFiles.Clear();
        if (DuplicateGroupsList.SelectedItem is DuplicateGroup group)
            foreach (var file in group.Files) vm.SelectedDuplicateFiles.Add(file);
        DuplicateGroupInfo.Text = DuplicateGroupsList.SelectedItem is DuplicateGroup g
            ? $"{g.CountText} • {g.SizeText} each • {g.ReclaimableText} reclaimable\nSHA-256: {g.Hash}"
            : "Select a duplicate group to inspect its files.";
    }

    void DuplicateFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var path = DuplicateFilesList.SelectedItem as string;
        DuplicateSelectedPath.Text = path ?? "No file selected";
    }

    void OpenDuplicateLocation_Click(object sender, RoutedEventArgs e)
    {
        var path = DuplicateFilesList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    void RecycleDuplicateSelected_Click(object sender, RoutedEventArgs e)
    {
        var files = DuplicateFilesList.SelectedItems.OfType<string>().Where(File.Exists).ToList();
        if (files.Count == 0) { WpfMessageBox.Show("Select one or more duplicate files first.", "Duplicate Cleanup"); return; }
        if (WpfMessageBox.Show($"Send {files.Count} selected duplicate file(s) to the Recycle Bin?\n\nThis can be undone from the Recycle Bin.", "Confirm Duplicate Cleanup", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Warning) != WpfMessageBoxResult.Yes) return;
        try
        {
            foreach (var file in files) { vm.Operations.DeleteToRecycleBin(file); vm.AddHistory("Recycle duplicate", file, null, false); }
            if (!string.IsNullOrWhiteSpace(vm.SelectedFolder)) _ = vm.ScanAsync(vm.SelectedFolder);
            WpfMessageBox.Show($"{files.Count} file(s) sent to the Recycle Bin.", "Duplicate Cleanup");
        }
        catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Duplicate cleanup failed"); }
    }

    void KeepDuplicateSelected_Click(object sender, RoutedEventArgs e)
    {
        var keep = DuplicateFilesList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(keep) || !File.Exists(keep)) { WpfMessageBox.Show("Select exactly one file to keep.", "Keep Duplicate"); return; }
        if (DuplicateGroupsList.SelectedItem is not DuplicateGroup group) return;
        var remove = group.Files.Where(f => !string.Equals(f, keep, StringComparison.OrdinalIgnoreCase) && File.Exists(f)).ToList();
        if (remove.Count == 0) return;
        if (WpfMessageBox.Show($"Keep this file?\n\n{keep}\n\nRecycle the other {remove.Count} identical copies?", "Keep One Copy", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Warning) != WpfMessageBoxResult.Yes) return;
        try
        {
            foreach (var file in remove) { vm.Operations.DeleteToRecycleBin(file); vm.AddHistory("Recycle duplicate", file, null, false); }
            if (!string.IsNullOrWhiteSpace(vm.SelectedFolder)) _ = vm.ScanAsync(vm.SelectedFolder);
            WpfMessageBox.Show($"Kept the selected copy and recycled {remove.Count} duplicate(s).", "Keep One Copy");
        }
        catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Duplicate cleanup failed"); }
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => vm.Cancel();
    void FilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selected = FilesGrid.SelectedItem as FileItem; if (selected == null) return;
        InspectorName.Text = selected.Name;
        InspectorInfo.Text = $"Type: {selected.Type}\nExtension: {selected.Extension}\nSize: {selected.SizeText}\nModified: {selected.ModifiedText}\nCreated: {selected.CreatedText}\nAttributes: {selected.AttributesText}\n\n{selected.FullPath}";
        PreviewImage.Source = null;
        if (selected.Type == "Image") try { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.UriSource = new Uri(selected.FullPath); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.DecodePixelWidth = 700; bmp.EndInit(); bmp.Freeze(); PreviewImage.Source = bmp; } catch { }
    }
    void Open_Click(object sender, RoutedEventArgs e) { if (selected != null) Process.Start(new ProcessStartInfo(selected.FullPath) { UseShellExecute = true }); }
    void CopyPath_Click(object sender, RoutedEventArgs e) { if (selected != null) WpfClipboard.SetText(selected.FullPath); }
    async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (selected == null) return; var input = Microsoft.VisualBasic.Interaction.InputBox("New file name:", "Rename", selected.Name); if (string.IsNullOrWhiteSpace(input) || input == selected.Name) return;
        try { var dest = Path.Combine(Path.GetDirectoryName(selected.FullPath)!, input); vm.Rename(selected, input); vm.AddHistory("Rename", selected.FullPath, dest, true); await vm.ScanAsync(vm.SelectedFolder); } catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Rename failed"); }
    }
    async void Recycle_Click(object sender, RoutedEventArgs e)
    {
        if (selected == null) return; if (WpfMessageBox.Show($"Send '{selected.Name}' to the Recycle Bin?", "Confirm", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Warning) != WpfMessageBoxResult.Yes) return;
        try { vm.Delete(selected); vm.AddHistory("Recycle", selected.FullPath, null, false); await vm.ScanAsync(vm.SelectedFolder); } catch (Exception ex) { WpfMessageBox.Show(ex.Message, "Recycle failed"); }
    }
}
