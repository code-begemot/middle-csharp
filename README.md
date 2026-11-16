# Middle C# — Events API

Учебный проект: REST API для управления событиями на ASP.NET Core (.NET 10). Второй спринт добавил фильтрацию и пагинацию событий, глобальную обработку ошибок и юнит-тесты. Третий спринт добавил бронирования с фоновой обработкой заявок.

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git

## Запуск

```bash
git clone https://github.com/code-begemot/middle-csharp.git
cd middle-csharp
dotnet run --project src/MiddleCsharp
```

По умолчанию запускается http-профиль, приложение слушает:

- API: `http://localhost:5027`
- Swagger UI: `http://localhost:5027/swagger`

Опционально можно запустить https-профиль:

```bash
dotnet run --project src/MiddleCsharp --launch-profile https
```

Тогда приложение также слушает `https://localhost:7054`. При первом запуске https-профиля может понадобиться доверить dev-сертификат:

```bash
dotnet dev-certs https --trust
```

Точные адреса всегда печатаются в консоли при запуске (`Now listening on: ...`).

## Сборка и тесты

```bash
dotnet build          # собирает решение целиком (API + тесты)
dotnet test           # запускает юнит-тесты сервиса и middleware
```

## API

Базовый путь: `/events`

| Метод | Путь | Описание | Успех | Ошибки |
|---|---|---|---|---|
| GET | `/events` | Список событий с фильтрацией и пагинацией | `200 OK` | `400`, `500` |
| GET | `/events/{id}` | Событие по id | `200 OK` | `404`, `500` |
| POST | `/events` | Создать событие | `201 Created` | `400`, `413`, `500` |
| PUT | `/events/{id}` | Обновить событие целиком | `204 No Content` | `400`, `404`, `413`, `500` |
| DELETE | `/events/{id}` | Удалить событие | `204 No Content` | `404`, `500` |
| POST | `/events/{id}/book` | Создать бронь на событие | `202 Accepted` | `404`, `500` |
| GET | `/bookings/{id}` | Текущий статус брони | `200 OK` | `404`, `500` |

Для неподдерживаемого HTTP-метода (например, `PATCH /events`) возвращается `405 Method Not Allowed`.

## Фильтрация и пагинация `GET /events`

Все параметры опциональные и комбинируются по логическому **И**. События упорядочены по `startAt`, затем по `id`, поэтому страницы стабильны.

| Параметр | Тип | По умолчанию | Описание |
|---|---|---|---|
| `title` | string | — | Частичное совпадение с названием, регистронезависимо |
| `from` | DateTime | — | События, которые начинаются не раньше указанной даты (`startAt >= from`) |
| `to` | DateTime | — | События, которые заканчиваются не позже указанной даты (`endAt <= to`) |
| `page` | int | `1` | Номер страницы, начиная с 1 |
| `pageSize` | int | `10` | Количество элементов на странице, от 1 до 100 |

Некорректные `page`/`pageSize` (например, `0` или больше 100) дают `400 Bad Request`.

Пример запроса: события со словом «meet» в названии, вторая страница по 5 элементов:

```bash
curl "http://localhost:5027/events?title=meet&from=2025-01-01T00:00:00&page=2&pageSize=5"
```

### Формат ответа

`GET /events` возвращает `PaginatedResult<EventResponse>`:

```json
{
  "totalCount": 12,
  "page": 2,
  "pageSize": 5,
  "totalPages": 3,
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Meeting",
      "description": "Sprint planning",
      "startAt": "2025-01-10T10:00:00",
      "endAt": "2025-01-10T11:00:00"
    }
  ]
}
```

- `totalCount` — общее количество событий после фильтрации;
- `page` — номер текущей страницы;
- `pageSize` — размер страницы;
- `totalPages` — общее количество страниц (`0`, если ничего не найдено);
- `items` — события текущей страницы.

Если запрошена страница за пределами диапазона, возвращается `200` с пустым `items` и реальными `totalCount`/`totalPages`.

## Бронирования

Бронь создаётся по паттерну «быстрый ответ + отложенная обработка»: `POST /events/{id}/book` сразу возвращает `202 Accepted` с телом брони и заголовком `Location`, а подтверждение выполняет фоновый сервис.

