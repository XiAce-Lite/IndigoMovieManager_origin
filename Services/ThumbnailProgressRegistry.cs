namespace IndigoMovieManager.Services
{
    /// <summary>
    /// サムネイル進捗ポップアップを全ループ横断で追跡し、DB 切替時に一括破棄する。
    /// </summary>
    internal static class ThumbnailProgressRegistry
    {
        private static readonly object Lock = new();
        private static readonly List<ThumbnailProgressSession> Active = [];

        public static void Register(ThumbnailProgressSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (Lock)
            {
                Active.Add(session);
            }
        }

        public static void Unregister(ThumbnailProgressSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (Lock)
            {
                Active.Remove(session);
            }
        }

        public static void DismissAll()
        {
            List<ThumbnailProgressSession> copy;
            lock (Lock)
            {
                copy = [.. Active];
                Active.Clear();
            }

            foreach (ThumbnailProgressSession session in copy)
            {
                try
                {
                    session.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
