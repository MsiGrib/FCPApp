using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FCPApp.Services.FileSystem;

public class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path)
        => Directory.Exists(path);

    public IEnumerable<string> GetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public bool IsFolderLocked(string path)
    {
        try
        {
            var testFile = Path.Combine(path, ".test_delete_permission");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return true;
        }
    }

    public void RemoveReadOnlyAttribute(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            dirInfo.Attributes = FileAttributes.Normal;

            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(path))
                RemoveReadOnlyAttribute(dir);
        }
        catch { }
    }

    public async Task<bool> DeleteDirectoryAsync(string path, bool recursive)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(path)) return true;
                RemoveReadOnlyAttribute(path);
                Directory.Delete(path, recursive);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var normalized = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? fullPath.ToLowerInvariant()
                : fullPath;

            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            normalized = normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return normalized;
        }
        catch
        {
            return path;
        }
    }
}