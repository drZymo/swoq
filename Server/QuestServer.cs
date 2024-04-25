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
        var quest = new Quest();

        lock (questsWriteMutex)
        {
            quests = quests.Add(quest.Id, quest);
        }

        return new StartResult(quest.Id, quest.Height, quest.Width, Parameters.PlayerVisibilityRange, quest.State);
    }

    public GameState Act(Guid questId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (!quests.TryGetValue(questId, out var quest))
        {
            throw new UnknownGameIdException();
        }

        quest.Act(action1, action2);
        // TODO: update player level
        return quest.State;
    }
}
