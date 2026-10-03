using System.ComponentModel.DataAnnotations;

namespace MiddleCsharp.DTOs;

public abstract class EventRequestBase : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt is required.")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "EndAt is required.")]
    public DateTime EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartAt == default)
            yield return new ValidationResult("StartAt is required.", new[] { nameof(StartAt) });

        if (EndAt == default)
            yield return new ValidationResult("EndAt is required.", new[] { nameof(EndAt) });

        if (StartAt != default && EndAt != default && EndAt <= StartAt)
            yield return new ValidationResult("EndAt must be later than StartAt.", new[] { nameof(EndAt) });
    }
}