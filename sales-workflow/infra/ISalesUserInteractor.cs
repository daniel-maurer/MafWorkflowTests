using MafWorkflow.Shared;
using SalesWorkflow.Models;

namespace SalesWorkflow;

public interface ISalesUserInteractor : IUserInteractor
{
    CustomerInfo? CurrentCustomer { get; set; }
}
