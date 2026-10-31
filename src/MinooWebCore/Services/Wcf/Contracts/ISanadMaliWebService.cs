using System.ServiceModel;

namespace MinooWebCore.Services.Wcf.Contracts;

/// <summary>
/// Client-side contract for the SanadMali WCF service.
/// The contract name must match the name exposed by the service WSDL.
/// </summary>
[ServiceContract(Namespace = "http://Minoo.Services")]
public interface ISanadMaliWebService
{
    [OperationContract(Name = "NegashtCaller")]
    Task<bool> NegashtCallerAsync(
        string ServiceUserID,
        string ServiceUserPassword,
        bool callSynchronous);
}
