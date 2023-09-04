#! /bin/bash
dotnet publish -c Release -r linux-x64 -p:PublishReadyToRun=true
currentDir=${PWD##*/}
zip -r "${currentDir}".ccm bin/Release/net7.0/linux-x64/publish/*
ugs deploy "${currentDir}".ccm
