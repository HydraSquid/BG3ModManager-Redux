using DivinityModManager.Models.Updates;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DivinityModManager.AppServices;

public static class ReduxUpdateResultService
{
	private const int MaximumResultBytes = 16 * 1024;
	private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
	{
		"succeeded", "displayVersion", "message", "completedAtUtc"
	};

	public static string DefaultResultPath => Path.Combine(
		ReduxUpdatePackageService.DefaultUpdatesDirectory,
		"last-update-result.json");

	public static bool TryConsume(out ReduxUpdateCompletionResult result, string resultPath = null)
	{
		result = null;
		var path = Path.GetFullPath(resultPath ?? DefaultResultPath);
		try
		{
			var info = new FileInfo(path);
			if (!info.Exists) return false;
			if (info.Length <= 0 || info.Length > MaximumResultBytes)
				throw new InvalidDataException("The Redux update result has an invalid size.");

			JObject json;
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var text = new StreamReader(stream))
			using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
			{
				json = JObject.Load(reader, new JsonLoadSettings
				{
					DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
				});
				if (reader.Read()) throw new InvalidDataException("The Redux update result contains trailing content.");
			}
			var unknown = json.Properties().FirstOrDefault(property => !AllowedProperties.Contains(property.Name));
			if (unknown != null) throw new InvalidDataException("The Redux update result contains unsupported data.");
			if (json["succeeded"]?.Type != JTokenType.Boolean
				|| json["displayVersion"]?.Type != JTokenType.String
				|| json["message"]?.Type != JTokenType.String
				|| json["completedAtUtc"]?.Type != JTokenType.String)
				throw new InvalidDataException("The Redux update result is incomplete.");

			var displayVersion = json["displayVersion"]!.Value<string>()?.Trim() ?? String.Empty;
			var message = json["message"]!.Value<string>()?.Trim() ?? String.Empty;
			if (displayVersion.Length == 0 || displayVersion.Length > 64 || message.Length == 0 || message.Length > 512)
				throw new InvalidDataException("The Redux update result text is invalid.");
			if (!DateTimeOffset.TryParse(json["completedAtUtc"]!.Value<string>(), out var completedAtUtc))
				throw new InvalidDataException("The Redux update completion time is invalid.");

			result = new ReduxUpdateCompletionResult
			{
				Succeeded = json["succeeded"]!.Value<bool>(),
				DisplayVersion = displayVersion,
				Message = message,
				CompletedAtUtc = completedAtUtc
			};
			return true;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not read the previous Redux update result:\n{ex}");
			return false;
		}
		finally
		{
			try
			{
				if (File.Exists(path)) File.Delete(path);
			}
			catch (Exception ex)
			{
				DivinityApp.Log($"Could not remove the consumed Redux update result:\n{ex}");
			}
		}
	}
}
