using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Chess;
using Microsoft.Extensions.Logging;
using Unity.Services.Leaderboards.Model;
using Unity.Services.Lobby.Model;

namespace ChessCloudCode;

public class Chess
{
    private const string LeaderboardId = "EloRatings";
    private const int KValue = 30;
    private const int StartingElo = 1500;
    
    private readonly IGameApiClient _gameApiClient;
    private readonly IPushClient _pushClient;
    private readonly ILogger<Chess> _logger;
    private readonly Random _rng;

    public Chess(IGameApiClient gameApiClient, IPushClient pushClient, ILogger<Chess> logger, Random rng)
    {
        _gameApiClient = gameApiClient;
        _pushClient = pushClient;
        _logger = logger;
        _rng = rng;
    }

    [CloudCodeFunction("HostGame")]
    public async Task<HostGameResponse> HostGame(IExecutionContext context)
    {
        var lobbyResult = await _gameApiClient.Lobby.CreateLobbyAsync(context, context.AccessToken, null, null,
            new CreateRequest($"{context.PlayerId}'s game", 2, false, false, new Player(context.PlayerId)));
        
        var chessBoard = new ChessBoard();
        await _gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
            lobbyResult.Data.Id,
            new SetItemBatchBody(new List<SetItemBody>(){ 
                new("board", chessBoard.ToFen()),
                new("whitePlayerId", context.PlayerId)
            }));