### Модель `Booking`

| Поле | Тип | Обязательность | Описание |
|---|---|---|---|
| `Id` | `Guid` | да | Уникальный идентификатор брони, генерируется на сервере |
| `EventId` | `Guid` | да | Идентификатор события, к которому относится бронь |
| `Status` | `BookingStatus` | да | Текущий статус брони |
| `CreatedAt` | `DateTime` | да | Время создания брони (UTC) |
| `ProcessedAt` | `DateTime?` | нет | Время обработки брони фоновым сервисом |

### Статусы `BookingStatus`

| Статус | Значение |
|---|---|
| `Pending` | Бронь создана и ожидает обработки |
| `Confirmed` | Бронь подтверждена фоновым сервисом |
| `Rejected` | Бронь отклонена; метод есть в домене, в текущем спринте воркер всегда подтверждает |

В JSON статус передаётся строкой: `"status": "Pending"`.

### Создание брони `POST /events/{id}/book`

Тело запроса не требуется. Если событие с указанным `id` не найдено, возвращается `404` в формате Problem Details.

```bash
curl -i -X POST http://localhost:5027/events/3fa85f64-5717-4562-b3fc-2c963f66afa6/book
```

Ответ:

```http
HTTP/1.1 202 Accepted
Content-Type: application/json; charset=utf-8
Location: http://localhost:5027/bookings/c1c9b2d4-1f3a-4b6e-9c2a-7d1e5f0a8b11

{
  "id": "c1c9b2d4-1f3a-4b6e-9c2a-7d1e5f0a8b11",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "createdAt": "2025-01-10T10:00:00Z",
  "processedAt": null
}
```

`Location` указывает на `GET /bookings/{id}` — ресурс с текущим состоянием брони.

### Получение брони `GET /bookings/{id}`

```bash
curl http://localhost:5027/bookings/c1c9b2d4-1f3a-4b6e-9c2a-7d1e5f0a8b11
```

Возвращает `200 OK` с текущим состоянием брони. Пока бронь не обработана — `"status": "Pending"` и `"processedAt": null`; после обработки — `"status": "Confirmed"` и заполненный `processedAt`. Для несуществующей брони — `404` в формате Problem Details.

### Фоновая обработка

`BookingProcessingService` — реализация `BackgroundService`, зарегистрированная через `AddHostedService`. Сервис:

- раз в секунду опрашивает хранилище на наличие броней в статусе `Pending` (`BookingProcessing:PollInterval`, по умолчанию 1 секунда);
- для каждой брони выдерживает искусственную задержку 2 секунды (`BookingProcessing:ProcessingDelay`) — имитация обращения к внешней системе;
- переводит бронь в `Confirmed` и заполняет `ProcessedAt`;
- логирует результат через `ILogger`, ошибка по одной брони не останавливает обработку остальных;
- корректно завершается по `CancellationToken` при остановке приложения.

Настройки можно переопределить в `appsettings.json`:

```json
"BookingProcessing": {
  "PollInterval": "00:00:01",
  "ProcessingDelay": "00:00:02"
}
```

### Сценарий целиком

1. `POST /events` — создать событие, запомнить `id`.
2. `POST /events/{id}/book` — получить `202 Accepted`, тело брони и `Location`.
3. Сразу выполнить `GET /bookings/{bookingId}` — статус `Pending`, `processedAt` пустой.
4. Подождать 2–3 секунды и повторить запрос — статус `Confirmed`, `processedAt` заполнен.

## Формат ошибок

