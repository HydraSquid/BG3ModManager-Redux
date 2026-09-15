using System.IO.Compression;

namespace DivinityModManager.AppServices;

public sealed record SaveExportProgress(string Name, int Completed, int Total);

public static class SaveArchiveExportService
{
    public static async Task ExportAsync(IEnumerable<Bg3SaveGameEntry> saves, string outputPath,
        IProgress<SaveExportProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var selected = saves.DistinctBy(save => Path.GetFullPath(save.SaveFilePath), StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Select saves to export.");
        if (selected.Length > Bg3SaveGameService.MaximumSaveGroupsPerArchive)
            throw new IOException("Select up to 32 saves per ZIP so Redux can restore it. Export larger campaigns in smaller selections.");
        var destination = Path.GetFullPath(outputPath);
        var files = new List<(string Path, string Entry, long Length, DateTime Modified)>();
        long totalBytes = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var save in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetFullPath(save.FolderPath);
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked save folders cannot be exported.");
            if (destination.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose an export location outside the save folders.");
            if (!File.Exists(save.SaveFilePath)) throw new IOException("A selected save no longer exists. Refresh and try again.");
            if (!names.Add(Path.GetFileName(folder))) throw new IOException("Selected saves have duplicate folder names.");
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked save files cannot be exported.");
                if (info.Length > Bg3SaveGameService.MaximumEntryBytes || totalBytes > Bg3SaveGameService.MaximumArchiveBytes - info.Length)
                    throw new IOException("This selection exceeds the save archive limits (1 GB total, 256 MB per file). Select fewer saves.");
                totalBytes += info.Length;
                files.Add((path, Path.GetFileName(folder) + "/" + info.Name, info.Length, info.LastWriteTimeUtc));
            }
        }
        var temporary = Path.Combine(Path.GetDirectoryName(destination), ".redux-save-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
                for (var index = 0; index < files.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = files[index];
                    progress?.Report(new SaveExportProgress(file.Entry, index, files.Count));
                    await using var input = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                    var entry = archive.CreateEntry(file.Entry, CompressionLevel.Fastest);
                    await using var target = entry.Open();
                    await input.CopyToAsync(target, cancellationToken);
                }
            }
            foreach (var file in files)
            {
                var info = new FileInfo(file.Path);
                if (!info.Exists || info.Length != file.Length || info.LastWriteTimeUtc != file.Modified)
                    throw new IOException("A save changed during export. Try again after the game finishes saving.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            progress?.Report(new SaveExportProgress("Export complete", files.Count, files.Count));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
