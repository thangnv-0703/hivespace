namespace Identity.Domain;

/// <summary>Roles a user can hold (FR-ROLE-001). Every registration starts as Buyer; Seller arrives with FEAT-009.</summary>
public enum Role
{
    Buyer,
    Seller,
}
