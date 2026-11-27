using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.Processing;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Infrastructure.Persistence;
using MiddleCsharp.Infrastructure.Persistence.Repositories;

namespace MiddleCsharp.Tests;

internal sealed class TestApplication : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    public TestApplication(
        TimeProvider? timeProvider = null,
        Action<DbContextOptionsBuilder>? configureDb = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            configureDb?.Invoke(options);
        });
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<BookingProcessor>();
        services.AddSingleton(timeProvider ?? TimeProvider.System);

        _serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        _scope = _serviceProvider.CreateScope();
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public IServiceScopeFactory ScopeFactory => _serviceProvider.GetRequiredService<IServiceScopeFactory>();

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
