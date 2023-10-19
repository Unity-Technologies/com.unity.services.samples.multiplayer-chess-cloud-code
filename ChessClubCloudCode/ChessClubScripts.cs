using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace ChessClubCloudCode;

public class ChessClubScripts
{
    private readonly ILogger<ChessClubScripts> _logger;
    public ChessClubScripts(ILogger<ChessClubScripts> logger)
    {
        _logger = logger;
    }
    
    [CloudCodeFunction("CreateClub")]
    public async Task<Dictionary<string, object>> CreateClub(IExecutionContext context, IGameApiClient gameApiClient, ChessClub club)
    {
        try
        {
            // Apply profanity filter
            if (!NameIsProfanitySafe(club.Name))
            {
                return new Dictionary<string, object> { { "success", false }, {"error", "Club name cannot contain profanity"} };
            }
            
            // Check for existing club with the same name
            var matchingClubs = await gameApiClient.CloudSaveData.QueryDefaultCustomDataAsync(context, context.ServiceToken,
                context.ProjectId, new QueryIndexBody(new List<FieldFilter>
                {
                    new FieldFilter("entityType", "club", FieldFilter.OpEnum.EQ, true),
                    new FieldFilter("country", club.Country, FieldFilter.OpEnum.EQ, true),
                    new FieldFilter("name", club.Name, FieldFilter.OpEnum.EQ, true)
                }));
            if (matchingClubs.Data.Results.Count > 0)
            {
                return new Dictionary<string, object> { { "success", false }, {"error", "Club with the same name already exists in the same country"} };
            }

            club.Id = Guid.NewGuid();
            var cloudSaveId = $"club_{club.Id}";
            await gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody("entityType", "club"),
                    new SetItemBody("id", club.Id),
                    new SetItemBody("name", club.Name),
                    new SetItemBody("members", new List<string>{context.PlayerId}),
                    new SetItemBody("memberCount", 1),
                    new SetItemBody("country", club.Country),
                    new SetItemBody("approvalRequired", club.ApprovalRequired)
                }));
            await gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new SetItemBody("admin", context.PlayerId));
            
            // Update creator's membership
            var existingMemberships = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                context.ServiceToken, context.ProjectId, context.PlayerId, new List<string> { "memberClubs", "adminClubs" });
            var memberClubs = GetCloudSaveValue<List<string>>(existingMemberships.Data.Results, "memberClubs");
            var adminClubs = GetCloudSaveValue<List<string>>(existingMemberships.Data.Results, "adminClubs");

            memberClubs.Value.Add(club.Id.ToString());
            adminClubs.Value.Add(club.Id.ToString());

            await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(memberClubs.Key, memberClubs.Value, memberClubs.WriteLock),
                    new SetItemBody(adminClubs.Key, adminClubs.Value, adminClubs.WriteLock)
                }));

            return new Dictionary<string, object> { {"success", true}, { "id", club.Id } };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }

    private bool NameIsProfanitySafe(string name)
    {
        return !name.ToLower().Contains("banasco");
    }
    
    [CloudCodeFunction("JoinClub")]
    public async Task<Dictionary<string, object>> JoinClub(IExecutionContext context, IGameApiClient gameApiClient, string id)
    {
        try
        {
            var cloudSaveId = $"club_{id}";
            var clubConfig = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new List<string> { "members", "memberCount", "approvalRequired" });
            
            var memberCount = GetCloudSaveValue<int>(clubConfig.Data.Results, "memberCount");
            if (memberCount.Value == 0)
            {
                return new Dictionary<string, object> { {"success", false}, {"error", "Club does not exist"} };
            }
            
            var members = GetCloudSaveValue<List<string>>(clubConfig.Data.Results, "members");
            if (members.Value.Contains(context.PlayerId))
            {
                return new Dictionary<string, object> { {"success", false}, {"error", "Player is already a member of the club"} };
            }
            
            var approvalRequired = GetCloudSaveValue<bool>(clubConfig.Data.Results, "approvalRequired");

            if (approvalRequired.Value)
            {
                var clubApprovals = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                    context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "pendingApprovals" });
                var pendingApprovals = GetCloudSaveValue<List<string>>(clubApprovals.Data.Results, "pendingApprovals");
                
                if (pendingApprovals.Value.Contains(context.PlayerId))
                {
                    return new Dictionary<string, object> { {"success", false}, {"error", "Player already has an approval pending"} };
                }
                
                pendingApprovals.Value.Add(context.PlayerId);
                // TODO: Handle conflict case where pendingApprovals is being updated by multiple game clients at the same time
                await gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken,
                    context.ProjectId, cloudSaveId,
                    new SetItemBody(pendingApprovals.Key, pendingApprovals.Value, pendingApprovals.WriteLock));
                return new Dictionary<string, object> { {"success", true}, {"status", "pending"} };
            }

            members.Value.Add(context.PlayerId);
            memberCount.Value += 1;
            // TODO: Handle conflict case where members list is being updated by multiple game clients at the same time
            await gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(members.Key, members.Value, members.WriteLock),
                    new SetItemBody(memberCount.Key, memberCount.Value, memberCount.WriteLock)
                }));
            
            // Update player's membership
            var existingMemberships = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                context.ServiceToken, context.ProjectId, context.PlayerId, new List<string> { "memberClubs" });
            var memberClubs = GetCloudSaveValue<List<string>>(existingMemberships.Data.Results, "memberClubs");

            memberClubs.Value.Add(id);
            await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                context.ProjectId, context.PlayerId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(memberClubs.Key, memberClubs.Value, memberClubs.WriteLock)
                }));
            
            return new Dictionary<string, object> { {"success", true}, {"status", "joined"} };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("ListMyClubs")]
    public async Task<Dictionary<string, object>> ListMyClubs(IExecutionContext context, IGameApiClient gameApiClient)
    {
        try
        {
            var cloudSaveResponse = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                context.ServiceToken, context.ProjectId, context.PlayerId, new List<string> { "memberClubs" });
            var memberClubIds = GetCloudSaveValue<List<string>>(cloudSaveResponse.Data.Results, "memberClubs");

            var memberClubs = memberClubIds.Value.Select(async id =>
            {
                var cloudSaveId = $"club_{id}";
                var clubCloudSaveResponse =
                    await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                        context.ProjectId, cloudSaveId, new List<string>{"id", "name", "memberCount", "country"});
                
                return new ChessClub
                {
                    Id = GetCloudSaveValue<Guid>(clubCloudSaveResponse.Data.Results, "id").Value,
                    Name = GetCloudSaveValue<string>(clubCloudSaveResponse.Data.Results, "name").Value,
                    MemberCount = GetCloudSaveValue<int>(clubCloudSaveResponse.Data.Results, "memberCount").Value,
                    Country = GetCloudSaveValue<string>(clubCloudSaveResponse.Data.Results, "country").Value,
                };
            }).Select(t => t.Result).ToList();
            
            return new Dictionary<string, object> { {"success", true}, {"clubs", memberClubs} };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("SearchClubs")]
    public async Task<Dictionary<string, object>> SearchClubs(IExecutionContext context, IGameApiClient gameApiClient)
    {
        try
        {
            var cloudSaveResponse = await gameApiClient.CloudSaveData.QueryDefaultCustomDataAsync(context,
                context.ServiceToken, context.ProjectId, new QueryIndexBody(
                    new List<FieldFilter>
                    {
                        new FieldFilter("entityType", "club", FieldFilter.OpEnum.EQ, true),
                        new FieldFilter("name", "", FieldFilter.OpEnum.GE, true)
                    }, new List<string>
                    {
                        "id", "name", "country", "memberCount"
                    }));
            var clubs = cloudSaveResponse.Data.Results.Select(r => new ChessClub
            {
                Id = GetCloudSaveValue<Guid>(r.Data, "id").Value,
                Name = GetCloudSaveValue<string>(r.Data, "name").Value,
                MemberCount = GetCloudSaveValue<int>(r.Data, "memberCount").Value,
                Country = GetCloudSaveValue<string>(r.Data, "country").Value,
            }).ToList();
            
            return new Dictionary<string, object> { {"success", true}, {"clubs", clubs} };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("LoadClub")]
    public async Task<Dictionary<string, object>> LoadClub(IExecutionContext context, IGameApiClient gameApiClient, Guid id)
    {
        try
        {
            var cloudSaveId = $"club_{id}";
            var clubConfig = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId);

            var club = new ChessClub
            {
                Id = GetCloudSaveValue<Guid>(clubConfig.Data.Results, "id").Value,
                Name = GetCloudSaveValue<string>(clubConfig.Data.Results, "name").Value,
                MemberCount = GetCloudSaveValue<int>(clubConfig.Data.Results, "memberCount").Value,
                Members = GetCloudSaveValue<List<string>>(clubConfig.Data.Results, "members").Value,
                Country = GetCloudSaveValue<string>(clubConfig.Data.Results, "country").Value,
                ApprovalRequired = GetCloudSaveValue<bool>(clubConfig.Data.Results, "approvalRequired").Value
            };

            club.MemberNames = club.Members.ToDictionary(m => m,
                m => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, m).Result.Data
                    .Name);
            
            var clubAdminResponse = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new List<string>{"admin"});
            club.AdminId = GetCloudSaveValue<string>(clubAdminResponse.Data.Results, "admin").Value;

            string myStatus;
            if (club.Members.Contains(context.PlayerId))
            {
                myStatus = "member";
            }
            else if (club.ApprovalRequired)
            {
                var clubApprovals = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                    context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "pendingApprovals" });
                var pendingApprovals = GetCloudSaveValue<List<string>>(clubApprovals.Data.Results, "pendingApprovals").Value;
                myStatus = pendingApprovals.Contains(context.PlayerId) ? "pending" : "none";
            }
            else
            {
                myStatus = "none";
            }
            
            return new Dictionary<string, object>
                { {"success", true}, { "club", club }, {"myStatus", myStatus} };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("LoadClubJoinRequests")]
    public async Task<Dictionary<string, object>> LoadClubJoinRequests(IExecutionContext context, IGameApiClient gameApiClient, Guid id)
    {
        try
        {
            var cloudSaveId = $"club_{id}";
            var clubApprovals = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "pendingApprovals" });
            var pendingApprovals = GetCloudSaveValue<List<string>>(clubApprovals.Data.Results, "pendingApprovals").Value;

            var pendingApprovalsWithNames = pendingApprovals.ToDictionary(pa => pa,
                pa => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, pa).Result.Data.Name);

            return new Dictionary<string, object>
                { {"success", true}, { "requests", pendingApprovalsWithNames } };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("AdmitClubMember")]
    public async Task<Dictionary<string, object>> AdmitClubMember(IExecutionContext context, IGameApiClient gameApiClient, Guid clubId, string memberId)
    {
        try
        {
            var cloudSaveId = $"club_{clubId}";
            
            var privateClubConfig = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "admin", "pendingApprovals" });
            var admin = GetCloudSaveValue<string>(privateClubConfig.Data.Results, "admin").Value;
            if (context.PlayerId != admin)
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Only the club admin can approve join requests" } };
            }

            var pendingApprovals = GetCloudSaveValue<List<string>>(privateClubConfig.Data.Results, "pendingApprovals");
            if (!pendingApprovals.Value.Contains(memberId))
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Provided player has not requested to join the club" } };
            }
            
            var clubConfig = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new List<string>{"members", "memberCount"});
            var members = GetCloudSaveValue<List<string>>(clubConfig.Data.Results, "members");
            var memberCount = GetCloudSaveValue<int>(clubConfig.Data.Results, "memberCount");
            
            if (members.Value.Contains(memberId))
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Provided player is already a member of the club" } };
            }

            pendingApprovals.Value.Remove(memberId);
            members.Value.Add(memberId);
            memberCount.Value++;

            await gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(members.Key, members.Value, members.WriteLock),
                    new SetItemBody(memberCount.Key, memberCount.Value, memberCount.WriteLock)
                }));
            await gameApiClient.CloudSaveData.SetPrivateCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(pendingApprovals.Key, pendingApprovals.Value, pendingApprovals.WriteLock),
                }));
            
            var memberClubResponse = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                context.ServiceToken, context.ProjectId, memberId, new List<string> { "memberClubs" });
            var memberClubs = GetCloudSaveValue<List<string>>(memberClubResponse.Data.Results, "memberClubs");
            memberClubs.Value.Add(clubId.ToString());
            await gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
                memberId,
                new SetItemBody(memberClubs.Key, memberClubs.Value, memberClubs.WriteLock));
            
            var pendingApprovalsWithNames = pendingApprovals.Value.ToDictionary(pa => pa,
                pa => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, pa).Result.Data.Name);
            var memberNames = members.Value.ToDictionary(m => m,
                m => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, m).Result.Data
                    .Name);

            return new Dictionary<string, object>
            {
                { "success", true }, { "requests", pendingApprovalsWithNames }, { "members", members.Value },
                { "memberCount", memberCount.Value }, { "memberNames", memberNames }
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { { "success", false }, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("KickClubMember")]
    public async Task<Dictionary<string, object>> KickClubMember(IExecutionContext context, IGameApiClient gameApiClient, Guid clubId, string memberId)
    {
        try
        {
            var cloudSaveId = $"club_{clubId}";
            
            var privateClubConfig = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "admin" });
            var admin = GetCloudSaveValue<string>(privateClubConfig.Data.Results, "admin").Value;
            if (context.PlayerId != admin)
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Only the club admin can kick members" } };
            }
            
            var clubConfig = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new List<string>{"members", "memberCount"});
            var members = GetCloudSaveValue<List<string>>(clubConfig.Data.Results, "members");
            var memberCount = GetCloudSaveValue<int>(clubConfig.Data.Results, "memberCount");
            
            if (!members.Value.Contains(memberId))
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Provided player is not a club member" } };
            }

            members.Value.Remove(memberId);
            memberCount.Value--;

            await gameApiClient.CloudSaveData.SetCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(members.Key, members.Value, members.WriteLock),
                    new SetItemBody(memberCount.Key, memberCount.Value, memberCount.WriteLock)
                }));
            
            var memberClubResponse = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                context.ServiceToken, context.ProjectId, memberId, new List<string> { "memberClubs" });
            var memberClubs = GetCloudSaveValue<List<string>>(memberClubResponse.Data.Results, "memberClubs");
            memberClubs.Value.Remove(clubId.ToString());
            await gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
                memberId,
                new SetItemBody(memberClubs.Key, memberClubs.Value, memberClubs.WriteLock));
            
            var memberNames = members.Value.ToDictionary(m => m,
                m => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, m).Result.Data
                    .Name);

            return new Dictionary<string, object>
            {
                { "success", true }, { "members", members.Value }, { "memberCount", memberCount.Value },
                { "memberNames", memberNames }
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("DenyClubMember")]
    public async Task<Dictionary<string, object>> DenyClubMember(IExecutionContext context, IGameApiClient gameApiClient, Guid clubId, string memberId)
    {
        try
        {
            var cloudSaveId = $"club_{clubId}";
            
            var privateClubConfig = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "admin", "pendingApprovals" });
            var admin = GetCloudSaveValue<string>(privateClubConfig.Data.Results, "admin").Value;
            if (context.PlayerId != admin)
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Only the club admin can approve join requests" } };
            }

            var pendingApprovals = GetCloudSaveValue<List<string>>(privateClubConfig.Data.Results, "pendingApprovals");
            if (!pendingApprovals.Value.Contains(memberId))
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Provided player has not requested to join the club" } };
            }

            pendingApprovals.Value.Remove(memberId);
            
            await gameApiClient.CloudSaveData.SetPrivateCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId, new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(pendingApprovals.Key, pendingApprovals.Value, pendingApprovals.WriteLock),
                }));
            
            var pendingApprovalsWithNames = pendingApprovals.Value.ToDictionary(pa => pa,
                pa => gameApiClient.PlayerNamesApi.GetNameAsync(context, context.ServiceToken, pa).Result.Data.Name);

            return new Dictionary<string, object>
            {
                { "success", true }, { "requests", pendingApprovalsWithNames }
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { { "success", false }, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [CloudCodeFunction("DeleteClub")]
    public async Task<Dictionary<string, object>> DeleteClub(IExecutionContext context, IGameApiClient gameApiClient, Guid id)
    {
        try
        {
            var cloudSaveId = $"club_{id}";
            
            var privateClubConfig = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context,
                context.ServiceToken, context.ProjectId, cloudSaveId, new List<string> { "admin" });
            var admin = GetCloudSaveValue<string>(privateClubConfig.Data.Results, "admin").Value;
            if (context.PlayerId != admin)
            {
                return new Dictionary<string, object>
                    { { "success", false }, { "error", "Only the club admin can delete the club" } };
            }
            
            var clubConfig = await gameApiClient.CloudSaveData.GetCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId, new List<string>{"members", "memberCount"});
            var members = GetCloudSaveValue<List<string>>(clubConfig.Data.Results, "members");

            foreach (var member in members.Value)
            {
                var memberClubResponse = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context,
                    context.ServiceToken, context.ProjectId, member, new List<string> { "memberClubs" });
                var memberClubs = GetCloudSaveValue<List<string>>(memberClubResponse.Data.Results, "memberClubs");
                memberClubs.Value.Remove(id.ToString());
                await gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
                    member,
                    new SetItemBody(memberClubs.Key, memberClubs.Value, memberClubs.WriteLock));
            }

            await gameApiClient.CloudSaveData.DeleteCustomItemsAsync(context, context.ServiceToken, context.ProjectId,
                cloudSaveId);
            await gameApiClient.CloudSaveData.DeletePrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, cloudSaveId);

            return new Dictionary<string, object>
            {
                { "success", true }
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
                { {"success", false}, { "error", exception.Message }, { "stackTrace", exception.StackTrace } };
        }
    }
    
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ChessClub
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<string> Members { get; set; }
        public Dictionary<string, string> MemberNames { get; set; }
        public int MemberCount { get; set; }
        public string Country { get; set; }
        public bool ApprovalRequired { get; set; }
        public string AdminId { get; set; }
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

    private class ConvertedCloudSaveItem<T>
    {
        public string Key { get; set; }
        public T Value { get; set; }
        public string WriteLock { get; set; }
    }

    private ConvertedCloudSaveItem<T> GetCloudSaveValue<T>(List<Item> response, string key)
    {
        var convertedItem = new ConvertedCloudSaveItem<T>
        {
            Key = key
        };
        
        var item = response.FirstOrDefault(r => r.Key == key);
        if (item == null)
        {
            var constructor = typeof(T).GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                convertedItem.Value = (T)constructor.Invoke(null);
            }
            else
            {
                convertedItem.Value = default;
            }
            convertedItem.WriteLock = "";
            return convertedItem;
        }

        convertedItem.WriteLock = item.WriteLock;

        if (item.Value is JContainer jCont)
        {
            convertedItem.Value = jCont.ToObject<T>();
        }
        else if (typeof(T) == typeof(Guid))
        {
            convertedItem.Value = (T)Convert.ChangeType(Guid.Parse((string)item.Value), typeof(T));
        }
        else
        {
            convertedItem.Value = (T)Convert.ChangeType(item.Value, typeof(T));
        }
        
        return convertedItem;
    }
}

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
    }
}