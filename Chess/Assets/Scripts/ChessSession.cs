using System.Collections.Generic;
using System.Linq;
using Unity.Services.Multiplayer;

public class ChessSession
{
    public List<string> PlayerIds = new List<string>();

    public void FromSession(ISession session)
    {
        PlayerIds = session.Players.Select(player => player.Id).ToList();
    }
}