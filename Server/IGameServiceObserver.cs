using Swoq.Interface;

namespace Swoq.Server;

internal interface IGameServiceObserver
{
    void Started(string playerName, Guid gameId, StartRequest request, StartResponse response);
    void Acted(Guid gameId, ActionRequest request, ActionResponse response);
}
