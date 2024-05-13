using Swoq.Infra;
using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server;

internal class QuestQueue
{
    private record QueueEntry(string PlayerId, string PlayerName, DateTime StartTime, DateTime LastQueryTime);

    private IImmutableDictionary<string, QueueEntry> entries = ImmutableDictionary<string, QueueEntry>.Empty;

    private IImmutableList<string> queuedPlayers = [];

    public event EventHandler<IImmutableList<string>>? Updated;

    public string FrontPlayerId => entries.OrderBy(kvp => kvp.Value.StartTime).First().Key;

    public void Enqueue(Player player)
    {
        if (player.Id == null) throw new ArgumentNullException(nameof(player));

        var now = Clock.Now;

        // Cleanup inactive queued players.
        var inactiveQuests = entries.
            Where(kvp => kvp.Value.LastQueryTime < now - Parameters.MaxQuestInactivityTime);
        var inactivePlayerNames = inactiveQuests.
            Select(kvp => kvp.Value.PlayerName).
            ToImmutableArray();
        if (inactiveQuests.Any())
        {
            entries = entries.RemoveRange(inactiveQuests.Select(kvp => kvp.Key));
        }
        if (inactivePlayerNames.Length > 0)
        {
            queuedPlayers = queuedPlayers.RemoveRange(inactivePlayerNames);
        }

        if (entries.TryGetValue(player.Id, out var entry))
        {
            // Refresh query time of existing entry
            entries = entries.SetItem(player.Id, entry with { LastQueryTime = now });
        }
        else
        {
            // Enqueue this player if not queued already
            entry = new QueueEntry(player.Id, player.Name, now, now);
            entries = entries.Add(player.Id, entry);
            queuedPlayers = queuedPlayers.Add(player.Name);
            OnUpdated();
        }
    }

    public (string playerId, string playerName) Dequeue()
    {
        var frontPlayerId = FrontPlayerId;

        var entry = entries[frontPlayerId];
        entries = entries.Remove(frontPlayerId);
        queuedPlayers = queuedPlayers.RemoveAt(0);

        OnUpdated();

        return (entry.PlayerId, entry.PlayerName);
    }

    private void OnUpdated()
    {
        Updated?.Invoke(this, queuedPlayers);
    }
}