Все ошибки отдаются в формате [Problem Details (RFC 7807)](https://www.rfc-editor.org/rfc/rfc9457) с `Content-Type: application/problem+json`. Исключения контроллеров и сервисов перехватывает глобальный middleware `ExceptionHandlingMiddleware`, а ответы без тела (404 для неизвестного маршрута, 405 для неподдерживаемого метода, 413 для слишком большого тела) дополняются через `AddProblemDetails()` и `UseStatusCodePages()`.

| Код | Когда | Пример `title` |
|---|---|---|
| `400 Bad Request` | невалидная модель, query-параметры или фильтр `from > to` | `One or more validation errors occurred.` / `Validation failed` |
| `404 Not Found` | событие или бронь с указанным id не найдены, либо маршрут не совпал | `Resource not found` / `Not Found` |
| `405 Method Not Allowed` | неподдерживаемый HTTP-метод | `Method Not Allowed` |
| `413 Payload Too Large` | тело запроса превышает лимит Kestrel | `Payload too large` |
| `500 Internal Server Error` | непредвиденная ошибка | `Internal server error` |

Пример ошибки `404`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource not found",
  "status": 404,
  "detail": "Event with id '3fa85f64-5717-4562-b3fc-2c963f66afa6' was not found.",
  "instance": "/events/3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "traceId": "0HNQ35B05HMII:00000001"
}
```

Пример ошибки `400` при нарушении правил валидации:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "page": ["Page must be greater than 0."]
  },
  "traceId": "00-8419b2f66b25496df4f5c6d7017247ba-30744435e6f12a1f-00"
}
```

Пример ошибки `500`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Internal server error",
  "status": 500,
  "detail": "An unexpected error occurred while processing the request.",
  "instance": "/events",
  "traceId": "0HNQ35B05HMII:00000002"
}
```

При `500` подробности исключения наружу не отдаются — в ответе только общее сообщение, а полное исключение пишется в лог с уровнем `Error`. Ошибки `400`/`404` логируются как предупреждения.

## DTO

API принимает и возвращает DTO, а не доменную модель напрямую:

- **`CreateEventRequest`** — тело `POST /events`. Поля: `title`, `description`, `startAt`, `endAt`. Поле `id` не принимается — генерируется на сервере.
- **`UpdateEventRequest`** — тело `PUT /events/{id}`. Те же поля, что у `CreateEventRequest`.
- **`EventResponse`** — ответ `GET /events/{id}` и `POST /events`.
- **`PaginatedResult<T>`** — обёртка для `GET /events`.
- **`GetEventsQuery`** — query-параметры `GET /events` (`title`, `from`, `to`, `page`, `pageSize`).
- **`BookingInfo`** — ответ `POST /events/{id}/book` и `GET /bookings/{id}`. Поля: `id`, `eventId`, `status`, `createdAt`, `processedAt`.

### Правила валидации

- `Title` — обязательное поле;
- `StartAt` — обязательное поле;
- `EndAt` — обязательное поле, должно быть **позже** `StartAt`;
- `page` ≥ 1, `1 ≤ pageSize ≤ 100`;
- если заданы оба фильтра `from` и `to`, то `from ≤ to`.

Правила проверяются на двух уровнях: атрибутами DTO (дают `400` до вызова сервиса) и в `EventService` (защищают бизнес-логику при прямом вызове и в тестах). Проверка `from > to` выполняется в сервисе, поэтому такой запрос доходит до `ExceptionHandlingMiddleware` и возвращает `400`.

При `PUT /events/{id}` тело сначала валидируется, а затем проверяется существование события. Поэтому невалидное тело вместе с несуществующим `id` даёт `400`, а не `404`.

### Пример запроса на создание события

```bash
curl -X POST http://localhost:5027/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Meeting",
    "description": "Sprint planning",
    "startAt": "2025-01-10T10:00:00",
    "endAt": "2025-01-10T11:00:00"
  }'
