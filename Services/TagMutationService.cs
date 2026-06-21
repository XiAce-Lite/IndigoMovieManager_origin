using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Services
{
    internal static class TagMutationService
    {
        public static void ApplyPaste(MovieRecords rec, string clipboardText)
        {
            rec.Tags = clipboardText;
            List<string> tagArray = [];
            foreach (string tagItem in rec.Tags.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Distinct())
            {
                tagArray.Add(tagItem);
            }

            rec.Tag = tagArray;
        }

        public static void ApplyAdd(MovieRecords rec, string addedTags)
        {
            string tagsEditedWithNewLine = rec.Tags + Environment.NewLine + addedTags;
            List<string> tagArray = [];
            string tagsWithNewLine = "";
            if (!string.IsNullOrEmpty(tagsEditedWithNewLine))
            {
                string[] splitTags = tagsEditedWithNewLine.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                tagsWithNewLine = ConvertTagsWithNewLine([.. splitTags]);
                foreach (string tagItem in splitTags.Distinct())
                {
                    tagArray.Add(tagItem);
                }
            }

            rec.Tag = tagArray;
            rec.Tags = tagsWithNewLine;
        }

        public static void ApplyDelete(MovieRecords rec, string tagsToRemove)
        {
            List<string> tagArray = rec.Tag;
            if (string.IsNullOrEmpty(tagsToRemove))
            {
                return;
            }

            foreach (string tagItem in tagsToRemove.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Distinct())
            {
                tagArray.Remove(tagItem);
            }

            rec.Tag = tagArray;
            rec.Tags = ConvertTagsWithNewLine([.. tagArray]);
        }

        public static void ApplyEdit(MovieRecords rec, string tagsEditedWithNewLine)
        {
            if (!string.IsNullOrEmpty(tagsEditedWithNewLine))
            {
                string[] splitTags = tagsEditedWithNewLine.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                string tagsWithNewLine = ConvertTagsWithNewLine([.. splitTags]);
                List<string> tagArray = [];
                foreach (string tagItem in splitTags.Distinct())
                {
                    tagArray.Add(tagItem);
                }

                rec.Tag = tagArray;
                rec.Tags = tagsWithNewLine;
            }
            else
            {
                rec.Tag = [];
                rec.Tags = "";
            }
        }
    }
}
