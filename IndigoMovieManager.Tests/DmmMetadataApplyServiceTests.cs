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
            Comment2 = "既存メーカー",
            Comment3 = "",
            Title = "",
            Genre = "",
            Tags = "既存タグ",
            Tag = ["既存タグ"],
        };

        var item = new DmmItemDto
        {
            Title = "作品タイトル",
            AffiliateUrl = "https://al.fanza.co.jp/?lurl=example",
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
        Assert.False(summary.WroteComment2);
        Assert.True(summary.WroteComment3);
        Assert.True(summary.WroteTitle);
        Assert.True(summary.WroteGenre);
        Assert.Equal(2, summary.AddedTagCount);

        Assert.Equal("作品タイトル", rec.Comment1);
        Assert.Equal("既存メーカー", rec.Comment2);
        Assert.Equal("https://al.fanza.co.jp/?lurl=example", rec.Comment3);
        Assert.Equal("作品タイトル", rec.Title);
        Assert.Equal("ジャンルY / 既存タグ", rec.Genre);
        Assert.Contains("女優X", rec.Tag);
        Assert.Contains("ジャンルY", rec.Tag);
        Assert.Contains("既存タグ", rec.Tag);
        Assert.Equal(3, rec.Tag.Count);
    }
}
