namespace MiddleCsharp.BackgroundServices;

public sealed class BookingProcessingOptions
{
    public const string SectionName = "BookingProcessing";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromSeconds(2);
}
