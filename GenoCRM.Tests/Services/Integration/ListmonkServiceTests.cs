using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Integration;
using Xunit;

namespace GenoCRM.Tests.Services.Integration;

public class ListmonkServiceTests
{
    /// <summary>Records every request and answers from a canned map keyed by "METHOD path".</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string PathAndQuery, string Body)> Requests { get; } = new();
        public Dictionary<string, string> Responses { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
            var key = $"{request.Method} {request.RequestUri.AbsolutePath}";
            var json = Responses.TryGetValue(key, out var r) ? r : "{\"data\":{\"results\":[]}}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    private const string Lists = "{\"data\":{\"results\":[{\"id\":7,\"name\":\"Newsletter\"},{\"id\":9,\"name\":\"Other\"}]}}";

    private static (ListmonkService Service, FakeHandler Handler) Create(string? subscriberJson = null)
    {
        var handler = new FakeHandler();
        handler.Responses["GET /api/lists"] = Lists;
        if (subscriberJson != null)
        {
            handler.Responses["GET /api/subscribers"] = subscriberJson;
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"Listmonk:Enabled", "true"},
            {"Listmonk:BaseUrl", "https://listmonk.example"},
            {"Listmonk:Username", "api"},
            {"Listmonk:Password", "secret"},
            {"Listmonk:NewsletterListName", "Newsletter"},
        }).Build();
        var service = new ListmonkService(new HttpClient(handler), config, new Mock<ILogger<ListmonkService>>().Object);
        return (service, handler);
    }

    private static string Subscriber(int id, string email, string status, params int[] lists) =>
        JsonSerializer.Serialize(new { data = new { results = new[] { new { id, email, name = "N", status, lists = lists.Select(l => new { id = l }).ToArray(), attribs = new { } } } } });

    private static Member MemberWith(string email) =>
        new() { Id = 1, MemberNumber = "M001", FirstName = "Ann", LastName = "O'Brien", Email = email, Status = MemberStatus.Active, JoinDate = DateTime.UtcNow };

    [Fact]
    public async Task GetSubscriberByEmail_EscapesAndEncodesTheEmailInTheQuery()
    {
        var (service, handler) = Create();

        await service.GetSubscriberByEmailAsync("ann.o'brien+news@example.com");

        var query = handler.Requests.Single().PathAndQuery;
        Assert.Contains("subscribers.email%3D%27ann.o%27%27brien%2Bnews%40example.com%27", query);
        Assert.DoesNotContain("+", query.Split('?')[1]);
    }

    [Fact]
    public async Task Unsubscribe_UsesTheListUnsubscribeEndpointForTheNewsletterOnly()
    {
        var (service, handler) = Create(Subscriber(42, "a@b.de", "enabled", 7, 9));

        await service.UnsubscribeMemberAsync(MemberWith("a@b.de"));

        var put = handler.Requests.Single(r => r.Method == HttpMethod.Put);
        Assert.Equal("/api/subscribers/lists", put.PathAndQuery);
        var body = JsonSerializer.Deserialize<JsonElement>(put.Body);
        Assert.Equal(42, body.GetProperty("ids")[0].GetInt32());
        Assert.Equal("unsubscribe", body.GetProperty("action").GetString());
        Assert.Equal(7, body.GetProperty("target_list_ids")[0].GetInt32());
    }

    [Fact]
    public async Task CreateOrUpdate_KeepsOtherListMembershipsOfAnExistingSubscriber()
    {
        var (service, handler) = Create(Subscriber(42, "a@b.de", "enabled", 9));

        await service.CreateOrUpdateSubscriberAsync(MemberWith("a@b.de"));

        var put = handler.Requests.Single(r => r.Method == HttpMethod.Put);
        var lists = JsonSerializer.Deserialize<JsonElement>(put.Body).GetProperty("lists").EnumerateArray().Select(e => e.GetInt32()).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 7, 9 }, lists);
    }

    [Fact]
    public async Task CreateOrUpdate_DoesNotReenableABlocklistedSubscriber()
    {
        var (service, handler) = Create(Subscriber(42, "a@b.de", "blocklisted", 9));

        await service.CreateOrUpdateSubscriberAsync(MemberWith("a@b.de"));

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put || r.Method == HttpMethod.Post);
    }
}
