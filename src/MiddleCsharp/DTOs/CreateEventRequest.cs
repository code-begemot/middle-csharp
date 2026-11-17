using System.ComponentModel.DataAnnotations;

namespace MiddleCsharp.DTOs;

public class CreateEventRequest : EventRequestBase
{
    [Required(ErrorMessage = "TotalSeats is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats must be greater than 0.")]
    public int? TotalSeats { get; set; }
}
