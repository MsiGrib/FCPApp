using FCPApp.Models;
using FCPApp.Services.Config;
using FCPApp.Services.FileSystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FCPApp.Services.Tree;

public class FolderTreeService : IFolderTreeService
{
    private readonly IFileSystemService _fileSystem;

    public FolderTreeService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void LoadTree(string rootPath, ObservableCollection<FolderTreeNode> collection,
        List<string>? preSelectedPaths, int maxDepth, out int loadedCount)
    {
        collection.Clear();
        loadedCount = 0;
        LoadDirectoryRecursive(rootPath, collection, 0, maxDepth, preSelectedPaths, ref loadedCount);
    }

    private void LoadDirectoryRecursive(string path, ObservableCollection<FolderTreeNode> collection,
        int currentDepth, int maxDepth, List<string>? preSelectedPaths, ref int loadedCount)
    {
        if (currentDepth >= maxDepth) return;

        var subDirs = _fileSystem.GetDirectories(path);
        var savedSelectedPaths = ConfigService.Load()?.SelectedFolderPaths?
            .Select(_fileSystem.NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        foreach (var dir in subDirs)
        {
            var name = Path.GetFileName(dir);
            var normalizedDir = _fileSystem.NormalizePath(dir);
            var isChecked = savedSelectedPaths.Contains(normalizedDir) ||
                (preSelectedPaths?.Any(p => _fileSystem.NormalizePath(p) == normalizedDir) == true);

            var node = new FolderTreeNode(name, dir, currentDepth + 1)
            {
                IsChecked = isChecked,
                IsExpanded = currentDepth < 2
            };
            collection.Add(node);
            loadedCount++;

            if (currentDepth + 1 < 5)
            {
                LoadDirectoryRecursive(dir, node.Children, currentDepth + 1, maxDepth, preSelectedPaths, ref loadedCount);
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

    public void RefreshTree(ObservableCollection<FolderTreeNode> nodes, string currentPath,
        ISet<string> savedSelectedPaths, out int loadedCount)
    {
        var diskDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var dir in _fileSystem.GetDirectories(currentPath))
                diskDirs[dir] = Path.GetFileName(dir);
        }
        catch
        {
            loadedCount = 0;
            return;
        }

        var existingNodes = nodes.ToDictionary(n => _fileSystem.NormalizePath(n.FullPath));

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            var normalizedPath = _fileSystem.NormalizePath(node.FullPath);

            if (!diskDirs.ContainsKey(node.FullPath) && !diskDirs.Keys.Any(k => _fileSystem.NormalizePath(k) == normalizedPath))
            {
                RefreshTree(node.Children, node.FullPath, savedSelectedPaths, out _);
                nodes.RemoveAt(i);
            }
        }

        foreach (var (dirPath, dirName) in diskDirs)
        {
            var normalizedDirPath = _fileSystem.NormalizePath(dirPath);
            if (!existingNodes.ContainsKey(normalizedDirPath))
            {
                bool shouldBeChecked = savedSelectedPaths.Contains(normalizedDirPath);
                var newNode = new FolderTreeNode(dirName, dirPath, nodes.FirstOrDefault()?.Depth ?? 0)
                {
                    IsExpanded = false,
                    IsChecked = shouldBeChecked,
                    HasUnloadedChildren = _fileSystem.GetDirectories(dirPath).Any(),
                    IsHighlighted = shouldBeChecked
                };
                nodes.Add(newNode);

                _ = Task.Delay(2000).ContinueWith(_ => newNode.IsHighlighted = false,
                    TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        foreach (var node in nodes)
        {
            if (_fileSystem.DirectoryExists(node.FullPath))
                RefreshTree(node.Children, node.FullPath, savedSelectedPaths, out _);
        }

        loadedCount = CountNodes(nodes);
    }

    private int CountNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        int count = nodes.Count;

        foreach (var node in nodes) count += CountNodes(node.Children);

        return count;
    }

    public void ApplyFilter(ObservableCollection<FolderTreeNode> nodes, string filter)
    {
        foreach (var node in nodes)
        {
            ApplyFilter(node.Children, filter);
            bool selfMatches = string.IsNullOrWhiteSpace(filter) ||
                node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            bool childrenVisible = node.Children.Any(c => c.IsVisible);
            bool shouldBeVisible = string.IsNullOrWhiteSpace(filter) || selfMatches || childrenVisible;

            if (node.IsVisible != shouldBeVisible) node.IsVisible = shouldBeVisible;
        }
    }

    public int CountVisibleNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        int count = 0;

        foreach (var node in nodes)
        {
            if (node.IsVisible) count++;
            count += CountVisibleNodes(node.Children);
        }

        return count;
    }

    public void SetExpandedRecursive(ObservableCollection<FolderTreeNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            if (isExpanded && node.HasUnloadedChildren && node.Children.Count == 0)
            {
                int dummyCount = 0;
                LoadDirectoryRecursive(node.FullPath, node.Children, node.Depth + 1, 20, null, ref dummyCount);
                node.HasUnloadedChildren = false;
            }
            node.IsExpanded = isExpanded;
            SetExpandedRecursive(node.Children, isExpanded);
        }
    }

    public HashSet<string> GetExpandedPaths(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new HashSet<string>();

        foreach (var node in nodes)
        {
            if (node.IsExpanded) result.Add(node.FullPath);
            foreach (var p in GetExpandedPaths(node.Children)) result.Add(p);
        }

        return result;
    }

    public void RestoreExpandedPaths(ObservableCollection<FolderTreeNode> nodes, HashSet<string> expandedPaths)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expandedPaths.Contains(node.FullPath);
            RestoreExpandedPaths(node.Children, expandedPaths);
        }
    }
}