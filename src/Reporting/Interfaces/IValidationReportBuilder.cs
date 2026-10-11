using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Reporting.Interfaces;

public interface IValidationReportBuilder
{
    ValidationReport Build(PublishedContract contract, ContractValidationResult validationResult);
}