#!/bin/bash
echo "$(date +%c) Start server setup"
dotnet build ./GrpcMockServer/GrpcMockServer.csproj --no-restore --nologo /p:RunAnalyzers=false /p:FastTrack=true
echo "$(date +%c) Starting server"
dotnet /src/GrpcMockServer/bin/Debug/GrpcMockServer.dll