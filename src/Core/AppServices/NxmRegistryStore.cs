using Microsoft.Win32;

namespace DivinityModManager.AppServices;

public sealed class NxmRegistryStore : INxmRegistryStore
{
	private const string DefaultUserProtocolPath = @"Software\Classes\nxm";
	private const string MachineProtocolPath = @"Software\Classes\nxm";
	private const string DefaultBackupRootPath = @"Software\BG3ModManagerRedux\NxmAssociation";
	private readonly string _userProtocolPath;
	private readonly string _backupRootPath;

	public NxmRegistryStore() : this(DefaultUserProtocolPath, DefaultBackupRootPath) { }

	public NxmRegistryStore(string userProtocolPath, string backupRootPath)
	{
		if (String.IsNullOrWhiteSpace(userProtocolPath)) throw new ArgumentException("A user protocol path is required.", nameof(userProtocolPath));
		if (String.IsNullOrWhiteSpace(backupRootPath)) throw new ArgumentException("A backup root path is required.", nameof(backupRootPath));
		_userProtocolPath = userProtocolPath;
		_backupRootPath = backupRootPath;
	}

	public NxmRegistryKeySnapshot ReadUserKey() => ReadKey(Registry.CurrentUser.OpenSubKey(_userProtocolPath));
	public NxmRegistryKeySnapshot ReadMachineKey() => ReadKey(Registry.LocalMachine.OpenSubKey(MachineProtocolPath));

	public void WriteUserKey(NxmRegistryKeySnapshot key)
	{
		DeleteUserKey();
		using var target = Registry.CurrentUser.CreateSubKey(_userProtocolPath, true);
		WriteKey(target, key);
	}

	public void DeleteUserKey()
	{
		Registry.CurrentUser.DeleteSubKeyTree(_userProtocolPath, false);
	}

	public NxmAssociationBackup ReadBackup(string ownerId)
	{
		using var root = Registry.CurrentUser.OpenSubKey($@"{_backupRootPath}\{ownerId}");
		if (root == null) return null;
		var hadUserKey = Convert.ToInt32(root.GetValue("HadUserKey", 0)) == 1;
		var previousCommand = root.GetValue("PreviousCommand") as string;
		var userKey = ReadKey(root.OpenSubKey("PreviousUserKey"));
		return new NxmAssociationBackup(hadUserKey, userKey, previousCommand);
	}

	public void WriteBackup(string ownerId, NxmAssociationBackup backup)
	{
		DeleteBackup(ownerId);
		using var root = Registry.CurrentUser.CreateSubKey($@"{_backupRootPath}\{ownerId}", true);
		root.SetValue("HadUserKey", backup.HadUserKey ? 1 : 0, RegistryValueKind.DWord);
		if (backup.PreviousCommand != null) root.SetValue("PreviousCommand", backup.PreviousCommand, RegistryValueKind.String);
		if (backup.UserKey != null)
		{
			using var previous = root.CreateSubKey("PreviousUserKey", true);
			WriteKey(previous, backup.UserKey);
		}
	}

	public void DeleteBackup(string ownerId)
	{
		Registry.CurrentUser.DeleteSubKeyTree($@"{_backupRootPath}\{ownerId}", false);
	}

	private static NxmRegistryKeySnapshot ReadKey(RegistryKey key)
	{
		if (key == null) return null;
		using (key)
		{
			var snapshot = new NxmRegistryKeySnapshot();
			foreach (var name in key.GetValueNames())
			{
				snapshot.Values[name] = new NxmRegistryValue(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), key.GetValueKind(name));
			}
			foreach (var name in key.GetSubKeyNames())
			{
				var child = ReadKey(key.OpenSubKey(name));
				if (child != null) snapshot.Subkeys[name] = child;
			}
			return snapshot;
		}
	}

	private static void WriteKey(RegistryKey key, NxmRegistryKeySnapshot snapshot)
	{
		foreach (var (name, value) in snapshot.Values) key.SetValue(name, value.Value, value.Kind);
		foreach (var (name, child) in snapshot.Subkeys)
		{
			using var childKey = key.CreateSubKey(name, true);
			WriteKey(childKey, child);
		}
	}
}
