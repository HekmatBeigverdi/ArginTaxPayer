using ArginTaxPayer.Application.Modules;

namespace ArginTaxPayer.Application.Modules.Reporting;

/// <summary>Architectural boundary declaration; contains no implemented business behavior.</summary>
public sealed class ReportingModule : IModuleDescriptor
{
    public string Name => "Reporting";
    public string Ownership => "Scoped read-model projections";
    public string SecurityInvariant => "No direct settlement mutations";
}
