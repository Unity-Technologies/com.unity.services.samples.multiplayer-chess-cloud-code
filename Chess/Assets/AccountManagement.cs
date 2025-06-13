using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SignInScenePlayer : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField playerNameInput;

    private const string ChessDemoScene = "ChessDemo";
    private const string SignInScene = "SignIn";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await SignInCachedUserAsync();
            
            SetupEvents();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }
    }

    public async void SignIn()
    {
        try
        {
            var username = usernameInput.text;
            var password = passwordInput.text;
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            var currentPlayer = AuthenticationService.Instance.PlayerId;
            Debug.Log($"Player: {currentPlayer} signed in successfully");
            LoadSceneByName(ChessDemoScene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void SignOut()
    {
        try
        {
            AuthenticationService.Instance.SignOut(true);
            LoadSceneByName(SignInScene);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public async void SignUp()
    {
        try
        {
            var username = usernameInput.text;
            var password = passwordInput.text;
            var playerNameInputText = playerNameInput.text;
            // Sign up with username and password
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Debug.LogError("Username and password cannot be empty!");
                return;
            }

            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            
            // Set a playerName if provided. If not provided, autogenerate one
            if (playerNameInputText != "")
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(playerNameInputText);
            }
            else
            {
                var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
                await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            }
            LoadSceneByName(ChessDemoScene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
    
    public async void SignInAnonymously()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            LoadSceneByName(ChessDemoScene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name cannot be empty!");
            return;
        }

        Debug.Log("Attempting to load scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
    
    // Setup authentication event handlers if desired
    private static void SetupEvents() {
        AuthenticationService.Instance.SignedIn += () => {
            // Shows how to get a playerID
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");

            // Shows how to get an access token
            Debug.Log($"Access Token: {AuthenticationService.Instance.AccessToken}");

        };

        AuthenticationService.Instance.SignInFailed += (err) => {
            Debug.LogError(err);
        };

        AuthenticationService.Instance.SignedOut += () => {
            Debug.Log("Player signed out.");
        };

        AuthenticationService.Instance.Expired += () =>
        {
            Debug.Log("Player session could not be refreshed and expired.");
        };
    }
    
    async Task SignInCachedUserAsync()
    {
        // Check if a cached player already exists by checking if the session token exists
        if (!AuthenticationService.Instance.SessionTokenExists)
        {
            // if not, then do nothing
            return;
        }

        // Sign in Anonymously
        // This call will sign in the cached player.
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            SceneManager.LoadScene(ChessDemoScene);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }
}
