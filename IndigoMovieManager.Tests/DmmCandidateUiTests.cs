using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmInitialKeywordTests
{
    [Fact]
    public void FromMovieName_prefers_product_code()
    {
        string keyword = DmmInitialKeyword.FromMovieName("ABC-123_sample.mp4");
        Assert.Contains("ABC", keyword, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromMovieName_falls_back_to_file_name_without_extension()
    {
        string keyword = DmmInitialKeyword.FromMovieName("素人タイトル作品.mp4");
        Assert.Equal("素人タイトル作品", keyword);
    }

    [Fact]
    public void SuggestSearchVariants_includes_hyphen_and_compact_forms()
    {
        IReadOnlyList<string> variants = DmmInitialKeyword.SuggestSearchVariants("xxxx-024.mp4");

        Assert.Contains("xxxx-024", variants);
        Assert.Contains("xxxx 024", variants);
        Assert.Contains("xxxx024", variants);
        Assert.True(variants.Count >= 2);
    }

    [Fact]
    public void SuggestSearchVariants_includes_space_and_stripped_branch_forms()
    {
        IReadOnlyList<string> variants = DmmInitialKeyword.SuggestSearchVariants("xxxx-024b.mp4");

        Assert.Contains("xxxx-024", variants);
        Assert.Contains("xxxx 024", variants);
        Assert.Contains("xxxx-024b", variants);
    }

    [Fact]
    public void FromMovieName_returns_hyphen_form_for_compact_style_code()
    {
        Assert.Equal("xxxx-024", DmmInitialKeyword.FromMovieName("xxxx-024.mp4"));
    }

    [Fact]
    public void FromMovieName_strips_trailing_branch_letter()
    {
        Assert.Equal("xxxx-024", DmmInitialKeyword.FromMovieName("xxxx-024a.mp4"));
    }
}

public class DmmPendingCandidateStoreTests : IDisposable
{
    private readonly string _dbPath;

    public DmmPendingCandidateStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"imm-dmm-pending-{Guid.NewGuid():N}.wb");
        SQLite.CreateDatabase(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public void Save_list_and_delete_roundtrip()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            new()
            {
                FloorLabel = "videoa",
                Item = new DmmItemDto
                {
                    ContentId = "abc123",
                    Title = "テスト作品",
                },
            },
        };

        DmmPendingCandidateStore.Save(_dbPath, 10, "sample.mp4", "abc-123", candidates, "auto");

        List<DmmPendingCandidateRecord> listed = DmmPendingCandidateStore.List(_dbPath);
        Assert.Single(listed);
        Assert.Equal(10, listed[0].MovieId);
        Assert.Equal("sample.mp4", listed[0].MovieName);
        Assert.Equal("abc-123", listed[0].InitialKeyword);
        Assert.Equal("auto", listed[0].Source);
        Assert.Single(listed[0].Candidates);
        Assert.Equal("abc123", listed[0].Candidates[0].Item.ContentId);

        DmmPendingCandidateStore.Delete(_dbPath, listed[0].PendingId);
        Assert.Empty(DmmPendingCandidateStore.List(_dbPath));
    }

    [Fact]
    public void Save_overwrites_existing_movie_id()
    {
        DmmPendingCandidateStore.Save(_dbPath, 5, "a.mp4", "kw1", [], "auto");
        DmmPendingCandidateStore.Save(_dbPath, 5, "a.mp4", "kw2", [], "bulk");

        List<DmmPendingCandidateRecord> listed = DmmPendingCandidateStore.List(_dbPath);
        Assert.Single(listed);
        Assert.Equal("kw2", listed[0].InitialKeyword);
        Assert.Equal("bulk", listed[0].Source);
    }

    [Fact]
    public void Count_and_DeleteOrphaned_work()
    {
        DmmPendingCandidateStore.Save(_dbPath, 999, "orphan.mp4", "kw", [], "auto");
        Assert.Equal(1, DmmPendingCandidateStore.Count(_dbPath));

        int removed = DmmPendingCandidateStore.DeleteOrphaned(_dbPath);
        Assert.Equal(1, removed);
        Assert.Equal(0, DmmPendingCandidateStore.Count(_dbPath));
    }

    [Fact]
    public void DeleteByMovieId_removes_pending_for_movie()
    {
        DmmPendingCandidateStore.Save(_dbPath, 42, "x.mp4", "kw", [], "auto");
        DmmPendingCandidateStore.DeleteByMovieId(_dbPath, 42);
        Assert.Empty(DmmPendingCandidateStore.List(_dbPath));
    }
}

