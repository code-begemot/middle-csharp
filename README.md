# Middle C# — Events & Bookings API

Учебный проект: REST API для управления событиями и бронированиями на ASP.NET Core (.NET 10). Второй спринт добавил фильтрацию и пагинацию событий, глобальную обработку ошибок и юнит-тесты. Третий спринт добавил бронирования с фоновой обработкой заявок. Четвёртый спринт добавил ограничение мест на событии, защиту от овербукинга и параллельную обработку броней.

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
dotnet test           # запускает юнит-тесты сервисов, хранилища, middleware и фоновой обработки
```

## API

Ресурсы: `/events` и `/bookings`

| Метод | Путь | Описание | Успех | Ошибки |
|---|---|---|---|---|
| GET | `/events` | Список событий с фильтрацией и пагинацией | `200 OK` | `400`, `500` |
| GET | `/events/{id}` | Событие по id | `200 OK` | `404`, `500` |
| POST | `/events` | Создать событие | `201 Created` | `400`, `413`, `500` |
| PUT | `/events/{id}` | Обновить событие целиком | `204 No Content` | `400`, `404`, `413`, `500` |
| DELETE | `/events/{id}` | Удалить событие | `204 No Content` | `404`, `500` |
| POST | `/events/{id}/book` | Создать бронь на событие | `202 Accepted` | `404`, `409`, `500` |
| GET | `/bookings/{id}` | Текущий статус брони | `200 OK` | `404`, `500` |

Для неподдерживаемого HTTP-метода (например, `PATCH /events`) возвращается `405 Method Not Allowed`.

## Модель `Event`

| Поле | Тип | Обязательность | Описание |
|---|---|---|---|
| `Id` | `Guid` | да | Уникальный идентификатор, генерируется на сервере |
| `Title` | `string` | да | Название события |
| `Description` | `string?` | нет | Описание |
| `StartAt` | `DateTime` | да | Начало события |
| `EndAt` | `DateTime` | да | Окончание события, строго позже `StartAt` |
| `TotalSeats` | `int` | да | Общее количество мест на событии, строго больше 0 |
| `AvailableSeats` | `int` | да | Текущее количество свободных мест |

При создании события `AvailableSeats` равно `TotalSeats`. Каждая успешная бронь уменьшает `AvailableSeats` на 1; если мест нет, возвращается `409 Conflict`. Место возвращается в пул, если бронь отклонена из-за ошибки обработки. Через `PUT /events/{id}` ёмкость не меняется: `TotalSeats` и `AvailableSeats` сохраняются из текущего состояния события.

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
      "endAt": "2025-01-10T11:00:00",
      "totalSeats": 10,
      "availableSeats": 9
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
| `Rejected` | Бронь отклонена: событие удалено до обработки или при обработке произошла ошибка |

В JSON статус передаётся строкой: `"status": "Pending"`.

### Создание брони `POST /events/{id}/book`

Тело запроса не требуется. Если событие с указанным `id` не найдено, возвращается `404` в формате Problem Details. Если свободных мест не осталось, возвращается `409 Conflict` — новая бронь не создаётся и место не резервируется.

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

Если места закончились:

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json; charset=utf-8

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "No available seats for this event",
  "instance": "/events/3fa85f64-5717-4562-b3fc-2c963f66afa6/book",
  "traceId": "0HNQ35B05HMII:00000003"
}
```

### Получение брони `GET /bookings/{id}`

```bash
curl http://localhost:5027/bookings/c1c9b2d4-1f3a-4b6e-9c2a-7d1e5f0a8b11
```

Возвращает `200 OK` с текущим состоянием брони. Пока бронь не обработана — `"status": "Pending"` и `"processedAt": null`; после обработки — `"status": "Confirmed"` и заполненный `processedAt`. Для несуществующей брони — `404` в формате Problem Details.

### Фоновая обработка

`BookingProcessingService` — реализация `BackgroundService`, зарегистрированная через `AddHostedService`. Сервис:

