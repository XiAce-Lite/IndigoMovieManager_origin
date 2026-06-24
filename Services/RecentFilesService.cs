using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using IndigoMovieManager.ModelViews;

namespace IndigoMovieManager.Services
{
    internal static class RecentFilesService
    {
        public static Stack<string> LoadFromSettings(StringCollection savedFiles)
        {
            Stack<string> stack = new();
            if (savedFiles == null)
            {
                return stack;
            }

            foreach (string item in savedFiles.Cast<string>())
            {
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                stack.Push(item);
            }

            return stack;
        }

        public static void PopulateTreeChildren(Stack<string> recentFiles, TreeSource rootItem)
        {
            foreach (string item in recentFiles)
            {
                rootItem.Add(new TreeSource { Text = item, IsExpanded = false });
            }
        }

        public static Stack<string> ReStack(Stack<string> recentFiles, string newItem, int maxCount)
        {
            Stack<string> temp = new();
            foreach (string item in recentFiles.Reverse())
            {
                if (item != newItem)
                {
                    temp.Push(item);
                }
            }

            Stack<string> updated = temp;
            while (updated.Count + 1 > maxCount)
            {
                updated = new Stack<string>(updated.Reverse().Skip(1));
            }

            updated.Push(newItem);
            return updated;
        }

        public static Stack<string> Remove(Stack<string> recentFiles, string pathToRemove)
        {
            Stack<string> updated = new();
            foreach (string item in recentFiles.Reverse())
            {
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                if (string.Equals(item, pathToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                updated.Push(item);
            }

            return updated;
        }

        public static void RebuildTreeChildren(
            ObservableCollection<TreeSource> recentTreeRoot,
            Stack<string> recentFiles)
        {
            if (recentTreeRoot.Count == 0)
            {
                return;
            }

            TreeSource rootItem = recentTreeRoot[0];
            rootItem.Children?.Clear();

            foreach (string item in recentFiles)
            {
                rootItem.Add(new TreeSource { Text = item, IsExpanded = false });
            }
        }
    }
}
