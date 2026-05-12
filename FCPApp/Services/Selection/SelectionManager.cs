using FCPApp.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FCPApp.Services.Selection;

public class SelectionManager : ISelectionManager
{
    public List<string> GetSelectedPaths(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new List<string>();

        foreach (var node in nodes)
        {
            if (node.IsChecked) result.Add(node.FullPath);

            result.AddRange(GetSelectedPaths(node.Children));
        }

        return result;
    }

    public List<FolderTreeNode> GetSelectedNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        var result = new List<FolderTreeNode>();

        foreach (var node in nodes)
        {
            if (node.IsChecked) result.Add(node);

            result.AddRange(GetSelectedNodes(node.Children));
        }

        return result;
    }

    public void UncheckAllRecursive(ObservableCollection<FolderTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsChecked) node.IsChecked = false;

            UncheckAllRecursive(node.Children);
        }
    }

    public ObservableCollection<FolderTreeNode> BuildSelectedTree(ObservableCollection<FolderTreeNode> source)
    {
        var result = new ObservableCollection<FolderTreeNode>();
        BuildSelectedTreeRecursive(source, result);

        return result;
    }

    private void BuildSelectedTreeRecursive(ObservableCollection<FolderTreeNode> source,
        ObservableCollection<FolderTreeNode> target)
    {
        foreach (var node in source)
        {
            if (node.IsChecked)
            {
                var displayNode = new FolderTreeNode(node.Name, node.FullPath, node.Depth)
                {
                    IsChecked = true,
                    IsVisible = true,
                    IsExpanded = true,
                    DisplayPath = node.FullPath
                };
                target.Add(displayNode);
                BuildSelectedTreeRecursive(node.Children, displayNode.Children);
            }
            else BuildSelectedTreeRecursive(node.Children, target);
        }
    }
}