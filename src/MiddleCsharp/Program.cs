using MiddleCsharp.BackgroundServices;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Middleware;
using MiddleCsharp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<IBookingStore, InMemoryBookingStore>();
builder.Services.AddSingleton<IBookingService, BookingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddOptions<BookingProcessingOptions>()
    .Bind(builder.Configuration.GetSection(BookingProcessingOptions.SectionName))
    .Validate(
        options => options.PollInterval > TimeSpan.Zero,
        "BookingProcessing:PollInterval must be greater than zero.")
    .Validate(
        options => options.ProcessingDelay >= TimeSpan.Zero,
        "BookingProcessing:ProcessingDelay must be zero or greater.")
    .ValidateOnStart();
builder.Services.AddSingleton<BookingProcessor>();
builder.Services.AddHostedService<BookingProcessingService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
