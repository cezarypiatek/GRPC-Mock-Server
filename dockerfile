FROM mcr.microsoft.com/dotnet/sdk:7.0
ADD src /src
ADD run.sh /src/run.sh
WORKDIR /src
# Pre-build project to cache nugets inside the container
RUN dotnet build
# GRPC endpoint
EXPOSE 5033
# WireMock endpoint
EXPOSE 9095
RUN mkdir /protos
ENV ProtoRoot=/protos
ENTRYPOINT ["/src/run.sh"]