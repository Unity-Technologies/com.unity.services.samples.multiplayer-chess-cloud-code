using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Clubs : MonoBehaviour
{
    public GameObject landingCanvas;
    public GameObject creationCanvas;
    public GameObject myClubsCanvas;
    public GameObject searchClubsCanvas;
    public GameObject clubCanvas;
    public GameObject clubManagementCanvas;

    public TMP_InputField clubIDInputText;
    public TextMeshProUGUI playerNameText;
    public TMP_InputField clubNameInputText;
    public TMP_InputField clubCountryInputText;
    public Toggle clubApprovalRequiredToggle;
    public TextMeshProUGUI clubCreationErrorText;

    public ClubsView myClubsView;
    public ClubsView searchClubsView;

    public TMP_InputField clubSearchNameInput;
    public TMP_InputField clubSearchCountryInput;
    public TMP_Dropdown clubSearchMemberCountSortInput;
    public Button clubSearchSubmitButton;
    public Button clubSearchClearButton;

    public MembersView clubMembersView;
    public MembersManagementView clubMembersManagementView;
    public MembersManagementView clubRequestsManagementView;

    public ChessClub ActiveClub
    {
        get => _activeClub;
        set
        {
            _activeClub = value;
            if (value == null)
            {
                return;
            }

            clubNameText.text = value.Name;
            clubCountryText.text = value.Country;
            clubMembersHeaderText.text = $"Members ({value.MemberCount})";
            clubMembersView.UpdateMembersView(value.MemberNames.Values.ToList());
            clubMembersManagementView.UpdateMembersView(value.ID, value.MemberNames, KickMember, null);
            clubManagementNameText.text = value.Name;
            clubManagementCountryText.text = value.Country;
            clubManagementMembersHeaderText.text = $"Members ({value.MemberCount})";
            if (value.MyStatus == "member")
            {
                joinClubButton.gameObject.SetActive(false);
            }
            else
            {
                joinClubButton.gameObject.SetActive(true);
                if (value.MyStatus == "pending")
                {
                    joinClubButton.interactable = false;
                    joinClubButton.GetComponentInChildren<TextMeshProUGUI>().text = "Request Pending";
                }
                else
                {
                    joinClubButton.interactable = true;
                    joinClubButton.GetComponentInChildren<TextMeshProUGUI>().text =
                        ActiveClub.ApprovalRequired ? "Request To Join" : "Join Club";
                }
            }
        }
    }

    private ChessClub _activeClub;

    public Dictionary<string, string> JoinRequests
    {
        get => _joinRequests;
        set
        {
            _joinRequests = value;
            if (value == null)
            {
                return;
            }

            clubManagementRequestsHeaderText.text = $"Requests ({value.Count})";
            clubRequestsManagementView.UpdateMembersView(_activeClub.ID, value, AdmitMember, DenyMember);
        }
    }

    private Dictionary<string, string> _joinRequests;

    public TextMeshProUGUI clubNameText;
    public TextMeshProUGUI clubCountryText;
    public TextMeshProUGUI clubMembersHeaderText;
    public TextMeshProUGUI clubManagementNameText;
    public TextMeshProUGUI clubManagementCountryText;
    public TextMeshProUGUI clubManagementMembersHeaderText;
    public TextMeshProUGUI clubManagementRequestsHeaderText;
    public Button joinClubButton;
    public Button clubManagementButton;

    private GameObject _originCanvas;
    private Func<Task> _updateClubList;

    private async void Start()
    {
        landingCanvas.SetActive(true);
        creationCanvas.SetActive(false);
        myClubsCanvas.SetActive(false);
        searchClubsCanvas.SetActive(false);
        clubCanvas.SetActive(false);
        clubManagementCanvas.SetActive(false);

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        playerNameText.text = AuthenticationService.Instance.PlayerId;
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ChessClub
    {
        [JsonProperty] public Guid ID { get; private set; }
        public string Name { get; set; }
        [JsonProperty] public List<string> Members { get; set; }
        [JsonProperty] public Dictionary<string, string> MemberNames { get; set; }
        [JsonProperty] public int MemberCount { get; set; }
        public string Country { get; set; }
        public bool ApprovalRequired { get; set; }
        [JsonProperty] public string AdminId { get; private set; }
        public string MyStatus { get; set; }

        public ChessClub(string name, string country, bool approvalRequired)
        {
            Name = name;
            Country = country;
            ApprovalRequired = approvalRequired;
        }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class CloudCodeResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string StackTrace { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClubsResponse : CloudCodeResponse
    {
        public List<ChessClub> Clubs { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClubResponse : CloudCodeResponse
    {
        public ChessClub Club { get; set; }
        public string MyStatus { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClubJoinRequestsResponse : CloudCodeResponse
    {
        public Dictionary<string, string> Requests { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class MemberManagementResponse : CloudCodeResponse
    {
        public Dictionary<string, string> Requests { get; set; }
        public Dictionary<string, string> MemberNames { get; set; }
        public List<string> Members { get; set; }
        public int MemberCount { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClubCreationResponse : CloudCodeResponse
    {
        public string Id { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class JoinClubResponse : CloudCodeResponse
    {
        public string Status { get; set; }
    }

    public void LandingToClubCreation()
    {
        clubCreationErrorText.text = "";
        landingCanvas.SetActive(false);
        creationCanvas.SetActive(true);
    }

    public void ClubCreationToLanding()
    {
        clubCreationErrorText.text = "";
        clubNameInputText.text = "";
        clubCountryInputText.text = "";
        clubApprovalRequiredToggle.isOn = false;
        creationCanvas.SetActive(false);
        landingCanvas.SetActive(true);
    }

    public async void CreateClub()
    {
        if (string.IsNullOrWhiteSpace(clubNameInputText.text))
        {
            clubCreationErrorText.text = "invalid input: club name cannot be empty";
            return;
        }

        if (string.IsNullOrWhiteSpace(clubCountryInputText.text))
        {
            clubCreationErrorText.text = "invalid input: club country cannot be empty";
            return;
        }

        var club = new ChessClub(clubNameInputText.text, clubCountryInputText.text, clubApprovalRequiredToggle.isOn);

        var createClubResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<ClubCreationResponse>(
            "ChessClubCloudCode", "CreateClub",
            new Dictionary<string, object> { { "club", club } });
        if (createClubResponse.Success)
        {
            Debug.Log($"Successfully created club {createClubResponse.Id}");
            ClubCreationToLanding();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(createClubResponse.StackTrace))
            {
                Debug.LogError(createClubResponse.Error);
                Debug.LogError(createClubResponse.StackTrace);
            }
            else
            {
                clubCreationErrorText.text = createClubResponse.Error;
            }
        }
    }

    public async void JoinClub()
    {
        if (string.IsNullOrWhiteSpace(clubIDInputText.text))
        {
            Debug.LogError("invalid input: club id cannot be empty");
            return;
        }

        var joinClubResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<JoinClubResponse>(
            "ChessClubCloudCode", "JoinClub",
            new Dictionary<string, object> { { "id", clubIDInputText.text } });

        if (joinClubResponse.Success)
        {
            Debug.Log($"Successfully sent join request, status: {joinClubResponse.Status}");
        }
        else
        {
            Debug.LogError(joinClubResponse.Error);
            if (!string.IsNullOrWhiteSpace(joinClubResponse.StackTrace))
            {
                Debug.LogError(joinClubResponse.StackTrace);
            }
        }

        clubIDInputText.text = "";
    }

    public async void JoinClubFromClubPage()
    {
        var joinClubResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<JoinClubResponse>(
            "ChessClubCloudCode", "JoinClub",
            new Dictionary<string, object> { { "id", ActiveClub.ID } });

        if (joinClubResponse.Success)
        {
            var status = joinClubResponse.Status;
            if (status == "pending")
            {
                joinClubButton.interactable = false;
                joinClubButton.GetComponentInChildren<TextMeshProUGUI>().text = "Request Pending";
            }
            else
            {
                joinClubButton.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogError(joinClubResponse.Error);
            if (!string.IsNullOrWhiteSpace(joinClubResponse.StackTrace))
            {
                Debug.LogError(joinClubResponse.StackTrace);
            }
        }

        clubIDInputText.text = "";
    }

    private async Task ListMyClubs()
    {
        var myClubsResponse =
            await CloudCodeService.Instance.CallModuleEndpointAsync<ClubsResponse>("ChessClubCloudCode", "ListMyClubs");

        if (myClubsResponse.Success)
        {
            myClubsView.UpdateClubsView(myClubsResponse.Clubs, myClubsResponse.Clubs.Select(c => c.ID).ToList(),
                OpenClubPage);
        }
        else
        {
            Debug.LogError(myClubsResponse.Error);
            Debug.LogError(myClubsResponse.StackTrace);
        }
    }

    private async Task SearchClubs()
    {
        var searchParams = new Dictionary<string, object>
        {
            { "name", clubSearchNameInput.text },
            { "country", clubSearchCountryInput.text }
        };
        switch (clubSearchMemberCountSortInput.value)
        {
            case 1:
                searchParams.Add("memberCountSort", "desc");
                break;
            case 2:
                searchParams.Add("memberCountSort", "asc");
                break;
        }

        var searchClubsResponse =
            await CloudCodeService.Instance.CallModuleEndpointAsync<ClubsResponse>("ChessClubCloudCode", "SearchClubs",
                searchParams);
        var myClubsResponse =
            await CloudCodeService.Instance.CallModuleEndpointAsync<ClubsResponse>("ChessClubCloudCode", "ListMyClubs");

        var myClubIDs = myClubsResponse.Success ? myClubsResponse.Clubs.Select(c => c.ID).ToList() : new List<Guid>();

        if (searchClubsResponse.Success)
        {
            searchClubsView.UpdateClubsView(searchClubsResponse.Clubs, myClubIDs, OpenClubPage);
        }
        else
        {
            Debug.LogError(searchClubsResponse.Error);
            Debug.LogError(searchClubsResponse.StackTrace);
        }
    }

    private async Task LoadClub(Guid id)
    {
        var clubResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<ClubResponse>("ChessClubCloudCode",
            "LoadClub", new Dictionary<string, object> { { "id", id } });

        if (clubResponse.Success)
        {
            clubResponse.Club.MyStatus = clubResponse.MyStatus;
            ActiveClub = clubResponse.Club;
            clubManagementButton.gameObject.SetActive(ActiveClub.AdminId == AuthenticationService.Instance.PlayerId);
        }
        else
        {
            Debug.LogError(clubResponse.Error);
            Debug.LogError(clubResponse.StackTrace);
        }
    }

    private async Task LoadClubJoinRequests()
    {
        var clubResponse = await CloudCodeService.Instance.CallModuleEndpointAsync<ClubJoinRequestsResponse>(
            "ChessClubCloudCode",
            "LoadClubJoinRequests", new Dictionary<string, object> { { "id", _activeClub.ID } });

        if (clubResponse.Success)
        {
            JoinRequests = clubResponse.Requests;
        }
        else
        {
            Debug.LogError(clubResponse.Error);
            Debug.LogError(clubResponse.StackTrace);
        }
    }

    private async Task KickMember(Guid clubId, string memberId)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync<MemberManagementResponse>(
            "ChessClubCloudCode", "KickClubMember",
            new Dictionary<string, object> { { "clubId", clubId }, { "memberId", memberId } });
        if (!response.Success)
        {
            Debug.LogError(response.Error);
            if (!string.IsNullOrWhiteSpace(response.StackTrace))
            {
                Debug.LogError(response.StackTrace);
            }
        }

        var activeClub = ActiveClub;
        activeClub.Members = response.Members;
        activeClub.MemberCount = response.MemberCount;
        activeClub.MemberNames = response.MemberNames;
        ActiveClub = activeClub;
    }

    private async Task AdmitMember(Guid clubId, string memberId)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync<MemberManagementResponse>(
            "ChessClubCloudCode", "AdmitClubMember",
            new Dictionary<string, object> { { "clubId", clubId }, { "memberId", memberId } });
        if (!response.Success)
        {
            Debug.LogError(response.Error);
            if (!string.IsNullOrWhiteSpace(response.StackTrace))
            {
                Debug.LogError(response.StackTrace);
            }
        }

        var activeClub = ActiveClub;
        activeClub.Members = response.Members;
        activeClub.MemberCount = response.MemberCount;
        activeClub.MemberNames = response.MemberNames;
        ActiveClub = activeClub;

        JoinRequests = response.Requests;
    }

    private async Task DenyMember(Guid clubId, string memberId)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync<MemberManagementResponse>(
            "ChessClubCloudCode", "DenyClubMember",
            new Dictionary<string, object> { { "clubId", clubId }, { "memberId", memberId } });
        if (!response.Success)
        {
            Debug.LogError(response.Error);
            if (!string.IsNullOrWhiteSpace(response.StackTrace))
            {
                Debug.LogError(response.StackTrace);
            }
        }

        JoinRequests = response.Requests;
    }

    private async Task DeleteClub(Guid clubId)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync<CloudCodeResponse>("ChessClubCloudCode",
            "DeleteClub",
            new Dictionary<string, object> { { "id", clubId } });
        if (!response.Success)
        {
            Debug.LogError(response.Error);
            if (!string.IsNullOrWhiteSpace(response.StackTrace))
            {
                Debug.LogError(response.StackTrace);
            }
        }
        else
        {
            ActiveClub = null;
            await _updateClubList();
            clubManagementCanvas.SetActive(false);
            _originCanvas.SetActive(true);
        }
    }

    public async void ApplySearchFilters()
    {
        await SearchClubs();
    }

    public async void ClearSearchFilters()
    {
        clubSearchNameInput.text = "";
        clubSearchCountryInput.text = "";
        clubSearchMemberCountSortInput.value = 0;
        await SearchClubs();
    }

    public async void LandingToMyClubs()
    {
        await ListMyClubs();
        _originCanvas = myClubsCanvas;
        _updateClubList = ListMyClubs;
        landingCanvas.SetActive(false);
        myClubsCanvas.SetActive(true);
    }
    
    public void MyClubsToLanding()
    {
        _originCanvas = null;
        _updateClubList = null;
        myClubsCanvas.SetActive(false);
        landingCanvas.SetActive(true);
    } 
    
    public async void LandingToSearchClubs()
    {
        await SearchClubs();
        _originCanvas = searchClubsCanvas;
        _updateClubList = SearchClubs;
        landingCanvas.SetActive(false);
        searchClubsCanvas.SetActive(true);
    }
    
    public void SearchClubsToLanding()
    {
        _originCanvas = null;
        _updateClubList = null;
        searchClubsCanvas.SetActive(false);
        landingCanvas.SetActive(true);
    }

    private async Task OpenClubPage(Guid id, bool isInClub)
    {
        await LoadClub(id);
        _originCanvas.SetActive(false);
        clubCanvas.SetActive(true);
    }
    
    public async void ClubToClubsList()
    {
        ActiveClub = null;
        await _updateClubList();
        clubCanvas.SetActive(false);
        _originCanvas.SetActive(true);
    }
    
    public async void ClubToClubManagement()
    {
        await LoadClubJoinRequests();
        clubCanvas.SetActive(false);
        clubManagementCanvas.SetActive(true);
    }
    
    public async void ClubManagementToClub()
    {
        JoinRequests = null;
        await LoadClub(ActiveClub.ID);
        clubManagementCanvas.SetActive(false);
        clubCanvas.SetActive(true);
    }

    public async void DeleteClubToClubList()
    {
        await DeleteClub(ActiveClub.ID);
    }

    public void SwitchToGameScene()
    {
        SceneManager.LoadScene("ChessDemo", LoadSceneMode.Single);
    }
}