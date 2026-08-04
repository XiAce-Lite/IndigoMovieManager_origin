using System.Collections.Generic;
using IndigoMovieManager.Services.WpfSkin;

namespace IndigoMovieManager.Services.WpfSkin.Design
{
    /// <summary>
    /// 構造テンプレート 1 件。スキンのノードツリーを直接組み立てる。
    /// </summary>
    public sealed class WpfSkinStructTemplate
    {
        public string Id          { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";

        // サムネイル既定サイズ
        public int ThumbWidth  { get; init; } = 320;
        public int ThumbHeight { get; init; } = 180;
        public int ThumbColumns { get; init; } = 1;
        public int ThumbRows { get; init; } = 1;

        /// <summary>true なら thumbnail.preferJacket を立てる。</summary>
        public bool PreferJacket { get; init; }

        /// <summary>true なら comment1+local の thumbnail.sources（同居）を設定する。</summary>
        public bool UseCoexistSources { get; init; }

        // カード全体
        public double CardWidth   { get; init; } = 0;
        public bool   CardStretch { get; init; } = false;

        /// <summary>ルートノードを組み立てる。</summary>
        public Func<WpfSkinNode> BuildLayout { get; init; }

        /// <summary>既定 styles を返す。</summary>
        public Func<Dictionary<string, WpfSkinStyle>> BuildStyles { get; init; }

        public WpfSkinStructTemplate()
        {
            BuildLayout = DefaultLayout;
            BuildStyles = DefaultStyles;
        }

        private static WpfSkinNode DefaultLayout() => new()
        {
            Panel = "stack",
            Stack = "vertical",
            Children = new List<WpfSkinNode>(),
        };

        private static Dictionary<string, WpfSkinStyle> DefaultStyles() => WpfSkinStructTemplateCatalog.BaseStyles();
    }

    /// <summary>
    /// 組み込み構造テンプレートの一覧。
    /// テンプレギャラリーの「構造から」タブに表示する。
    /// </summary>
    internal static class WpfSkinStructTemplateCatalog
    {
        // ──────────────────────────────────────────────
        //  公開 API
        // ──────────────────────────────────────────────

        public static IReadOnlyList<WpfSkinStructTemplate> All { get; } = Build();

        // ──────────────────────────────────────────────
        //  共通スタイル
        // ──────────────────────────────────────────────

        public static Dictionary<string, WpfSkinStyle> BaseStyles() => new()
        {
            ["title"] = new WpfSkinStyle { FontSize = 14, Bold = true, Foreground = "#1A1A1A", Wrap = true },
            ["meta"]  = new WpfSkinStyle { FontSize = 11, Foreground = "#555555" },
            ["path"]  = new WpfSkinStyle { FontSize = 10, Foreground = "#1060C0" },
        };

        // ──────────────────────────────────────────────
        //  テンプレートビルダ
        // ──────────────────────────────────────────────

        private static List<WpfSkinStructTemplate> Build() =>
        [
            // ── 1) 縦並び（サムネなし）────────────────────────────────
            new()
            {
                Id = "vertical_plain",
                DisplayName = "縦並び（シンプル）",
                Description = "1 列・縦方向に要素を積む最もシンプルな構造。",
                ThumbWidth = 0, ThumbHeight = 0,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "stack", Stack = "vertical",
                    Children = [],
                },
                BuildStyles = BaseStyles,
            },

