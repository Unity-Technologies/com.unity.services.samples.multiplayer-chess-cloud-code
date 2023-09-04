using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Chess;

namespace ChessCloudCode;

public class Chess
{
    [CloudCodeFunction("JoinGame")]
    public async Task<Dictionary<string, string>> JoinGame(IExecutionContext context, IGameApiClient gameApiClient,
        string session)
    {
        try
        {
            // Get the current board state
            var boardResponse = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId,
                session, new List<string> { "board" });
            Dictionary<string, string> moduleResponse;

            // If no board exists, create a new one
            ChessBoard chessBoard;
            if (boardResponse.Data.Results.Count == 0)
            {
                chessBoard = new ChessBoard();
                await gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
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
    public async Task<Dictionary<string, string>> MakeMove(IExecutionContext context, PushClient pushClient,
        IGameApiClient gameApiClient, string session, string fromPosition, string toPosition)
    {
        try
        {
            // Get the current board state and lobby
            var lobbyRequest =
                gameApiClient.Lobby.GetLobbyAsync(context, context.AccessToken, session);
            var boardResponse =
                await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                    session, new List<string> { "board" });

            var chessBoard = ChessBoard.LoadFromFen(boardResponse.Data.Results.First().Value.ToString()!);

            var lobbyResponse = await lobbyRequest;

            // Check if the move is valid according to chess rules
            if (!chessBoard.IsValidMove(new Move(fromPosition, toPosition)))
            {
                return new Dictionary<string, string>
                {
                    { "error", $"Invalid move from {fromPosition} to {toPosition}" }, { "board", chessBoard.ToFen() }
                };
            }

            var validMove = chessBoard.Move(new Move(fromPosition, toPosition));
            if (!validMove) return new Dictionary<string, string> { { "board", chessBoard.ToFen() } };

            // Save the new board state
            await gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                session,
                new SetItemBody("board", chessBoard.ToFen()));

            var otherPlayer = lobbyResponse.Data.Players.Find(p => p.Id != context.PlayerId);

            if (otherPlayer == null) return new Dictionary<string, string> { { "board", chessBoard.ToFen() } };
            
            // Send the updated board state to the other player
            var message = new BoardUpdatedMessage { Session = session, Board = fromPosition };
            await pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(message), message.Type,
                otherPlayer!.Id);

            return new Dictionary<string, string> { { "board", chessBoard.ToFen() } };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, string>
                { { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }

    [CloudCodeFunction("ClearBoard")]
    public async Task<Dictionary<string, string>> ClearBoard(IExecutionContext context, PushClient pushClient,
        IGameApiClient gameApiClient, string session)
    {
        try
        {
            // Clear the board state and send a message to all players to clear their boards
            await gameApiClient.CloudSaveData.SetCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                session,
                new SetItemBody("board", new ChessBoard().ToFen()));
            var lobbyResponse = await gameApiClient.Lobby.GetLobbyAsync(context, context.ServiceToken, session);
            foreach (var player in lobbyResponse.Data.Players)
            {
                var clearBoardMessage = new ClearBoardMessage { Session = session };
                await pushClient.SendPlayerMessageAsync(context, JsonConvert.SerializeObject(clearBoardMessage),
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
    }

    private class ClearBoardMessage
    {
        public string Session { get; set; }
    }
}

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddSingleton(PushClient.Create());
    }
}