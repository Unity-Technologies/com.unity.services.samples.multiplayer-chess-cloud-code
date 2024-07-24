using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Model;

namespace ChessCloudCode
{
    public static class Leaderboards
    {
        private const string LeaderboardId = "EloRatings";
        private const int StartingElo = 1500;

        public static async Task<LeaderboardEntryWithUpdatedTime> GetLeaderboardEntry(IExecutionContext context,
            IGameApiClient gameApiClient, ILogger logger)
        {
            try
            {
                return await FetchLeaderboardEntry(context, gameApiClient);
            }
            catch (ApiException e) when (IsEntryNotFoundError(e, logger))
            {
                Helpers.LogException(logger, e, "Unable to find players elo rating, setting up initial rating");
                return await CreateDefaultEntry(context, gameApiClient);
            }
        }

        public static async Task<LeaderboardEntryWithUpdatedTime> FetchLeaderboardEntry(IExecutionContext context,
            IGameApiClient gameApiClient)
        {
            var response = await gameApiClient.Leaderboards.GetLeaderboardPlayerScoreAsync(
                context,
                context.ServiceToken,
                new Guid(context.ProjectId), LeaderboardId,
                context.PlayerId
            );

            return response.Data;
        }

        public static bool IsEntryNotFoundError(ApiException e, ILogger logger)
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

        public static async Task<LeaderboardEntryWithUpdatedTime> CreateDefaultEntry(IExecutionContext context,
            IGameApiClient gameApiClient)
        {
            var response = await gameApiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                context,
                context.ServiceToken,
                new Guid(context.ProjectId), LeaderboardId,
                context.PlayerId,
                new AddLeaderboardScore(StartingElo)
            );

            return response.Data;
        }
    }
}