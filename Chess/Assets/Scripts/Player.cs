using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    private GameObject _selectedPiece;
    public Camera playerCamera;
    public GameObject cameraPivot;
    public TextMeshProUGUI joinCodeInput;
    public TextMeshProUGUI joinCodeDisplayText;

    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerEloText;
    public TextMeshProUGUI opponentNameText;
    public TextMeshProUGUI opponentEloText;

    public GameObject resignButton;
    public GameObject uiPanel;
    public TextMeshProUGUI resultText;
    public GameObject board;

    public ISession Session;
    private ChessCloudCodeBindings _chessCloudCodeBindings;
    public Match Match;

    private readonly Dictionary<string, UnityEngine.Object> _prefabs = new();
    private const string StartingBoard = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private bool _gameStarted;
    private bool _isWhite;
    private string _currentSession;

    private readonly Color32 _selectedColor = new(84, 84, 255, 255);
    private readonly Color32 _lightColor = new(223, 210, 194, 255);
    private readonly Color32 _darkColor = new(84, 84, 84, 255);

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        // await SubscribeToPlayerMessages();
        SyncBoard(StartingBoard);
        InitializePlayer();
        _chessCloudCodeBindings = new ChessCloudCodeBindings(CloudCodeService.Instance);
        resignButton.SetActive(false);
    }

    private async Task InitializePlayer()
    {
        try
        {
            await RefreshPlayerInfo();
        }
        catch (LeaderboardsException e)
        {
            // If player is not present on lb create a new entry for them
            if (e.Reason == LeaderboardsExceptionReason.EntryNotFound)
            {
                var response = await LeaderboardsService.Instance.AddPlayerScoreAsync("EloRatings", 1500);
                playerEloText.text = "Rating: " + Math.Round(response?.Score ?? 1500);
                playerNameText.text = response.PlayerName;
            }
            else
            {
                throw;
            }
        }
    }

    private async Task RefreshPlayerInfo()
    {
        var response = await LeaderboardsService.Instance.GetPlayerScoreAsync("EloRatings");

        playerEloText.text = "Rating: " + Math.Round(response?.Score ?? 1500);
        playerNameText.text = response.PlayerName;
    }

    private async Task SetOpponentInfo(string opponentId)
    {
        Debug.Log($"Setting opponent info");
        var response =
            await LeaderboardsService.Instance.GetScoresByPlayerIdsAsync("EloRatings",
                new List<string>() { opponentId });
        var opponent = response?.Results?.FirstOrDefault();
        Debug.Log($"Setting opponent info {opponent?.PlayerId}");
        opponentEloText.text = "Rating: " + Math.Round(opponent?.Score ?? 1500);
        opponentNameText.text = opponent?.PlayerName ?? "Unknown";
    }

    public async void CreateGame()
    {
        var hostGameResponse =
            await CloudCodeService.Instance.CallModuleEndpointAsync<HostGameResponse>("ChessCloudCode", "HostGame");

        joinCodeDisplayText.text = hostGameResponse.LobbyCode;
    }

    private void SetPov()
    {
        var angle = _isWhite ? 0 : 180;
        cameraPivot.transform.eulerAngles = new Vector3(0, angle, 0);
    }

    public async void Resign()
    {
        try
        {
            var boardUpdate = await CloudCodeService.Instance.CallModuleEndpointAsync<BoardUpdateResponse>(
                "ChessCloudCode", "Resign",
                new Dictionary<string, object> { { "session", _currentSession } });
            OnBoardUpdate(boardUpdate);
        }
        catch (LobbyServiceException exception)
        {
            Debug.LogException(exception);
        }
    }

    public async void JoinLobbyByCode()
    {
        try
        {
            // There's a weird no space character that gets added to the end of the lobby code, let's remove it for now
            var sanitizedLobbyCode = Regex.Replace(joinCodeInput.text, @"\s", "").Replace("\u200B", "");

            var joinGameResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<JoinGameResponse>(
                "ChessCloudCode", "JoinGame",
                new Dictionary<string, object> { { "lobbyCode", sanitizedLobbyCode } });
            joinCodeDisplayText.text = sanitizedLobbyCode;

            OnGameStart(joinGameResponse);
        }
        catch (LobbyServiceException exception)
        {
            Debug.LogException(exception);
        }
    }

    private void SyncBoard(string fen)
    {
        var boardState = FenToDict(fen);
        try
        {
            foreach (Transform child in board.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (var piece in boardState)
            {
                var pieceType = char.ToLower(piece.Value) switch
                {
                    'p' => "Pawn",
                    'n' => "Knight",
                    'b' => "Bishop",
                    'r' => "Rook",
                    'q' => "Queen",
                    'k' => "King",
                    _ => ""
                };
                var prefabName = pieceType + (char.IsUpper(piece.Value) ? "Light" : "Dark");
                if (!_prefabs.ContainsKey(prefabName))
                {
                    _prefabs[prefabName] = Resources.Load($"{pieceType}/Prefabs/{prefabName}");
                }

                var newObject = Instantiate(_prefabs[prefabName], board.transform);
                newObject.GameObject().transform.position = new Vector3(piece.Key.Item1, 0, piece.Key.Item2);
                newObject.GameObject().transform.rotation = Quaternion.Euler(0, char.IsLower(piece.Value) ? 180 : 0, 0);
            }
        }
        catch (CloudCodeException exception)
        {
            Debug.LogException(exception);
        }
    }

    private async void MakeMove(GameObject piece, Vector3 toPos)
    {
        if (piece == null) return;
        var result = await CloudCodeService.Instance.CallModuleEndpointAsync<BoardUpdateResponse>(
            "ChessCloudCode",
            "MakeMove",
            new Dictionary<string, object>
            {
                { "session", _currentSession },
                { "fromPosition", PosToFen(piece.transform.position) },
                { "toPosition", PosToFen(toPos) }
            });

        SelectPiece(null);
        OnBoardUpdate(result);
    }

    private async void OnBoardUpdate(BoardUpdateResponse boardUpdateResponse)
    {
        SyncBoard(boardUpdateResponse.Board);
        if (boardUpdateResponse.GameOver)
        {
            uiPanel.SetActive(true);
            resignButton.SetActive(false);
            resultText.text = boardUpdateResponse.EndgameType;
            RefreshPlayerInfo();
        }
    }

    private async void OnGameStart(JoinGameResponse joinGameResponse)
    {
        Debug.Log($"Opponent joined: {joinGameResponse.OpponentId}");
        _currentSession = joinGameResponse.Session;
        SetOpponentInfo(joinGameResponse.OpponentId);
        SyncBoard(joinGameResponse.Board);
        uiPanel.SetActive(false);
        resignButton.SetActive(true);
        _isWhite = joinGameResponse.IsWhite;
        SetPov();
        _gameStarted = true;
    }

    private Task SubscribeToPlayerMessages()
    {
        var callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += @event =>
        {
            switch (@event.MessageType)
            {
                case "boardUpdated":
                    var message = JsonConvert.DeserializeObject<BoardUpdateResponse>(@event.Message);
                    OnBoardUpdate(message);
                    break;
                case "opponentJoined":
                    var opponentJoinedMessage = JsonConvert.DeserializeObject<JoinGameResponse>(@event.Message);
                    OnGameStart(opponentJoinedMessage);
                    break;
                default:
                    Debug.Log(
                        $"Got unsupported player Message: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
                    break;
            }
        };
        callbacks.ConnectionStateChanged += @event =>
        {
            if (@event == EventConnectionState.Subscribed && _currentSession != null && _gameStarted)
            {
            }

            Debug.Log($"Got player subscription ConnectionStateChanged: {@event.ToString()}");
        };
        callbacks.Kicked += () => { Debug.Log($"Got player subscription Kicked"); };
        callbacks.Error += @event =>
        {
            Debug.Log($"Got player subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };
        return CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
    }

    public void PlayerInteract(InputAction.CallbackContext context)
    {
        if (!context.performed && _currentSession != null) return;
        var mousePosition = Mouse.current.position.ReadValue();
        var rayOrigin = playerCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(rayOrigin, out var hitInfo))
        {
            var gameObject = hitInfo.transform.gameObject;
            if (hitInfo.transform.gameObject.name == "Board"
                || (_selectedPiece != null && gameObject.name.Contains("Light") != _isWhite))
            {
                var boardPos = new Vector3(Mathf.RoundToInt(hitInfo.point.x), 0, Mathf.RoundToInt(hitInfo.point.z));
                MakeMove(_selectedPiece, boardPos);
            }
            else if (gameObject.name.Contains("Light") == _isWhite)
            {
                SelectPiece(hitInfo.transform.gameObject);
                Debug.Log($"Piece selected: {_selectedPiece.name}");
            }
        }
        else
        {
            SelectPiece(null);
        }
    }

    private void SelectPiece(GameObject piece)
    {
        if (_selectedPiece != null)
        {
            ChangeMaterialColor(_selectedPiece,
                _selectedPiece.name.Contains("Light") ? _lightColor : _darkColor);
        }

        _selectedPiece = piece;
        if (_selectedPiece == null) return;
        ChangeMaterialColor(_selectedPiece, _selectedColor);
    }

    private static Dictionary<Tuple<int, int>, char> FenToDict(string fen)
    {
        var fenParts = fen.Split(' ');
        var boardState = fenParts[0];
        var ranks = boardState.Split('/');

        var coordinatesDict = new Dictionary<Tuple<int, int>, char>();
        var x = 0;
        var y = 7;

        foreach (var rank in ranks)
        {
            foreach (var c in rank)
            {
                if (char.IsDigit(c))
                {
                    x += int.Parse(c.ToString());
                }
                else
                {
                    var coordinates = new Tuple<int, int>(x, y);
                    coordinatesDict.Add(coordinates, c);
                    x += 1;
                }
            }

            x = 0;
            y -= 1;
        }

        return coordinatesDict;
    }

    public async void FindMatch()
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

        Session = await MultiplayerService.Instance.MatchmakeSessionAsync(matchmakerOptions, sessionOptions,
            matchmakerCancellationSource.Token);
        Session.Changed += OnSessionChanged;
    }

    private async void OnSessionChanged()
    {
        if (Session.PlayerCount == Session.MaxPlayers && !_gameStarted)
        {
            if (this.Match == null)
            {
                var match = await _chessCloudCodeBindings.InitializeMatch(Session.Id);
                if (match.Status == "OK")
                {
                    SyncMatch();
                }
            }
        }
        else if (_gameStarted)
        {
            SyncMatch();
        }
    }

    public async Task SyncMatch()
    {
        // Get Match Id from cloud save
        var playerData =
            await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "currentMatchId" });

        if (!playerData.ContainsKey("currentMatchId"))
        {
            Debug.Log("no `currentMatchId` stored on Player in Cloud Save");
            // TODO throw exception?
        }

        // Get match from cloud save
        var matchId = playerData["currentMatchId"];
        var matchData = await MatchLoader.LoadMatchDataAsync(matchId.Value.GetAs<string>());

        // Update local match state
        UpdateMatchState(matchData);
    }

    private async Task UpdateMatchState(Match matchData)
    {
        var thisPlayerId = AuthenticationService.Instance.PlayerId;
        if (this.Match == null)
        {
            await InitializePlayer();
            var opponentId = thisPlayerId == matchData.WhitePlayerId
                ? matchData.BlackPlayerId
                : matchData.WhitePlayerId;
            await SetOpponentInfo(opponentId);
        }

        this.Match = matchData;
        this._isWhite = this.Match.WhitePlayerId == thisPlayerId;
        SyncBoard(this.Match.Board);
    }

    private void ChangeMaterialColor(GameObject obj, Color newColor)
    {
        var selectedRenderer = obj.GetComponent<Renderer>();
        selectedRenderer.material.color = newColor;
    }

    public class HostGameResponse
    {
        public string LobbyCode { get; set; }
    }

    public class BoardUpdateResponse
    {
        public string Board { get; set; }
        public bool GameOver { get; set; }
        public string EndgameType { get; set; }
    }

    public class JoinGameResponse
    {
        public string Session { get; set; }
        public string Board { get; set; }
        public string OpponentId { get; set; }
        public bool IsWhite { get; set; }
    }

    private string PosToFen(Vector3 pos)
    {
        return (char)(pos.x + 97) + ((char)pos.z + 1).ToString();
    }
}