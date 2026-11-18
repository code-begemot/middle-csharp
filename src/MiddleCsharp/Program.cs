using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.BackgroundServices;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Middleware;
using MiddleCsharp.Services;

var builder = WebApplication.CreateBuilder(args);

var problemJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

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
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new ContentResult
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentType = "application/problem+json",
            Content = JsonSerializer.Serialize(problem, problemJsonOptions)
        };
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
        options => options.PollingInterval > TimeSpan.Zero,
        "BookingProcessing:PollingInterval must be greater than zero.")
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