- раз в секунду опрашивает хранилище на наличие броней в статусе `Pending` (`BookingProcessing:PollInterval`, по умолчанию 1 секунда);
- передаёт всю пачку `Pending`-броней в `BookingProcessor`;
- обрабатывает брони **параллельно**: для каждой запускается отдельная задача, результаты собираются через `Task.WhenAll`;
- для каждой брони выдерживает искусственную задержку 2 секунды (`BookingProcessing:ProcessingDelay`) — имитация обращения к внешней системе. Задержка выполняется до захвата семафора, поэтому пачка из N броней занимает примерно `ProcessingDelay`, а не N × `ProcessingDelay`;
- под семафором `SemaphoreSlim(1, 1)` проверяет, существует ли событие, и переводит бронь в `Confirmed` с заполнением `ProcessedAt`;
- если событие к моменту обработки удалено, бронь переходит в `Rejected` с заполненным `ProcessedAt`;
- если при обработке произошла непредвиденная ошибка, бронь переводится в `Rejected`, а зарезервированное место возвращается в событие;
- логирует старт пачки, подтверждение, отказ и ошибки через `ILogger`; сбой одной брони не останавливает остальные;
- корректно завершается по `CancellationToken` при остановке приложения: незавершённые брони остаются `Pending`.

Настройки можно переопределить в `appsettings.json`:

```json
"BookingProcessing": {
  "PollInterval": "00:00:01",
  "ProcessingDelay": "00:00:02"
}
```

Некорректные значения (`PollInterval` ≤ 0 или `ProcessingDelay` < 0) останавливают запуск приложения с ошибкой валидации настроек.

### Синхронизация и защита от овербукинга

`BookingService` — синглтон, поэтому все запросы на бронирование проходят через один `private readonly object _bookingLock`. Под `lock` выполняется критическая секция:

1. проверка свободных мест и их резерв (`TryReserveSeats`);
2. создание и сохранение брони.

Если мест нет, `TryReserveSeats` возвращает `false`, и сервис выбрасывает `NoAvailableSeatsException`, которую `ExceptionHandlingMiddleware` превращает в `409 Conflict`. Чтение брони (`GET /bookings/{id}`) не блокируется. Если сохранение брони не удалось, зарезервированное место возвращается через `ReleaseSeats`.

В фоновой обработке используется `SemaphoreSlim(1, 1)`: `lock` нельзя применять вместе с `await`, а семафор умеет асинхронно ждать. Он защищает обновление статуса брони и возврат места при отказе.

Почему `lock` + `SemaphoreSlim`, а не только `ConcurrentDictionary`: конкурентная проблема не в отдельной операции, а в паре «прочитать свободные места → решить, что место есть → создать бронь». Без блокировки два потока могут одновременно увидеть одно и то же свободное место и создать две брони. `ConcurrentDictionary` делает атомарными отдельные операции над словарём, но не эту последовательность.

Сценарий овербукинга: событие на 3 места, 5 одновременных запросов на бронь. Ровно 3 запроса получают `202 Accepted`, остальные 2 — `409 Conflict`; `AvailableSeats` в итоге равно 0, а количество броней не превышает `TotalSeats`.

### Сценарий целиком

1. `POST /events` — создать событие с `totalSeats`, запомнить `id`.
2. `POST /events/{id}/book` — получить `202 Accepted`, тело брони и `Location`.
3. Сразу выполнить `GET /bookings/{bookingId}` — статус `Pending`, `processedAt` пустой.
4. Подождать 2–3 секунды и повторить запрос — статус `Confirmed`, `processedAt` заполнен.
5. Если удалить событие до обработки брони, она перейдёт в `Rejected` с заполненным `processedAt`.
6. Если мест не осталось, очередной `POST /events/{id}/book` вернёт `409 Conflict`.

## Формат ошибок

