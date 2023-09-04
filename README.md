# Multiplayer Chess Sample

A sample project that demonstrates how to implement a server authoritative multiplayer game using [Unity Gaming Services](https://unity.com/solutions/gaming-services).

## Setup

For this sample to work, you would first need to publish, zip and deploy your Cloud Code module via the UGS CLI. Remember to [install and configure](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/) the CLI first. This requires a project ID and a service account key, both of which can be created and found in the [Unity Dashboard](https://dashboard.unity.com).

Once the CLI is set up, open the `ChessCloudCode/ChessCloudCode.sln` solution in Rider or Visual studio. Publish the solution:

```
dotnet publish -c Release -r linux-x64 -p:PublishReadyToRun=true
```

Package the solution in a zip file (note the `.ccm` extension), e.g.:

```
zip -r ChessCloudCode.ccm ~/ChessCloudCode/bin/Release/net7.0/linux-x64/publish/*
```

Deploy the zip file using the UGS CLI:
```
ugs deploy ChessCloudCode.ccm
```

To run the chess sample, import the Chess folder as a Unity project and open the `ChessDemo.unity` scene. Then run the scene and you should be able to play chess against yourself. To run another game client with a different player ID, go to `File -> Build and Run`.

To call the clear board endpoint, use a [service account's key credentials](https://services.docs.unity.com/docs/service-account-auth/#service-accounts) and exchange that for a [stateless token](https://services.docs.unity.com/docs/service-account-auth/#using-stateless-tokens).

First encode the secret in base64
```
echo -n "<KEY_ID>:<SECRET>" | base64
```
Then exchange those credentials for a token
```
curl -X POST -H "Authorization: Basic <ENCODED_SECRET>"  'https://services.api.unity.com/auth/v1/token-exchange?projectId=<PROJECT_ID>&environmentId=<ENVIRONMENT_ID>
```
Then call the clear board endpoint
```
curl --request POST 'https://cloud-code.services.api.unity.com/v1/projects/<PROJECT_ID>/modules/ChessCloudCode/ClearBoard' \
--header 'Content-Type: application/json' \
--header 'Authorization: Bearer <TOKEN>' \
--data-raw '{"params": {"session":"demo-session"}}'
```

To limit access to specific Cloud Code endpoints from authenticated players (i.e. the game client), have a look at the [Access Control](https://docs.unity.com/ugs-overview/en/manual/access-control) documentation.

## Credits

This project uses the [Free Low Poly Chess Set](https://assetstore.unity.com/packages/3d/props/free-low-poly-chess-set-116856) asset for the board and chess pieces, and the [Gera Chess Library](https://github.com/Geras1mleo/Chess) for validating the moves made by players.
