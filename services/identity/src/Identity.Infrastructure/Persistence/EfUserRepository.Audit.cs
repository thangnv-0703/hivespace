using System.Diagnostics;
using Identity.Domain;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository
{
    public void Add(AuditEntry entry)
    {
        // Joins the row to the trace of the request (design §5.2); never an email, address or token (D-4).
        entry.SetCorrelationId(Activity.Current?.TraceId.ToString());
        db.AuditEntries.Add(entry);
    }
}
