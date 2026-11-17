using System.Net;
using System.Text.Json;
using Reqnroll;
using Xunit;

namespace Catalog.ContractTests;

[Binding]
public sealed class CategoriesSteps
{
    private static CatalogApiFactory? _factory;
    private HttpResponseMessage? _response;
    private JsonElement _category;

    [BeforeTestRun]
    public static async Task StartApi()
    {
        _factory = new CatalogApiFactory();
        await _factory.StartAsync();
    }

    [AfterTestRun]
    public static async Task StopApi()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [When("a client requests the category list")]
    public async Task WhenAClientRequestsTheCategoryList()
    {
        using var client = _factory!.CreateClient();
        _response = await client.GetAsync(new Uri("/v1/categories", UriKind.Relative));
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status) =>
        Assert.Equal((HttpStatusCode)status, _response!.StatusCode);

    [Then("the response contains a category with slug {string}")]
    public async Task ThenTheResponseContainsACategoryWithSlug(string slug)
    {
        using var body = JsonDocument.Parse(await _response!.Content.ReadAsStringAsync());
        _category = body.RootElement.GetProperty("items").EnumerateArray()
            .Single(c => c.GetProperty("slug").GetString() == slug)
            .Clone();
    }

    [Then("that category has a Vietnamese name and an English name")]
    public void ThenThatCategoryHasAVietnameseNameAndAnEnglishName()
    {
        var name = _category.GetProperty("name");
        Assert.False(string.IsNullOrWhiteSpace(name.GetProperty("vi").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(name.GetProperty("en").GetString()));
    }
}
