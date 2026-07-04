using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    internal class SQLite
    {
        private static readonly object HistoryInsertLock = new();

        public static DataTable GetData(string dbFullPath, string sql)
        {
            return SqliteDataAccess.Query(dbFullPath, sql);
        }

        public static void CreateDatabase(string dbFullPath)
        {
            try
            {
                SQLiteConnection.CreateFile(dbFullPath);
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    //bookmark
                    cmd.CommandText = @"
                        CREATE TABLE bookmark(
                        movie_id integer primary key not null, 
                        movie_name text not null default '', 
                        movie_path text not null default '', 
                        movie_length integer not null default 0, 
                        movie_size integer not null default 0, 
                        last_date datetime not null, 
                        file_date datetime not null, 
                        regist_date datetime not null, 
                        score integer not null default 0, 
                        view_count integer not null default 0, 
                        hash text not null default '', 
                        container text not null default '', 
                        video text not null default '', 
                        audio text not null default '', 
                        extra text not null default '', 
                        title text not null default '', 
                        artist text not null default '', 
                        album text not null default '', 
                        grouping text not null default '', 
                        writer text not null default '', 
                        genre text not null default '', 
                        track text not null default '', 
                        camera text not null default '', 
                        create_time text not null default '', 
                        kana text not null default '', 
                        roma text not null default '', 
                        tag text not null default '', 
                        comment1 text not null default '', 
                        comment2 text not null default '', 
                        comment3 text not null default '' )";
                    cmd.ExecuteNonQuery();
                    //findfact
                    cmd.CommandText = @"
                        CREATE TABLE findfact(
                        find_text text primary key not null, 
                        find_count integer not null default 0, 
                        last_date datetime not null )";
                    cmd.ExecuteNonQuery();
                    //history
                    cmd.CommandText = @"
                        CREATE TABLE history(
                        find_id integer primary key not null, 
                        find_text text not null, 
                        find_date datetime not null )";
                    cmd.ExecuteNonQuery();
                    //movie
                    cmd.CommandText = @"
                        CREATE TABLE movie(movie_id integer primary key not null, 
                        movie_name text not null default '', 
                        movie_path text not null default '', 
                        movie_length integer not null default 0, 
                        movie_size integer not null default 0, 
                        last_date datetime not null, 
                        file_date datetime not null, 
                        regist_date datetime not null, 
                        score integer not null default 0, 
                        view_count integer not null default 0, 
                        hash text not null default '', 
                        container text not null default '', 
                        video text not null default '', 
                        audio text not null default '', 
                        extra text not null default '', 
                        title text not null default '', 
                        artist text not null default '', 
                        album text not null default '', 
                        grouping text not null default '', 
                        writer text not null default '', 
                        genre text not null default '', 
                        track text not null default '', 
                        camera text not null default '', 
                        create_time text not null default '', 
                        kana text not null default '', 
                        roma text not null default '', 
                        tag text not null default '', 
                        comment1 text not null default '', 
                        comment2 text not null default '', 
                        comment3 text not null default '' )";
                    cmd.ExecuteNonQuery();
                    //profile
                    cmd.CommandText = @"
                        CREATE TABLE profile(
                        skin text not null, 
                        key text not null, 
                        value text not null, 
                        primary key(skin, key))";
                    cmd.ExecuteNonQuery();
                    //sysbin
                    cmd.CommandText = @"
                        CREATE TABLE sysbin(attr text primary key not null, value blob not null )";
                    cmd.ExecuteNonQuery();
                    //system
                    cmd.CommandText = @"
                        CREATE TABLE system(attr text primary key not null, value text not null )";
                    cmd.ExecuteNonQuery();
                    //tagbar
                    cmd.CommandText = @"
                        CREATE TABLE tagbar(item_id integer primary key not null, 
                        parent_id integer not null default 0, 
                        order_id integer not null default 0, 
                        group_id integer not null default 0, 
                        title text not null default '', 
                        contents text not null default '' )";
                    cmd.ExecuteNonQuery();

                    EnsureBuiltInStarRatingItemsCore(connection, transaction);

                    //watch
                    cmd.CommandText = @"
                        CREATE TABLE watch(dir text primary key not null, 
                        auto integer not null default 0, 
                        watch integer not null default 0, 
                        sub integer not null default 1 )";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void DeleteWatchTable(string dbFullPath)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "delete from watch";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void InsertWatchTable(string dbFullPath, WatchRecords watchRec)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "insert into watch (dir,auto,watch,sub) values (@dir,@auto,@watch,@sub)";
                    cmd.Parameters.Add(new SQLiteParameter("@dir", watchRec.Dir));
                    cmd.Parameters.Add(new SQLiteParameter("@auto", watchRec.Auto == true ? 1 : 0));
                    cmd.Parameters.Add(new SQLiteParameter("@watch", watchRec.Watch == true ? 1 : 0));
                    cmd.Parameters.Add(new SQLiteParameter("@sub", watchRec.Sub == true ? 1 : 0));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpdateMovieSingleColumn(string dbFullPath, long movieId, string columnName, object value)
        {
            if (!MovieColumnExtensions.TryParseColumnName(columnName, out MovieColumn column))
            {
                UpdateMovieSingleColumnUnsafe(dbFullPath, movieId, columnName, value);
                return;
            }

            UpdateMovieSingleColumn(dbFullPath, movieId, column, value);
        }

        public static void UpdateMovieSingleColumn(string dbFullPath, long movieId, MovieColumn column, object value)
        {
            string columnName = column.ToColumnName();
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"update movie set {columnName} = @value where movie_id = @id";
                    cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                    cmd.Parameters.Add(new SQLiteParameter("@value", value));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                SqliteDataAccess.ReportError(e);
            }
        }

        private static void UpdateMovieSingleColumnUnsafe(string dbFullPath, long movieId, string columnName, object value)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"update movie set {columnName} = @value where movie_id = @id";
                    cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                    cmd.Parameters.Add(new SQLiteParameter("@value", value));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                SqliteDataAccess.ReportError(e);
            }
        }

        public static void UpdateMovieFileInfo(
            string dbFullPath,
            long movieId,
            SinkuMetadata metadata,
            long existingMovieLengthSec)
        {
            if (metadata == null)
            {
                return;
            }

            try
            {
                long movieLengthSec = existingMovieLengthSec;
                if (existingMovieLengthSec < 1 && metadata.MovieLengthSec > 0)
                {
                    movieLengthSec = metadata.MovieLengthSec;
                }

                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    if (existingMovieLengthSec < 1 && metadata.MovieLengthSec > 0)
                    {
                        cmd.CommandText =
                            "update movie set container = @container, video = @video, audio = @audio, " +
                            "extra = @extra, movie_length = @movie_length where movie_id = @id";
                        cmd.Parameters.Add(new SQLiteParameter("@movie_length", movieLengthSec));
                    }
                    else
                    {
                        cmd.CommandText =
                            "update movie set container = @container, video = @video, audio = @audio, " +
                            "extra = @extra where movie_id = @id";
                    }

                    cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                    cmd.Parameters.Add(new SQLiteParameter("@container", metadata.Container ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@video", metadata.Video ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@audio", metadata.Audio ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@extra", metadata.Extra ?? ""));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpdateMovieZipInfo(string dbFullPath, long movieId, int imageCount)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "update movie set container = @container, video = '', audio = '', extra = '', " +
                        "movie_length = @movie_length where movie_id = @id";
                    cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                    cmd.Parameters.Add(new SQLiteParameter("@container", "zip"));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_length", imageCount));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpsertSystemTable(string dbFullPath, string attr, string value)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return;
            }

            DataTable dt = GetData(dbFullPath, $"select * from system where attr = '{attr}'");
            if (dt == null)
            {
                return;
            }

            if (dt.Rows.Count > 0)
            {
                UpdateSystemTable(dbFullPath, attr, value); 
            }
            else
            {
                InsertSystemTable(dbFullPath, attr, value);
            }
        }

        private static void InsertSystemTable(string dbFullPath, string attr, string value)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "insert into system (attr, value) values (@attr, @value)";
                    cmd.Parameters.Add(new SQLiteParameter("@attr", attr));
                    cmd.Parameters.Add(new SQLiteParameter("@value", value));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        private static void UpdateSystemTable(string dbFullPath,string attr, string value)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "update system set value = @value where attr = @attr";
                    cmd.Parameters.Add(new SQLiteParameter("@attr", attr));
                    cmd.Parameters.Add(new SQLiteParameter("@value", value));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void DeleteMovieTable(string dbFullPath, long movieId)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"delete from movie where movie_id = {movieId}";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void DeleteHistoryTable(string dbFullPath, long findId)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"delete from history where find_id = {findId}";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static async Task<bool> InsertMovieTable(string dbFullPath, MovieInfo mvi)
        {
            try
            {
                mvi.MoviePath = MediaPathNormalizer.Normalize(mvi.MoviePath);
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                // データベースから最大IDを取得
                string sql = "select max(movie_id) from movie";
                using SQLiteCommand selectCmd = connection.CreateCommand();
                selectCmd.CommandText = sql;

                // DataAdapterの生成
                SQLiteDataAdapter da = new(selectCmd);

                DataTable dt = new();
                da.Fill(dt);
                if (dt.Rows.Count < 1) 
                {
                    mvi.MovieId = 1;    //ゼロ行なので、1
                }
                else
                {
                    if (dt.Rows[0][0].ToString() != "")
                    {
                        mvi.MovieId = (long)dt.Rows[0][0] + 1;  //Max + 1
                    }
                    else
                    {
                        //ここ、通らない気がする。
                        mvi.MovieId = 1;    //ゼロ行なので、1
                    }
                }

                string container = "";
                string video = "";
                string extra = "";
                string audio = "";
                long movieLengthLong = mvi.MovieLength;

                if (SinkuMetadataFetcher.TryFetch(mvi.MoviePath, out SinkuMetadata metadata))
                {
                    if (string.IsNullOrEmpty(container))
                    {
                        container = metadata.Container;
                    }

                    video = metadata.Video;
                    audio = metadata.Audio;
                    extra = metadata.Extra;
                    if (movieLengthLong < 1 && metadata.MovieLengthSec > 0)
                    {
                        movieLengthLong = metadata.MovieLengthSec;
                    }
                }

                if (!string.IsNullOrEmpty(mvi.Container))
                {
                    container = mvi.Container;
                    if (string.Equals(container, "zip", StringComparison.OrdinalIgnoreCase)
                        && mvi.MovieLength > 0)
                    {
                        movieLengthLong = mvi.MovieLength;
                    }
                }

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = 
                        "insert into movie (" +
                        "   movie_id," +
                        "   movie_name," +
                        "   movie_path," +
                        "   movie_length," +    
                        "   movie_size," +
                        "   last_date," +
                        "   file_date," +
                        "   regist_date," +
                        "   hash, " +
                        "   container," +
                        "   video," +
                        "   audio," +
                        "   extra)" +
                        "   values (" +
                        "   @movie_id," +
                        "   @movie_name," +
                        "   @movie_path," +
                        "   @movie_length," +
                        "   @movie_size," +
                        "   @last_date," +
                        "   @file_date," +
                        "   @regist_date," +
                        "   @hash," +
                        "   @container," +
                        "   @video," +
                        "   @audio," +
                        "   @extra" +
                        ")";

                    cmd.Parameters.Add(new SQLiteParameter("@movie_id", mvi.MovieId));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_name", mvi.MovieName.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_path", mvi.MoviePath));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_length", movieLengthLong));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_size", mvi.MovieSize / 1024));
                    cmd.Parameters.Add(new SQLiteParameter("@last_date", mvi.LastDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@file_date", mvi.FileDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@regist_date", mvi.RegistDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@hash", mvi.Hash));
                    cmd.Parameters.Add(new SQLiteParameter("@container", container));
                    cmd.Parameters.Add(new SQLiteParameter("@video", video));
                    cmd.Parameters.Add(new SQLiteParameter("@audio", audio));
                    cmd.Parameters.Add(new SQLiteParameter("@extra", extra));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
                return true;
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [SQLite] InsertMovieTable failed: {mvi?.MoviePath} : {e.Message}");
                return false;
            }
            finally
            {
                await Task.Delay(5).ConfigureAwait(false);
            }
        }

        public static void InsertHistoryTable(string dbFullPath, string find_text)
        {
            if (string.IsNullOrEmpty(dbFullPath) || string.IsNullOrEmpty(find_text))
            {
                return;
            }

            lock (HistoryInsertLock)
            {
                try
                {
                    using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                    connection.Open();

                    var now = DateTime.Now;
                    var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                    using var transaction = connection.BeginTransaction();

                    using (SQLiteCommand updateCmd = connection.CreateCommand())
                    {
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText =
                            "update history set find_date = @find_date where find_text = @find_text";
                        updateCmd.Parameters.Add(new SQLiteParameter("@find_date", result));
                        updateCmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                        if (updateCmd.ExecuteNonQuery() > 0)
                        {
                            transaction.Commit();
                            return;
                        }
                    }

                    long find_id = 1;
                    using (SQLiteCommand selectCmd = connection.CreateCommand())
                    {
                        selectCmd.Transaction = transaction;
                        selectCmd.CommandText = "select max(find_id) from history";
                        object maxId = selectCmd.ExecuteScalar();
                        if (maxId != null && maxId != DBNull.Value)
                        {
                            find_id = Convert.ToInt64(maxId) + 1;
                        }
                    }

                    using (SQLiteCommand insertCmd = connection.CreateCommand())
                    {
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText =
                            "insert into history (find_id,find_text,find_date) values (@find_id,@find_text,@find_date)";
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_id", find_id));
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_date", result));
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                    UiErrorReporter.ShowError(e.Message, title);
                }
            }
        }

        public static void EnsureTagBarTable(string dbFullPath)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return;
            }

            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tagbar(
                        item_id integer primary key not null,
                        parent_id integer not null default 0,
                        order_id integer not null default 0,
                        group_id integer not null default 0,
                        title text not null default '',
                        contents text not null default '' )";
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        /// <summary>
        /// 既定の★評価（★〜★★★★★）が tagbar に無ければ挿入する。挿入件数を返す。
        /// </summary>
        public static int EnsureBuiltInStarRatingItems(string dbFullPath)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return 0;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                int inserted = EnsureBuiltInStarRatingItemsCore(connection, transaction);
                transaction.Commit();
                return inserted;
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
                return 0;
            }
        }

        private static int EnsureBuiltInStarRatingItemsCore(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            HashSet<string> existingTitles = new(StringComparer.Ordinal);
            using (SQLiteCommand selectTitlesCmd = connection.CreateCommand())
            {
                selectTitlesCmd.Transaction = transaction;
                selectTitlesCmd.CommandText = "select title from tagbar";
                using SQLiteDataReader reader = selectTitlesCmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                    {
                        existingTitles.Add(reader[0].ToString().Trim());
                    }
                }
            }

            long nextItemId = 1;
            using (SQLiteCommand selectMaxIdCmd = connection.CreateCommand())
            {
                selectMaxIdCmd.Transaction = transaction;
                selectMaxIdCmd.CommandText = "select max(item_id) from tagbar";
                object maxId = selectMaxIdCmd.ExecuteScalar();
                if (maxId != DBNull.Value)
                {
                    nextItemId = Convert.ToInt64(maxId) + 1;
                }
            }

            int inserted = 0;
            for (int i = 0; i < TagBarService.BuiltInStarRatingTitles.Length; i++)
            {
                string title = TagBarService.BuiltInStarRatingTitles[i];
                if (existingTitles.Contains(title))
                {
                    continue;
                }

                using SQLiteCommand insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText =
                    "insert into tagbar (item_id, parent_id, order_id, group_id, title, contents) " +
                    "values (@item_id, 0, @order_id, 0, @title, '')";
                insertCmd.Parameters.Add(new SQLiteParameter("@item_id", nextItemId));
                insertCmd.Parameters.Add(new SQLiteParameter("@order_id", i));
                insertCmd.Parameters.Add(new SQLiteParameter("@title", title));
                insertCmd.ExecuteNonQuery();

                nextItemId++;
                inserted++;
            }

            return inserted;
        }

        public static long InsertTagBarItem(string dbFullPath, string title, string contents)
        {
            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                long itemId = 1;
                long orderId = 0;
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = "select max(item_id), max(order_id) from tagbar";
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader[0] != DBNull.Value)
                        {
                            itemId = Convert.ToInt64(reader[0]) + 1;
                        }

                        if (reader[1] != DBNull.Value)
                        {
                            orderId = Convert.ToInt64(reader[1]) + 1;
                        }
                    }
                }

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "insert into tagbar (item_id, parent_id, order_id, group_id, title, contents) " +
                        "values (@item_id, 0, @order_id, 0, @title, @contents)";
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.Parameters.Add(new SQLiteParameter("@order_id", orderId));
                    cmd.Parameters.Add(new SQLiteParameter("@title", title ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@contents", contents ?? ""));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return itemId;
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
                return 0;
            }
        }

        public static void UpdateTagBarItem(string dbFullPath, long itemId, string title, string contents)
        {
            if (itemId <= 0)
            {
                return;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.Transaction = transaction;
                    selectCmd.CommandText =
                        "select order_id, title, contents from tagbar where item_id = @item_id";
                    selectCmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read()
                        && TagBarService.IsBuiltInStarRatingRow(
                            Convert.ToInt64(reader["order_id"]),
                            reader["title"]?.ToString(),
                            reader["contents"]?.ToString()))
                    {
                        return;
                    }
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "update tagbar set title = @title, contents = @contents where item_id = @item_id";
                    cmd.Parameters.Add(new SQLiteParameter("@title", title ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@contents", contents ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
            }
        }

        public static void DeleteTagBarItem(string dbFullPath, long itemId)
        {
            if (itemId <= 0)
            {
                return;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.Transaction = transaction;
                    selectCmd.CommandText =
                        "select order_id, title, contents from tagbar where item_id = @item_id";
                    selectCmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read()
                        && TagBarService.IsBuiltInStarRatingRow(
                            Convert.ToInt64(reader["order_id"]),
                            reader["title"]?.ToString(),
                            reader["contents"]?.ToString()))
                    {
                        return;
                    }
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "delete from tagbar where item_id = @item_id";
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
            }
        }

        public static void DeleteHistoryTable(string dbFullPath, int keepHistoryCount)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = 
                        $"DELETE from history where find_id < " +
                        $"(select find_id from " +
                        $"  (select find_id from history order by find_id desc LIMIT {keepHistoryCount}) " +
                        $" order by find_id limit 1)";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void InsertFindFactTable(string dbFullPath, string find_text)
        {
            if (string.IsNullOrEmpty(dbFullPath) || string.IsNullOrEmpty(find_text))
            {
                return;
            }

            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                var now = DateTime.Now;
                var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                using var transaction = connection.BeginTransaction();

                // 存在判定とINSERT/UPDATEを1文のUPSERTにまとめる。
                // SELECTを文字列補間していたためキーワードにシングルクオートが含まれると
                // 存在判定が壊れ、UNIQUE制約違反(find_text)になっていた。
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    "insert into findfact (find_text, find_count, last_date) " +
                    "values (@find_text, 1, @last_date) " +
                    "on conflict(find_text) do update set " +
                    "find_count = find_count + 1, last_date = @last_date";
                cmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                cmd.Parameters.Add(new SQLiteParameter("@last_date", result));
                cmd.ExecuteNonQuery();
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void InsertBookmarkTable(
            string dbFullPath,
            MovieInfo mvi,
            string sourceMoviePath,
            string sourceHash = "")
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                // データベースから最大IDを取得
                string sql = "select max(movie_id) from bookmark";
                using SQLiteCommand selectCmd = connection.CreateCommand();
                selectCmd.CommandText = sql;

                // DataAdapterの生成
                SQLiteDataAdapter da = new(selectCmd);

                DataTable dt = new();
                da.Fill(dt);
                if (dt.Rows.Count < 1) 
                {
                    mvi.MovieId = 1;    //ゼロ行なので、1
                }
                else
                {
                    if (dt.Rows[0][0].ToString() != "")
                    {
                        mvi.MovieId = (long)dt.Rows[0][0] + 1;  //Max + 1
                    }
                    else
                    {
                        mvi.MovieId = 1;    //ゼロ行なので、1
                    }
                }

                var now = DateTime.Now;
                var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "insert into bookmark (" +
                        "   movie_id," +
                        "   movie_name," +
                        "   movie_path," +
                        "   last_date," +
                        "   file_date," +
                        "   regist_date," +
                        "   hash," +
                        "   comment1)" +
                        "   values (" +
                        "   @movie_id," +
                        "   @movie_name," +
                        "   @movie_path," +
                        "   @last_date," +
                        "   @file_date," +
                        "   @regist_date," +
                        "   @hash," +
                        "   @comment1)";

                    cmd.Parameters.Add(new SQLiteParameter("@movie_id", mvi.MovieId));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_name", mvi.MovieName.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_path", mvi.MoviePath.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@last_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@file_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@regist_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@hash", (sourceHash ?? "").ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@comment1", (sourceMoviePath ?? "").ToLower()));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpdateBookmarkViewCount(string dbFullPath, long movieId)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"update bookmark set view_count = view_count + 1 where movie_id = @id";
                    cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpdateBookmarkRename(string dbFullPath, string oldName, string newName)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                oldName = oldName.ToLower();
                newName = newName.ToLower();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = 
                        $"update bookmark set " +
                        $"movie_name = replace(movie_name,'{oldName}', '{newName}'), " +
                        $"movie_path = replace(movie_path,'{oldName}', '{newName}') " +
                        $"where lower(movie_name) like '%{oldName}%'";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void DeleteBookmarkTable(string dbFullPath, long movie_id)
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        $"DELETE from bookmark where movie_id = {movie_id}";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

    }
}
