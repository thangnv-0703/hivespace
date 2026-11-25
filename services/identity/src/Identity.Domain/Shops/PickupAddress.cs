using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// Where the shop's parcels are collected (FR-SHOP-006), free text with the district kept (FEAT-010 D-2). Private to Identity:
/// never part of an event (FR-SHOP-009). The phone is stored in E.164.
/// </summary>
public sealed record PickupAddress
{
    private PickupAddress(string contactName, string phone, string province, string district, string ward, string street)
    {
        ContactName = contactName;
        Phone = phone;
        Province = province;
        District = district;
        Ward = ward;
        Street = street;
    }

    public string ContactName { get; }

    public string Phone { get; }

    public string Province { get; }

    public string District { get; }

    public string Ward { get; }

    public string Street { get; }

    /// <summary>Validates every field and reports every unmet rule at once.</summary>
    public static (PickupAddress? Address, IReadOnlyList<Violation> Violations) Create(
        string? contactName, string? phone, string? province, string? district, string? ward, string? street)
    {
        var violations = new List<Violation>();
        string? Text(string? raw, string field, int max)
        {
            var (value, violation) = AddressText.Create(
                raw, field, 1, max, "identity.address_field_required", "identity.address_field_too_long", "identity.address_field_invalid");
            if (violation is not null)
            {
                violations.Add(violation);
            }

            return value;
        }

        var contact = Text(contactName, "contactName", 80);
        string? e164 = null;
        if (string.IsNullOrWhiteSpace(phone))
        {
            violations.Add(new Violation("phone", "identity.address_field_required"));
        }
        else
        {
            var (number, violation) = PhoneNumber.Create(phone);
            if (violation is not null)
            {
                violations.Add(violation);
            }
            else
            {
                e164 = number!.Value;
            }
        }

        var provinceText = Text(province, "province", 60);
        var districtText = Text(district, "district", 60);
        var wardText = Text(ward, "ward", 60);
        var streetText = Text(street, "street", 200);

        return violations.Count > 0
            ? (null, violations)
            : (new PickupAddress(contact!, e164!, provinceText!, districtText!, wardText!, streetText!), violations);
    }
}
