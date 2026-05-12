using System.Collections.Generic;
using System.Threading.Tasks;

namespace FCPApp.Services.FileSystem;

public interface IFileSystemService
{
    public bool DirectoryExists(string path);
    public IEnumerable<string> GetDirectories(string path);
    public bool IsFolderLocked(string path);
    public void RemoveReadOnlyAttribute(string path);
    public Task<bool> DeleteDirectoryAsync(string path, bool recursive);
    public string NormalizePath(string path);
}