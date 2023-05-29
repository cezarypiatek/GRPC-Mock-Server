using System.Diagnostics;
using GrpcTestKit.TestConnectors;
using GrpcTestKit.TestConnectors.Docker;
using GrpcTestKit.TestConnectors.Kubernetes;

namespace GrpcTestKit.Demo
{
    public class Tests
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
                requests: new []
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

            await using var connector = new TestChartGrpcMockServerConnector("protos");

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


            grpcMockClient.Inspect();
        }
    }
}