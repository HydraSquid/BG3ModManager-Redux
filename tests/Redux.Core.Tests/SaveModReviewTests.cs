using System;
using System.Linq;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;
public sealed class SaveModReviewTests
{
    public void ParentDropBlockSurvivesNestedDialogs()
    {
        var parent = new System.Windows.Window { Content = new System.Windows.Controls.Grid() };
        var first = new System.Windows.Window();
        var second = new System.Windows.Window();
        RegressionAssert.True(!ReduxWindowBehavior.HasActiveChild(parent));
        ReduxWindowBehavior.ApplyOwnerBackdrop(first, parent);
        ReduxWindowBehavior.ApplyOwnerBackdrop(second, parent);
        RegressionAssert.True(ReduxWindowBehavior.HasActiveChild(parent));
        ReduxWindowBehavior.RemoveOwnerBackdrop(first);
        RegressionAssert.True(ReduxWindowBehavior.HasActiveChild(parent));
        ReduxWindowBehavior.RemoveOwnerBackdrop(second);
        RegressionAssert.True(!ReduxWindowBehavior.HasActiveChild(parent));
    }

    public void SavePackageReviewDistinguishesEmptyMissingAndMalformedMetadata()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxSaveReview", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            string WriteSave(string name, bool modsNode, bool missingUuid, bool hasMod)
            {
                var resource = new LSLib.LS.Resource();
                var region = new LSLib.LS.Region { Name = "MetaData", RegionName = "MetaData" };
                resource.Regions.Add("MetaData", region);
                if (modsNode)
                {
                    var mods = new LSLib.LS.Node { Name = "Mods", Parent = region };
                    region.AppendChild(mods);
                    if (hasMod)
                    {
                        var mod = new LSLib.LS.Node { Name = "ModuleShortDesc", Parent = mods };
                        mod.Attributes["Name"] = new LSLib.LS.NodeAttribute(LSLib.LS.AttributeType.LSString) { Value = "Recorded mod" };
                        if (!missingUuid)
                            mod.Attributes["UUID"] = new LSLib.LS.NodeAttribute(LSLib.LS.AttributeType.LSString) { Value = A };
                        mods.AppendChild(mod);
                    }
                }
                using var stream = new System.IO.MemoryStream();
                new LSLib.LS.LSFWriter(stream).Write(resource);
                var build = new LSLib.LS.PackageBuildData();
                build.Files.Add(new LSLib.LS.PackageBuildInputFile { Path = "meta.lsf", Body = stream.ToArray() });
                var path = System.IO.Path.Combine(root, name + ".lsv");
                using (var writer = LSLib.LS.PackageWriterFactory.Create(build, path)) writer.Write();
                return path;
            }
            var populated = WriteSave("populated", true, false, true);
            var original = System.IO.File.ReadAllBytes(populated);
            var order = DivinityModDataLoader.GetLoadOrderFromSave(populated, includeEmpty: true);
            if (order == null) throw new InvalidOperationException("Populated package returned no mod list.");
            RegressionAssert.Equal(A, order.Order.Single().UUID);
            RegressionAssert.SequenceEqual(original, System.IO.File.ReadAllBytes(populated));
            var empty = WriteSave("empty", true, false, false);
            RegressionAssert.Equal(0, DivinityModDataLoader.GetLoadOrderFromSave(empty, includeEmpty: true).Order.Count);
            RegressionAssert.True(DivinityModDataLoader.GetLoadOrderFromSave(empty) == null);
            RegressionAssert.True(DivinityModDataLoader.GetLoadOrderFromSave(WriteSave("missing", false, false, false), includeEmpty: true) == null);
            var malformed = DivinityModDataLoader.GetLoadOrderFromSave(WriteSave("malformed", true, true, true), includeEmpty: true);
            var review = SaveModReviewService.Review(malformed.Order, Array.Empty<DivinityModData>(), Array.Empty<string>(), Array.Empty<string>());
            RegressionAssert.Equal(SaveModStatus.Unavailable, review.Single().Status);
            var corrupt = System.IO.Path.Combine(root, "corrupt.lsv");
            System.IO.File.WriteAllText(corrupt, "not a save package");
            RegressionAssert.True(DivinityModDataLoader.GetLoadOrderFromSave(corrupt, includeEmpty: true) == null);
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    public void SaveKindsRecognizeGeneratedNamesWithoutMatchingCustomTitles()
    {
        RegressionAssert.Equal("Autosave", Bg3SaveGameService.ClassifySaveKind("AutoSave_12.lsv"));
        RegressionAssert.Equal("Quicksave", Bg3SaveGameService.ClassifySaveKind("Tav__QuickSave_4.lsv"));
        RegressionAssert.Equal("Save", Bg3SaveGameService.ClassifySaveKind("Before_quicksave_experiment.lsv"));
        RegressionAssert.Equal("Save", Bg3SaveGameService.ClassifySaveKind("Autosave backup.lsv"));
    }

