using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Api;
using Unity.Services.Leaderboards.Model;

namespace ChessCloudCode
{
    public static class Leaderboards
    {
        private const string LeaderboardId = "EloRatings";
        private const int StartingElo = 1500;

        public static async Task<LeaderboardEntryWithUpdatedTime> GetOrInitLeaderboardEntry(IExecutionContext context,
            ILeaderboardsApi apiClient, ILogger logger, string playerId)
        {
            try
            {
                var response = await apiClient.GetLeaderboardPlayerScoreAsync(
                    context,
                    context.ServiceToken,
                    new Guid(context.ProjectId), LeaderboardId,
                    playerId
                );
                
                return response.Data;
            }
            catch (ApiException e) when (IsEntryNotFoundError(e, logger))
            {
                Helpers.LogException(logger, e, "Unable to find players elo rating, setting up initial rating");
                return await CreateDefaultEntry(context, apiClient, playerId);
            }
        }

        private static async Task<LeaderboardEntryWithUpdatedTime> CreateDefaultEntry(IExecutionContext context,
            ILeaderboardsApi apiClient, string playerId)
        {
            var response = await apiClient.AddLeaderboardPlayerScoreAsync(
                context,
                context.ServiceToken,
                new Guid(context.ProjectId), LeaderboardId,
                playerId,
                new AddLeaderboardScore(StartingElo)
            );

            return response.Data;
        }

        private static bool IsEntryNotFoundError(ApiException e, ILogger logger)
        {
            try
            {
                var problemDetails = JsonConvert.DeserializeObject<ProblemDetails>(e.Response.RawContent);
                return problemDetails?.Code == 27009;
            }
            catch (Exception exception)
            {
                Helpers.LogException(logger, exception, "Unable to deserialise error response");
                throw;
            }
        }
    }
}