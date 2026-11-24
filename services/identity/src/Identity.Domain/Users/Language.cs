using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>The preferred language: <c>vi</c> or <c>en</c> (FR-PROF-006, FR-LOC-002).</summary>
public sealed record Language
{
    private Language(string value) => Value = value;

    public string Value { get; }

    public static (Language? Language, Violation? Violation) Create(string? raw) =>
        raw is "vi" or "en"
            ? (new Language(raw), null)
            : (null, new Violation("language", "identity.language_unsupported"));
}
