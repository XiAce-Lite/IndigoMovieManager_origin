using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;
using static IndigoMovieManager.SQLite;
namespace IndigoMovieManager
{
    public partial class MainWindow
    {
        #region 保存済み検索条件（TagBar）

        private void SaveSearchTagButton_Click(object sender, RoutedEventArgs e) =>
            BeginAddTagBarItem(SearchBox.Text);

        private void TagBarAdd_Click(object sender, RoutedEventArgs e) =>
            BeginAddTagBarItem("");

        private void BeginAddTagBarItem(string initialContents)
        {
            if (!EnsureDatabaseSelected())
            {
                return;
            }

            string contents = initialContents ?? "";
            string initialTitle = string.IsNullOrEmpty(contents) ? "" : contents;
            if (!TryShowTagBarEditDialog(initialTitle, contents, out string title, out string savedContents))
            {
                return;
            }

            long itemId = InsertTagBarItem(MainVM.DbInfo.DBFullPath, title, savedContents);
            if (itemId <= 0)
            {
                return;
            }

            GetTagBarTable();
            SelectTagBarItem(itemId);
        }

        private void TagBarEdit_Click(object sender, RoutedEventArgs e) =>
            EditSelectedTagBarItem(focusContents: false);

        private void TagBarRenameMenuItem_Click(object sender, RoutedEventArgs e) =>
            EditSelectedTagBarItem(focusContents: false);

        private void TagBarEditContentsMenuItem_Click(object sender, RoutedEventArgs e) =>
            EditSelectedTagBarItem(focusContents: true);

        private void TagBarDuplicateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TagBarItem item = GetTagBarItemFromMenuSender(sender) ?? TagBarList.SelectedItem as TagBarItem;
            if (item == null)
            {
                return;
            }

            string title = TagBarService.BuildDuplicateTitle(item.Title);
            if (!TryShowTagBarEditDialog(title, item.Contents, out string savedTitle, out string savedContents))
            {
                return;
            }

            long itemId = InsertTagBarItem(MainVM.DbInfo.DBFullPath, savedTitle, savedContents);
            if (itemId <= 0)
            {
                return;
            }

            GetTagBarTable();
            SelectTagBarItem(itemId);
        }

        private void TagBarDelete_Click(object sender, RoutedEventArgs e) =>
            DeleteSelectedTagBarItem();

        private void TagBarDeleteMenuItem_Click(object sender, RoutedEventArgs e) =>
            DeleteTagBarItemFromDb(GetTagBarItemFromMenuSender(sender));

        private void EditSelectedTagBarItem(bool focusContents)
        {
            TagBarItem item = TagBarList.SelectedItem as TagBarItem;
            string blockReason = TagBarService.GetEditBlockReason(item);
            if (blockReason != null)
            {
                MessageBox.Show(
                    this,
                    blockReason,
                    AppTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!TryShowTagBarEditDialog(item.Title, item.Contents, out string title, out string contents, focusContents))
            {
                return;
            }

            UpdateTagBarItem(MainVM.DbInfo.DBFullPath, item.Item_Id, title, contents);
            TagBarService.ApplyEditedFields(item, title, contents);
            TagBarList.Items.Refresh();
        }

        private void DeleteSelectedTagBarItem()
        {
            TagBarItem item = TagBarList.SelectedItem as TagBarItem;
            if (item == null)
            {
                MessageBox.Show(
                    this,
                    TagBarService.MessageSelectToDelete,
                    AppTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DeleteTagBarItemFromDb(item);
        }

        private void DeleteTagBarItemFromDb(TagBarItem item)
        {
            string blockReason = TagBarService.GetDeleteBlockReason(item);
            if (blockReason != null)
            {
                if (item != null)
                {
                    MessageBox.Show(
                        this,
                        blockReason,
                        AppTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            DeleteTagBarItem(MainVM.DbInfo.DBFullPath, item.Item_Id);
            TagBarService.TryRemoveFromCollection(MainVM.TagBarRecs, item);
            TagBarList.SelectedItem = null;
            UpdateTagBarCommandButtonState();
        }

        private void TagBarList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateTagBarCommandButtonState();

        private void UpdateTagBarCommandButtonState()
        {
            (bool editEnabled, bool deleteEnabled) =
                TagBarService.GetCommandButtonState(TagBarList.SelectedItem as TagBarItem);

            TagBarEditButton.IsEnabled = editEnabled;
            TagBarDeleteButton.IsEnabled = deleteEnabled;
        }

        private void TagBarItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not ListBoxItem listItem || listItem.DataContext is not TagBarItem item)
            {
                return;
            }

            if (listItem.ContextMenu == null)
            {
                return;
            }

            bool canModify = TagBarService.CanModify(item);
            foreach (object child in listItem.ContextMenu.Items)
            {
                if (child is not MenuItem menuItem)
                {
                    continue;
                }

                if ("TagBarDeleteMenuItem".Equals(menuItem.Tag)
                    || "TagBarRenameMenuItem".Equals(menuItem.Tag)
                    || "TagBarEditContentsMenuItem".Equals(menuItem.Tag))
                {
                    menuItem.IsEnabled = canModify;
                }
            }
        }

        private async void TagBarItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1)
            {
                return;
            }

            if (sender is not ListBoxItem listItem || listItem.DataContext is not TagBarItem item)
            {
                return;
            }

            TagBarList.SelectedItem = item;
            await SearchByKeywordAsync(item.EffectiveContents, addToHistory: false).ConfigureAwait(true);
        }

        private void TagBarItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            e.Handled = true;

            if (sender is not ListBoxItem listItem || listItem.DataContext is not TagBarItem item)
            {
                return;
            }

            TagBarList.SelectedItem = item;
            AppendTagBarContentsToSelectedMovies(item);
        }

        private void AppendTagBarContentsToSelectedMovies(TagBarItem item)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            List<MovieRecords> selected = GetSelectedMovies();
            if (selected == null || selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    TagBarService.MessageSelectRecordsForTag,
                    AppTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string tagText = TagBarService.ResolveAppendTagText(item);
            if (string.IsNullOrWhiteSpace(tagText))
            {
                return;
            }

            foreach (MovieRecords rec in selected)
            {
                TagMutationService.ApplyAdd(rec, tagText);
                UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "tag", rec.Tags);
            }

            Refresh();
        }

        private bool TryShowTagBarEditDialog(
            string initialTitle,
            string initialContents,
            out string title,
            out string contents,
            bool focusContents = false)
        {
            title = initialTitle ?? "";
            contents = initialContents ?? "";

            var dialog = new TagBarEditWindow
            {
                Owner = this,
                DisplayTitle = title,
                SearchContents = contents,
                FocusSearchContentsOnOpen = focusContents,
            };

            if (dialog.ShowDialog() != true || dialog.CloseStatus() != MessageBoxResult.OK)
            {
                return false;
            }

            title = dialog.DisplayTitle.Trim();
            contents = dialog.SearchContents.Trim();
            return true;
        }

        private void SelectTagBarItem(long itemId)
        {
            TagBarItem item = MainVM.TagBarRecs.FirstOrDefault(x => x.Item_Id == itemId);
            if (item != null)
            {
                TagBarList.SelectedItem = item;
                TagBarList.ScrollIntoView(item);
            }
        }

        private static TagBarItem GetTagBarItemFromMenuSender(object sender)
        {
            if (sender is not MenuItem menuItem)
            {
                return null;
            }

            return menuItem.DataContext as TagBarItem;
        }

        #endregion
    }
}
