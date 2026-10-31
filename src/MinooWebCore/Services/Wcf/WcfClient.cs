using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace MinooWebCore.Services.Wcf;

/// <summary>
/// Creates a WCF channel for each call, invokes the requested operation,
/// and closes or aborts the channel safely.
/// </summary>
public sealed class WcfClient(ILogger<WcfClient>? logger = null) : IWcfClient
{
    public TResult Invoke<TContract, TResult>(
        string endpointUrl,
        Func<TContract, TResult> operation,
        Binding? binding = null)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(operation);

        ChannelFactory<TContract>? factory = null;
        TContract? proxy = null;
        var stage = "CreateFactory";
        var stopwatch = Stopwatch.StartNew();

        logger?.LogInformation(
            "Starting synchronous WCF call. Contract: {Contract}; Endpoint: {Endpoint}",
            typeof(TContract).FullName,
            endpointUrl);

        try
        {
            factory = CreateFactory<TContract>(endpointUrl, binding);

            stage = "CreateChannel";
            proxy = factory.CreateChannel();
            logger?.LogDebug("WCF channel created for {Contract}.", typeof(TContract).FullName);

            stage = "InvokeOperation";
            logger?.LogInformation("Invoking synchronous WCF operation for {Contract}.", typeof(TContract).FullName);
            var result = operation(proxy);
            logger?.LogInformation(
                "WCF operation completed for {Contract} in {ElapsedMilliseconds} ms.",
                typeof(TContract).FullName,
                stopwatch.ElapsedMilliseconds);

            stage = "CloseChannel";
            CloseCommunicationObject(proxy);

            stage = "CloseFactory";
            CloseCommunicationObject(factory);

            logger?.LogDebug("WCF call completed successfully for {Contract}.", typeof(TContract).FullName);
            return result;
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "WCF call failed at stage {Stage}. Contract: {Contract}; Endpoint: {Endpoint}; Elapsed: {ElapsedMilliseconds} ms.",
                stage,
                typeof(TContract).FullName,
                endpointUrl,
                stopwatch.ElapsedMilliseconds);

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

        ChannelFactory<TContract>? factory = null;
        TContract? proxy = null;
        var stage = "CreateFactory";
        var stopwatch = Stopwatch.StartNew();

        logger?.LogInformation(
            "Starting asynchronous WCF call. Contract: {Contract}; Endpoint: {Endpoint}",
            typeof(TContract).FullName,
            endpointUrl);

        try
        {
            factory = CreateFactory<TContract>(endpointUrl, binding);

            stage = "CreateChannel";
            proxy = factory.CreateChannel();
            logger?.LogDebug("WCF channel created for {Contract}.", typeof(TContract).FullName);

            stage = "InvokeOperation";
            logger?.LogInformation("Invoking asynchronous WCF operation for {Contract}.", typeof(TContract).FullName);
            var result = await operation(proxy).ConfigureAwait(false);
            logger?.LogInformation(
                "WCF operation completed for {Contract} in {ElapsedMilliseconds} ms.",
                typeof(TContract).FullName,
                stopwatch.ElapsedMilliseconds);

            stage = "CloseChannel";
            CloseCommunicationObject(proxy);

            stage = "CloseFactory";
            CloseCommunicationObject(factory);

            logger?.LogDebug("WCF call completed successfully for {Contract}.", typeof(TContract).FullName);
            return result;
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "WCF call failed at stage {Stage}. Contract: {Contract}; Endpoint: {Endpoint}; Elapsed: {ElapsedMilliseconds} ms.",
                stage,
                typeof(TContract).FullName,
                endpointUrl,
                stopwatch.ElapsedMilliseconds);

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

        return new CustomBinding(encoding, transport)
        {
            // TimeSpan.MaxValue disables the WCF client-side operation time limits.
            // Calls can remain pending indefinitely if the remote service never responds.
            OpenTimeout = TimeSpan.MaxValue,
            CloseTimeout = TimeSpan.MaxValue,
            SendTimeout = TimeSpan.MaxValue,
            ReceiveTimeout = TimeSpan.MaxValue
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
