using Swoq.Interface;

namespace Swoq.Server;

internal interface ITrainingObserver
{
    void Started(string playerName, Guid gameId, StartTrainingRequest request, StartResponse response);
    void Acted(Guid gameId, ActionRequest request, ActionResponse response);
}
