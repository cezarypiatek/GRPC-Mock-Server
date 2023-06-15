using Grpc.Core;
using Grpc.Net.Client;
using GrpcTestKit.TestConnectors;
using My.Package;

namespace GrpcTestKit.Demo;


public class InMemoryConnectorTests
{
    [Test]
    public async Task should_mock_unary_method_manually()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        // Define mock for unary method
        await grpcMockClient.MockRequestReply
        (
            serviceName: "my.package.Sample",
            methodName: "TestRequestReply",
            request: new { name = "Hello 1" },
            response: new { message = "Hi there 1" }
        );
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var response  = client.TestRequestReply(new HelloRequest { Name = "Hello 1"});
        
        Assert.That(response.Message, Is.EqualTo("Hi there 1"));
    }
    
    [Test]
    public async Task should_mock_unary_method_with_generated_helper()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();
    
        //Create mock helper
        var mockHelper = new SampleMockHelper(grpcMockClient);
        
        // Define mock for unary method
        _ = await mockHelper.MockTestRequestReply
        (
            request: new HelloRequest {Name = "Hello 1"},
            response: new HelloReply {Message = "Hi there 1"}
        );
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var response  = client.TestRequestReply(new HelloRequest { Name = "Hello 1"});
        
        Assert.That(response.Message, Is.EqualTo("Hi there 1"));
    }
    
    [Test]
    public async Task should_mock_server_streaming_manually()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        // Define mock for server streaming
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
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var call = client.TestServerStreaming(new HelloRequest {Name =  "Hello streaming"});
      
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 1"));
        
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 2"));
        
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 3"));
    }
    
    [Test]
    public async Task should_mock_server_streaming_with_generated_helper()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        //Create mock helper
        var mockHelper = new SampleMockHelper(grpcMockClient);
        
        // Define mock for server streaming
        await mockHelper.MockTestServerStreaming
        (
            request: new HelloRequest
            {
                Name = "Hello streaming"
            },
            response: new HelloReply[]
            {
                new() {Message = "Hi there 1"},
                new() {Message = "Hi there 2"},
                new() {Message = "Hi there 3"}
            }
        );
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var call = client.TestServerStreaming(new HelloRequest {Name =  "Hello streaming"});
      
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 1"));
        
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 2"));
        
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Hi there 3"));
    }
    
    [Test]
    public async Task should_mock_client_streaming_manually()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        // Define mock for client streaming
        await grpcMockClient.MockClientStreaming
        (
            serviceName: "my.package.Sample",
            methodName: "TestClientStreaming",
            request: new []
            {
                new { name = "Hello streaming 1" },
                new { name = "Hello streaming 2" }
            },
            response: new { message = "Hi there streaming client" }
        );
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with client streaming
        var call = client.TestClientStreaming();
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Hello streaming 1"
        });
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Hello streaming 2"
        });
        await call.RequestStream.CompleteAsync();
        
        var response = await call;
        Assert.That(response.Message, Is.EqualTo("Hi there streaming client"));
    }
    
    [Test]
    public async Task should_mock_client_streaming_with_generated_helper()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        //Create mock helper
        var mockHelper = new SampleMockHelper(grpcMockClient);
        
        // Define mock for client streaming
        await mockHelper.MockTestClientStreaming
        (
            request: new HelloRequest[]
            {
                new() {Name = "Hello streaming 1"},
                new() {Name = "Hello streaming 2"}
            },
            response: new HelloReply
            {
                Message = "Hi there streaming client"
            }
        );
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with client streaming
        var call = client.TestClientStreaming();
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Hello streaming 1"
        });
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Hello streaming 2"
        });
        await call.RequestStream.CompleteAsync();
        
        var response = await call;
        Assert.That(response.Message, Is.EqualTo("Hi there streaming client"));
    }
    
    [Test]
    public async Task should_mock_duplex_streaming_manually()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        // Define mock for duplex streaming
        await grpcMockClient.MockDuplexStreaming(
            serviceName: "my.package.Sample",
            methodName: "TestClientServerStreaming", 
            scenario: new MessageExchange[]
            {
                new ()
                {
                    Requests = new[]
                    {
                        new {name = "Ping 1a"},
                        new {name = "Ping 1b"}
                    },
                    Responses = new[]
                    {
                        new {message = "Pong 1"}
                    }
                },
                new ()
                {
                    Requests = new[]
                    {
                        new {name = "Ping 2"},
                    },
                    Responses = new[]
                    {
                        new {message = "Pong 2a"},
                        new {message = "Pong 2b"}
                    }
                },
            });
        
        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var call = client.TestClientServerStreaming();
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 1a"
        });
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 1b"
        });
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 1"));
        
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 2"
        });

        await call.RequestStream.CompleteAsync();
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 2a"));
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 2b"));
    }
    
    [Test]
    public async Task should_mock_duplex_streaming_with_generated_helper()
    {
        //Create connector
        await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

        //Setup GrpcMockServer
        var connectionInfo = await connector.Install();

        //Create mocking client
        var grpcMockClient = connector.CreateClient();

        //Create mock helper
        var mockHelper = new SampleMockHelper(grpcMockClient);
        
        // Define mock for duplex streaming
        await mockHelper.MockTestClientServerStreaming(new MessageExchange<HelloRequest, HelloReply>[]
        {
            new()
            {
                Requests = new HelloRequest[]
                {
                    new() {Name = "Ping 1a"},
                    new() {Name = "Ping 1b"}
                },
                Responses = new HelloReply[]
                {
                    new() {Message = "Pong 1"}
                }
            },
            new()
            {
                Requests = new HelloRequest[]
                {
                    new() {Name = "Ping 2"},
                },
                Responses = new HelloReply[]
                {
                    new() {Message = "Pong 2a"},
                    new() {Message = "Pong 2b"}
                }
            },
        });

        //Create GRPC client
        var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
        var client = new Sample.SampleClient(channel);
        
        //Execute method with duplex streaming
        var call = client.TestClientServerStreaming();
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 1a"
        });
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 1b"
        });
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 1"));
        
        await call.RequestStream.WriteAsync(new HelloRequest
        {
            Name = "Ping 2"
        });

        await call.RequestStream.CompleteAsync();
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 2a"));
        await call.ResponseStream.MoveNext();
        Assert.That(call.ResponseStream.Current.Message, Is.EqualTo("Pong 2b"));
    }
}