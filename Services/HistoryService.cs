using System.Collections.ObjectModel;
using System.Data;
using IndigoMovieManager.ModelViews;

namespace IndigoMovieManager.Services
{
    internal static class HistoryService
    {
        public const string LatestPerKeywordSql = @"SELECT find_id, find_text, find_date
                            FROM (
                                SELECT *,
                                       ROW_NUMBER() OVER (PARTITION BY find_text ORDER BY find_date DESC) AS rn
                                FROM history
                                )
                            WHERE rn = 1
                            ORDER BY find_date DESC";

        public static void LoadInto(DataTable historyData, ObservableCollection<History> target)
        {
            if (historyData == null)
            {
                return;
            }

            target.Clear();
            var seen = new HashSet<string>();
            foreach (DataRow row in historyData.AsEnumerable())
            {
                string findText = row["find_text"].ToString();
                if (!seen.Add(findText))
                {
                    continue;
                }

                target.Add(new History
                {
                    Find_Id = (long)row["find_id"],
                    Find_Text = findText,
                    Find_Date = ((DateTime)row["find_date"]).ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
    }
}
