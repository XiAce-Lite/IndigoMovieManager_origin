using System.Collections.ObjectModel;
using System.Data;

namespace IndigoMovieManager.Services
{
    internal static class TagBarService
    {
        public static readonly string[] BuiltInStarRatingTitles =
            ["★★★★★", "★★★★", "★★★", "★★", "★"];

        public const string SelectAllOrderedSql =
            "SELECT item_id, parent_id, order_id, group_id, title, contents FROM tagbar ORDER BY order_id, item_id";

        public static void LoadInto(DataTable tagBarData, ObservableCollection<TagBarItem> target)
        {
            if (tagBarData == null)
            {
                return;
            }

            target.Clear();
            foreach (DataRow row in tagBarData.AsEnumerable()
                         .OrderBy(r => Convert.ToInt64(r["order_id"]))
                         .ThenBy(r => Convert.ToInt64(r["item_id"])))
            {
                target.Add(new TagBarItem
                {
                    Item_Id = Convert.ToInt64(row["item_id"]),
                    Parent_Id = Convert.ToInt64(row["parent_id"]),
                    Order_Id = Convert.ToInt64(row["order_id"]),
                    Group_Id = Convert.ToInt64(row["group_id"]),
                    Title = row["title"]?.ToString() ?? "",
                    Contents = row["contents"]?.ToString() ?? "",
                });
            }
        }

        public static string BuildDuplicateTitle(string title)
        {
            string baseTitle = string.IsNullOrWhiteSpace(title) ? "無題" : title.Trim();
            return $"{baseTitle} (コピー)";
        }

        /// <summary>
        /// 新規 DB 作成時に挿入する既定の★評価ボタンかどうか（削除不可）。
        /// </summary>
        public static bool IsBuiltInStarRating(TagBarItem item)
        {
            if (item == null)
            {
                return false;
            }

            return IsBuiltInStarRatingTitle(item.Title);
        }

        public static bool IsBuiltInStarRatingTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            return BuiltInStarRatingTitles.Contains(title.Trim());
        }

        /// <summary>
        /// DB の contents が空のときは title を検索・表示用に流用する（DB には書き込まない）。
        /// </summary>
        public static string GetEffectiveContents(TagBarItem item)
        {
            if (item == null)
            {
                return "";
            }

            if (!string.IsNullOrWhiteSpace(item.Contents))
            {
                return item.Contents.Trim();
            }

            return item.Title?.Trim() ?? "";
        }

        /// <summary>
        /// 保存時に title / contents のどちらか一方が空なら、もう一方の値で補完する。
        /// 両方空のときは false。
        /// </summary>
        public static bool TryNormalizeSaveFields(ref string title, ref string contents)
        {
            title = title?.Trim() ?? "";
            contents = contents?.Trim() ?? "";

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(contents))
            {
                return false;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = contents;
            }
            else if (string.IsNullOrEmpty(contents))
            {
                contents = title;
            }

            return true;
        }

        /// <summary>
        /// 中クリックでタグ追記するとき、空白区切りの各語を個別タグに展開する。
        /// </summary>
        public static string ExpandContentsForTagAppend(string effectiveContents)
        {
            if (string.IsNullOrWhiteSpace(effectiveContents))
            {
                return "";
            }

            string[] tokens = effectiveContents.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return "";
            }

            return string.Join(Environment.NewLine, tokens);
        }
    }
}
