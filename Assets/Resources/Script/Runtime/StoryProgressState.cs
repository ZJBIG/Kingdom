using System;
using System.Collections.Generic;

/// <summary>
/// Mutable runtime history for the authored story archive. The archive itself
/// remains definition data; this state only records chapters that have been
/// completed in order.
/// </summary>
[Serializable]
public sealed class StoryProgressState
{
    private readonly HashSet<string> completedChapterIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> CompletedChapterIds => completedChapterIds;
    public int Version { get; private set; }

    internal bool Contains(string chapterId)
    {
        return !string.IsNullOrWhiteSpace(chapterId) &&
            completedChapterIds.Contains(chapterId.Trim());
    }

    internal bool TryComplete(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId) ||
            !completedChapterIds.Add(chapterId.Trim()))
            return false;

        Version++;
        return true;
    }

    internal void Restore(IReadOnlyList<string> chapterIds)
    {
        if (chapterIds == null)
            throw new ArgumentNullException(nameof(chapterIds));

        completedChapterIds.Clear();
        for (int i = 0; i < chapterIds.Count; i++)
        {
            string chapterId = chapterIds[i];
            if (!string.IsNullOrWhiteSpace(chapterId))
                completedChapterIds.Add(chapterId.Trim());
        }
        Version++;
    }

    internal void Reset()
    {
        completedChapterIds.Clear();
        Version++;
    }
}
