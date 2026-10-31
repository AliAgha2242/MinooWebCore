using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

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

    /// <summary>
    /// The SanadMali endpoint publishes a SOAP 1.2 binding over HTTP.
    /// BasicHttpBinding sends SOAP 1.1 (text/xml), which is incompatible
    /// with this endpoint. Use SOAP 1.2 without WS-Addressing.
    /// </summary>
    private static Binding CreateDefaultBinding(Uri endpoint)
    {
        var encoding = new TextMessageEncodingBindingElement(
            MessageVersion.Soap12,
            Encoding.UTF8)
        {
            ReaderQuotas = XmlDictionaryReaderQuotas.Max
        };

        TransportBindingElement transport = endpoint.Scheme.Equals(
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase)
                ? new HttpsTransportBindingElement()
                : new HttpTransportBindingElement();

        transport.MaxReceivedMessageSize = 10 * 1024 * 1024;

        if (transport is HttpTransportBindingElement httpTransport)
            httpTransport.MaxBufferSize = 10 * 1024 * 1024;

        if (transport is HttpsTransportBindingElement httpsTransport)
            httpsTransport.MaxBufferSize = 10 * 1024 * 1024;

        return new CustomBinding(encoding, transport)
        {
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
