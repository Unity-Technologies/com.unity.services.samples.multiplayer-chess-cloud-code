#! /bin/bash
ugs deploy ChessCloudCode/ChessCloudCode.sln --services cloud-code-modules
ugs deploy EloRatings.lb --services leaderboards
