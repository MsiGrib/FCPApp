using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FCPApp.Models;

public partial class FolderTreeNode : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public int Depth { get; }
    public FolderTreeNode? Parent { get; }

    [ObservableProperty] private string _relativePath = string.Empty;
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private ObservableCollection<FolderTreeNode> _children = new();
    [ObservableProperty] private bool _isHighlighted;
    [ObservableProperty] private string _displayPath = string.Empty;
    [ObservableProperty] private bool _hasUnloadedChildren = true;

    public FolderTreeNode(string name, string fullPath, int depth = 0, FolderTreeNode? parent = null)
    {
        Name = name;
        FullPath = fullPath;
        Depth = depth;
        Parent = parent;

        UpdateRelativePath();

        this.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(IsChecked))
            {
                UpdateChildrenSelection();
                UpdateParentSelection();

                if (this is FolderTreeNode node && node.Parent != null)
                {
                    var root = node;
                    while (root.Parent != null) root = root.Parent;
                }
            }
        };
    }

    private void UpdateRelativePath()
    {
        if (Parent == null) RelativePath = string.Empty;
        else
        {
            var parts = new List<string>();
            var current = this;

            while (current?.Parent != null)
            {
                parts.Insert(0, current.Name);
                current = current.Parent;
            }

            RelativePath = parts.Count > 1 ? string.Join(" > ", parts.Skip(1)) : "";
        }
    }

    public void UpdatePathRecursively()
    {
        UpdateRelativePath();

        foreach (var child in Children)
            child.UpdatePathRecursively();
    }

    private void UpdateChildrenSelection()
    {
        foreach (var child in Children)
        {
            if (child.IsChecked != IsChecked)
                child.IsChecked = IsChecked;
        }
    }

    private void UpdateParentSelection()
    {
        if (Parent == null) return;

        var allChecked = Parent.Children.All(c => c.IsChecked);

        if (allChecked != Parent.IsChecked)
            Parent.IsChecked = allChecked;
    }

    public bool MatchesFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;

        if (Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;

        return Children.Any(c => c.MatchesFilter(filter));
    }
}