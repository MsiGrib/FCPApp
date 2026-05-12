using FCPApp.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FCPApp.Services.Tree;

public interface IFolderTreeService
{
    public void LoadTree(string rootPath, ObservableCollection<FolderTreeNode> collection,
        List<string>? preSelectedPaths, int maxDepth, out int loadedCount);

    public void RefreshTree(ObservableCollection<FolderTreeNode> nodes, string currentPath,
        ISet<string> savedSelectedPaths, out int loadedCount);

    public void ApplyFilter(ObservableCollection<FolderTreeNode> nodes, string filter);
    public int CountVisibleNodes(ObservableCollection<FolderTreeNode> nodes);
    public void SetExpandedRecursive(ObservableCollection<FolderTreeNode> nodes, bool isExpanded);
    public HashSet<string> GetExpandedPaths(ObservableCollection<FolderTreeNode> nodes);
    public void RestoreExpandedPaths(ObservableCollection<FolderTreeNode> nodes, HashSet<string> expandedPaths);
}