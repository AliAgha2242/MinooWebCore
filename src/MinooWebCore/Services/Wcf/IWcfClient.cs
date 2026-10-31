using System.ServiceModel.Channels;

namespace MinooWebCore.Services.Wcf;

/// <summary>
/// Invokes WCF operations using an endpoint URL supplied by the caller.
/// </summary>
public interface IWcfClient
{
    TResult Invoke<TContract, TResult>(
        string endpointUrl,
        Func<TContract, TResult> operation,
        Binding? binding = null)
        where TContract : class;

    Task<TResult> InvokeAsync<TContract, TResult>(
        string endpointUrl,
        Func<TContract, Task<TResult>> operation,
        Binding? binding = null)
        where TContract : class;
}
