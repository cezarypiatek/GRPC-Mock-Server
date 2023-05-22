namespace GrpcTestKit.Demo
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task Test1()
        {

            await using var connector = new TestContainerGrpcMockServerConnector("protos");

            await connector.Install();

            var grpcMockClient = connector.CreateClient();

            //await connector.MockEndpoint(modelBuilder =>
            //{
            //    modelBuilder.WithRequest(x =>
            //        x.UsingPost()
            //         .WithPath("/my.package.Sample/TestRequestReply")
            //    ).WithResponse((x) => 
            //        x.WithStatusCode(200)
            //         .WithBodyAsJson(new
            //        {
            //            message = " Hello from tests"
            //        }));
            //});

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