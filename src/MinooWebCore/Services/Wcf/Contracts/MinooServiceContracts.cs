using Polenter.Serialization;
using Polenter.Serialization.Advanced.Serializing;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;


namespace MinooWebCore.Services.Wcf.Contracts;

internal static class MinooServiceContractNamespace
{
    public const string Value = "http://Minoo.Services";
}

[DataContract(Name = "ClientInfo", Namespace = MinooServiceContractNamespace.Value)]
public class ClientInfo
{
    public ClientInfo()
    {
        CreationTime = DateTime.Now;
        MachineName = Environment.MachineName;
        Domain = Environment.UserDomainName;
        UserName = Environment.UserName;
        RunTimeVersion = Environment.Version.ToString();
    }

    [DataMember] public string? RunTimeVersion { get; set; }
    [DataMember] public DateTime CreationTime { get; set; }
    [DataMember] public string? MachineName { get; set; }
    [DataMember] public string? Domain { get; set; }
    [DataMember] public string? UserName { get; set; }
    [DataMember] public string? ApplicationVersion { get; set; }
    [DataMember] public string? ApplicationTitle { get; set; }
    [DataMember] public string? CustomInfo { get; set; }
}

[DataContract(Name = "ServiceConsumerInfo", Namespace = MinooServiceContractNamespace.Value)]
public class ServiceConsumerInfo : ClientInfo
{
    [DataMember] public string? ConsumerUsername { get; set; }
    [DataMember] public string? ConsumerPassword { get; set; }
    [DataMember] public string? ResponseConsumer { get; set; }
}

[DataContract(Name = "ServiceRequest", Namespace = MinooServiceContractNamespace.Value)]
public sealed class ServiceRequest : ServiceConsumerInfo
{
    [DataMember] public Guid Id { get; set; }
    [DataMember] public string? ServiceName { get; set; }
    [DataMember] public string? ServiceCommand { get; set; }
    [DataMember] public object? ServiceParameters { get; set; }
    [DataMember] public bool SuppressExecption { get; set; }

    /// <summary>
    /// Serializes command arguments using the legacy .NET Framework SharpSerializer
    /// type names expected by the Minoo middleware.
    /// </summary>
    public string? SetArguments(params object[] arguments)
    {
        var serialized = SharpSerializerPayload.Serialize(arguments);
        ServiceParameters = serialized;
        return serialized;
    }
}

[DataContract(Name = "ServiceResponse", Namespace = MinooServiceContractNamespace.Value)]
public sealed class ServiceResponse : ClientInfo
{
    [DataMember] public Guid Id { get; set; }
    [DataMember] public object? ServiceResult { get; set; }
    [DataMember] public int ErrorCode { get; set; }
}

internal static class SharpSerializerPayload
{
    public static string? Serialize(params object[]? data)
    {
        if (data is null || data.Length == 0)
            return null;

        var settings = new SharpSerializerXmlSettings();
        settings.AdvancedSettings.TypeNameConverter = new LegacyFrameworkTypeNameConverter();

        using var stream = new MemoryStream();
        var serializer = new SharpSerializer(settings);

        // The legacy service runs on .NET Framework. Its SharpSerializer expects
        // BCL types to be identified through mscorlib, not System.Private.CoreLib.
        // Serialize multiple arguments as one object[] exactly as XmlHelper does.
        serializer.Serialize(data.Length > 1 ? (object)data : data[0], stream);

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class LegacyFrameworkTypeNameConverter : ITypeNameConverter
    {
        private static readonly Regex AssemblyMetadata =
            new(@", (Version|Culture|PublicKeyToken)=[^,]+", RegexOptions.Compiled);

        public string ConvertToTypeName(Type type)
        {
            var assemblyQualifiedName = type.AssemblyQualifiedName
                ?? throw new InvalidOperationException($"Type '{type.FullName}' has no assembly-qualified name.");

            var legacyName = assemblyQualifiedName.Replace(
                "System.Private.CoreLib",
                "mscorlib",
                StringComparison.Ordinal);

            return AssemblyMetadata.Replace(legacyName, string.Empty);
        }

        public Type ConvertToType(string typeName)
        {
            var runtimeName = typeName.Replace(
                "mscorlib",
                "System.Private.CoreLib",
                StringComparison.Ordinal);

            return Type.GetType(runtimeName, throwOnError: true)!;
        }
    }
}
