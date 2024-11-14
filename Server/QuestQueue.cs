using Swoq.Data;
using Swoq.Infra;
using System.Collections.Immutable;

namespace Swoq.Server;

internal class QuestQueue
{
    private record QueueEntry(string UserId, string UserName, DateTime StartTime, DateTime LastQueryTime);

    private IImmutableDictionary<string, QueueEntry> entries = ImmutableDictionary<string, QueueEntry>.Empty;

    private IImmutableList<string> queuedUsers = [];

    public event EventHandler<QueueUpdatedEventArgs>? Updated;

    public string FrontUserId => entries.OrderBy(kvp => kvp.Value.StartTime).First().Key;

    public void Enqueue(User user)
    {
        if (user.Id == null) throw new ArgumentNullException(nameof(user));

        var now = Clock.Now;

        // Cleanup inactive queued users.
        var inactiveQuests = entries.
            Where(kvp => kvp.Value.LastQueryTime < now - Parameters.MaxQuestInactivityTime);
        var inactiveUserNames = inactiveQuests.
            Select(kvp => kvp.Value.UserName).
            ToImmutableArray();
        if (inactiveQuests.Any())
        {
            entries = entries.RemoveRange(inactiveQuests.Select(kvp => kvp.Key));
        }
        if (inactiveUserNames.Length > 0)
        {
            queuedUsers = queuedUsers.RemoveRange(inactiveUserNames);
        }

        if (entries.TryGetValue(user.Id, out var entry))
        {
            // Refresh query time of existing entry
            entries = entries.SetItem(user.Id, entry with { LastQueryTime = now });
        }
        else
        {
            // Enqueue this user if not queued already
            entry = new QueueEntry(user.Id, user.Name, now, now);
            entries = entries.Add(user.Id, entry);
            queuedUsers = queuedUsers.Add(user.Name);
            OnUpdated();
        }
    }

    public (string userId, string userName) Dequeue()
    {
        var frontUserId = FrontUserId;

        var entry = entries[frontUserId];
        entries = entries.Remove(frontUserId);
        queuedUsers = queuedUsers.RemoveAt(0);

        OnUpdated();

        return (entry.UserId, entry.UserName);
    }

    private void OnUpdated()
    {
        Updated?.Invoke(this, new QueueUpdatedEventArgs(queuedUsers));
    }
}
