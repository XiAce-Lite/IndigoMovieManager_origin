using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// Skins/Wpf/&lt;name&gt;/skin.json を読み込む。
    /// </summary>
    internal static class WpfSkinLoader
    {
        public const string RootFolderName = "Wpf";
        public const string DefinitionFileName = "skin.json";
        public const string DefaultSkinName = "CardLarge";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static string SkinsRoot =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skins", RootFolderName);

        /// <summary>利用可能なスキンフォルダ名を列挙する。</summary>
        public static IReadOnlyList<string> EnumerateSkins() =>
            EnumerateSkinsFrom(SkinsRoot);

        /// <summary>指定ルート配下のスキンフォルダ名を列挙する（テスト・一時フォルダ用）。</summary>
        public static IReadOnlyList<string> EnumerateSkinsFrom(string skinsRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(skinsRoot) || !Directory.Exists(skinsRoot))
                {
                    return Array.Empty<string>();
                }

                IEnumerable<string> names = Directory.GetDirectories(skinsRoot)
                    .Where(dir => File.Exists(Path.Combine(dir, DefinitionFileName)))
                    .Select(Path.GetFileName);
                return SkinNameSortHelper.OrderDefaultFirst(names);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>既定スキンを読み込む。存在しなければ列挙先頭、なければ組み込み既定。</summary>
        public static WpfSkinDefinition LoadDefault() =>
            LoadDefaultFrom(SkinsRoot);

        /// <summary>指定ルートで既定スキンを解決する（テスト・一時フォルダ用）。</summary>
        public static WpfSkinDefinition LoadDefaultFrom(string skinsRoot)
        {
            if (TryLoadFrom(skinsRoot, DefaultSkinName, out WpfSkinDefinition def))
            {
                return def;
            }

            foreach (string name in EnumerateSkinsFrom(skinsRoot))
            {
                if (TryLoadFrom(skinsRoot, name, out def))
                {
                    return def;
                }
            }

            return CreateBuiltInDefault();
        }

        public static bool TryLoad(string skinName, out WpfSkinDefinition definition) =>
            TryLoadFrom(SkinsRoot, skinName, out definition);

        /// <summary>指定ルート下の &lt;skinName&gt;/skin.json を読み込む（テスト・一時フォルダ用）。</summary>
        public static bool TryLoadFrom(string skinsRoot, string skinName, out WpfSkinDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(skinsRoot) || string.IsNullOrWhiteSpace(skinName))
            {
                return false;
            }

            try
            {
                string path = Path.Combine(skinsRoot, skinName, DefinitionFileName);
                if (!File.Exists(path))
                {
                    return false;
                }

                string json = File.ReadAllText(path);
                WpfSkinDefinition parsed = JsonSerializer.Deserialize<WpfSkinDefinition>(json, JsonOptions);
                if (parsed == null)
                {
                    return false;
                }

                parsed.Thumbnail ??= new WpfSkinThumbnail();
                parsed.Card ??= new WpfSkinCard();
                parsed.Card.Layout ??= new WpfSkinNode();
                parsed.Surface ??= new WpfSkinSurface();
                parsed.Styles ??= new Dictionary<string, WpfSkinStyle>();
                if (string.IsNullOrWhiteSpace(parsed.Name))
                {
                    parsed.Name = skinName;
                }

                // Name は表示名になり得るため、フォルダキーは別途保持する
                parsed.FolderName = skinName;

                definition = parsed;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WpfSkinLoader] TryLoad failed: {skinName} : {ex.Message}");
                return false;
            }
        }

        /// <summary>skin.json が無くても最低限描画できる組み込み既定。</summary>
        public static WpfSkinDefinition CreateBuiltInDefault()
        {
            return new WpfSkinDefinition
            {
                Name = DefaultSkinName,
                FolderName = DefaultSkinName,
                Type = "card",
                Thumbnail = new WpfSkinThumbnail
                {
                    Width = 400,
                    Height = 225,
                    Columns = 1,
                    Rows = 1,
                },
                Card = new WpfSkinCard
                {
                    Padding = 8,
                    Background = "",
                    Layout = new WpfSkinNode
                    {
                        Stack = "vertical",
                        Children =
                        [
                            new WpfSkinNode { Type = "text", Field = "title", Align = "left", FontSize = 13, Bold = true, Foreground = "#000000" },
                            new WpfSkinNode { Type = "thumbnail" },
                        ],
                    },
                },
            };
        }
    }
}
