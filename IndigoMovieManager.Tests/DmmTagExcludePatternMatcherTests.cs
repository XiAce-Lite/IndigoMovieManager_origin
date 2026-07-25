using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmTagExcludePatternMatcherTests
{
    [Theory]
    [InlineData("ハイビジョン", "^ハイビジョン$")]
    [InlineData("独占*", "^独占.*$")]
    [InlineData("*版", "^.*版$")]
    [InlineData("a?b", "^a.b$")]
    public void GlobToAnchoredRegex_converts_wildcards(string pattern, string expected)
    {
        Assert.Equal(expected, DmmTagExcludePatternMatcher.GlobToAnchoredRegex(pattern));
    }

    [Fact]
    public void IsExcluded_matches_exact_prefix_suffix_and_regex()
    {
        var matcher = new DmmTagExcludePatternMatcher();
        matcher.ReloadFrom("""
            ハイビジョン
            独占*
            *版
            re:^限定.+配信$
            """);

        Assert.True(matcher.IsExcluded("ハイビジョン"));
        Assert.True(matcher.IsExcluded("独占"));
        Assert.True(matcher.IsExcluded("独占配信"));
        Assert.True(matcher.IsExcluded("廉価版"));
        Assert.True(matcher.IsExcluded("限定プレミアム配信"));
        Assert.False(matcher.IsExcluded("ドラマ"));
        Assert.False(matcher.IsExcluded("配信独占"));
    }

    [Fact]
    public void Validate_reports_invalid_regex_lines()
    {
        DmmTagExcludePatternMatcher.ParseResult result = DmmTagExcludePatternMatcher.Validate("""
            ハイビジョン
            re:(
            """);

        Assert.False(result.IsValid);
        Assert.Equal(1, result.PatternCount);
        Assert.Single(result.InvalidLines);
    }

    [Fact]
    public void NormalizeForStorage_removes_duplicate_keywords_case_insensitive()
    {
        string normalized = DmmTagExcludePatternMatcher.NormalizeForStorage("""
            ハイビジョン
            4K
            ハイビジョン
            4k
            独占*
            """);

        string[] lines = normalized.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["ハイビジョン", "4K", "独占*"], lines);
    }
}

public class DmmMetadataApplyServiceExcludeTests
{
    [Fact]
    public void Apply_skips_excluded_genres_but_keeps_actress_and_genre_column()
    {
        DmmTagExcludePatternMatcher.Shared.ReloadFrom("""
            ハイビジョン
            独占*
            """);

        var rec = new MovieRecords
        {
            Movie_Id = 1,
            Comment1 = "",
            Comment3 = "",
            Title = "",
            Genre = "",
            Artist = "",
            Tags = "",
            Tag = [],
        };

        var item = new DmmItemDto
        {
            Title = "作品タイトル",
            ItemInfo = new DmmItemInfo
            {
                Actress = [new DmmNamedEntity { Name = "女優X" }],
                Genre =
                [
                    new DmmNamedEntity { Name = "ハイビジョン" },
                    new DmmNamedEntity { Name = "独占配信" },
                    new DmmNamedEntity { Name = "ドラマ" },
                ],
            },
        };

        var service = new DmmMetadataApplyService();
        DmmMetadataApplyService.ApplySummary summary = service.Apply(dbFullPath: null, rec, item);

        Assert.True(summary.WroteGenre);
        Assert.Equal("ハイビジョン / 独占配信 / ドラマ", rec.Genre);
        Assert.Contains("女優X", rec.Tag);
        Assert.Contains("ドラマ", rec.Tag);
        Assert.DoesNotContain("ハイビジョン", rec.Tag);
        Assert.DoesNotContain("独占配信", rec.Tag);
        Assert.Equal(2, summary.AddedTagCount);
        Assert.Equal(2, rec.Tag.Count);

        DmmTagExcludePatternMatcher.Shared.ReloadFrom("");
    }

    [Fact]
    public void Apply_removes_existing_tags_that_match_exclude_list()
    {
        DmmTagExcludePatternMatcher.Shared.ReloadFrom("""
            ハイビジョン
            独占*
            """);

        var rec = new MovieRecords
        {
            Movie_Id = 1,
            Comment1 = "https://example.test/pl.jpg",
            Comment3 = "既存",
            Title = "既存タイトル",
            Genre = "既存ジャンル",
            Artist = "既存メーカー",
            Tags = "ハイビジョン" + Environment.NewLine + "ドラマ" + Environment.NewLine + "独占配信" + Environment.NewLine + "女優X",
            Tag = ["ハイビジョン", "ドラマ", "独占配信", "女優X"],
        };

        var item = new DmmItemDto
        {
            Title = "新タイトル",
            ItemInfo = new DmmItemInfo
            {
                Actress = [new DmmNamedEntity { Name = "女優X" }],
                Genre =
                [
                    new DmmNamedEntity { Name = "ハイビジョン" },
                    new DmmNamedEntity { Name = "ドラマ" },
                ],
            },
        };

        var service = new DmmMetadataApplyService();
        DmmMetadataApplyService.ApplySummary summary = service.Apply(dbFullPath: null, rec, item);

        Assert.Equal(2, summary.RemovedTagCount);
        Assert.Equal(0, summary.AddedTagCount);
        Assert.DoesNotContain("ハイビジョン", rec.Tag);
        Assert.DoesNotContain("独占配信", rec.Tag);
        Assert.Contains("ドラマ", rec.Tag);
        Assert.Contains("女優X", rec.Tag);
        Assert.Equal(2, rec.Tag.Count);
        Assert.Equal("既存ジャンル", rec.Genre);

        DmmTagExcludePatternMatcher.Shared.ReloadFrom("");
    }
}
