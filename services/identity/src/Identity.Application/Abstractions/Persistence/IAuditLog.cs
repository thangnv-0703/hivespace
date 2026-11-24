using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>The security audit log (NFR-SEC-009); rows are committed with whatever else the unit of work holds.</summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);
}
