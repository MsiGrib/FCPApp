using System.Collections.Generic;

namespace FCPApp.Models
{
    public record FolderConfig
    {
        public string? RootPath { get; set; }
        public List<string> SelectedFolderPaths { get; set; } = new();
        public bool SkipAllErrors { get; set; } = false;
    }
}