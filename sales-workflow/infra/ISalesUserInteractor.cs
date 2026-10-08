using MafWorkflow.Shared;
using SalesWorkflow.Models;

namespace SalesWorkflow;

public interface ISalesUserInteractor : IUserInteractor
{
    string SessionId { get; }
    CustomerInfo? CurrentCustomer { get; set; }
}
