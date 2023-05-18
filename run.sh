#!/bin/bash
dotnet build --no-restore --nologo
dotnet /src/GrpcMockServer/bin/Debug/GrpcMockServer.dll