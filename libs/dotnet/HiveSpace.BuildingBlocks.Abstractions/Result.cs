namespace HiveSpace.BuildingBlocks.Abstractions;

/// <summary>One unmet rule: the offending field and its stable error code (contracts/errors).</summary>
public sealed record Violation(string Field, string Code);

/// <summary>Why a handler refused: a stable error code and, for validation, every unmet rule.</summary>
public sealed record Failure(string Code, IReadOnlyList<Violation>? Violations = null);

/// <summary>The outcome of a handler: a value or a <see cref="Failure"/>. The Api maps failures to the error envelope.</summary>
public sealed record Result<T>
{
    private Result()
    {
    }

    public T? Value { get; private init; }

    public Failure? Failure { get; private init; }

    public bool IsSuccess => Failure is null;

    public static Result<T> Ok(T value) => new() { Value = value };

    public static Result<T> Fail(string code, IReadOnlyList<Violation>? violations = null) =>
        new() { Failure = new Failure(code, violations) };
}

/// <summary>Marker value for handlers that return nothing.</summary>
public readonly record struct Unit;
