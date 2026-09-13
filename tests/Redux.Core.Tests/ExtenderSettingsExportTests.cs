using System.Linq;
using DivinityModManager.Extensions;
using DivinityModManager.Models;
using DivinityModManager.Models.Extender;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Redux.Core.Tests;

internal sealed class ExtenderSettingsExportTests
{
	public void AchievementSettingRoundTripsInBothExportModes()
	{
		var settings = new ScriptExtenderSettings();
		settings.EnableAchievements = false;
		RegressionAssert.Equal(false, JObject.Parse(settings.ToConfigJson())["EnableAchievements"]!.Value<bool>());
		settings.EnableAchievements = true;
		RegressionAssert.True(JObject.Parse(settings.ToConfigJson())["EnableAchievements"] == null);
		RegressionAssert.True(JsonConvert.DeserializeObject<ScriptExtenderSettings>(settings.ToConfigJson())!.EnableAchievements);
		settings.ExportDefaultExtenderSettings = true;
		foreach (var property in typeof(ScriptExtenderSettings).GetProperties().Where(p => p.PropertyType == typeof(bool)
			&& p.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), true).Any()
			&& !p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any()))
		{
			foreach (var value in new[] { false, true })
			{
				property.SetValue(settings, value);
				RegressionAssert.Equal(value, JObject.Parse(settings.ToConfigJson())[property.Name]!.Value<bool>());
			}
		}
		RegressionAssert.True(JObject.Parse(settings.ToConfigJson())["ExportDefaultExtenderSettings"] == null);
	}

	public void ExportPreferenceSurvivesReduxRestartAndGameConfigReload()
	{
		var original = new DivinityModManagerSettings();
		original.ExtenderSettings.ExportDefaultExtenderSettings = true;
		var loaded = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(original))!;
		RegressionAssert.True(loaded.ExtenderSettings.ExportDefaultExtenderSettings);
		loaded.ExtenderSettings.ApplyGameConfig(JsonConvert.DeserializeObject<ScriptExtenderSettings>("{\"EnableAchievements\":false}")!);
		RegressionAssert.True(loaded.ExtenderSettings.ExportDefaultExtenderSettings);
		RegressionAssert.False(loaded.ExtenderSettings.EnableAchievements);
		loaded.ExtenderSettings.EnableAchievements = true;
		RegressionAssert.Equal(true, JObject.Parse(loaded.ExtenderSettings.ToConfigJson())["EnableAchievements"]!.Value<bool>());
	}
}
