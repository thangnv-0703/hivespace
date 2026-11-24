using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-006 T3: the sign-in code challenge rules (FR-AUTH-028, FR-AUTH-032, FR-AUTH-033, FR-AUTH-034).</summary>
public sealed class SignInCodeChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private static SignInCodeChallenge NewChallenge() =>
        SignInCodeChallenge.Issue(Guid.CreateVersion7(Now), [1], Guid.CreateVersion7(Now), [2], Now);

    [Fact]
    public void Issue_opens_a_challenge_valid_for_five_minutes()
    {
        var challenge = NewChallenge();

        Assert.False(challenge.IsEnded);
        Assert.Null(challenge.EndReason);
        Assert.Equal(0, challenge.FailedCount);
        Assert.Equal(Now.AddMinutes(5), challenge.ExpiresAt);
        Assert.Equal(Now.AddSeconds(60), challenge.ResendAvailableAt);
    }

    [Fact]
    public void Expiry_is_at_the_exact_boundary()
    {
        var challenge = NewChallenge();

        Assert.False(challenge.IsExpired(Now.AddMinutes(5).AddTicks(-1)));
        Assert.True(challenge.IsExpired(Now.AddMinutes(5)));
    }

    [Fact]
    public void Retry_after_counts_down_to_zero_at_sixty_seconds()
    {
        var challenge = NewChallenge();

        Assert.Equal(TimeSpan.FromSeconds(60), challenge.RetryAfter(Now));
        Assert.Equal(TimeSpan.FromSeconds(1), challenge.RetryAfter(Now.AddSeconds(59)));
        Assert.Equal(TimeSpan.Zero, challenge.RetryAfter(Now.AddSeconds(60)));
        Assert.Equal(TimeSpan.Zero, challenge.RetryAfter(Now.AddSeconds(61)));
    }

    [Fact]
    public void The_fifth_wrong_code_ends_the_challenge_and_a_sixth_changes_nothing()
    {
        var challenge = NewChallenge();

        for (var i = 1; i <= 4; i++)
        {
            Assert.False(challenge.RecordWrongCode(Now.AddSeconds(i)));
            Assert.False(challenge.IsEnded);
        }

        Assert.True(challenge.RecordWrongCode(Now.AddSeconds(5)));
        Assert.Equal(5, challenge.FailedCount);
        Assert.Equal(ChallengeEndReason.Attempts, challenge.EndReason);
        Assert.Equal(Now.AddSeconds(5), challenge.EndedAt);

        Assert.False(challenge.RecordWrongCode(Now.AddSeconds(6)));
        Assert.Equal(5, challenge.FailedCount);
        Assert.Equal(Now.AddSeconds(5), challenge.EndedAt);
    }

    [Fact]
    public void Supersede_ends_an_open_challenge()
    {
        var challenge = NewChallenge();

        challenge.Supersede(Now.AddSeconds(70));

        Assert.True(challenge.IsEnded);
        Assert.Equal(ChallengeEndReason.Superseded, challenge.EndReason);
        Assert.Equal(Now.AddSeconds(70), challenge.EndedAt);
    }

    [Fact]
    public void Supersede_and_mark_used_on_an_ended_challenge_change_nothing()
    {
        var used = NewChallenge();
        used.MarkUsed(Now.AddSeconds(10));

        used.Supersede(Now.AddSeconds(20));
        used.MarkUsed(Now.AddSeconds(30));

        Assert.Equal(ChallengeEndReason.Used, used.EndReason);
        Assert.Equal(Now.AddSeconds(10), used.EndedAt);

        var superseded = NewChallenge();
        superseded.Supersede(Now.AddSeconds(10));
        superseded.MarkUsed(Now.AddSeconds(20));

        Assert.Equal(ChallengeEndReason.Superseded, superseded.EndReason);
        Assert.Equal(Now.AddSeconds(10), superseded.EndedAt);
    }
}
