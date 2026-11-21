using System.ComponentModel.DataAnnotations;
using MiddleCsharp.Models;

namespace MiddleCsharp.DTOs;

public abstract class EventRequestBase : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(
        EventConstraints.TitleMaxLength,
        ErrorMessage = "Title must be at most 200 characters long.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(
        EventConstraints.DescriptionMaxLength,
        ErrorMessage = "Description must be at most 2000 characters long.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt is required.")]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "EndAt is required.")]
    public DateTime? EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartAt is { } startAt && EndAt is { } endAt && endAt <= startAt)
        {
            yield return new ValidationResult("EndAt must be later than StartAt.", new[] { nameof(EndAt) });
        }
    }
}