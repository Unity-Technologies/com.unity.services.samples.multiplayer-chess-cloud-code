# Multiplayer Chess Sample

A sample project that demonstrates how to implement a server authoritative asynchronous multiplayer game using [Unity Gaming Services](https://unity.com/solutions/gaming-services), without needing a dedicated game server.

## Setup

For this sample to work, you first need to publish your Cloud Code module and Leaderboard via the UGS CLI. Remember to [install and configure](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/) the CLI first. This requires a project ID and a service account key, both of which can be created and found in the [Unity Dashboard](https://dashboard.unity.com).

Once the CLI is set up, the module and leaderboard can be deployed with the following commands (or by running the `deploy.sh` script):

```
ugs deploy ChessCloudCode/ChessCloudCode.sln
ugs deploy EloRatings.lb
```

To run the chess sample, import the Chess folder as a Unity project, open and run the `ChessDemo.unity` scene. To run another game client locally to play against, go to `File -> Build and Run`. Then you can create a game in one client, and join it in the other using the generated code shown in the top right of the game window.

To limit access to specific Cloud Code endpoints from authenticated players (i.e. the game client), have a look at the [Access Control](https://docs.unity.com/ugs-overview/en/manual/access-control) documentation.

## Credits

This project uses the [Free Low Poly Chess Set](https://assetstore.unity.com/packages/3d/props/free-low-poly-chess-set-116856) asset for the board and chess pieces, and the [Gera Chess Library](https://github.com/Geras1mleo/Chess) for validating the moves made by players.

See [Third Party Notices](Third%20Party%20Notices.md) for more information.
