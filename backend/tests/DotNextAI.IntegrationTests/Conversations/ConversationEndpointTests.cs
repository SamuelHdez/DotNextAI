using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.IntegrationTests.Chat;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Conversations;

public class ConversationEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<JsonElement> CreateAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(Url(path), body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    /// <summary>The events of a text/event-stream body.</summary>
    private static List<(string Event, JsonElement Data)> Events(string body) =>
        body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(block =>
            {
                var lines = block.Split('\n');
                var name = lines.First(l => l.StartsWith("event:", StringComparison.Ordinal))[6..].Trim();
                var data = string.Join("\n", lines.Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l[5..].TrimStart()));
                return (name, JsonDocument.Parse(data).RootElement.Clone());
            })
            .ToList();

    private static async Task<List<(string Event, JsonElement Data)>> AskAsync(HttpClient client, string conversationId, object body)
    {
        using var response = await client.PostAsJsonAsync(Url($"/api/conversations/{conversationId}/messages/stream"), body, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Events(await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Folders_CreateListRenameDelete()
    {
        using var client = factory.CreateClientWith(new FakeChatService());

        var folder = await CreateAsync(client, "/api/folders", new { name = "  Work  " });
        Assert.Equal("Work", folder.GetProperty("name").GetString());
        Assert.Equal(0, folder.GetProperty("conversationCount").GetInt32());

        using var renamed = await client.PatchAsJsonAsync(Url($"/api/folders/{Id(folder)}"), new { name = "Projects" }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var list = await client.GetFromJsonAsync<JsonElement>(Url("/api/folders"), Ct);
        Assert.Equal("Projects", Assert.Single(list.EnumerateArray()).GetProperty("name").GetString());

        using var deleted = await client.DeleteAsync(Url($"/api/folders/{Id(folder)}"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var again = await client.DeleteAsync(Url($"/api/folders/{Id(folder)}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("NotFound", (await JsonAsync(again)).GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Folders_DuplicateName_Returns409()
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        await CreateAsync(client, "/api/folders", new { name = "Work" });

        using var response = await client.PostAsJsonAsync(Url("/api/folders"), new { name = "WORK" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DuplicateFolderName", (await JsonAsync(response)).GetProperty("failure").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Folders_EmptyName_Returns400WithTheField(string name)
    {
        using var client = factory.CreateClientWith(new FakeChatService());

        using var response = await client.PostAsJsonAsync(Url("/api/folders"), new { name }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Conversations_CreateWithDefaultsRenameMoveAndDelete()
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        var folder = await CreateAsync(client, "/api/folders", new { name = "Work" });

        var conversation = await CreateAsync(client, "/api/conversations", new { });
        Assert.Equal("New conversation", conversation.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, conversation.GetProperty("folderId").ValueKind);

        using var renamed = await client.PatchAsJsonAsync(Url($"/api/conversations/{Id(conversation)}"), new { title = "Offsite" }, Ct);
        Assert.Equal("Offsite", (await JsonAsync(renamed)).GetProperty("title").GetString());

        using var moved = await client.PutAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/folder"), new { folderId = Id(folder) }, Ct);
        Assert.Equal(Id(folder), (await JsonAsync(moved)).GetProperty("folderId").GetString());
        var folders = await client.GetFromJsonAsync<JsonElement>(Url("/api/folders"), Ct);
        Assert.Equal(1, folders[0].GetProperty("conversationCount").GetInt32());

        using var unfiled = await client.PutAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/folder"), new { folderId = (string?)null }, Ct);
        Assert.Equal(JsonValueKind.Null, (await JsonAsync(unfiled)).GetProperty("folderId").ValueKind);

        using var deleted = await client.DeleteAsync(Url($"/api/conversations/{Id(conversation)}"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var gone = await client.GetAsync(Url($"/api/conversations/{Id(conversation)}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Conversations_MoveToAnUnknownFolder_Returns400OnFolderId()
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        using var response = await client.PutAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/folder"), new { folderId = Guid.NewGuid() }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("folderId", out _));
    }

    [Fact]
    public async Task Conversations_CreateInAnUnknownFolder_Returns400()
    {
        using var client = factory.CreateClientWith(new FakeChatService());

        using var response = await client.PostAsJsonAsync(Url("/api/conversations"), new { folderId = Guid.NewGuid() }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("FolderNotFound", (await JsonAsync(response)).GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Ask_StreamsSavesAndSendsTheSavedHistory()
    {
        var chat = new FakeChatService(q => new ChatAnswer($"answer to {q}", "gpt-5.6-luna", false, new TokenUsage(10, 3, 13)));
        using var client = factory.CreateClientWith(chat);
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        var first = await AskAsync(client, Id(conversation), new { question = "My name is Ana." });
        var done = first[^1];
        Assert.Equal("done", done.Event);
        Assert.Equal("answer to My name is Ana.", done.Data.GetProperty("answer").GetString());
        var saved = done.Data.GetProperty("conversation");
        Assert.Equal("My name is Ana.", saved.GetProperty("title").GetString());
        Assert.Equal(1, saved.GetProperty("questionCount").GetInt32());

        await AskAsync(client, Id(conversation), new { question = "What is my name?" });

        Assert.Equal(["My name is Ana.", "answer to My name is Ana."], chat.Calls[1].History.Select(t => t.Text));
        var detail = await client.GetFromJsonAsync<JsonElement>(Url($"/api/conversations/{Id(conversation)}"), Ct);
        Assert.Equal(["user", "assistant", "user", "assistant"], detail.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        Assert.Equal(26, detail.GetProperty("conversation").GetProperty("usage").GetProperty("totalTokens").GetInt64());
        var list = await client.GetFromJsonAsync<JsonElement>(Url("/api/conversations"), Ct);
        Assert.Equal(2, Assert.Single(list.EnumerateArray()).GetProperty("questionCount").GetInt32());
    }

    [Fact]
    public async Task Ask_ImagesAreSavedAndServedWithTheirType()
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        await AskAsync(client, Id(conversation), new { question = "What is this?", images = new[] { new { mediaType = "image/png", data = Png } } });

        var detail = await client.GetFromJsonAsync<JsonElement>(Url($"/api/conversations/{Id(conversation)}"), Ct);
        var image = Assert.Single(detail.GetProperty("messages")[0].GetProperty("images").EnumerateArray());
        using var response = await client.GetAsync(Url(image.GetProperty("url").GetString()!), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Convert.FromBase64String(Png), await response.Content.ReadAsByteArrayAsync(Ct));

        using var otherConversation = await client.GetAsync(Url($"/api/conversations/{Guid.NewGuid()}/images/{image.GetProperty("id").GetString()}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, otherConversation.StatusCode);
    }

    [Fact]
    public async Task Ask_UnknownConversation_Returns404BeforeCallingTheModel()
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(Url($"/api/conversations/{Guid.NewGuid()}/messages/stream"), new { question = "Hi" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Ask_InvalidQuestion_Returns400()
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        using var response = await client.PostAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/messages/stream"), new { question = " " }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Ask_ModelFailure_KeepsItsStatusAndSavesNothing()
    {
        using var client = factory.CreateClientWith(FakeChatService.Failing(ChatFailure.ModelUnavailable, "No healthy deployments."));
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        using var response = await client.PostAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/messages/stream"), new { question = "Hi" }, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("ModelUnavailable", (await JsonAsync(response)).GetProperty("failure").GetString());
        var detail = await client.GetFromJsonAsync<JsonElement>(Url($"/api/conversations/{Id(conversation)}"), Ct);
        Assert.Empty(detail.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task Ask_FailureMidStream_SendsAnErrorEventAndSavesNothing()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two", "gpt-5.6-luna", false))
        {
            FailMidStream = new ChatServiceException(ChatFailure.Unavailable, "Dropped."),
        };
        using var client = factory.CreateClientWith(chat);
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        var events = await AskAsync(client, Id(conversation), new { question = "Hi" });

        Assert.Equal("error", events[^1].Event);
        var detail = await client.GetFromJsonAsync<JsonElement>(Url($"/api/conversations/{Id(conversation)}"), Ct);
        Assert.Empty(detail.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task Ask_UnknownModel_Returns400OnModel()
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        var conversation = await CreateAsync(client, "/api/conversations", new { });

        using var response = await client.PostAsJsonAsync(Url($"/api/conversations/{Id(conversation)}/messages/stream"), new { question = "Hi", model = "not-a-model" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("model", out _));
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Preflight_EditingMethodsFromFrontendOrigin_AreAllowed(string method)
    {
        using var client = factory.CreateClientWith(new FakeChatService());
        using var request = new HttpRequestMessage(HttpMethod.Options, Url($"/api/conversations/{Guid.NewGuid()}"));
        request.Headers.Add("Origin", DotNextAIApiFactory.FrontendOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries)));
    }
}
