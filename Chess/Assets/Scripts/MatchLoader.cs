using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

public static class MatchLoader
{
    public static async Task<Match> LoadMatchDataAsync(string matchId)
    {
        var keys = new HashSet<string>
        {
            "board",
            "whitePlayerId",
            "blackPlayerId",
            "turnCounter",
            "matchState",
            "matchCreatedAt"
        };

        try
        {
            var data = await CloudSaveService.Instance.Data.Custom.LoadAsync(matchId, keys);
            var match = MapToMatch(data);
            return match;
        }
        catch (CloudSaveValidationException ex)
        {
            Console.WriteLine($"CloudSaveValidationException: {ex.Message}");
            throw;
        }
        catch (CloudSaveRateLimitedException ex)
        {
            Console.WriteLine($"CloudSaveRateLimitedException: {ex.Message}");
            throw;
        }
        catch (CloudSaveException ex)
        {
            Console.WriteLine($"CloudSaveException: {ex.Reason}, {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            throw;
        }
    }

    private static Match MapToMatch(Dictionary<string, Item> data)
    {
        return new Match(
            data["board"].Value.GetAsString(),
            data["whitePlayerId"].Value.GetAsString(),
            data["blackPlayerId"].Value.GetAsString(),
            data["turnCounter"].Value.GetAs<int>(),
            data["matchState"].Value.GetAsString()
        );
    }
}