#nullable disable

using System;
using System.IO;

using Microsoft.Win32;

using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class NxmAssociationTests
{
	private const string Owner = "11111111-1111-1111-1111-111111111111";
	private const string Executable = @"C:\Portable\Redux\Redux.exe";

	public void EnableAndDisableRestoresPriorUserHandler()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "MO2 marker");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone() };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.True(service.Enable().Success);
		RegressionAssert.Equal(Owner, store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(Executable), store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.True(service.Disable().Success);
		RegressionAssert.Equal("MO2 marker", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(@"C:\Tools\MO2\nxmhandler.exe", store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.Equal(null, store.Backup);
	}

	public void DisableRevealsMachineHandlerWhenNoUserHandlerExisted()
	{
		var store = new MemoryNxmRegistryStore { MachineKey = Handler(@"C:\Program Files\Vortex\Vortex.exe", null) };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.True(service.Enable().Success);
		RegressionAssert.True(service.Disable().Success);
		RegressionAssert.Equal(null, store.UserKey);
		RegressionAssert.Equal(@"C:\Program Files\Vortex\Vortex.exe", store.MachineKey.GetString(@"shell\open\command", ""));
	}

	public void RepairUpdatesOnlyOwnedMovedRegistration()
	{
		var oldExecutable = @"D:\Old Redux\Redux.exe";
		var store = new MemoryNxmRegistryStore
		{
			UserKey = NxmAssociationService.CreateOwnedKey(Owner, oldExecutable),
			Backup = new NxmAssociationBackup(false, null, @"C:\Vortex\Vortex.exe")
		};
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.Equal(NxmAssociationStatus.NeedsRepair, service.GetStatus().Status);
		RegressionAssert.True(service.Repair().Success);
		RegressionAssert.Equal(NxmAssociationStatus.Owned, service.GetStatus().Status);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(Executable), store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void DisableNeverOverwritesAnInterveningHandler()
	{
		var store = new MemoryNxmRegistryStore { UserKey = Handler(@"C:\Tools\MO2\nxmhandler.exe", null) };
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);

		store.UserKey = Handler(@"C:\Program Files\Vortex\Vortex.exe", "new owner");
		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, result.Status);
		RegressionAssert.Equal("new owner", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(@"C:\Program Files\Vortex\Vortex.exe", store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void DifferentReduxInstallationCannotRepairOrDisableOwner()
	{
		var store = new MemoryNxmRegistryStore { UserKey = NxmAssociationService.CreateOwnedKey("other-owner", @"D:\Redux\Redux.exe") };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, service.GetStatus().Status);
		RegressionAssert.False(service.Repair().Success);
		RegressionAssert.False(service.Disable().Success);
		RegressionAssert.Equal("other-owner", store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
	}

	public void RegistrySnapshotPreservesValueKindsAndSubkeys()
	{
		var prior = Handler(@"C:\Handler.exe", null);
		prior.SetValue("", "Number", 42, RegistryValueKind.DWord);
		prior.SetValue("metadata", "Bytes", new byte[] { 1, 2, 3 }, RegistryValueKind.Binary);
		var clone = prior.Clone();

		RegressionAssert.Equal(RegistryValueKind.DWord, clone.GetValue("", "Number").Kind);
		RegressionAssert.Equal(42, clone.GetValue("", "Number").Value);
		RegressionAssert.Equal(RegistryValueKind.Binary, clone.GetValue("metadata", "Bytes").Kind);
		RegressionAssert.SequenceEqual(new byte[] { 1, 2, 3 }, (byte[])clone.GetValue("metadata", "Bytes").Value);
	}

	public void ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths()
	{
		var root = $@"Software\BG3MMReduxTests\{Guid.NewGuid():N}";
		try
		{
			var store = new NxmRegistryStore($@"{root}\nxm", $@"{root}\backups");
			var prior = Handler(@"C:\Handler.exe", "marker");
			prior.SetValue("metadata", "Number", 42, RegistryValueKind.DWord);
			store.WriteUserKey(prior);
			store.WriteBackup(Owner, new NxmAssociationBackup(true, prior, @"C:\Handler.exe"));

			RegressionAssert.Equal("marker", store.ReadUserKey().GetString("", "CustomValue"));
			RegressionAssert.Equal(42, store.ReadBackup(Owner).UserKey.GetValue("metadata", "Number").Value);

			store.DeleteUserKey();
			store.DeleteBackup(Owner);
			RegressionAssert.Equal(null, store.ReadUserKey());
			RegressionAssert.Equal(null, store.ReadBackup(Owner));
		}
		finally
		{
			Registry.CurrentUser.DeleteSubKeyTree(root, false);
		}
	}

	public void FailedEnableRestoresPriorHandler()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "before");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone(), FailNextUserWrite = true };
		var service = new NxmAssociationService(store, Owner, Executable);

		var result = service.Enable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal("before", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(null, store.Backup);
	}

	public void FailedDisableKeepsOwnedHandlerAndBackup()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "before");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone() };
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.FailNextUserWrite = true;

		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(Owner, store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
		RegressionAssert.True(store.Backup != null);
	}

	public void ReduxShapedInterveningCommandIsNotOwned()
	{
		var store = new MemoryNxmRegistryStore();
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.UserKey.SetValue(@"shell\open\command", "", NxmAssociationService.BuildCommand(@"C:\Other\Redux.exe"), RegistryValueKind.String);

		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(@"C:\Other\Redux.exe"),
			store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void MissingExecutableMarkerIsAnOwnershipConflict()
	{
		var store = new MemoryNxmRegistryStore();
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.UserKey.Values.Remove(NxmAssociationService.ExecutableValueName);

		var result = service.GetStatus();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, result.Status);
	}

	public void PreviousHandlerCommandIsParsedWithoutShell()
	{
		var executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
		var command = $"\"{executable}\" --from-nexus \"%1\"";
		const string link = "nxm://skyrim/mods/42/files/99?key=secret";

		RegressionAssert.True(NxmPreviousHandlerForwarder.TryCreateStartInfo(command, link,
			@"C:\Redux\Redux.exe", out var startInfo, out _));
		RegressionAssert.Equal(executable, startInfo.FileName);
		RegressionAssert.False(startInfo.UseShellExecute);
		RegressionAssert.SequenceEqual(new[] { "--from-nexus", link }, startInfo.ArgumentList);
	}

	public void PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion()
	{
		var executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
		RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(
			$"\"{executable}\" --url=%1", "nxm://skyrim/mods/42/files/99", @"C:\Redux\Redux.exe", out _, out _));
		RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(
			"\"C:\\Redux\\Redux.exe\" \"%1\"", "nxm://skyrim/mods/42/files/99", @"C:\Redux\Redux.exe", out _, out _));
	}

	private static NxmRegistryKeySnapshot Handler(string command, string marker)
	{
		var key = new NxmRegistryKeySnapshot();
		key.SetValue(@"shell\open\command", "", command, RegistryValueKind.String);
		if (marker != null) key.SetValue("", "CustomValue", marker, RegistryValueKind.String);
		return key;
	}

	private sealed class MemoryNxmRegistryStore : INxmRegistryStore
	{
		public NxmRegistryKeySnapshot UserKey { get; set; }
		public NxmRegistryKeySnapshot MachineKey { get; set; }
		public NxmAssociationBackup Backup { get; set; }
		public bool FailNextUserWrite { get; set; }

		public NxmRegistryKeySnapshot ReadUserKey() => UserKey?.Clone();
		public NxmRegistryKeySnapshot ReadMachineKey() => MachineKey?.Clone();
		public void WriteUserKey(NxmRegistryKeySnapshot key)
		{
			if (FailNextUserWrite)
			{
				FailNextUserWrite = false;
				UserKey = null;
				throw new IOException("simulated registry write failure");
			}
			UserKey = key?.Clone();
		}
		public void DeleteUserKey() => UserKey = null;
		public NxmAssociationBackup ReadBackup(string ownerId) => Backup?.DeepCopy();
		public void WriteBackup(string ownerId, NxmAssociationBackup backup) => Backup = backup?.DeepCopy();
		public void DeleteBackup(string ownerId) => Backup = null;
	}
}
