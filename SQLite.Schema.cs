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
    internal partial class SQLite
    {
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
                        sub integer not null default 1,
                        dmm_auto integer not null default 0 )";
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
