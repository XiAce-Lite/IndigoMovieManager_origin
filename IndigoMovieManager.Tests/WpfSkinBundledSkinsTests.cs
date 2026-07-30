using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinBundledSkinsTests
{
    [Fact]
    public void All_repo_Wpf_skins_TryLoadFrom()
    {
        string root = ResolveRepoWpfSkinsRoot();
        Assert.True(Directory.Exists(root), root);

        string[] folders = Directory.GetDirectories(root)
            .Where(d => File.Exists(Path.Combine(d, WpfSkinLoader.DefinitionFileName)))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.NotEmpty(folders);

        var failures = new List<string>();
        foreach (string name in folders)
        {
            if (!WpfSkinLoader.TryLoadFrom(root, name, out WpfSkinDefinition def) || def == null)
            {
                failures.Add(name);
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(def.FolderName));
            Assert.NotNull(def.Thumbnail);
            Assert.NotNull(def.Card);
        }

        Assert.True(
            failures.Count == 0,
            "TryLoadFrom failed: " + string.Join(", ", failures));
    }

    private static string ResolveRepoWpfSkinsRoot()
    {
        string fromBase = Path.Combine(AppContext.BaseDirectory, "Skins", "Wpf");
        if (Directory.Exists(fromBase)
            && Directory.GetDirectories(fromBase).Any(d =>
                File.Exists(Path.Combine(d, WpfSkinLoader.DefinitionFileName))))
        {
            return fromBase;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Skins", "Wpf"));
    }
}
