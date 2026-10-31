using System.ServiceModel;
using System.ServiceModel.Channels;

namespace MinooWebCore.Services.Wcf;

/// <summary>
/// Creates a WCF channel for each call, invokes the requested operation,
/// and closes or aborts the channel safely.
/// </summary>
public sealed class WcfClient : IWcfClient
{
    public TResult Invoke<TContract, TResult>(
        string endpointUrl,
        Func<TContract, TResult> operation,
        Binding? binding = null)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(operation);

        var factory = CreateFactory<TContract>(endpointUrl, binding);
        TContract? proxy = null;

        try
        {
            proxy = factory.CreateChannel();
            var result = operation(proxy);
            CloseCommunicationObject(proxy);
            CloseCommunicationObject(factory);
            return result;
        }
        catch
        {
            AbortCommunicationObject(proxy);
            AbortCommunicationObject(factory);
            throw;
        }
    }

    public async Task<TResult> InvokeAsync<TContract, TResult>(
        string endpointUrl,
        Func<TContract, Task<TResult>> operation,
        Binding? binding = null)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(operation);

        var factory = CreateFactory<TContract>(endpointUrl, binding);
        TContract? proxy = null;

        try
        {
            proxy = factory.CreateChannel();
            var result = await operation(proxy).ConfigureAwait(false);
            CloseCommunicationObject(proxy);
            CloseCommunicationObject(factory);
            return result;
        }
        catch
        {
            AbortCommunicationObject(proxy);
            AbortCommunicationObject(factory);
            throw;
        }
    }

    private static ChannelFactory<TContract> CreateFactory<TContract>(
        string endpointUrl,
        Binding? binding)
        where TContract : class
    {
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpoint))
            throw new ArgumentException("The WCF endpoint URL must be an absolute URL.", nameof(endpointUrl));

        binding ??= CreateDefaultBinding(endpoint);
        return new ChannelFactory<TContract>(binding, new EndpointAddress(endpoint));
    }

    private static Binding CreateDefaultBinding(Uri endpoint)
    {
        var securityMode = endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? BasicHttpSecurityMode.Transport
            : BasicHttpSecurityMode.None;

        return new BasicHttpBinding(securityMode)
        {
            MaxReceivedMessageSize = 10 * 1024 * 1024,
            OpenTimeout = TimeSpan.FromSeconds(30),
            CloseTimeout = TimeSpan.FromSeconds(30),
            SendTimeout = TimeSpan.FromMinutes(2),
            ReceiveTimeout = TimeSpan.FromMinutes(2)
        };
    }

    private static void CloseCommunicationObject(object? value)
    {
        if (value is ICommunicationObject communicationObject)
            communicationObject.Close();
    }

    private static void AbortCommunicationObject(object? value)
    {
        if (value is ICommunicationObject communicationObject)
            communicationObject.Abort();
    }
}
