using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Chess;
using Microsoft.Extensions.Logging;
using Unity.Services.Leaderboards.Model;

namespace ChessCloudCode;

public class Chess
{
    private const string LeaderboardId = "EloRatings";
    private const int KValue = 30;
    private const int StartingElo = 1500;
    private readonly IGameApiClient _gameApiClient;
    private readonly IPushClient _pushClient;
    private readonly ILogger<Chess> _logger;

    public Chess(IGameApiClient gameApiClient, IPushClient pushClient, ILogger<Chess> logger)
    {
        _gameApiClient = gameApiClient;
        _pushClient = pushClient;
        _logger = logger;
    }
    
    [CloudCodeFunction("JoinGame")]
    public async Task<Dictionary<string, string>> JoinGame(IExecutionContext context, string session)
    {
        try
        {
            // Get the current board state
            var boardResponse = await _gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId,
                session, new List<string> { "board" });

            // If no board exists, create a new one
            ChessBoard chessBoard;
            if (boardResponse.Data.Results.Count == 0)
            {
                chessBoard = new ChessBoard();
                await _gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                    session,
                    new SetItemBody("board", chessBoard.ToFen()));
            }
            else
            {
                // Otherwise, load the board from the saved state
                chessBoard = ChessBoard.LoadFromFen(boardResponse.Data.Results.First().Value.ToString()!);
            }

            return new Dictionary<string, string>
                { { "board", chessBoard.ToFen() } };
        }
        catch (Exception)
        {
            return new Dictionary<string, string> { { "error", "Error loading the board" } };
        }
    }

    [CloudCodeFunction("MakeMove")]
    public async Task<Dictionary<string, string>> MakeMove(IExecutionContext context, string session, string fromPosition, string toPosition)
    {
        try
        {
            // Get the current board state and lobby
            var lobbyRequest =
                _gameApiClient.Lobby.GetLobbyAsync(context, context.AccessToken, session);
            var boardResponse =
                await _gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                    session, new List<string> { "board" });

            var chessBoard = ChessBoard.LoadFromFen(boardResponse.Data.Results.First().Value.ToString()!);

            var lobbyResponse = await lobbyRequest;
            var player = lobbyResponse.Data.Players.Find(p => p.Id == context.PlayerId);
            var whitePlayer = lobbyResponse.Data.Players.Find(d => d.Data.ContainsKey("colour") && d.Data["colour"].Value == "white").Id;
            var activePlayerColour = player?.Id == whitePlayer ? 1 : 2;
            
            if (activePlayerColour != chessBoard.Turn.Value)
            {
                return new Dictionary<string, string>
                {
                    { "error", $"Invalid move, not active player" }, 
                    { "board", chessBoard.ToFen() }
                };
            }
            
            // Check if the move is valid according to chess rules
            if (!chessBoard.IsValidMove(new Move(fromPosition, toPosition)))
            {
                return new Dictionary<string, string>
                {
                    { "error", $"Invalid move from {fromPosition} to {toPosition}" },
                    { "board", chessBoard.ToFen() }
                };
            }
            var validMove = chessBoard.Move(new Move(fromPosition, toPosition));
            if (!validMove) return new Dictionary<string, string> {
            {
                "board", chessBoard.ToFen()
            } };

            // Save the new board state
            await _gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                session,
                new SetItemBody("board", chessBoard.ToFen()));

            // Send the updated board state to the other player
            var otherPlayer = lobbyResponse.Data.Players.Find(p => p.Id != context.PlayerId);
            if (otherPlayer == null) return new Dictionary<string, string> { { "board", chessBoard.ToFen() } };
            var message = new BoardUpdatedMessage
            {
                Session = session, 
                Board = chessBoard.ToFen(), 
                GameOver = chessBoard.IsEndGame, 
                EndgameType = chessBoard.EndGame?.EndgameType.ToString()
            };
            await _pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(message), message.Type,
                otherPlayer!.Id);
                
            if (chessBoard.IsEndGame)
            {
                var playerScore = activePlayerColour == chessBoard.EndGame.WonSide ? 1 :
                    chessBoard.EndGame.WonSide == null ? 0.5 : 0;
                await UpdateElos(
                    context,
                    otherPlayer.Id,
                    playerScore);
                return new Dictionary<string, string>
                {
                    { "board", chessBoard.ToFen() },
                    { "result", chessBoard.EndGame.EndgameType.ToString()}
                };
            }
            return new Dictionary<string, string> { { "board", chessBoard.ToFen() } };
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Error, exception.Message);
            _logger.Log(LogLevel.Error, exception.StackTrace);
            return new Dictionary<string, string>
                { { "error", exception.Message }, { "stackTrace", exception.StackTrace }, {"board", new ChessBoard().ToFen()} };
        }
    }

    public async Task UpdateElos(IExecutionContext context, string opponentId, double playerScore)
    {
        var projectId = Guid.Parse(context.ProjectId);
        var elos = await _gameApiClient.Leaderboards.GetLeaderboardScoresByPlayerIdsAsync(context, context.ServiceToken,
            projectId, LeaderboardId,
            new LeaderboardPlayerIds(new List<string>() {context.PlayerId, opponentId}));
        var playerElo = elos.Data?.Results?.Find(r => r.PlayerId == context.PlayerId)?.Score ?? StartingElo;
        var opponentElo = elos.Data?.Results?.Find(r => r.PlayerId == opponentId)?.Score ?? StartingElo;
        
        var expectedScore = 1 / (1 + Math.Pow(10, (opponentElo - playerElo) / 400));
        var eloChange = KValue * (playerScore - expectedScore);
        var playerNewElo = playerElo + eloChange;
        var opponentNewElo = opponentElo - eloChange;
        _logger.LogInformation($"Updating {context.PlayerId}'s Elo from {playerElo} to {playerNewElo}");
        _logger.LogInformation($"Updating {opponentId}'s Elo from {opponentElo} to {opponentNewElo}");
        var tasks = new Task[]
        {
            _gameApiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(context, context.ServiceToken, projectId, LeaderboardId,
                context.PlayerId, new LeaderboardScore(playerNewElo)),
            _gameApiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(context, context.ServiceToken, projectId, LeaderboardId,
                opponentId, new LeaderboardScore(opponentNewElo))
        };
        await Task.WhenAll(tasks);
    }

    [CloudCodeFunction("ClearBoard")]
    public async Task<Dictionary<string, string>> ClearBoard(IExecutionContext context, string session)
    {
        try
        {
            // Clear the board state and send a message to all players to clear their boards
            await _gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                session,
                new SetItemBody("board", new ChessBoard().ToFen()));
            var lobbyResponse = await _gameApiClient.Lobby.GetLobbyAsync(context, context.ServiceToken, session);
            foreach (var player in lobbyResponse.Data.Players)
            {
                var clearBoardMessage = new ClearBoardMessage { Session = session };
                await _pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(clearBoardMessage),
                    "clearBoard", player.Id);
            }

            return new Dictionary<string, string> { { "cleared", "true" } };
        }
        catch (Exception)
        {
            return new Dictionary<string, string> { { "error", "Error clearing the board" } };
        }
    }

    private class BoardUpdatedMessage
    {
        public string Session { get; set; }
        public string Board { get; set; }
        public string Type = "boardUpdated";
        public bool GameOver { get; set; }
        public string EndgameType { get; set; }
    }

    private class ClearBoardMessage
    {
        public string Session { get; set; }
    }
}