        return new HostGameResponse()
        {
            LobbyCode = lobbyResult.Data.LobbyCode,
        };
    }

    [CloudCodeFunction("JoinGame")]
    public async Task<JoinGameResponse> JoinGame(IExecutionContext context, string lobbyCode)
    {
        try
        {
            var joinLobbyResponse = await _gameApiClient.Lobby.JoinLobbyByCodeAsync(context, context.AccessToken,
                joinByCodeRequest: new JoinByCodeRequest(lobbyCode, new Player(context.PlayerId)));
            return await StartGame(context, joinLobbyResponse.Data);
        }
        catch (Exception e)
        {
            _logger.LogError($"{e.Message} | {e.GetType().Name}");
            var lobbyIds = _gameApiClient.Lobby.GetJoinedLobbiesAsync(context, context.AccessToken);
            var lobbyResponse = await _gameApiClient.Lobby.GetLobbyAsync(context, context.AccessToken, lobbyIds.Result.Data.First());
            return await Rejoin(context, lobbyResponse.Data);
        }
    }

    private async Task<JoinGameResponse> StartGame(IExecutionContext context, Lobby lobby)
    {
        var opponentId = lobby.Players.Select(p => p.Id).First(id => id != context.PlayerId);
        var isWhite = _rng.NextDouble() >= 0.5;

        var chessBoard = new ChessBoard();
        await _gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
            lobby.Id,
            new SetItemBatchBody(new List<SetItemBody>()
            {
                new("board", chessBoard.ToFen()),
                new("whitePlayerId", isWhite ? context.PlayerId : opponentId),
                new("blackPlayerId", isWhite ? opponentId : context.PlayerId)
            }));

        var message = new JoinGameResponse()
        {
            Session = lobby.Id,
            Board = chessBoard.ToFen(),
            OpponentId = context.PlayerId,
            IsWhite = !isWhite
        };
        await _pushClient.SendPlayerMessageAsync(
            context,
            JsonConvert.SerializeObject(message),
            "opponentJoined",
            opponentId);

        return new JoinGameResponse()
        {
            Session = lobby.Id,
            Board = chessBoard.ToFen(),
            OpponentId = opponentId,
            IsWhite = isWhite
        };
    }

    private async Task<JoinGameResponse> Rejoin(IExecutionContext context, Lobby lobby)
    {
        var saveResponse =
            await _gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                lobby.Id, new List<string> { "board", "whitePlayerId", "blackPlayerId" });

        var chessBoard = ChessBoard.LoadFromFen(saveResponse.Data.Results.Find(r => r.Key == "board").Value.ToString());
        var opponentId = lobby.Players.Select(p => p.Id).First(id => id != context.PlayerId);;
        var whitePlayer = saveResponse.Data.Results.Find(r => r.Key == "whitePlayerId").Value.ToString();
        
        var playerIsWhite = whitePlayer == context.PlayerId;
        
        return new JoinGameResponse()
        {
            Session = lobby.Id,
            Board = chessBoard.ToFen(),
            OpponentId = opponentId,
            IsWhite = playerIsWhite 
        };
    }

    [CloudCodeFunction("MakeMove")]
    public async Task<MakeMoveResponse> MakeMove(IExecutionContext context, string session, string fromPosition, string toPosition)
    {
        var saveResponse =
            await _gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                session, new List<string> { "board", "whitePlayerId", "blackPlayerId" });

        var chessBoard = ChessBoard.LoadFromFen(saveResponse.Data.Results.Find(r => r.Key == "board").Value.ToString());
        var whitePlayer = saveResponse.Data.Results.Find(r => r.Key == "whitePlayerId").Value.ToString();
        var blackPlayer = saveResponse.Data.Results.Find(r => r.Key == "blackPlayerId").Value.ToString();
        
        var playerIsWhite = whitePlayer == context.PlayerId;

        var playerColour = context.PlayerId switch
        {
            var value when value == whitePlayer => PieceColor.White,                
            var value when value == blackPlayer => PieceColor.Black,
            _ => throw new Exception("Player is not in the game")
        };
        
        // Check if it is the moving player's turn
        if (chessBoard.Turn != playerColour)
        {
            _logger.LogInformation($"{chessBoard.Turn} = {playerColour}");
            throw new Exception("Invalid move, not active player");
        }

        // Check if the move is valid according to chess rules
        if (!chessBoard.IsValidMove(new Move(fromPosition, toPosition)))
        {
            throw new Exception($"Invalid move from {fromPosition} to {toPosition}");
        }
        
        // Make the move
        chessBoard.Move(new Move(fromPosition, toPosition));

        // Save the new board state
        await _gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
            session,
            new SetItemBody("board", chessBoard.ToFen()));

        var opponentId = playerIsWhite ? blackPlayer : whitePlayer;
            
        if (chessBoard.IsEndGame)
        {
            var playerScore = playerColour == chessBoard.EndGame.WonSide ? 1 :
                chessBoard.EndGame.WonSide == null ? 0.5 : 0;
            await UpdateElos(
                context,
                opponentId,
                playerScore);
        }
        
        var boardUpdatedResponse = new MakeMoveResponse
        {
            Board = chessBoard.ToFen(), 
            GameOver = chessBoard.IsEndGame, 
            EndgameType = chessBoard.EndGame?.EndgameType.ToString()
        };
        await _pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(boardUpdatedResponse), "boardUpdated",
            opponentId);
        return boardUpdatedResponse;
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

    [CloudCodeFunction("Resign")]
    public async Task<MakeMoveResponse> Resign(IExecutionContext context, string session)
    {
        var saveResponse =
            await _gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                session, new List<string> { "board", "whitePlayerId", "blackPlayerId" });

        var chessBoard = ChessBoard.LoadFromFen(saveResponse.Data.Results.Find(r => r.Key == "board").Value.ToString());
        var whitePlayer = saveResponse.Data.Results.Find(r => r.Key == "whitePlayerId").Value.ToString();
        var blackPlayer = saveResponse.Data.Results.Find(r => r.Key == "blackPlayerId").Value.ToString();

        var playerColour = context.PlayerId switch
        {
            var value when value == whitePlayer => PieceColor.White,                
            var value when value == blackPlayer => PieceColor.Black,
            _ => throw new Exception("Player is not in the game")
        };
        
        chessBoard.Resign(playerColour);
        
        await _gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
            session,
            new SetItemBody("board", chessBoard.ToFen()));
        
        var playerIsWhite = whitePlayer == context.PlayerId;
        var opponentId = playerIsWhite ? blackPlayer : whitePlayer;
        var playerScore = playerColour == chessBoard.EndGame.WonSide ? 1 :
            chessBoard.EndGame.WonSide == null ? 0.5 : 0;
        await UpdateElos(
            context,
            opponentId,
            playerScore);
        
        var boardUpdatedResponse = new MakeMoveResponse
        {
            Board = chessBoard.ToFen(), 
            GameOver = chessBoard.IsEndGame, 
            EndgameType = chessBoard.EndGame?.EndgameType.ToString()
        };
        await _pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(boardUpdatedResponse), "boardUpdated",
            opponentId);
        return boardUpdatedResponse;
    }
}

public class HostGameResponse
{
    public string LobbyCode { get; set; }
}

public class JoinGameResponse
{    
    public string Session { get; set; }
    public string Board { get; set; }
    public string OpponentId { get; set; }
    public bool IsWhite { get; set; }
}

public class MakeMoveResponse
{
    public string Board { get; set; }
    public bool GameOver { get; set; }
    public string EndgameType { get; set; }
}

public class ClearBoardMessage
{
    public string Session { get; set; }
}