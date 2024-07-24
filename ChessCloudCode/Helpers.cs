using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Shared;

namespace ChessCloudCode;

public class Helpers
{
    public static void LogException(ILogger logger, Exception e, string message)
    {
        if (e is ApiException apiException)
        {
            logger.LogError("{Message}. ApiException: {Exception}, Type: {Type}, Content: {Response}",
                message, apiException, apiException.Type, apiException.Response.RawContent);
        }
        else
        {
            logger.LogError("{Message}. Exception: {Exception}", message, e);
        }
    }
}