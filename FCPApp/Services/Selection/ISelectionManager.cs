using FCPApp.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FCPApp.Services.Selection;

public interface ISelectionManager
{
    public List<string> GetSelectedPaths(ObservableCollection<FolderTreeNode> nodes);
    public List<FolderTreeNode> GetSelectedNodes(ObservableCollection<FolderTreeNode> nodes);
    public void UncheckAllRecursive(ObservableCollection<FolderTreeNode> nodes);
    public ObservableCollection<FolderTreeNode> BuildSelectedTree(ObservableCollection<FolderTreeNode> source);
}