using System;
using System.Text.Json.Serialization;

namespace FCPApp.Models;

public record AppSettings
{
    [JsonPropertyName("StartWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "2.0.0";

    [JsonPropertyName("LastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}