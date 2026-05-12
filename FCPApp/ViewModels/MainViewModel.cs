using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCPApp.Models;
using FCPApp.Services;
using FCPApp.Services.Config;
using FCPApp.Services.FileSystem;
using FCPApp.Services.Refresh;
using FCPApp.Services.Selection;
using FCPApp.Services.Tree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    private readonly IFileSystemService _fileSystem;
    private readonly IFolderTreeService _treeService;
    private readonly ISelectionManager _selectionManager;
    private readonly IAutoRefreshManager _autoRefresh;

    public MainViewModel(
        IFileSystemService? fileSystem = null,
        IFolderTreeService? treeService = null,
        ISelectionManager? selectionManager = null,
        IAutoRefreshManager? autoRefresh = null)
    {
        _fileSystem = fileSystem ?? new FileSystemService();
        _treeService = treeService ?? new FolderTreeService(_fileSystem);
        _selectionManager = selectionManager ?? new SelectionManager();
        _autoRefresh = autoRefresh ?? new AutoRefreshManager();

        LoadConfigAndRestore();

        if (!string.IsNullOrEmpty(RootPath)) StartAutoRefresh();
    }

    #region Commands

    [RelayCommand]
    private void ToggleSkipErrors()
    {
        SkipAllErrors = !SkipAllErrors;
        SaveConfig();
        StatusText = SkipAllErrors ? "⏭ Error Skipping: ON" : "⏭ Error Skipping: OFF";
    }

    [RelayCommand]
    private void LoadChildren(FolderTreeNode node)
    {
        if (!node.HasUnloadedChildren || node.Children.Count > 0) return;

        _treeService.LoadTree(node.FullPath, node.Children, null, 20, out _);
        node.HasUnloadedChildren = false;
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        _treeService.ApplyFilter(FolderTree, FolderFilter);
        UpdateStatusWithFilter();
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(RootPath)) return;
        var selectedPaths = _selectionManager.GetSelectedPaths(FolderTree);

        ConfigService.Save(new FolderConfig
        {
            RootPath = RootPath,
            SelectedFolderPaths = selectedPaths,
            SkipAllErrors = SkipAllErrors
        });
        StatusText = $"✅ Saved ({selectedPaths.Count} folders)";
    }

    [RelayCommand]
    private void ClearSelection()
    {
        _selectionManager.UncheckAllRecursive(FolderTree);
        UpdateSelectedFoldersTree();
        SaveConfig();
        StatusText = "✅ Selection cleared";
    }

    [RelayCommand]
    private void ExpandAll()
        => _treeService.SetExpandedRecursive(FolderTree, true);

    [RelayCommand]
    private void CollapseAll()
        => _treeService.SetExpandedRecursive(FolderTree, false);

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var toDelete = _selectionManager.GetSelectedNodes(FolderTree);
        if (toDelete.Count == 0)
        {
            StatusText = "⚠️ No folders selected";
            return;
        }

        StopAutoRefresh();
        IsProcessing = true;
        StatusText = $"🔄 Deleting {toDelete.Count} folders...";
        HasErrors = false;
        ErrorText = string.Empty;

        var deleted = 0;
        var errors = new List<string>();
        var skipped = 0;
        var alreadyDeleted = 0;

        var sortedToDelete = toDelete.OrderByDescending(n => n.Depth).ToList();

        foreach (var node in sortedToDelete)
        {
            try
            {
                if (!_fileSystem.DirectoryExists(node.FullPath))
                {
                    alreadyDeleted++;
                    continue;
                }

                if (_fileSystem.IsFolderLocked(node.FullPath))
                {
                    if (SkipAllErrors)
                    {
                        skipped++;
                        continue;
                    }

                    errors.Add($"{node.Name}: Folder is locked");
                    continue;
                }

                _fileSystem.RemoveReadOnlyAttribute(node.FullPath);

                if (await _fileSystem.DeleteDirectoryAsync(node.FullPath, true)) deleted++;
                else throw new IOException("Delete failed");
            }
            catch (UnauthorizedAccessException) when (SkipAllErrors)
            {
                skipped++;
            }
            catch (IOException) when (SkipAllErrors)
            {
                skipped++;
            }
            catch (Exception ex)
            {
                if (!SkipAllErrors) errors.Add($"{node.Name}: {ex.Message}");
                else skipped++;
            }
        }

        var expandedPaths = _treeService.GetExpandedPaths(FolderTree);
        _treeService.LoadTree(RootPath, FolderTree, null, 20, out int newCount);
        LoadedCount = newCount;
        _treeService.RestoreExpandedPaths(FolderTree, expandedPaths);

        if (!string.IsNullOrWhiteSpace(FolderFilter))
            _treeService.ApplyFilter(FolderTree, FolderFilter);

        UpdateSelectedFoldersTree();
        StartAutoRefresh();
        IsProcessing = false;

        if (errors.Count > 0)
        {
            HasErrors = true;
            ErrorText = "❌ Errors:\n" + string.Join("\n", errors.Take(20));
            if (errors.Count > 20) ErrorText += $"\n... +{errors.Count - 20} more";
            if (skipped > 0) ErrorText += $"\n⏭ Skipped: {skipped}";
        }
        else
        {
            HasErrors = false;
            ErrorText = string.Empty;
        }
        StatusText = $"✅ Deleted: {deleted} folders{(alreadyDeleted > 0 ? $"\n🗑️ Already gone: {alreadyDeleted}" : "")}{(skipped > 0 ? $"\n⏭ Skipped: {skipped}" : "")}";
    }

    #endregion

    #region Public API

    public void StartAutoRefresh()
        => _autoRefresh.Start(RefreshTreeAsync);

    public void StopAutoRefresh()
        => _autoRefresh.Stop();

    public void SetRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.DirectoryExists(path)) return;

        StopAutoRefresh();
        RootPath = path;
        _treeService.LoadTree(RootPath, FolderTree, null, 20, out int count);
        LoadedCount = count;
        SaveConfig();
        StartAutoRefresh();
    }

    public void OnWindowClosing()
        => StopAutoRefresh();

    #endregion

    #region Private Helpers

    private void LoadConfigAndRestore()
    {
        var config = ConfigService.Load();

        if (config != null && !string.IsNullOrEmpty(config.RootPath) && _fileSystem.DirectoryExists(config.RootPath))
        {
            RootPath = config.RootPath;
            SkipAllErrors = config.SkipAllErrors;
            _treeService.LoadTree(RootPath, FolderTree, config.SelectedFolderPaths, 20, out int count);
            LoadedCount = count;
            UpdateSelectedFoldersTree();
        }
    }

    private async Task RefreshTreeAsync()
    {
        if (string.IsNullOrEmpty(RootPath) || !_fileSystem.DirectoryExists(RootPath)) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var savedSelected = ConfigService.Load()?.SelectedFolderPaths?
                .Select(_fileSystem.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>();

            _treeService.RefreshTree(FolderTree, RootPath, savedSelected, out int count);
            LoadedCount = count;
            if (!string.IsNullOrWhiteSpace(FolderFilter))
                _treeService.ApplyFilter(FolderTree, FolderFilter);
            UpdateSelectedFoldersTree();
            StatusText = $"🔄 Updated: {LoadedCount} folders";
        });
    }

    private void UpdateStatusWithFilter()
    {
        var visibleCount = _treeService.CountVisibleNodes(FolderTree);
        StatusText = string.IsNullOrWhiteSpace(FolderFilter)
            ? $"Total: {LoadedCount}"
            : $"Shown: {visibleCount} of {LoadedCount} (filter: \"{FolderFilter}\")";
    }

    private void UpdateSelectedFoldersTree()
        => SelectedFoldersTree = _selectionManager.BuildSelectedTree(FolderTree);

    #endregion
}