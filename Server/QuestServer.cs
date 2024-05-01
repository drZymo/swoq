using Swoq.Server.Services;
using System.Collections.Immutable;

namespace Swoq.Server;

internal class QuestServer(ISwoqDatabase database)
{
    public record StartResult(Guid GameId, int Height, int Width, int VisibilityRange, GameState State);

    private readonly object questsWriteMutex = new();
    private IImmutableDictionary<Guid, Quest> quests = ImmutableDictionary<Guid, Quest>.Empty;

    public StartResult Start(string playerId)
    {
        var player = database.FindPlayerByIdAsync(playerId).Result ?? throw new UnknownPlayerException();

        var quest = new Quest(player, database);
        lock (questsWriteMutex)
        {
            quests = quests.Add(quest.Id, quest);
        }

        CleanupOldQuests();

        return new StartResult(quest.Id, Parameters.MapHeight, Parameters.MapWidth, Parameters.PlayerVisibilityRange, quest.State);
    }

    public GameState Act(Guid questId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (!quests.TryGetValue(questId, out var quest)) throw new UnknownGameIdException();

        quest.Act(action1, action2);
        return quest.State;
    }

    private void CleanupOldQuests()
    {
        // Gather ids to remove
        var idsToRemove = ImmutableList<Guid>.Empty;
        var now = DateTime.Now;
        foreach (var quest in quests.Values)
        {
            var age = now - quest.LastAction;
            if (age > Parameters.MaxGameIdleTime)
            {
                idsToRemove = idsToRemove.Add(quest.Id);
            }
        }

        // Remove all at once
        if (idsToRemove.Count > 0)
        {
            lock (questsWriteMutex)
            {
                quests = quests.RemoveRange(idsToRemove);
            }
        }
    }
}
