using System.Net;
using System.Net.Http.Json;
using MiddleCsharp.Application.DTOs;

namespace MiddleCsharp.Presentation.Tests.Hosting;

public sealed class CompositionRootTests : IClassFixture<PresentationApplicationFactory>
{
    private readonly HttpClient _client;

    public CompositionRootTests(PresentationApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEvents_ReturnsOkWithJson()
    {
        // Act
        var response = await _client.GetAsync("/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var page = await response.Content.ReadFromJsonAsync<PaginatedResult<EventResponse>>();
        Assert.NotNull(page);
    }

    [Fact]
    public async Task GetUnknownEvent_ReturnsProblemDetailsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/events/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateEvent_ReturnsCreatedAndEventCanBeFetched()
    {
        // Arrange
        var request = new
        {
            title = "Smoke event",
            description = "Composition root",
            startAt = "2026-12-10T10:00:00",
            endAt = "2026-12-10T11:00:00",
            totalSeats = 3
        };

        // Act
        var response = await _client.PostAsync("/events", JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);

        var fetched = await _client.GetAsync($"/events/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }
}
