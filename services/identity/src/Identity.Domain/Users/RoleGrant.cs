namespace Identity.Domain;

/// <summary>A role held by a user.</summary>
public sealed class RoleGrant
{
    private RoleGrant()
    {
    }

    public Guid UserId { get; private set; }

    public Role Role { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    internal static RoleGrant Create(Guid userId, Role role, DateTimeOffset now) =>
        new() { UserId = userId, Role = role, GrantedAt = now };
}
