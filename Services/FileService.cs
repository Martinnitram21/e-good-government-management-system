using System;
using System.IO;
using System.Threading.Tasks;

namespace GoodGovernanceApp.Services;

public class FileService
{
    private readonly string _uploadDirectory;

    public FileService()
    {
        // Program Files is read-only for standard users. Keep uploaded files in
        // the same per-user writable application-data root as the configuration.
        string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            throw new InvalidOperationException("A writable application-data folder could not be located.");
        }

        _uploadDirectory = Path.Combine(appDataRoot, "GoodGovernanceApp", "Uploads");
        Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task<string> SaveFileAsync(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourceFilePath);

        string fileName = Path.GetFileName(sourceFilePath);
        string fileExtension = Path.GetExtension(sourceFilePath);
        string uniqueFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid()}{fileExtension}";
        string destinationPath = Path.Combine(_uploadDirectory, uniqueFileName);

        using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
        using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await sourceStream.CopyToAsync(destinationStream);
        }

        return destinationPath;
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
