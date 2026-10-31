using System.ServiceModel;

namespace MinooWebCore.Services.Wcf.Contracts;

/// <summary>
/// Client-side contract matching Minoo.Services.Common.IBaseService.
/// The service exposes Run(ServiceRequest), not NegashtCaller as a top-level operation.
/// </summary>
[ServiceContract(
    Name = "IBaseService",
    Namespace = "http://Minoo.Services",
    SessionMode = SessionMode.Allowed)]
public interface IBaseService
{
    [OperationContract(Name = "Run")]
    Task<ServiceResponse> RunAsync(ServiceRequest request);
}