```

## Хранилище

In-memory хранилища на `ConcurrentDictionary`: отдельно события (`EventService`) и бронирования (`InMemoryBookingStore`). Данные сохраняются только на время работы процесса и очищаются при перезапуске. Наружу сервисы и хранилище отдают копии объектов, поэтому изменение полученного DTO не влияет на сохранённые данные.

## Тесты

Отдельный проект `tests/MiddleCsharp.Tests` на xUnit, ссылается на `src/MiddleCsharp`. Покрыто:

- CRUD: создание (включая игнорирование `id` из запроса), чтение списка и по id, обновление, удаление; возвращаемые объекты изолированы от внутреннего хранилища;
- неуспешные сценарии: неизвестный id для `GET`/`PUT`/`DELETE`, пустой `Title`, отсутствующие даты, `EndAt <= StartAt`;
- валидация DTO: обязательность дат, `EndAt <= StartAt`, валидный запрос;
- фильтрация: по названию (регистр, часть строки, отсутствие совпадений, пустая строка), `from`, `to`, диапазон дат, комбинированные фильтры, ошибка при `from > to`;
- пагинация: первая/средняя/последняя страница, страница за пределами, очень большой номер страницы, `pageSize` больше количества и больше допустимого максимума, порядок по `startAt`, невалидные `page`/`pageSize`, пустое хранилище;
- middleware: `400`/`404`/`413`/`500`, `Content-Type: application/problem+json`, `status`/`title`/`detail`, отсутствие внутренних деталей в ответе `500`, отсутствие ответа при отмене запроса клиентом;
- сущность `Booking`: создание в статусе `Pending`, `Confirm`/`Reject`, заполнение `ProcessedAt`, запрет повторной обработки;
- хранилище броней: добавление и чтение, изоляция копий, выборка только `Pending`, атомарная смена статуса;
- `BookingService`: создание брони для существующего события, уникальные `Id` у нескольких броней, чтение по `Id`, отражение смены статуса, ошибки для несуществующего и удалённого события, неизвестной брони;
- `BookingProcessor`: подтверждение `Pending`-броней, заполнение `ProcessedAt`, обработка нескольких броней, отмена по `CancellationToken`, пропуск уже обработанных броней.

Всего 80 тестов.

Тесты написаны по принципу AAA (Arrange-Act-Assert); тесты сервисов работают со свежими экземплярами `EventService`, `InMemoryBookingStore` и `FixedTimeProvider`.

```bash
dotnet test
```

## Структура проекта

```
middle-csharp/
├── src/
│   └── MiddleCsharp/
│       ├── BackgroundServices/
│       │   ├── BookingProcessingOptions.cs
│       │   ├── BookingProcessingService.cs
│       │   └── BookingProcessor.cs
│       ├── Controllers/
│       │   ├── BookingsController.cs
│       │   └── EventsController.cs
│       ├── DataAccess/
│       │   ├── IBookingStore.cs
│       │   └── InMemoryBookingStore.cs
│       ├── DTOs/
│       │   ├── BookingInfo.cs
│       │   ├── BookingMappingExtensions.cs
│       │   ├── CreateEventRequest.cs
│       │   ├── UpdateEventRequest.cs
│       │   ├── EventResponse.cs
│       │   ├── EventRequestBase.cs
│       │   ├── GetEventsQuery.cs
│       │   ├── PaginatedResult.cs
│       │   └── EventMappingExtensions.cs
│       ├── Exceptions/
│       │   ├── BookingNotFoundException.cs
│       │   ├── EventNotFoundException.cs
│       │   └── EventValidationException.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Models/
│       │   ├── Booking.cs
│       │   ├── BookingStatus.cs
│       │   └── Event.cs
│       ├── Services/
│       │   ├── IBookingService.cs
│       │   ├── BookingService.cs
│       │   ├── IEventService.cs
│       │   ├── EventService.cs
│       │   ├── EventFilter.cs
│       │   └── PaginationLimits.cs
│       ├── Program.cs
│       └── middle-csharp.csproj
├── tests/
│   └── MiddleCsharp.Tests/
│       ├── BackgroundServices/
│       │   └── BookingProcessorTests.cs
│       ├── DataAccess/
│       │   └── InMemoryBookingStoreTests.cs
│       ├── DTOs/
│       │   └── EventRequestValidationTests.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddlewareTests.cs
│       ├── Models/
│       │   └── BookingTests.cs
│       ├── Services/
│       │   ├── BookingServiceTests.cs
│       │   ├── EventServiceCrudTests.cs
│       │   ├── EventServiceFailureTests.cs
│       │   └── EventServiceQueryTests.cs
│       ├── FixedTimeProvider.cs
│       ├── TestData.cs
│       └── MiddleCsharp.Tests.csproj
├── middle-csharp.http
├── middle-csharp.sln
└── README.md
```
