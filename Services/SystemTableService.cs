using System.Data;

namespace IndigoMovieManager.Services
{
    internal static class SystemTableService
    {
        public static string SelectValue(DataTable systemData, string attr)
        {
            if (systemData == null)
            {
                return "";
            }

            DataRow[] rows = systemData.Select($"attr='{attr}'");
            return rows.Length > 0 ? rows[0]["value"].ToString() : "";
        }

        public static void ApplyToDbInfo(DataTable systemData, DBInfo dbInfo)
        {
            if (systemData == null || dbInfo == null)
            {
                return;
            }

            string skin = SelectValue(systemData, "skin");
            dbInfo.Skin = skin == "" ? "Default Small" : skin;

            string sort = SelectValue(systemData, "sort");
            dbInfo.Sort = sort == "" ? "1" : sort;

            dbInfo.ThumbFolder = SelectValue(systemData, "thum");
            dbInfo.BookmarkFolder = SelectValue(systemData, "bookmark");
            dbInfo.ExcludeExt = SelectValue(systemData, "excludeExt");
            dbInfo.PreGenThumbsOnNewMovies = PreGenThumbSkinSelection.ParseEnabled(
                SelectValue(systemData, PreGenThumbSkinSelection.SystemAttrEnabled));
            dbInfo.PreGenThumbSkinKeys = SelectValue(systemData, PreGenThumbSkinSelection.SystemAttrSkinKeys);
        }
    }
}