public class DmmCandidateDisplayTests
{
    [Fact]
    public void FromEntry_formats_row_fields()
    {
        var row = DmmCandidateRow.FromEntry(new DmmCandidateEntry
        {
            FloorLabel = "dvd",
            Item = new DmmItemDto
            {
                ContentId = "cid001",
                Title = "作品名",
                ItemInfo = new DmmItemInfo
                {
                    Maker = [new DmmNamedEntity { Name = "MakerX" }],
                    Label = [new DmmNamedEntity { Name = "LabelY" }],
                },
            },
        });

        Assert.Equal("作品名", row.Title);
        Assert.Equal("cid001", row.ContentId);
        Assert.Equal("MakerX / LabelY", row.MakerLabelSeries);
        Assert.Equal("dvd", row.FloorLabel);
        Assert.Equal("×", row.JacketLabel);
    }

    [Fact]
    public void FromEntry_shows_jacket_label_when_large_url_exists()
    {
        var row = DmmCandidateRow.FromEntry(new DmmCandidateEntry
        {
            FloorLabel = "videoa",
            Item = new DmmItemDto
            {
                ContentId = "abc123",
                ImageUrl = new DmmImageUrlDto { Large = "https://pics.dmm.co.jp/testpl.jpg" },
            },
        });

        Assert.Equal("○", row.JacketLabel);
        Assert.True(row.HasJacket);
    }

    [Fact]
    public void FromEntries_orders_jacket_first_and_PreferSelection_picks_jacket()
    {
        var entries = new List<DmmCandidateEntry>
        {
            new()
            {
                FloorLabel = "dvd",
                Item = new DmmItemDto
                {
                    ContentId = "nojacket",
                    Title = "ジャケなし",
                },
            },
            new()
            {
                FloorLabel = "videoa",
                Item = new DmmItemDto
                {
                    ContentId = "withjacket",
                    Title = "ジャケあり",
                    ImageUrl = new DmmImageUrlDto { Large = "https://pics.dmm.co.jp/abc123pl.jpg" },
                },
            },
        };

        List<DmmCandidateRow> rows = DmmCandidateRow.FromEntries(entries);
        Assert.Equal("withjacket", rows[0].ContentId);
        Assert.Equal("nojacket", rows[1].ContentId);

        DmmCandidateRow preferred = DmmCandidateRow.PreferSelection(rows);
        Assert.Equal("withjacket", preferred.ContentId);
    }

    [Fact]
    public void PreferSelection_selects_single_candidate_without_jacket()
    {
        List<DmmCandidateRow> rows = DmmCandidateRow.FromEntries(
        [
            new DmmCandidateEntry
            {
                FloorLabel = "dvd",
                Item = new DmmItemDto { ContentId = "onlyone", Title = "単独" },
            },
        ]);

        Assert.Equal("onlyone", DmmCandidateRow.PreferSelection(rows).ContentId);
    }
}

public class DmmCidNormalizerCompactFormTests
{
    [Fact]
    public void ExtractFromFileName_includes_compact_cid()
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName("xxxx-024.mp4");

        Assert.True(result.HasProductCode);
        Assert.Equal("xxxx-024", result.ProductCode);
        Assert.Contains("xxxx024", result.CidCandidates);
    }
}