Все ошибки отдаются в формате [Problem Details (RFC 7807)](https://www.rfc-editor.org/rfc/rfc9457) с `Content-Type: application/problem+json`. Исключения контроллеров и сервисов перехватывает глобальный middleware `ExceptionHandlingMiddleware`, а ответы без тела (404 для неизвестного маршрута, 405 для неподдерживаемого метода, 413 для слишком большого тела) дополняются через `AddProblemDetails()` и `UseStatusCodePages()`.

| Код | Когда | Пример `title` |
|---|---|---|
| `400 Bad Request` | невалидная модель, query-параметры или фильтр `from > to` | `One or more validation errors occurred.` / `Validation failed` |
| `404 Not Found` | событие или бронь с указанным id не найдены, либо маршрут не совпал | `Resource not found` / `Not Found` |
| `405 Method Not Allowed` | неподдерживаемый HTTP-метод | `Method Not Allowed` |
| `409 Conflict` | на событии не осталось свободных мест | `Conflict` |
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

При `500` подробности исключения наружу не отдаются — в ответе только общее сообщение, а полное исключение пишется в лог с уровнем `Error`. Ошибки `400`/`404`/`409` логируются как предупреждения.

## DTO

API принимает и возвращает DTO, а не доменную модель напрямую:

- **`CreateEventRequest`** — тело `POST /events`. Поля: `title`, `description`, `startAt`, `endAt`, `totalSeats`. Поле `id` не принимается — генерируется на сервере.
- **`UpdateEventRequest`** — тело `PUT /events/{id}`. Поля: `title`, `description`, `startAt`, `endAt`. Ёмкость события (`totalSeats`/`availableSeats`) через `PUT` не меняется.
- **`EventResponse`** — ответ `GET /events/{id}` и `POST /events`. Поля: `id`, `title`, `description`, `startAt`, `endAt`, `totalSeats`, `availableSeats`.
- **`PaginatedResult<T>`** — обёртка для `GET /events`.
- **`GetEventsQuery`** — query-параметры `GET /events` (`title`, `from`, `to`, `page`, `pageSize`).
- **`BookingInfo`** — ответ `POST /events/{id}/book` и `GET /bookings/{id}`. Поля: `id`, `eventId`, `status`, `createdAt`, `processedAt`.

### Правила валидации

- `Title` — обязательное поле;
- `StartAt` — обязательное поле;
- `EndAt` — обязательное поле, должно быть **позже** `StartAt`;
- `TotalSeats` — обязательное поле при создании, целое число больше 0;
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
    "endAt": "2025-01-10T11:00:00",
    "totalSeats": 10
  }'
