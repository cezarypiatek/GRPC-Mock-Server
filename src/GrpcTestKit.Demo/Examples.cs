using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcTestKit.TestConnectors;
using GrpcTestKit.TestConnectors.Docker;
using GrpcTestKit.TestConnectors.Kubernetes;
using My.Package;

namespace GrpcTestKit.Demo
{
    [Explicit]
    public class Examples
    {
        [Test]
        public async Task test_with_testcontainers()
        {
            using var activity = new Activity("test").Start();
            await using var connector = new TestContainerGrpcMockServerConnector("protos");

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
                request: new []
                {
                    new { name = "Hello streaming 1" },
                    new { name = "Hello streaming 2" }
                },
                response: new { message = "Hi there streaming client" }
            );


            grpcMockClient.Inspect();
        }

        [Test]
        public async Task test_with_testcharts()
        {
            await using var connector = new TestChartGrpcMockServerConnector("protos", grpcPort: 8889);

            var connectionInfo = await connector.Install();

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
                request: new []
                {
                    new { name = "Hello streaming 1" },
                    new { name = "Hello streaming 2" }
                },
                response: new { message = "Hi there streaming client" }
            );


            grpcMockClient.Inspect();
        }     
        
        [Test]
        public async Task test_with_inmemoryconnector()
        {
            await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);

            var connectionInfo = await connector.Install();

            var grpcMockClient = connector.CreateClient();

            // await grpcMockClient.MockRequestReply
            // (
            //     serviceName: "my.package.Sample",
            //     methodName: "TestRequestReply",
            //     request: new { name = "Hello 1" },
            //     response: new { message = "Hi there 1" }
            // );
            //
            // await grpcMockClient.MockRequestReply
            // (
            //     serviceName: "my.package.Sample",
            //     methodName: "TestRequestReply",
            //     request: new { name = "Hello 2" },
            //     response: new { message = "Hi there 2" }
            // );
            //
            // await grpcMockClient.MockServerStreaming
            // (
            //     serviceName: "my.package.Sample",
            //     methodName: "TestServerStreaming",
            //     request: new { name = "Hello streaming" },
            //     response: new[]
            //     {
            //         new {message = "Hi there 1"},
            //         new {message = "Hi there 2"},
            //         new {message = "Hi there 3"}
            //     }
            // );
            //
            // await grpcMockClient.MockClientStreaming
            // (
            //     serviceName: "my.package.Sample",
            //     methodName: "TestServerStreaming",
            //     request: new []
            //     {
            //         new { name = "Hello streaming 1" },
            //         new { name = "Hello streaming 2" }
            //     },
            //     response: new { message = "Hi there streaming client" }
            // );

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
            var channel = GrpcChannel.ForAddress(connectionInfo.GrpcEndpoint);
            var client = new Sample.SampleClient(channel);

           

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
            await call.RequestStream.WriteAsync(new HelloRequest
            {
                Name = "Ping 2"
            });

            await call.RequestStream.CompleteAsync();
            await call.ResponseStream.MoveNext();
            await call.ResponseStream.MoveNext();
            grpcMockClient.Inspect();
        }

        [Test]
        public async Task test_with_inmemoryconnector_with_mock_helper()
        {
            await using var connector = new InMemoryGrpcMockServerConnector(grpcPort:5033, wireMockPort: 9594);
                
            _ = await connector.Install();

            var grpcMockClient = connector.CreateClient();
            
            var mockHelper = new SampleMockHelper(grpcMockClient);

            _ = await mockHelper.MockTestRequestReply
            (
                request: new HelloRequest {Name = "Hello 1"},
                response: new HelloReply {Message = "Hi there 1"}
            );

            _ = await mockHelper.MockTestServerStreaming
            (
                request: new HelloRequest {Name = "Hello streaming"},
                response: new[]
                {
                    new HelloReply {Message = "Hi there 1"},
                    new HelloReply {Message = "Hi there 2"},
                    new HelloReply {Message = "Hi there 2"},
                }
            );

            _ = await mockHelper.MockTestClientStreaming
            (
                request: new []
                {
                    new HelloRequest {Name = "Hello streaming 1"},
                    new HelloRequest {Name = "Hello streaming 2"},
                },
                response: new HelloReply
                {
                    Message = "Hi there streaming client"
                }
            );

            grpcMockClient.Inspect();
        }
    }

    [GrpcMockServerFor(typeof(Sample.SampleBase))]
    public partial class InMemoryGrpcMockServerConnector
    {
        
    }

    [GrpcMockHelperFor(typeof(Sample.SampleBase))]
    public partial class SampleMockHelper
    {

    }

   
}