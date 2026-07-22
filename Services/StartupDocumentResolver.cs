using System.IO;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 起動引数から開く対象の .wb パスを解決する。
    /// </summary>
    internal static class StartupDocumentResolver
    {
        public static string Resolve(IEnumerable<string> args)
        {
            if (args == null)
            {
                return null;
            }

            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string trimmed = raw.Trim().Trim('"');
                if (!trimmed.EndsWith(".wb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    return Path.GetFullPath(trimmed);
                }
                catch
                {
                    return trimmed;
                }
            }

            return null;
        }
    }
}
