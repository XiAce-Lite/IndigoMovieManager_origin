namespace IndigoMovieManager.Services;

/// <summary>
/// 検索ボックスの入力がインクリメント検索（入力中の絞り込み）の対象かどうかを判定する。
/// </summary>
internal static class SearchInputClassifier
{
    /// <summary>
    /// 手入力のインクリメント検索対象か。空文字・空白のみ・先頭が <c>{</c> の場合は対象外（Enter 等の明示検索のみ）。
    /// </summary>
    public static bool IsIncrementalSearchEligible(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && !text.TrimStart().StartsWith('{');
}
