using System.Threading;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace DefaultNamespace
{
    public class MatchManager : MonoBehaviour
    {
        
        
        async void FindMatch()
        {
            var matchmakerOptions = new MatchmakerOptions
            {
                QueueName = "default-queue"
            };

            var sessionOptions = new SessionOptions()
            {
                MaxPlayers = 2,
                IsPrivate = true
            };

            var matchmakerCancellationSource = new CancellationTokenSource();

            var session = await MultiplayerService.Instance.MatchmakeSessionAsync(matchmakerOptions, sessionOptions, matchmakerCancellationSource.Token);

            //session.Id; // == matchId == lobbyId
        }
    }
}