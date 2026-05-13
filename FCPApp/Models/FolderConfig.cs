using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FCPApp.Models;

public record FolderConfig
{
    [JsonPropertyName("RootPath")]
    public string? RootPath { get; set; }

    [JsonPropertyName("SelectedFolderPaths")]
    public List<string> SelectedFolderPaths { get; set; } = new();

    [JsonPropertyName("SkipAllErrors")]
    public bool SkipAllErrors { get; set; } = false;
}