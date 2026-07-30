using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinLoaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryLoadFrom_blank_name_fails(string name)
    {
        using var temp = new TempSkinsRoot();
        Assert.False(WpfSkinLoader.TryLoadFrom(temp.Root, name, out WpfSkinDefinition def));
        Assert.Null(def);
    }

    [Fact]
    public void TryLoadFrom_missing_folder_fails()
    {
        using var temp = new TempSkinsRoot();
        Assert.False(WpfSkinLoader.TryLoadFrom(temp.Root, "NoSuchSkin", out _));
    }

    [Fact]
    public void TryLoadFrom_folder_without_json_fails()
    {
        using var temp = new TempSkinsRoot();
        Directory.CreateDirectory(Path.Combine(temp.Root, "EmptyFolder"));
        Assert.False(WpfSkinLoader.TryLoadFrom(temp.Root, "EmptyFolder", out _));
    }

    [Fact]
    public void TryLoadFrom_empty_object_succeeds_with_defaults()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("EmptyObj", "{}");
        Assert.True(WpfSkinLoader.TryLoadFrom(temp.Root, "EmptyObj", out WpfSkinDefinition def));
        Assert.NotNull(def.Thumbnail);
        Assert.NotNull(def.Card);
        Assert.NotNull(def.Card.Layout);
        Assert.NotNull(def.Surface);
        Assert.NotNull(def.Styles);
        Assert.Equal("EmptyObj", def.Name);
        Assert.Equal("EmptyObj", def.FolderName);
    }

    [Fact]
    public void TryLoadFrom_type_mismatch_fails()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("BadType", """{"thumbnail":"oops"}""");
        Assert.False(WpfSkinLoader.TryLoadFrom(temp.Root, "BadType", out _));
    }

    [Fact]
    public void TryLoadFrom_invalid_json_fails()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("BadJson", "{ name: ");
        Assert.False(WpfSkinLoader.TryLoadFrom(temp.Root, "BadJson", out _));
    }

    [Fact]
    public void TryLoadFrom_missing_name_uses_folder()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("FolderOnly", """{"type":"card"}""");
        Assert.True(WpfSkinLoader.TryLoadFrom(temp.Root, "FolderOnly", out WpfSkinDefinition def));
        Assert.Equal("FolderOnly", def.Name);
        Assert.Equal("FolderOnly", def.FolderName);
    }

    [Fact]
    public void TryLoadFrom_allows_comments_and_trailing_commas()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("Comments", """
            {
              // comment
              "name": "Comments",
              "type": "card",
            }
            """);
        Assert.True(WpfSkinLoader.TryLoadFrom(temp.Root, "Comments", out WpfSkinDefinition def));
        Assert.Equal("Comments", def.Name);
    }

    [Fact]
    public void EnumerateSkinsFrom_missing_root_is_empty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "imm-no-skins-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(WpfSkinLoader.EnumerateSkinsFrom(missing));
    }

    [Fact]
    public void EnumerateSkinsFrom_skips_folders_without_json()
    {
        using var temp = new TempSkinsRoot();
        Directory.CreateDirectory(Path.Combine(temp.Root, "NoJson"));
        temp.WriteSkin("HasJson", """{"name":"HasJson"}""");
        IReadOnlyList<string> names = WpfSkinLoader.EnumerateSkinsFrom(temp.Root);
        Assert.Equal(["HasJson"], names);
    }

    [Fact]
    public void LoadDefaultFrom_falls_back_to_first_valid()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("OnlyOne", """{"name":"OnlyOne","type":"card"}""");
        WpfSkinDefinition def = WpfSkinLoader.LoadDefaultFrom(temp.Root);
        Assert.Equal("OnlyOne", def.FolderName);
    }

    [Fact]
    public void LoadDefaultFrom_all_invalid_returns_built_in()
    {
        using var temp = new TempSkinsRoot();
        temp.WriteSkin("Broken", "{");
        WpfSkinDefinition def = WpfSkinLoader.LoadDefaultFrom(temp.Root);
        Assert.Equal(WpfSkinLoader.DefaultSkinName, def.Name);
        Assert.Equal(400, def.Thumbnail.Width);
    }

    private sealed class TempSkinsRoot : IDisposable
    {
        public string Root { get; }

        public TempSkinsRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "imm-wpf-skins-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public void WriteSkin(string folderName, string json)
        {
            string dir = Path.Combine(Root, folderName);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, WpfSkinLoader.DefinitionFileName), json);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // 一時掃除失敗は無視
            }
        }
    }
}
