using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FCPApp.Models;

public record Profile
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("RootPath")]
    public string? RootPath { get; set; }

    [JsonPropertyName("SelectedFolderPaths")]
    public List<string> SelectedFolderPaths { get; set; } = new();

    [JsonPropertyName("SkipAllErrors")]
    public bool SkipAllErrors { get; set; } = false;

    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Profile CloneProfile()
        => this with
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}