            // ── 2) 左サムネ + 右テキスト（横 2 列）──────────────────────
            new()
            {
                Id = "thumb_left_text_right",
                DisplayName = "左サムネ ＋ 右テキスト",
                Description = "左にサムネイル、右に縦並びテキスト情報を置く定番レイアウト。",
                ThumbWidth = 320, ThumbHeight = 180,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "grid",
                    Columns = ["320", "*"],
                    Rows = ["*"],
                    Children =
                    [
                        new WpfSkinNode
                        {
                            Type = "thumbnail",
                            Row = 0, Col = 0,
                            VAlign = "stretch", HAlign = "left",
                        },
                        new WpfSkinNode
                        {
                            Panel = "stack", Stack = "vertical",
                            Row = 0, Col = 1,
                            Margin = new WpfSkinSpacing { Left = 8, Top = 4, Right = 4, Bottom = 4 },
                            Children = [],
                        },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 3) 右サムネ + 左テキスト（横 2 列・逆）──────────────────
            new()
            {
                Id = "text_left_thumb_right",
                DisplayName = "左テキスト ＋ 右サムネ",
                Description = "左に縦並びテキスト情報、右にサムネイルを置くレイアウト。",
                ThumbWidth = 320, ThumbHeight = 180,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "grid",
                    Columns = ["*", "320"],
                    Rows = ["*"],
                    Children =
                    [
                        new WpfSkinNode
                        {
                            Panel = "stack", Stack = "vertical",
                            Row = 0, Col = 0,
                            Margin = new WpfSkinSpacing { Left = 4, Top = 4, Right = 8, Bottom = 4 },
                            Children = [],
                        },
                        new WpfSkinNode
                        {
                            Type = "thumbnail",
                            Row = 0, Col = 1,
                            VAlign = "stretch", HAlign = "right",
                        },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 4) 上サムネ + 下テキスト（縦 2 行）──────────────────────
            new()
            {
                Id = "thumb_top_text_bottom",
                DisplayName = "上サムネ ＋ 下テキスト",
                Description = "上部にサムネイル、下部にテキスト情報を縦に置くカード型。",
                ThumbWidth = 320, ThumbHeight = 180,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "grid",
                    Columns = ["*"],
                    Rows = ["auto", "*"],
                    Children =
                    [
                        new WpfSkinNode
                        {
                            Type = "thumbnail",
                            Row = 0, Col = 0,
                            HAlign = "center",
                        },
                        new WpfSkinNode
                        {
                            Panel = "stack", Stack = "vertical",
                            Row = 1, Col = 0,
                            Margin = new WpfSkinSpacing { Left = 4, Top = 6, Right = 4, Bottom = 4 },
                            Children = [],
                        },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 5) 左サムネ + 右 2 段（タイトル行 + メタ行）───────────────
            new()
            {
                Id = "thumb_left_2row_right",
                DisplayName = "左サムネ ＋ 右 2 段",
                Description = "左にサムネ、右を「タイトル行」と「メタ情報行」の 2 段に分けたレイアウト。",
                ThumbWidth = 320, ThumbHeight = 180,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "grid",
                    Columns = ["320", "*"],
                    Rows = ["*"],
                    Children =
                    [
                        new WpfSkinNode
                        {
                            Type = "thumbnail",
                            Row = 0, Col = 0,
                            VAlign = "stretch",
                        },
                        new WpfSkinNode
                        {
                            Panel = "grid",
                            Columns = ["*"],
                            Rows = ["auto", "*"],
                            Row = 0, Col = 1,
                            Margin = new WpfSkinSpacing { Left = 8, Top = 4, Right = 4, Bottom = 4 },
                            Children =
                            [
                                // タイトル行
                                new WpfSkinNode
                                {
                                    Panel = "stack", Stack = "vertical",
                                    Row = 0, Col = 0,
                                    Children = [],
                                },
                                // メタ行
                                new WpfSkinNode
                                {
                                    Panel = "stack", Stack = "vertical",
                                    Row = 1, Col = 0,
                                    Children = [],
                                },
                            ],
                        },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 6) 左ジャケ + 右ローカル 5×2（同居）─────────────────────
            new()
            {
                Id = "jacket_local_side",
                DisplayName = "左ジャケ ＋ 右ローカル（同居）",
                Description = "左にジャケ写（360×203）、右上にローカル 5×2（120×90）、右下にファイル名／タグ。JacketLocalSide 相当。",
                ThumbWidth = 120,
                ThumbHeight = 90,
                ThumbColumns = 5,
                ThumbRows = 2,
                UseCoexistSources = true,
                CardWidth = 980,
                CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "stack",
                    Stack = "horizontal",
                    VAlign = "top",
                    Children =
                    [
                        new WpfSkinNode
                        {
                            Type = "thumbnail",
                            Source = "comment1",
                            Width = WpfSkinThumbnailSources.JacketInfoFallbackWidth,
                            Height = WpfSkinThumbnailSources.JacketInfoFallbackHeight,
                            VAlign = "top",
                            HAlign = "left",
                            Margin = new WpfSkinSpacing { Right = 8 },
                        },
                        new WpfSkinNode
                        {
                            Panel = "stack",
                            Stack = "vertical",
                            VAlign = "top",
                            HAlign = "left",
                            MaxWidth = WpfSkinThumbnailSources.DefaultBig10DisplayWidth,
                            Children =
                            [
                                new WpfSkinNode
                                {
                                    Type = "thumbnail",
                                    Source = "local",
                                    Width = WpfSkinThumbnailSources.DefaultBig10DisplayWidth,
                                    Height = WpfSkinThumbnailSources.DefaultBig10DisplayHeight,
                                    VAlign = "top",
                                    HAlign = "left",
                                },
                                new WpfSkinNode
                                {
                                    Type = "text",
                                    Field = "title",
                                    Style = "title",
                                    Wrap = true,
                                    Margin = new WpfSkinSpacing { Top = 6, Bottom = 4 },
                                },
                                new WpfSkinNode { Type = "tags" },
                            ],
                        },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 7) 横 3 列（均等）────────────────────────────────────
            new()
            {
                Id = "3col_equal",
                DisplayName = "横 3 列（均等）",
                Description = "等幅の 3 列 grid。各列に自由に要素を配置できる。",
                ThumbWidth = 0, ThumbHeight = 0,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "grid",
                    Columns = ["*", "*", "*"],
                    Rows = ["*"],
                    Children =
                    [
                        new WpfSkinNode { Panel = "stack", Stack = "vertical", Row = 0, Col = 0, Children = [] },
                        new WpfSkinNode { Panel = "stack", Stack = "vertical", Row = 0, Col = 1, Children = [] },
                        new WpfSkinNode { Panel = "stack", Stack = "vertical", Row = 0, Col = 2, Children = [] },
                    ],
                },
                BuildStyles = BaseStyles,
            },

            // ── 8) リスト行（list 型用）──────────────────────────────────
            new()
            {
                Id = "list_row",
                DisplayName = "リスト行（list 型）",
                Description = "list 型スキン用。ヘッダーなしの横並び 1 行テンプレート。",
                ThumbWidth = 0, ThumbHeight = 0,
                CardWidth = 0, CardStretch = false,
                BuildLayout = () => new WpfSkinNode
                {
                    Panel = "stack", Stack = "horizontal",
                    Children = [],
                },
                BuildStyles = BaseStyles,
            },
        ];
    }
}
