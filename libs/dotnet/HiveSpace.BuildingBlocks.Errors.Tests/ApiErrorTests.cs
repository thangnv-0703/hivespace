using System.Text.Json;
using HiveSpace.BuildingBlocks.Abstractions;
using HiveSpace.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HiveSpace.BuildingBlocks.Errors.Tests;

/// <summary>AC-5 (FR-LOC-004): codes are stable, messages follow the request language, every code has vi and en text.</summary>
public sealed class ApiErrorTests
{
    [Fact]
    public void Every_code_has_a_vi_and_an_en_message()
    {
        Assert.NotEmpty(MessageCatalog.Codes);
        foreach (var code in MessageCatalog.Codes)
        {
            Assert.True(MessageCatalog.MessagesFor("vi").ContainsKey(code), $"vi message missing for {code}");
            Assert.True(MessageCatalog.MessagesFor("en").ContainsKey(code), $"en message missing for {code}");
        }
    }

    [Fact]
    public void Identity_codes_from_the_design_are_registered_with_their_status()
    {
        Assert.Equal(409, MessageCatalog.StatusOf("identity.email_in_use"));
        Assert.Equal(401, MessageCatalog.StatusOf("identity.invalid_credentials"));
        Assert.Equal(400, MessageCatalog.StatusOf("identity.password_too_short"));
        Assert.Equal(409, MessageCatalog.StatusOf("identity.email_already_verified"));
        Assert.Equal(400, MessageCatalog.StatusOf(ErrorCodes.ValidationFailed));
    }

    [Theory]
    [InlineData(null, "vi")]
    [InlineData("", "vi")]
    [InlineData("en", "en")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("fr-FR,vi;q=0.8", "vi")]
    [InlineData("fr", "vi")]
    public void Language_defaults_to_vi_and_supports_en(string? header, string expected) =>
        Assert.Equal(expected, LanguageResolver.Resolve(header));

    [Theory]
    [InlineData("en", "Some fields are not valid. Check the highlighted fields.")]
    [InlineData("vi", "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường được đánh dấu.")]
    public async Task Api_exception_maps_to_4xx_with_the_requested_language(string language, string message)
    {
        var (status, body) = await Run(
            ApiException.Validation([new FieldError("password", "identity.password_too_short")]), language);

        Assert.Equal(400, status);
        Assert.Equal("common.validation_failed", body.GetProperty("code").GetString());
        Assert.Equal(message, body.GetProperty("message").GetString());
        var detail = body.GetProperty("details")[0];
        Assert.Equal("password", detail.GetProperty("field").GetString());
        Assert.Equal("identity.password_too_short", detail.GetProperty("code").GetString());
        Assert.Equal(
            MessageCatalog.MessageOf("identity.password_too_short", language),
            detail.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Single_purpose_error_uses_its_own_status_and_code()
    {
        var (status, body) = await Run(new ApiException("identity.email_in_use"), "vi");

        Assert.Equal(409, status);
        Assert.Equal("identity.email_in_use", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unexpected_exception_stays_a_500_without_leaking_its_message()
    {
        var (status, body) = await Run(new InvalidOperationException("secret detail"), "en");

        Assert.Equal(500, status);
        Assert.Equal("common.internal", body.GetProperty("code").GetString());
        Assert.DoesNotContain("secret", body.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static async Task<(int Status, JsonElement Body)> Run(Exception exception, string language)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.AcceptLanguage = language;
        context.Response.Body = new MemoryStream();

        await ApiErrorResponder.WriteAsync(context, exception);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (context.Response.StatusCode, doc.RootElement.Clone());
    }
}
