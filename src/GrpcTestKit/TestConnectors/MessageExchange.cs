namespace GrpcTestKit.TestConnectors;

public class MessageExchange:MessageExchange<object,object>
{
}
public class MessageExchange<TRequest,TResponse>
{
    public IReadOnlyList<TRequest> Requests { get; set; }
    public IReadOnlyList<TResponse>? Responses { get; set; }

    public void Deconstruct(out IReadOnlyList<TRequest> requests, out IReadOnlyList<TResponse>? responses)
    {
        requests = Requests;
        responses = Responses;
    }
}