using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DefaultNamespace;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.CloudCode.GeneratedBindings.ChessCloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
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
    public TextMeshProUGUI errorText;

    public GameObject resignButton;
    public GameObject uiPanel;
    public TextMeshProUGUI resultText;
    public GameObject board;

    private ISession _session;
    private ChessCloudCodeBindings _chessCloudCodeBindings;

    private readonly Dictionary<string, UnityEngine.Object> _prefabs = new();
    private GameState _gameState;
    private bool _isWhite;
    
    private const string StartingBoard = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private readonly Color32 _selectedColor = new(84, 84, 255, 255);
    private readonly Color32 _lightColor = new(223, 210, 194, 255);
    private readonly Color32 _darkColor = new(84, 84, 84, 255);

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        SyncBoardFromFen(StartingBoard);
        
        // TODO Check for existing match
        // lookup match on player
        //  if match then join or create session
        // else
        //   Main menu
        
        _gameState = new GameState();
        _chessCloudCodeBindings = new ChessCloudCodeBindings(CloudCodeService.Instance);
       
        resignButton.SetActive(false);
        await LoadPlayerInfo(AuthenticationService.Instance.PlayerId, playerNameText, playerEloText);

        Events.current.errorAcknowledgedEvent.AddListener(() => _gameState.SetGamePhase(GamePhase.MainMenu));
        Events.current.phaseChangeEvent.AddListener(OnPhaseChange);
    }

    private async void OnPhaseChange(PhaseChangeContext phaseChangeContext)
    {
        var thisPlayerId = AuthenticationService.Instance.PlayerId;
        
        switch (phaseChangeContext.Phase)
        {
            case GamePhase.InMatch:
            {
                var result = await MatchLoader.LoadMatchDataAsync(_session.Id);
                SyncBoardFromFen(result.Board);
                await InitializeUI(thisPlayerId, result);
                break;
            }
            case GamePhase.MatchEnded:
                uiPanel.SetActive(true);
                resignButton.SetActive(false);
                //resultText.text = boardUpdateResponse.EndgameType;
                await LoadPlayerInfo(thisPlayerId, playerNameText, playerEloText);
                break;
        }
    }

    public async Task<string> LoadMatchIdFromPlayer()
    {
        var playerData =
            await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "currentMatchId" });

        if (!playerData.ContainsKey("currentMatchId"))
        {
            Debug.Log("no `currentMatchId` stored on Player in Cloud Save");
            _gameState.SetGamePhase(GamePhase.Error, "No Match ID on Player");
            // TODO throw exception?
        }

        return playerData["currentMatchId"].Value.GetAsString();
    }

    private void SyncBoardFromFen(string fen)
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
        await _chessCloudCodeBindings.MakeMove(_session.Id, PosToFen(piece.transform.position), PosToFen(toPos));
        SelectPiece(null);
    }

    public void PlayerInteract(InputAction.CallbackContext context)
    {
        if (!context.performed && _session != null) return;
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
        if (_gameState.GamePhase != GamePhase.MainMenu)
        {
            // TODO set error, raise exception;
            _gameState.SetGamePhase(GamePhase.Error,
                "Unable to create match, matches can only be created from the main menu");
            Debug.Log("Unable to create match, matches can only be created from the main menu");
        }
        else
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

            Debug.Log("Finding Match");
            try
            {
                _gameState.SetGamePhase(GamePhase.Finding);
                _session = await MultiplayerService.Instance.MatchmakeSessionAsync(matchmakerOptions, sessionOptions,
                    matchmakerCancellationSource.Token); // Blocking call until match is found
                Debug.Log($"Found Match, Session ID: {_session.Id}");
                OnSessionChanged(); // Session just created, call manually
                _session.Changed += OnSessionChanged;
            }
            catch (SessionException e)
            {
                Debug.Log($"Unable to Matchmake: {e}");
                _gameState.SetGamePhase(GamePhase.Error, "Unable to search for players");
                throw;
            }
            catch (Exception e)
            {
                Debug.Log($"Unable to Matchmake: {e}");
                _gameState.SetGamePhase(GamePhase.Error, "Unable to search for players");
                throw;
            }
        }
    }

    private async Task InitializeMatchState(ISession session)
    {
        try
        {
            Debug.Log("Initialising Match State using Cloud Code");
            var match = await _chessCloudCodeBindings.InitializeMatch(session.Id);
            Debug.Log($"Match status is: {match.Status}");
            if (match.Status == "OK")
            {
                _gameState.SetGamePhase(GamePhase.InMatch);
            }
            else
            {
                _gameState.SetGamePhase(GamePhase.Error, $"Error Initializing match state, status: {match.Status}");
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Error initializing match: {e}");
            _gameState.SetGamePhase(GamePhase.Error, "Unable to initialize match");
            throw;
        }
    }

    private async void OnSessionChanged()
    {
        Debug.Log("Session Changed");
        if (_gameState.GamePhase == GamePhase.Finding)
        {
            await InitializeMatchState(_session);
        }
        
        if (_gameState.GamePhase == GamePhase.InMatch)
        {
            var matchData = await MatchLoader.LoadMatchDataAsync(_session.Id);
            SyncBoardFromFen(matchData.Board);
        }
    }

    private async Task InitializeUI(string thisPlayerId, Match matchData)
    {
        var opponentId = thisPlayerId == matchData.WhitePlayerId
            ? matchData.BlackPlayerId
            : matchData.WhitePlayerId;
        await LoadPlayerInfo(thisPlayerId, playerNameText, playerEloText);
        await LoadPlayerInfo(opponentId, opponentNameText, opponentEloText);
        
        _isWhite = matchData.WhitePlayerId == thisPlayerId;
        uiPanel.SetActive(false);
        resignButton.SetActive(true);
        
        // Set PoV
        var angle = _isWhite ? 0 : 180;
        cameraPivot.transform.eulerAngles = new Vector3(0, angle, 0);
    }
    
    private async Task LoadPlayerInfo(string playerId, TextMeshProUGUI text, TextMeshProUGUI elo)
    {
        var playerData = await _chessCloudCodeBindings.PrepareAndFetchPlayerData(playerId);
        text.text = $"Rating: {playerData.EloScore}";
        elo.text = $"{playerData.Name}";
    }

    private void ChangeMaterialColor(GameObject obj, Color newColor)
    {
        var selectedRenderer = obj.GetComponent<Renderer>();
        selectedRenderer.material.color = newColor;
    }

    private string PosToFen(Vector3 pos)
    {
        return (char)(pos.x + 97) + ((char)pos.z + 1).ToString();
    }
    
    /* TODO replace manual Lobby integration with Sessions */
    public async void CreateGame()
    {
        var hostGameResponse =
            await CloudCodeService.Instance.CallModuleEndpointAsync<HostGameResponse>("ChessCloudCode", "HostGame");

        joinCodeDisplayText.text = hostGameResponse.LobbyCode;
    }

    public async void Resign()
    {
        try
        {
            var boardUpdate = await CloudCodeService.Instance.CallModuleEndpointAsync<BoardUpdateResponse>(
                "ChessCloudCode", "Resign",
                new Dictionary<string, object> { { "session", _session.Id } });
            //OnBoardUpdate(boardUpdate);
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

            Debug.Log($"Opponent joined: {joinGameResponse.OpponentId}");
            uiPanel.SetActive(false);
            resignButton.SetActive(true);
            //SetPov();
            _gameState.SetGamePhase(GamePhase.InMatch);
        }
        catch (LobbyServiceException exception)
        {
            Debug.LogException(exception);
        }
    }
}