    private const string A = "11111111-1111-1111-1111-111111111111";
    private const string B = "22222222-2222-2222-2222-222222222222";
    private const string C = "33333333-3333-3333-3333-333333333333";
    private static DivinityLoadOrderEntry Req(string id) => new() { UUID = id, Name = "Same display name" };
    private static DivinityModData Mod(string id) => new() { UUID = id, Name = "Same display name", ModType = "Add-on" };
    public void MatchesSaveUUIDsWithoutNameFallbackOrMutation()
    {
        var a = Mod(A); var b = Mod(B);
        var review = SaveModReviewService.Review([Req(A), Req(B), Req(C)], [a,b], [A], [B]);
        RegressionAssert.SequenceEqual([SaveModStatus.Active, SaveModStatus.Inactive, SaveModStatus.Missing], review.Select(r => r.Status));
        RegressionAssert.False(a.IsActive);
        RegressionAssert.False(b.IsActive);
        RegressionAssert.Equal(3, review.Count);
    }
    public void AmbiguousAndInvalidRequirementsCannotActivate()
    {
        var review = SaveModReviewService.Review([Req(A), Req("invalid"), Req(B)], [Mod(A),Mod(A),new DivinityModData { UUID=B, ModType="Adventure" }], [], [A,B]);
        RegressionAssert.True(review.All(r => r.Status == SaveModStatus.Unavailable));
        RegressionAssert.Equal(0, SaveModReviewService.SelectedActivationIds(review, [A,B,"invalid"]).Count);
        RegressionAssert.Contains(review[0].Detail, "Multiple");
    }
    public void ActivationKeepsSaveSequenceAndExistingActiveOrder()
    {
        var active = Mod(A); var b = Mod(B); var c = Mod(C);
        var required = new[] {Req(C),Req(B),Req(C),Req(A)};
        var review = SaveModReviewService.Review(required, [active,b,c], [A], [B,C]);
        var ids = SaveModReviewService.SelectedActivationIds(review, [A,B,C]);
        RegressionAssert.SequenceEqual([C,B], ids);
        var moved = VisualModListDropPolicy.Apply([active], [b,c], ids.Select(id => id == B ? b : c).ToArray(), true, 1);
        RegressionAssert.SequenceEqual([active,c,b], moved.ActiveItems);
        RegressionAssert.Equal(0, moved.InactiveItems.Count);
        var choice = new SaveModReviewChoice(review.Last()); choice.Selected = true;
        RegressionAssert.False(choice.Selected);
    }
    public void DatabasePresentationKeepsRecordedIdentityAndActivationRules()
    {
        var required = new SaveModRequirement("069e5871-efe8-44bb-b02a-fe957df5ae0e", "Recorded filename", SaveModStatus.Missing);
        var choice = new SaveModReviewChoice(required);
        RegressionAssert.Equal(ReduxModDatabaseService.TryResolveModuleUuid(required.UUID).Project.Name, choice.DisplayName);
        RegressionAssert.True(ReferenceEquals(required, choice.Requirement));
        choice.Selected = true;
        RegressionAssert.False(choice.Selected);
        RegressionAssert.Contains(choice.IdentityText, required.UUID);
        var unknown = new SaveModReviewChoice(new SaveModRequirement(A, "Unknown title", SaveModStatus.Inactive));
        RegressionAssert.Equal("Unknown title", unknown.DisplayName);
        RegressionAssert.Equal("", unknown.MetadataText);
    }

    public void SaveWarningsPreserveSelectionIdentity()
    {
        var item = new ReduxSaveGameItem(new Bg3SaveGameEntry("", "save", "Save", "Campaign", "save.lsv", "", DateTime.UtcNow, 1, Bg3SaveDifficulty.Unknown));
        var selected = new System.Collections.Generic.HashSet<ReduxSaveGameItem> { item };
        var list = new System.Windows.Controls.ListBox { ItemsSource = new[] { item } };
        list.SelectedItem = item;
        item.SetModCheck("1 missing mod", true);
        RegressionAssert.True(selected.Contains(item));
        RegressionAssert.Equal(1, list.SelectedItems.Count);
        list.SelectedItem = null;
        RegressionAssert.Equal(0, list.SelectedItems.Count);
        list.SelectedItem = item;
        RegressionAssert.Equal(1, list.SelectedItems.Count);
        RegressionAssert.True(((ReduxSaveGameItem)list.SelectedItem).CanReviewMods);
    }

