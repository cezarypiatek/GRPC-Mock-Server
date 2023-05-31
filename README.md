# GRPC-Mock-Server
Super fast, platform independent, standalone component for mocking GRPC services using [WireMock.NET](https://github.com/WireMock-Net/WireMock.Net) stubbing engine


## How to run GRPC-Mock-Server

### Option 1: Running docker container manually

```
docker run -it -p 5033:5033 -p 9095:9095 -v $(pwd)/protos:/protos cezarypiatek/grpc-mock-server
```

Ports:
- 5033 for GRPC
- 9095 for Stubbing (WireMock API)

### Option 2: Using TestContainerGrpcMockServerConnector

`TestContainerGrpcMockServerConnector` uses [Testcontainers for .NET](https://dotnet.testcontainers.org/) to spin docker container directly from the C# code

```cs
await using var connector = new TestContainerGrpcMockServerConnector(protoDirectory: "protos", grpcPort:5033);

await connector.Install();
```

### Option 3: Using TestChartGrpcMockServerConnector

`TestChartGrpcMockServerConnector` uses [SmoothSailing](https://github.com/cezarypiatek/SmoothSailing) to deploy GRPC-Mock-Server into Kubernetes cluster directly from the C# code

```cs
await using var connector = new TestChartGrpcMockServerConnector(protoDirectory: "protos", grpcPort:5033);

await connector.Install();
```



## How to prepare mocks

```cs
await using var connector = new TestContainerGrpcMockServerConnector( protoDirectory: "protos", grpcPort:5033);

await connector.Install();

var grpcMockClient = connector.CreateClient();

await grpcMockClient.MockRequestReply
(
    serviceName: "my.package.Sample",
    methodName: "TestRequestReply",
    request: new { name = "Hello 1" },
    response: new { message = "Hi there 1" }
);

await grpcMockClient.MockRequestReply
(
    serviceName: "my.package.Sample",
    methodName: "TestRequestReply",
    request: new { name = "Hello 2" },
    response: new { message = "Hi there 2" }
);

await grpcMockClient.MockServerStreaming
(
    serviceName: "my.package.Sample",
    methodName: "TestServerStreaming",
    request: new { name = "Hello streaming" },
    response: new[]
    {
        new {message = "Hi there 1"},
        new {message = "Hi there 2"},
        new {message = "Hi there 3"}
    }
);

await grpcMockClient.MockClientStreaming
(
    serviceName: "my.package.Sample",
    methodName: "TestServerStreaming",
    requests: new []
    {
        new { name = "Hello streaming 1" },
        new { name = "Hello streaming 2" }
    },
    response: new { message = "Hi there streaming client" }
);
```

## How does it work

GRPC-Mock-Server works in the following way:
- compile provided `*.proto` files
- generate proxy for every service and method defined in the `*.proto` files
- use the generated proxy to translate GRPC calls to REST and forward it to `WireMock` backend

## Supported GRPC communication patterns

|Pattern|Implementation status|
|---|----|
|request-reply|✅|
|server-streaming|✅|
|client-streaming|✅|
|client-server-streaming|❌|


## TODO
- [ ] Implement error response codes
- [x] Implement library that wraps WireMock API for stubbing
- [x] Implement test container

## Alternatives
- https://github.com/Adven27/grpc-wiremock
- https://github.com/tokopedia/gripmock
