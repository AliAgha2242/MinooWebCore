using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
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
    /// The target service is configured with wsHttpBinding. This binding uses
    /// SOAP 1.2 with WS-Addressing; a SOAP 1.2 CustomBinding without addressing
    /// is not wire-compatible with that endpoint.
    /// </summary>
    private static Binding CreateDefaultBinding(Uri endpoint)
    {
        if (!endpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The WCF endpoint scheme '{endpoint.Scheme}' is not supported by WSHttpBinding.");
        }

        var binding = new WSHttpBinding(SecurityMode.None)
        {
            CloseTimeout = TimeSpan.FromMinutes(1),
            OpenTimeout = TimeSpan.FromMinutes(1),
            ReceiveTimeout = TimeSpan.FromHours(2),
            SendTimeout = TimeSpan.FromMinutes(15),
            BypassProxyOnLocal = true,
            TransactionFlow = true,
            HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
            MaxBufferPoolSize = 99_999_999,
            MaxReceivedMessageSize = 99_999_999,
            MessageEncoding = WSMessageEncoding.Text,
            TextEncoding = Encoding.UTF8,
            UseDefaultWebProxy = true,
            AllowCookies = false
        };

        binding.ReaderQuotas.MaxDepth = 99;
        binding.ReaderQuotas.MaxStringContentLength = 99_999_999;
        binding.ReaderQuotas.MaxArrayLength = 99_999_999;
        binding.ReaderQuotas.MaxBytesPerRead = 99_999_999;
        binding.ReaderQuotas.MaxNameTableCharCount = 99_999_999;

        binding.ReliableSession.Ordered = true;
        binding.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(15);
        binding.ReliableSession.Enabled = false;

        return binding;
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