    public void CompanionPortraitsResolveAndUnknownOriginsRemainGeneric()
    {
        foreach (var name in new[] { "Gale", "Astarion", "Shadowheart", "Laezel", "Karlach", "Wyll", "Halsin", "Minthara", "Jaheira", "Minsc" })
        {
            var item = new SavePartyDisplayItem(new Bg3SavePartyMember(name, 5, "", "", ""));
            var resource = System.Windows.Application.GetResourceStream(new Uri(item.PortraitPath));
            RegressionAssert.True(resource != null);
            resource.Stream.Dispose();
        }
        foreach (var race in new[] { "Human", "HighElf", "HighHalfElf", "SeldarineDrow", "MountainDwarf", "Duergar", "RockGnome", "LightfootHalfling", "HalfOrc", "Githyanki", "ZarielTiefling", "WhiteDragonborn" })
        {
            var placeholder = new SavePartyDisplayItem(new Bg3SavePartyMember("Generic", 1, race, "", ""));
            var resource = System.Windows.Application.GetResourceStream(new Uri(placeholder.PortraitPath));
            RegressionAssert.True(resource != null);
            resource!.Stream.Dispose();
            RegressionAssert.Contains(placeholder.PortraitToolTip, "Representative");
            RegressionAssert.Equal(placeholder.Member.RaceDisplayName, placeholder.DisplayName);
        }
        foreach (var origin in new[] { "Halsin", " HALSIN ", "Lae'zel", "Lae’zel", "Shadow_Heart", "Minthara", "Jaheira", "Minsc" })
        {
            var member = new SavePartyDisplayItem(new Bg3SavePartyMember(origin, 1, "Human", "", ""));
            RegressionAssert.True(member.PortraitPath != null && !member.PortraitPath.Contains("race-"));
        }
        var modded = new SavePartyDisplayItem(new Bg3SavePartyMember("ModdedHalsinClone", 1, "WoodElf", "", ""));
        RegressionAssert.Contains(modded.PortraitPath, "race-elf.png");
        var companion = new SavePartyDisplayItem(new Bg3SavePartyMember("Gale", 5, "Human", "", ""));
        RegressionAssert.Contains(companion.PortraitPath, "/gale.png");
        var unknown = new SavePartyDisplayItem(new Bg3SavePartyMember("ModdedOrigin", null, "", "", ""));
        RegressionAssert.True(unknown.PortraitPath == null);
        RegressionAssert.Equal("ModdedOrigin", unknown.DisplayName);
    }

    public void SaveRowMenuUsesClickedSaveAndExistingReviewState()
    {
        var window = new ReduxSaveManagerWindow(null!, null!);
        var first = new ReduxSaveGameItem(new Bg3SaveGameEntry("", "one", "One", "Campaign", "one.lsv", "", DateTime.UtcNow, 1, Bg3SaveDifficulty.Unknown));
        var second = new ReduxSaveGameItem(new Bg3SaveGameEntry("", "two", "Two", "Campaign", "two.lsv", "", DateTime.UtcNow, 1, Bg3SaveDifficulty.Unknown));
        second.SetModCheck("1 missing mod", true);
        var list = (System.Windows.Controls.ListBox)window.FindName("SaveList");
        list.ItemsSource = new[] { first, second };
        list.SelectedItem = first;
        var row = new System.Windows.Controls.ListBoxItem { DataContext = second };
        var menu = list.ContextMenu;
        RegressionAssert.True(menu != null);
        RegressionAssert.True(window.PrepareSaveContextMenu(row, false));
        RegressionAssert.True(ReferenceEquals(second, list.SelectedItem));
        RegressionAssert.Equal(4, menu.Items.Count);
        RegressionAssert.Equal("Export Campaign…", ((System.Windows.Controls.MenuItem)menu.Items[2]).Header);
        var toolbar = (System.Windows.Controls.Button)window.FindName("ReviewModsButton");
        var reviewMenu = (System.Windows.Controls.MenuItem)menu.Items[0];
        RegressionAssert.True(reviewMenu.IsEnabled);
        toolbar.IsEnabled = false;
        RegressionAssert.True(!reviewMenu.IsEnabled);
        toolbar.IsEnabled = true;
        RegressionAssert.True(reviewMenu.IsEnabled);
        row.DataContext = first;
        RegressionAssert.True(window.PrepareSaveContextMenu(row, false));
        RegressionAssert.True(!reviewMenu.IsEnabled);
        RegressionAssert.True(!DivinityModManager.Util.ReduxMenuItemExtension.GetUseSemanticHover(reviewMenu));
        RegressionAssert.True(!System.Windows.Data.BindingOperations.IsDataBound(reviewMenu, System.Windows.Controls.Control.ForegroundProperty));
        RegressionAssert.True(window.PrepareSaveContextMenu(list, true));
        RegressionAssert.True(!window.PrepareSaveContextMenu(list, false));
        row.DataContext = second;
        RegressionAssert.True(window.PrepareSaveContextMenu(row, false));
        RegressionAssert.True(reviewMenu.IsEnabled);
        foreach (System.Windows.Controls.MenuItem item in menu.Items) RegressionAssert.True(item.Icon != null);
        window.Close();
    }

    public void EmptyReviewDoesNotOfferActivation()
    {
        var review = SaveModReviewService.Review([], [], [], []);
        RegressionAssert.Equal(0, review.Count);
        RegressionAssert.Equal(0, SaveModReviewService.SelectedActivationIds(review, [A]).Count);
    }
}
