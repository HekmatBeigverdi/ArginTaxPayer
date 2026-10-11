namespace ArginTaxPayer.Application.Modules;

/// <summary>Metadata for a bounded context. It is not a runtime service registration.</summary>
public interface IModuleDescriptor
{
    string Name { get; }
    string Ownership { get; }
    string SecurityInvariant { get; }
}
