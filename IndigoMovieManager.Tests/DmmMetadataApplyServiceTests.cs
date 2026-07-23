using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmMetadataApplyServiceTests
{
    [Fact]
    public void Apply_writes_blank_fields_and_merges_tags()
    {
        var rec = new MovieRecords
        {
            Movie_Id = 1,
            Comment1 = "",
            Comment2 = "既存コメント2",
            Comment3 = "",
            Title = "",
            Genre = "",
            Artist = "",
            Tags = "既存タグ",
            Tag = ["既存タグ"],
        };

        var item = new DmmItemDto
        {
            Title = "作品タイトル",
            ImageUrl = new DmmImageUrlDto
            {
                Large = "https://pics.dmm.co.jp/digital/video/abc123/abc123pl.jpg",
            },
            ItemInfo = new DmmItemInfo
            {
                Maker = [new DmmNamedEntity { Name = "メーカーA" }],
                Label = [new DmmNamedEntity { Name = "レーベルB" }],
                Series = [new DmmNamedEntity { Name = "シリーズC" }],
                Actress = [new DmmNamedEntity { Name = "女優X" }],
                Genre = [new DmmNamedEntity { Name = "ジャンルY" }, new DmmNamedEntity { Name = "既存タグ" }],
            },
        };

        var service = new DmmMetadataApplyService();
        DmmMetadataApplyService.ApplySummary summary = service.Apply(dbFullPath: null, rec, item);

        Assert.True(summary.WroteComment1);
        Assert.True(summary.WroteComment3);
        Assert.True(summary.WroteTitle);
        Assert.True(summary.WroteGenre);
        Assert.True(summary.WroteArtist);
        Assert.Equal(2, summary.AddedTagCount);

        Assert.Equal("https://pics.dmm.co.jp/digital/video/abc123/abc123pl.jpg", rec.Comment1);
        Assert.Equal("既存コメント2", rec.Comment2);
        Assert.Equal("メーカーA / レーベルB / シリーズC", rec.Comment3);
        Assert.Equal("作品タイトル", rec.Title);
        Assert.Equal("ジャンルY / 既存タグ", rec.Genre);
        Assert.Equal("メーカーA", rec.Artist);
        Assert.Contains("女優X", rec.Tag);
        Assert.Contains("ジャンルY", rec.Tag);
        Assert.Contains("既存タグ", rec.Tag);
        Assert.Equal(3, rec.Tag.Count);
    }

    [Fact]
    public void Apply_manualOverwrite_replaces_jacket_and_title()
    {
        var rec = new MovieRecords
        {
            Movie_Id = 1,
            Comment1 = "https://example.test/oldpl.jpg",
            Comment2 = "",
            Comment3 = "既存メーカー",
            Title = "旧タイトル",
            Genre = "既存ジャンル",
        };

        var item = new DmmItemDto
        {
            Title = "新タイトル",
            ImageUrl = new DmmImageUrlDto
            {
                Large = "https://pics.dmm.co.jp/digital/video/new/newpl.jpg",
            },
        };

        var service = new DmmMetadataApplyService();
        DmmMetadataApplyService.ApplySummary summary = service.Apply(
            dbFullPath: null,
            rec,
            item,
            manualOverwrite: true);

        Assert.True(summary.WroteComment1);
        Assert.True(summary.WroteTitle);
        Assert.False(summary.WroteComment3);
        Assert.False(summary.WroteGenre);
        Assert.Equal("https://pics.dmm.co.jp/digital/video/new/newpl.jpg", rec.Comment1);
        Assert.Equal("新タイトル", rec.Title);
        Assert.Equal("既存メーカー", rec.Comment3);
    }
}

public class DmmJacketUrlsTests
{
    [Theory]
    [InlineData("https://pics.dmm.co.jp/test.jpg", true)]
    [InlineData("https://pics.dmm.co.jp/testpl.jpg", true)]
    [InlineData("not-a-url", false)]
    [InlineData("", false)]
    public void IsHttpUrl_detects_http_urls(string value, bool expected)
    {
        Assert.Equal(expected, DmmJacketUrls.IsHttpUrl(value));
    }

    [Theory]
    [InlineData("https://pics.dmm.com/mono/movie/n/now_printing/now_printing.jpg", true)]
    [InlineData("https://pics.dmm.co.jp/digital/video/abc/abcpl.jpg", false)]
    public void IsPlaceholderJacketUri_detects_now_printing(string url, bool expected)
    {
        Assert.Equal(expected, DmmJacketUrls.IsPlaceholderJacketUri(new Uri(url)));
    }
}
