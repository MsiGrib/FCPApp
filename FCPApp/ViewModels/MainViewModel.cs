using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCPApp.Models;
using FCPApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FCPApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _rootPath = string.Empty;
    [ObservableProperty] private ObservableCollection<FolderTreeNode> _folderTree = new();
    [ObservableProperty] private string _folderFilter = string.Empty;
    [ObservableProperty] private string _statusText = "Select the root folder to begin working.";
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private int _loadedCount;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _hasErrors;
    [ObservableProperty] private bool _skipAllErrors;
    [ObservableProperty] private ObservableCollection<FolderTreeNode> _selectedFoldersTree = new();

    private CancellationTokenSource? _refreshCts = null;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(3);

    public MainViewModel()
    {
        LoadConfigAndRestore();

        if (!string.IsNullOrEmpty(RootPath))
            StartAutoRefresh();
    }

    #region Command

    [RelayCommand]
    private void ToggleSkipErrors()
    {
        SkipAllErrors = !SkipAllErrors;
        SaveConfig();
        StatusText = SkipAllErrors
            ? "⏭ Error Skipping Mode: ON (Saved)"
            : "⏭ Error Skipping Mode: OFF (Saved)";
    }

    [RelayCommand]
    private void LoadChildren(FolderTreeNode node)
    {
        if (!node.HasUnloadedChildren || node.Children.Count > 0) return;

        try
        {
            LoadDirectoryRecursive(node.FullPath, node.Children, node.Depth, 20, null);
            node.HasUnloadedChildren = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] LoadChildren: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        ApplyFilterToTree(FolderTree, FolderFilter);
        UpdateStatusWithFilter();
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(RootPath)) return;

        var selectedPaths = GetSelectedPaths(FolderTree);

        var config = new FolderConfig
        {
            RootPath = RootPath,
            SelectedFolderPaths = selectedPaths,
            SkipAllErrors = SkipAllErrors
        };
        ConfigService.Save(config);

        StatusText = $"✅ The selection has been saved. ({selectedPaths.Count} folders).";
    }

    [RelayCommand]
    private void ClearSelection()
    {
        UncheckAllRecursive(FolderTree);
        UpdateSelectedFoldersTree();
        SaveConfig();
        StatusText = $"✅ Selection cleared";
    }

    [RelayCommand]
    private void ExpandAll()
    {
        SetExpandedRecursive(FolderTree, true);
        StatusText = $"📂 The tree is expanded ({LoadedCount} folders)";
    }

    [RelayCommand]
    private void CollapseAll()
    {
        SetExpandedRecursive(FolderTree, false);
        StatusText = $"📁 The tree is twisted";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var toDelete = GetSelectedNodes(FolderTree);

        if (toDelete.Count == 0)
        {
            StatusText = "⚠️ There are no folders selected to delete.";
            HasErrors = false;
            ErrorText = string.Empty;

            return;
        }

        StopAutoRefresh();

        IsProcessing = true;
        StatusText = $"🔄 Removal {toDelete.Count} folders...";
        HasErrors = false;
        ErrorText = string.Empty;

        var deleted = new List<string>();
        var errors = new List<string>();
        var skipped = 0;
        var alreadyDeleted = 0;

        var sortedToDelete = toDelete.OrderByDescending(n => n.Depth).ToList();

        await Task.Run(() =>
        {
            foreach (var node in sortedToDelete)
            {
                try
                {
                    if (!Directory.Exists(node.FullPath))
                    {
                        alreadyDeleted++;
                        continue;
                    }

                    if (IsFolderLocked(node.FullPath))
                    {
                        if (SkipAllErrors)
                        {
                            skipped++;
                            continue;
                        }

                        errors.Add($"{node.Name}: The folder is locked");
                        continue;
                    }

                    RemoveReadOnlyAttribute(node.FullPath);

                    Directory.Delete(node.FullPath, true);
                    deleted.Add(node.Name);
                }
                catch (UnauthorizedAccessException)
                {
                    if (SkipAllErrors)
                    {
                        skipped++;
                        continue;
                    }
                    errors.Add($"{node.Name}: No rights. Run as administrator.");
                }
                catch (IOException)
                {
                    if (SkipAllErrors)
                    {
                        skipped++;
                        continue;
                    }
                    errors.Add($"{node.Name}: The file is being used by another program.");
                }
                catch (Exception ex)
                {
                    if (SkipAllErrors)
                    {
                        skipped++;
                        continue;
                    }
                    errors.Add($"{node.Name}: {ex.Message}");
                }
            }
        });

        IsProcessing = false;

        var expandedPaths = GetExpandedPaths(FolderTree);
        LoadTree(null);
        RestoreExpandedPaths(FolderTree, expandedPaths);

        if (!string.IsNullOrWhiteSpace(FolderFilter))
            ApplyFilterToTree(FolderTree, FolderFilter);

        UpdateSelectedFoldersTree();
        StartAutoRefresh();

        if (errors.Count > 0)
        {
            HasErrors = true;
            ErrorText = "❌ Errors when deleting:\n\n" + string.Join("\n", errors.Take(20));

            if (errors.Count > 20) ErrorText += $"\n... and more {errors.Count - 20} errors";
            if (skipped > 0) ErrorText += $"\n\n⏭ Missed: {skipped} folders";
        }
        else
        {
            HasErrors = false;
            ErrorText = string.Empty;
        }

        var resultMsg = $"✅ Deleted: {deleted.Count} folders";
        if (deleted.Any()) resultMsg += $"\n📁 {string.Join(", ", deleted.Take(10))}{(deleted.Count > 10 ? "..." : "")}";
        if (alreadyDeleted > 0) resultMsg += $"\n🗑️ More {alreadyDeleted} removed as part of the parent";
        if (skipped > 0) resultMsg += $"\n⏭ Missed: {skipped}";

        StatusText = resultMsg;
    }

    #endregion

    #region Public

    public void StartAutoRefresh()
    {
        StopAutoRefresh();

        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_refreshInterval, token);
                    if (!string.IsNullOrEmpty(RootPath) && Directory.Exists(RootPath))
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RefreshTree);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] AutoRefresh: {ex.Message}");
                }
            }
        }, token);
    }

    public void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    public void SetRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        StopAutoRefresh();
        RootPath = path;

        LoadTree(null);

        SaveConfig();
        StartAutoRefresh();
    }

    public void LoadTree(List<string>? preSelectedPaths, int maxDepth = 20)
    {
        FolderTree.Clear();
        LoadedCount = 0;

        try
        {
            LoadDirectoryRecursive(RootPath, FolderTree, 0, maxDepth, preSelectedPaths);
            StatusText = $"✅ Uploaded {LoadedCount} folders. Enter a name to filter by.";

            UpdateSelectedFoldersTree();
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Loading error: {ex.Message}";
        }
    }

    public void OnWindowClosing() => StopAutoRefresh();

    #endregion

    #region Private

    private void LoadConfigAndRestore()
    {
        var config = ConfigService.Load();

        if (config != null && !string.IsNullOrEmpty(config.RootPath) && Directory.Exists(config.RootPath))
        {
            RootPath = config.RootPath;
            SkipAllErrors = config.SkipAllErrors;

            LoadTree(config.SelectedFolderPaths);
            UpdateSelectedFoldersTree();
        }
    }

    private void RefreshTree()
    {
        if (string.IsNullOrEmpty(RootPath) || !Directory.Exists(RootPath)) return;

        SmartRefresh(FolderTree, RootPath);

        if (!string.IsNullOrWhiteSpace(FolderFilter))
            ApplyFilterToTree(FolderTree, FolderFilter);

        UpdateSelectedFoldersTree();
        StatusText = $"🔄 The tree has been updated: {LoadedCount} folders";
    }

    private void SmartRefresh(ObservableCollection<FolderTreeNode> nodes, string currentPath)
    {
        var config = ConfigService.Load();
        var savedSelectedPaths = config?.SelectedFolderPaths?
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        var diskDirs = new Dictionary<string, string>();
        try
        {
            foreach (var dir in Directory.GetDirectories(currentPath))
                diskDirs[dir] = Path.GetFileName(dir);
        }
        catch
        {
            return;
        }

        var existingNodes = nodes.ToDictionary(n => NormalizePath(n.FullPath));

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            var normalizedNodePath = NormalizePath(node.FullPath);

            if (!diskDirs.ContainsKey(node.FullPath) && !diskDirs.Keys.Any(k => NormalizePath(k) == normalizedNodePath))
            {
                SmartRefresh(node.Children, node.FullPath);
                nodes.RemoveAt(i);
                LoadedCount = Math.Max(0, LoadedCount - 1);
            }
        }

        foreach (var (dirPath, dirName) in diskDirs)
        {
            var normalizedDirPath = NormalizePath(dirPath);
            bool exists = existingNodes.ContainsKey(normalizedDirPath);

            if (!exists)
            {
                bool shouldBeChecked = savedSelectedPaths.Contains(normalizedDirPath);

                var newNode = new FolderTreeNode(dirName, dirPath, nodes.FirstOrDefault()?.Depth ?? 0)
                {
                    IsExpanded = false,
                    IsChecked = shouldBeChecked,
                    HasUnloadedChildren = Directory.EnumerateDirectories(dirPath).Any(),
                    IsHighlighted = shouldBeChecked
                };

                nodes.Add(newNode);
                LoadedCount++;

                _ = Task.Delay(2000).ContinueWith(_ => newNode.IsHighlighted = false,
                    TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        foreach (var node in nodes)
        {
            if (Directory.Exists(node.FullPath))
                SmartRefresh(node.Children, node.FullPath);
        }
    }

    private void LoadDirectoryRecursive(string path, ObservableCollection<FolderTreeNode> collection,
        int currentDepth, int maxDepth, List<string>? preSelectedPaths)
    {
        if (currentDepth >= maxDepth) return;

        string[] subDirs;

        try
        {
            subDirs = Directory.GetDirectories(path);
        }
        catch
        {
            return;
        }

        var config = ConfigService.Load();
        var savedSelectedPaths = config?.SelectedFolderPaths?
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        foreach (var dir in subDirs)
        {
            var name = Path.GetFileName(dir);
            var normalizedDir = NormalizePath(dir);

            var isChecked = savedSelectedPaths.Contains(normalizedDir) ||
                (preSelectedPaths?.Any(p => NormalizePath(p) == normalizedDir) == true);

            var node = new FolderTreeNode(name, dir, currentDepth + 1)
            {
                IsChecked = isChecked,
                IsExpanded = currentDepth < 2
            };
            collection.Add(node);
            LoadedCount++;

            if (currentDepth + 1 < 5)
            {
                LoadDirectoryRecursive(dir, node.Children, currentDepth + 1, maxDepth, preSelectedPaths);
                node.HasUnloadedChildren = node.Children.Count > 0;
            }
            else
            {
                try
                {
                    node.HasUnloadedChildren = Directory.EnumerateDirectories(dir).Any();
                }
                catch
                {
                    node.HasUnloadedChildren = false;
                }
            }
        }
    }

    private void ApplyFilterToTree(ObservableCollection<FolderTreeNode> nodes, string filter)
    {
        foreach (var node in nodes)
        {
            ApplyFilterToTree(node.Children, filter);

            bool selfMatches = string.IsNullOrWhiteSpace(filter) ||
                node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            bool childrenVisible = node.Children.Any(c => c.IsVisible);

            bool shouldBeVisible = string.IsNullOrWhiteSpace(filter) || selfMatches || childrenVisible;

            if (node.IsVisible != shouldBeVisible)
                node.IsVisible = shouldBeVisible;
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            foreach (var node in nodes)
            {
                if (!node.Children.Any(c => c.IsVisible) &&
                        !node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    node.IsVisible = false;
            }
        }
    }

    private void UpdateStatusWithFilter()
    {
        var visibleCount = CountVisibleNodes(FolderTree);
        StatusText = string.IsNullOrWhiteSpace(FolderFilter)
            ? $"Total folders: {LoadedCount}"
            : $"Shown: {visibleCount} from {LoadedCount} (filter: \"{FolderFilter}\")";
    }

    private int CountVisibleNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        int count = 0;

        foreach (var node in nodes)
        {
            if (node.IsVisible) count++;
            count += CountVisibleNodes(node.Children);
        }

        return count;
    }

    private List<string> GetSelectedPaths(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new List<string>();

        foreach (var node in nodes)
        {
            if (node.IsChecked) result.Add(node.FullPath);

            result.AddRange(GetSelectedPaths(node.Children));
        }

        return result;
    }

    private void UncheckAllRecursive(ObservableCollection<FolderTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsChecked) node.IsChecked = false;

            UncheckAllRecursive(node.Children);
        }
    }

    private void UpdateSelectedFoldersTree()
    {
        SelectedFoldersTree.Clear();

        var selectedNodes = GetSelectedNodesWithChildren(FolderTree);

        foreach (var selected in selectedNodes)
        {
            var displayNode = CreateFlatDisplayNode(selected, true);
            SelectedFoldersTree.Add(displayNode);

            AddSelectedChildrenOnly(selected, displayNode);
        }
    }
    private List<FolderTreeNode> GetSelectedNodesWithChildren(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new List<FolderTreeNode>();

        foreach (var node in nodes)
        {
            if (node.IsChecked) result.Add(node);

            result.AddRange(GetSelectedNodesWithChildren(node.Children));
        }

        return result;
    }

    private void AddSelectedChildrenOnly(FolderTreeNode source, FolderTreeNode displayParent)
    {
        foreach (var child in source.Children)
        {
            if (child.IsChecked)
            {
                var displayChild = CreateFlatDisplayNode(child, true);
                displayParent.Children.Add(displayChild);

                AddSelectedChildrenOnly(child, displayChild);
            }
        }
    }

    private FolderTreeNode CreateFlatDisplayNode(FolderTreeNode source, bool isChecked)
    {
        var displayNode = new FolderTreeNode(source.Name, source.FullPath, source.Depth)
        {
            IsChecked = isChecked,
            IsVisible = true,
            IsExpanded = true,
            DisplayPath = source.FullPath,
        };
        return displayNode;
    }

    private void CollectSelectedWithPath(ObservableCollection<FolderTreeNode> nodes,
        List<FolderTreeNode> currentPath, List<(FolderTreeNode, List<FolderTreeNode>)> result)
    {
        foreach (var node in nodes)
        {
            var newPath = new List<FolderTreeNode>(currentPath) { node };

            if (node.IsChecked) result.Add((node, newPath));

            CollectSelectedWithPath(node.Children, newPath, result);
        }
    }

    private void BuildSelectedSubTree(List<(FolderTreeNode node, List<FolderTreeNode> path)> items,
        FolderTreeNode displayParent, int depthIndex)
    {
        var byCurrentDepth = items
            .Where(x => x.path.Count > depthIndex)
            .GroupBy(x => x.path[depthIndex]);

        foreach (var group in byCurrentDepth)
        {
            var sourceNode = group.Key;
            var isFinalSelected = sourceNode.IsChecked;

            var displayNode = CreateDisplayNode(sourceNode, isFinalSelected, displayParent);

            displayParent.Children.Add(displayNode);

            BuildSelectedSubTree(group.ToList(), displayNode, depthIndex + 1);
        }
    }

    private FolderTreeNode CreateDisplayNode(FolderTreeNode source, bool isChecked, FolderTreeNode? displayParent = null)
    {
        var displayNode = new FolderTreeNode(source.Name, source.FullPath, source.Depth, displayParent)
        {
            IsChecked = isChecked,
            IsVisible = true,
            IsExpanded = true,
            DisplayPath = source.FullPath,
        };

        return displayNode;
    }

    private void AddSelectedToTree(ObservableCollection<FolderTreeNode> source, ObservableCollection<FolderTreeNode> target)
    {
        foreach (var node in source)
        {
            if (node.IsChecked)
            {
                var displayNode = new FolderTreeNode(node.Name, node.FullPath, node.Depth)
                {
                    IsChecked = true,
                    IsVisible = true,
                    IsExpanded = true
                };

                target.Add(displayNode);
                AddSelectedToTree(node.Children, displayNode.Children);
            }
            else AddSelectedToTree(node.Children, target);
        }
    }

    private void SetExpandedRecursive(ObservableCollection<FolderTreeNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            if (isExpanded && node.HasUnloadedChildren && node.Children.Count == 0)
                LoadChildren(node);

            node.IsExpanded = isExpanded;
            SetExpandedRecursive(node.Children, isExpanded);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            var fullPath = Path.GetFullPath(path);

            var normalized = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? fullPath.ToLowerInvariant()
                : fullPath;

            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            normalized = normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return normalized;
        }
        catch
        {
            return path;
        }
    }

    private bool IsFolderLocked(string path)
    {
        try
        {
            var testFile = Path.Combine(path, ".test_delete_permission");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return true;
        }
    }

    private List<FolderTreeNode> GetSelectedNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new List<FolderTreeNode>();

        foreach (var node in nodes)
        {
            if (node.IsChecked) result.Add(node);

            result.AddRange(GetSelectedNodes(node.Children));
        }

        return result;
    }

    private void RemoveReadOnlyAttribute(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            dirInfo.Attributes = FileAttributes.Normal;

            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(path))
                RemoveReadOnlyAttribute(dir);
        }
        catch { }
    }

    private HashSet<string> GetExpandedPaths(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new HashSet<string>();

        foreach (var node in nodes)
        {
            if (node.IsExpanded) result.Add(node.FullPath);

            foreach (var p in GetExpandedPaths(node.Children)) result.Add(p);
        }

        return result;
    }

    private void RestoreExpandedPaths(ObservableCollection<FolderTreeNode> nodes, HashSet<string> expandedPaths)
    {
        foreach (var node in nodes)
        {
            if (expandedPaths.Contains(node.FullPath)) node.IsExpanded = true;

            RestoreExpandedPaths(node.Children, expandedPaths);
        }
    }

    #endregion
}