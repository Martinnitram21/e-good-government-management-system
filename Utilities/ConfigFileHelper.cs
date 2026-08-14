using System;
using System.IO;

namespace GoodGovernanceApp.Utilities;

/// <summary>
/// Provides atomic file write operations for configuration files.
/// Uses the write-to-temp-then-rename pattern to ensure files are
/// never left in a half-written state if the app crashes mid-write.
/// </summary>
public static class ConfigFileHelper
{
    /// <summary>
    /// Atomically writes JSON content to the target file.
    /// 
    /// Steps:
    ///   1. Write content to {path}.tmp
    ///   2. Delete {path}.bak if it exists
    ///   3. Rename {path} → {path}.bak
    ///   4. Rename {path}.tmp → {path}
    ///
    /// On NTFS, File.Move is atomic — the target file is never in a
    /// half-written or empty state.
    /// </summary>
    public static void AtomicWriteJson(string path, string jsonContent)
    {
        string tmpPath = path + ".tmp";
        string bakPath = path + ".bak";

        // 1. Write to a temporary file first
        File.WriteAllText(tmpPath, jsonContent);

        // 2. Delete old backup if it exists
        if (File.Exists(bakPath))
            File.Delete(bakPath);

        // 3. Move the current file to backup (only if it exists)
        if (File.Exists(path))
            File.Move(path, bakPath);

        // 4. Move the temp file into place (atomic on NTFS)
        File.Move(tmpPath, path);
    }
}