```

## Хранилище

In-memory хранилища на `ConcurrentDictionary`: отдельно события (`EventService`) и бронирования (`InMemoryBookingStore`). Данные сохраняются только на время работы процесса и очищаются при перезапуске. Наружу сервисы и хранилище отдают копии объектов, поэтому изменение полученного DTO не влияет на сохранённые данные.

## Соответствие заданию четвёртого спринта

| Требование | Реализация в проекте |
|---|---|
| Ограничение мест события | `Event.TotalSeats`, `Event.AvailableSeats`, `TryReserveSeats`, `ReleaseSeats` |
| `AvailableSeats == TotalSeats` при создании | `EventService.Create` |
| Защита от овербукинга | `lock (_bookingLock)` в `BookingService.CreateBookingAsync` |
| `409 Conflict` при отсутствии мест | `NoAvailableSeatsException` и маппинг в `ExceptionHandlingMiddleware` |
| Параллельная фоновая обработка | `BookingProcessor` (`Task.WhenAll`) + `SemaphoreSlim`, запускается из `BookingProcessingService` |
| Потокобезопасное обновление хранилищ | `ConcurrentDictionary` + `TryUpdate`-циклы в `EventService`, `SemaphoreSlim` в `BookingProcessor` |
| Тесты на конкурентность | `BookingServiceTests`, `EventServiceSeatTests`, `BookingProcessorTests` |

В задании фоновый сервис назван `BookingBackgroundService`, а хранилище событий — `InMemoryEventStore`. В проекте эти роли выполняют `BookingProcessingService` + `BookingProcessor` и `EventService` соответственно.

## Тесты

Отдельный проект `tests/MiddleCsharp.Tests` на xUnit, ссылается на `src/MiddleCsharp`. Покрыто:

- CRUD: создание (включая игнорирование `id` из запроса), чтение списка и по id, обновление, удаление; возвращаемые объекты изолированы от внутреннего хранилища;
- неуспешные сценарии: неизвестный id для `GET`/`PUT`/`DELETE`, пустой `Title`, отсутствующие даты, `EndAt <= StartAt`;
- валидация DTO: обязательность дат, `EndAt <= StartAt`, валидный запрос;
- фильтрация: по названию (регистр, часть строки, отсутствие совпадений, пустая строка), `from`, `to`, диапазон дат, комбинированные фильтры, ошибка при `from > to`;
- пагинация: первая/средняя/последняя страница, страница за пределами, очень большой номер страницы, `pageSize` больше количества и больше допустимого максимума, порядок по `startAt`, невалидные `page`/`pageSize`, пустое хранилище;
- middleware: `400`/`404`/`409`/`413`/`500`, `Content-Type: application/problem+json`, `status`/`title`/`detail`, отсутствие внутренних деталей в ответе `500`, отсутствие ответа при отмене запроса клиентом;
- сущность `Event`: резерв одного и нескольких мест, нехватка мест, возврат мест, ограничение сверху `TotalSeats`, ненулевое количество, копирование мест в `Clone()`;
- `EventService` и места: `AvailableSeats == TotalSeats` при создании, `TotalSeats <= 0`, сохранение мест при `PUT`, атомарные резерв и возврат в хранилище, конкурентные запросы не превышают ёмкость;
- сущность `Booking`: создание в статусе `Pending`, `Confirm`/`Reject`, заполнение `ProcessedAt`, запрет повторной обработки, проверка обязательных дат;
- хранилище броней: добавление и чтение, изоляция копий, выборка только `Pending`, атомарная смена статуса;
- `EventService.Exists`: существующее, удалённое и неизвестное событие;
- `BookingService`: создание брони для существующего события, уменьшение `AvailableSeats`, бронирование до лимита мест, `NoAvailableSeatsException` после исчерпания, уникальные `Id`, чтение по `Id` без расхода места, отражение смены статуса, ошибки для несуществующего и удалённого события, неизвестной брони;
- конкурентность `BookingService`: 5 мест и 20 параллельных запросов (`Task.Run` + `Task.WhenAll`) дают ровно 5 успешных броней и 15 отказов, `AvailableSeats = 0`; 10 мест и 10 параллельных запросов дают 10 уникальных `Id`;
- `BookingProcessor`: подтверждение `Pending`-броней, заполнение `ProcessedAt`, параллельная обработка пачки, пропуск уже обработанных броней, отказ брони удалённого события, отказ и возврат места при ошибке подтверждения, сохранение места при успешном подтверждении, отмена по `CancellationToken` (в том числе посреди пачки);
- `BookingProcessingService`: запуск опроса, обработка брони и корректная остановка по `CancellationToken`.

Всего 122 теста.

Тесты написаны по принципу AAA (Arrange-Act-Assert); тесты сервисов работают со свежими экземплярами `EventService`, `InMemoryBookingStore` и `FixedTimeProvider`. Тесты на конкурентность используют реальный параллелизм (`Task.Run` + `Task.WhenAll`), а не последовательные вызовы.

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
│       │   ├── EventValidationException.cs
│       │   ├── NoAvailableSeatsException.cs
│       │   └── NotFoundException.cs
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
│       │   ├── BookingProcessingServiceTests.cs
│       │   └── BookingProcessorTests.cs
│       ├── DataAccess/
│       │   └── InMemoryBookingStoreTests.cs
│       ├── DTOs/
│       │   └── EventRequestValidationTests.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddlewareTests.cs
│       ├── Models/
│       │   ├── BookingTests.cs
│       │   └── EventTests.cs
│       ├── Services/
│       │   ├── BookingServiceTests.cs
│       │   ├── EventServiceCrudTests.cs
│       │   ├── EventServiceFailureTests.cs
│       │   ├── EventServiceQueryTests.cs
│       │   └── EventServiceSeatTests.cs
│       ├── FixedTimeProvider.cs
│       ├── TestData.cs
│       └── MiddleCsharp.Tests.csproj
├── middle-csharp.http
├── middle-csharp.sln
└── README.md
```
