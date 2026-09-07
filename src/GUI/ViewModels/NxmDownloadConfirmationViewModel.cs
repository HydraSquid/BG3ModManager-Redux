using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.ViewModels;

public sealed class NxmDownloadConfirmationViewModel
{
	public string ProjectName { get; }
	public string Author { get; }
	public string FileName { get; }
	public string Version { get; }
	public string Size { get; }
	public Uri NexusPage { get; }
	public string InstallationDescription { get; }

	public NxmDownloadConfirmationViewModel(NxmDownloadDescriptor descriptor)
	{
		ProjectName = descriptor.ProjectName;
		InstallationDescription = AppServices.ReduxGameDirectoryModCatalog.Find(descriptor.ModId) != null
			? "Redux will download this native-code archive. Installing it requires a separate game-directory review; it will not enter the PAK load order."
			: "Redux will download this file into its managed Downloads folder. Nothing is installed until you choose Review & Install.";
		Author = String.IsNullOrWhiteSpace(descriptor.Author) ? "Unknown" : descriptor.Author;
		FileName = descriptor.FileDisplayName;
		Version = String.IsNullOrWhiteSpace(descriptor.Version) ? "Unspecified" : descriptor.Version;
		Size = descriptor.SizeBytes > 0 ? $"{descriptor.SizeBytes / 1024d / 1024d:N1} MB" : "Unknown";
		NexusPage = new Uri($"https://www.nexusmods.com/baldursgate3/mods/{descriptor.ModId}?tab=files&file_id={descriptor.FileId}");
	}
}
