using System.ComponentModel.DataAnnotations;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;

namespace MiddleCsharp.Tests.DTOs;

public class EventRequestValidationTests
{
    [Fact]
    public void CreateEventRequest_MissingDates_IsInvalid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, result => result.ErrorMessage == "StartAt is required.");
        Assert.Contains(results, result => result.ErrorMessage == "EndAt is required.");
    }

    [Fact]
    public void CreateEventRequest_EndBeforeStart_IsInvalid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting",
            StartAt = new DateTime(2025, 1, 10, 11, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 10, 0, 0),
            TotalSeats = 10
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, result => result.ErrorMessage == "EndAt must be later than StartAt.");
    }

    [Fact]
    public void CreateEventRequest_MissingTotalSeats_IsInvalid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting",
            StartAt = new DateTime(2025, 1, 10, 10, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 11, 0, 0)
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, result => result.ErrorMessage == "TotalSeats is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateEventRequest_NonPositiveTotalSeats_IsInvalid(int totalSeats)
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting",
            StartAt = new DateTime(2025, 1, 10, 10, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 11, 0, 0),
            TotalSeats = totalSeats
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(results, result => result.ErrorMessage == "TotalSeats must be greater than 0.");
    }

    [Fact]
    public void CreateEventRequest_TitleTooLong_IsInvalid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = new string('t', EventConstraints.TitleMaxLength + 1),
            StartAt = new DateTime(2025, 1, 10, 10, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 11, 0, 0),
            TotalSeats = 10
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.ErrorMessage == "Title must be at most 200 characters long.");
    }

    [Fact]
    public void CreateEventRequest_DescriptionTooLong_IsInvalid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting",
            Description = new string('d', EventConstraints.DescriptionMaxLength + 1),
            StartAt = new DateTime(2025, 1, 10, 10, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 11, 0, 0),
            TotalSeats = 10
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.ErrorMessage == "Description must be at most 2000 characters long.");
    }

    [Fact]
    public void CreateEventRequest_Valid_IsValid()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Meeting",
            StartAt = new DateTime(2025, 1, 10, 10, 0, 0),
            EndAt = new DateTime(2025, 1, 10, 11, 0, 0),
            TotalSeats = 10
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        return results;
    }
}
