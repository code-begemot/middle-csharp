using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.Concurrency;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Exceptions;

namespace MiddleCsharp.Application.Services;

internal sealed class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;

    public EventService(IEventRepository events, IUnitOfWork unitOfWork)
    {
        _events = events;
        _unitOfWork = unitOfWork;
    }

    public async Task<Event> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evt = await _events.GetByIdAsync(id, cancellationToken);

        return evt ?? throw new EventNotFoundException(id);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new EventValidationException("Page must be greater than 0.");
        }

        if (pageSize < 1 || pageSize > PaginationLimits.MaxPageSize)
        {
            throw new EventValidationException($"PageSize must be between 1 and {PaginationLimits.MaxPageSize}.");
        }

        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
        {
            throw new EventValidationException("From must be earlier than or equal to To.");
        }

        return await _events.GetPageAsync(filter, page, pageSize, cancellationToken);
    }

    public async Task<Event> CreateAsync(Event newEvent, CancellationToken cancellationToken = default)
    {
        Validate(newEvent);

        if (newEvent.TotalSeats <= 0)
        {
            throw new EventValidationException("TotalSeats must be greater than 0.");
        }

        await _events.AddAsync(newEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newEvent;
    }

    public async Task UpdateAsync(
        Guid id,
        Event updatedEvent,
        CancellationToken cancellationToken = default)
    {
        Validate(updatedEvent);

        var stored = await _events.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new EventNotFoundException(id);

        stored.UpdateDetails(
            updatedEvent.Title,
            updatedEvent.Description,
            updatedEvent.StartAt,
            updatedEvent.EndAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SeatReservationLock.Instance.WaitAsync(cancellationToken);

        try
        {
            if (!await _events.RemoveAsync(id, cancellationToken))
            {
                throw new EventNotFoundException(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SeatReservationLock.Instance.Release();
        }
    }

    private static void Validate(Event evt)
    {
        if (string.IsNullOrWhiteSpace(evt.Title))
        {
            throw new EventValidationException("Title is required.");
        }

        if (evt.Title.Length > EventConstraints.TitleMaxLength)
        {
            throw new EventValidationException(
                $"Title must be at most {EventConstraints.TitleMaxLength} characters long.");
        }

        if (evt.Description?.Length > EventConstraints.DescriptionMaxLength)
        {
            throw new EventValidationException(
                $"Description must be at most {EventConstraints.DescriptionMaxLength} characters long.");
        }

        if (evt.StartAt == default)
        {
            throw new EventValidationException("StartAt is required.");
        }

        if (evt.EndAt == default)
        {
            throw new EventValidationException("EndAt is required.");
        }

        if (evt.EndAt <= evt.StartAt)
        {
            throw new EventValidationException("EndAt must be later than StartAt.");
        }
    }
}
