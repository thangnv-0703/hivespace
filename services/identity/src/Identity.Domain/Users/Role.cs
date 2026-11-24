namespace Identity.Domain;

/// <summary>Roles a user can hold (FR-ROLE-001). Every registration starts as Buyer; Seller is granted when the user registers a shop (FEAT-009).</summary>
public enum Role
{
    Buyer,
    Seller,
}
