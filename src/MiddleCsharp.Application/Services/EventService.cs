using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.Commands;
using MiddleCsharp.Application.Concurrency;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Exceptions;

namespace MiddleCsharp.Application.Services;

internal sealed class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeatReservationLock _seatReservationLock;

    public EventService(
        IEventRepository events,
        IUnitOfWork unitOfWork,
        ISeatReservationLock seatReservationLock)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _seatReservationLock = seatReservationLock;
    }

    public async Task<EventResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evt = await _events.GetByIdAsync(id, cancellationToken);

        return (evt ?? throw new EventNotFoundException(id)).ToResponse();
    }

    public async Task<PaginatedResult<EventResponse>> GetAllAsync(
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

        var result = await _events.GetPageAsync(filter, page, pageSize, cancellationToken);

        return result.ToResponse();
    }

    public async Task<EventResponse> CreateAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var newEvent = new Event(
            command.Title,
            command.Description,
            command.StartAt,
            command.EndAt,
            command.TotalSeats);

        Validate(newEvent);

        await _events.AddAsync(newEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newEvent.ToResponse();
    }

    public async Task UpdateAsync(
        Guid id,
        UpdateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateDetails(command.Title, command.Description, command.StartAt, command.EndAt);

        var stored = await _events.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new EventNotFoundException(id);

        stored.UpdateDetails(
            command.Title,
            command.Description,
            command.StartAt,
            command.EndAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _seatReservationLock.WaitAsync(cancellationToken);

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
            _seatReservationLock.Release();
        }
    }

    private static void Validate(Event evt)
    {
        ValidateDetails(evt.Title, evt.Description, evt.StartAt, evt.EndAt);

        if (evt.TotalSeats <= 0)
        {
            throw new EventValidationException("TotalSeats must be greater than 0.");
        }
    }

    private static void ValidateDetails(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new EventValidationException("Title is required.");
        }

        if (title.Length > EventConstraints.TitleMaxLength)
        {
            throw new EventValidationException(
                $"Title must be at most {EventConstraints.TitleMaxLength} characters long.");
        }

        if (description?.Length > EventConstraints.DescriptionMaxLength)
        {
            throw new EventValidationException(
                $"Description must be at most {EventConstraints.DescriptionMaxLength} characters long.");
        }

        if (startAt == default)
        {
            throw new EventValidationException("StartAt is required.");
        }

        if (endAt == default)
        {
            throw new EventValidationException("EndAt is required.");
        }

        if (endAt <= startAt)
        {
            throw new EventValidationException("EndAt must be later than StartAt.");
        }
    }
}
