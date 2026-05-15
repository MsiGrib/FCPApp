using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FCPApp.Models;

public record AppConfig
{
    [JsonPropertyName("CurrentProfileId")]
    public string? CurrentProfileId { get; set; }

    [JsonPropertyName("Profiles")]
    public List<Profile> Profiles { get; set; } = new();

    public Profile? CurrentProfile =>
        Profiles.FirstOrDefault(p => p.Id == CurrentProfileId);

    public void EnsureDefaultProfile()
    {
        if (Profiles.Count == 0)
        {
            var defaultProfile = new Profile { Name = "Default" };
            Profiles.Add(defaultProfile);
            CurrentProfileId = defaultProfile.Id;
        }
    }
}