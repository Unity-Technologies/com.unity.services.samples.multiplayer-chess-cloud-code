using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace ChessCloudCode;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddSingleton<IPushClient, PushClient>(_ => PushClient.Create());
        config.Dependencies.AddSingleton(new Random());
    }
}