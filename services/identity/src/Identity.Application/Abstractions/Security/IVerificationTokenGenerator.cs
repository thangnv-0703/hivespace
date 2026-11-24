namespace Identity.Application.Abstractions;

public interface IVerificationTokenGenerator
{
    GeneratedToken Generate();

    byte[] Hash(string token);
}
