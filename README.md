# Middle C# — Events & Bookings API

Учебный проект: REST API для управления событиями и бронированиями на ASP.NET Core (.NET 10). Второй спринт добавил фильтрацию и пагинацию событий, глобальную обработку ошибок и юнит-тесты. Третий спринт добавил бронирования с фоновой обработкой заявок. Четвёртый спринт добавил ограничение мест на событии, защиту от овербукинга и параллельную обработку броней. Пятый спринт заменил хранение данных в памяти на PostgreSQL через Entity Framework Core. Шестой спринт перевёл схему базы данных на миграции EF Core, выделил репозитории и добавил интеграционные тесты с PostgreSQL в Testcontainers. Седьмой спринт разбил решение на четыре слоя по принципам чистой архитектуры: Domain, Application, Infrastructure и Presentation.

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) или Docker Engine с Compose
- Git

## Запуск

```bash
git clone https://github.com/code-begemot/middle-csharp.git
cd middle-csharp
docker compose up -d
dotnet run --project src/MiddleCsharp.Presentation
```

Перед запуском приложения PostgreSQL должен быть готов принимать подключения. `docker compose up -d` поднимает контейнер `middle-csharp-postgres` с healthcheck; проверить состояние можно командой:

```bash
docker compose ps
```

Строка подключения по умолчанию находится в `src/MiddleCsharp.Presentation/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres"
}
```

Для другого окружения строку можно переопределить переменной окружения `ConnectionStrings__DefaultConnection` или через user secrets. Локальные логин и пароль `postgres/postgres` предназначены только для учебного запуска.

Контейнер PostgreSQL создаёт базу `eventapi`, а при первом запуске приложение применяет к ней миграции EF Core. Схема управляется миграциями, а не `EnsureCreated()`. Если база была создана в пятом спринте через `EnsureCreated()`, её нужно один раз пересоздать — иначе `Migrate()` не найдёт историю миграций:

```bash
docker compose down -v
docker compose up -d
```

Проверить применённые миграции можно по таблице `__EFMigrationsHistory`:

```bash
docker compose exec postgres psql -U postgres -d eventapi -c 'TABLE "__EFMigrationsHistory";'
```

Команды для работы с миграциями:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/MiddleCsharp.Infrastructure
dotnet ef database update --project src/MiddleCsharp.Infrastructure
```

`dotnet tool restore` нужен один раз после клонирования: версия `dotnet-ef` зафиксирована в `.config/dotnet-tools.json`. Миграции и `IDesignTimeDbContextFactory<AppDbContext>` лежат в `MiddleCsharp.Infrastructure`, поэтому `--startup-project` не нужен: инструменты создают контекст через фабрику. Строку подключения фабрика берёт из переменной `ConnectionStrings__DefaultConnection`, иначе использует локальную по умолчанию. Обычно `dotnet ef database update` вручную не требуется — приложение применяет миграции при старте.

По умолчанию запускается http-профиль, приложение слушает:

- API: `http://localhost:5027`
- Swagger UI: `http://localhost:5027/swagger`

Опционально можно запустить https-профиль:

```bash
dotnet run --project src/MiddleCsharp.Presentation --launch-profile https
```

Тогда приложение также слушает `https://localhost:7054`. При первом запуске https-профиля может понадобиться доверить dev-сертификат:

```bash
dotnet dev-certs https --trust
```

Точные адреса всегда печатаются в консоли при запуске (`Now listening on: ...`).

Редирект на https управляется настройкой `HttpsRedirection:Enabled` (по умолчанию `true`). В http-профиле она выключена переменной окружения `HttpsRedirection__Enabled=false`: https-адреса нет, поэтому middleware не подключается и в лог не попадает предупреждение `Failed to determine the https port for redirect`. В https-профиле редирект включён и перенаправляет `http://localhost:5027` на `https://localhost:7054`.

## Сборка и тесты

```bash
dotnet build          # собирает решение целиком (API + тесты)
dotnet test           # запускает юнит- и интеграционные тесты
```

Юнит-тесты используют EF Core InMemory и не требуют Docker. Интеграционные тесты поднимают PostgreSQL в Testcontainers, поэтому для команды `dotnet test` Docker должен быть запущен. Только быстрые тесты можно запустить отдельно:

```bash
dotnet test tests/MiddleCsharp.Tests/MiddleCsharp.Tests.csproj
dotnet test tests/MiddleCsharp.Presentation.Tests/MiddleCsharp.Presentation.Tests.csproj
```

## Слои приложения

Решение разбито на четыре сборки; зависимости направлены только «внутрь» и проверяются компилятором через `ProjectReference`:

| Слой | Назначение | Зависит от |
|---|---|---|
| `MiddleCsharp.Domain` | доменные сущности `Event`/`Booking`, перечисление `BookingStatus`, `EventConstraints`, доменные исключения | ни от чего |
| `MiddleCsharp.Application` | use cases (`EventService`, `BookingService`), команды (`CreateEventCommand`, `UpdateEventCommand`), порты (`IEventRepository`, `IBookingRepository`, `IUnitOfWork`, `ISeatReservationLock`), DTO, фоновая обработка | только `Domain` |
| `MiddleCsharp.Infrastructure` | адаптеры портов: `AppDbContext`, конфигурации Fluent API, репозитории, `UnitOfWork`, миграции, design-time фабрика | `Application`, `Domain` |
| `MiddleCsharp.Presentation` | контроллеры, HTTP-маппинг (`GetEventsQuery`), middleware ошибок, фильтр Problem Details, composition root | `Application`, `Domain`, `Infrastructure` |

Правила направления зависимостей:

- `Application` не ссылается на `Infrastructure`: бизнес-логика знает только интерфейсы портов. Обратная ссылка дала бы цикл проектов, поэтому нарушение ловится на сборке.
- `Domain` не содержит ни одного пакета и не импортирует ASP.NET Core, EF Core, логирование и JSON-атрибуты. Проверяется пустым списком `PackageReference` в `MiddleCsharp.Domain.csproj`.
- Порты объявлены в `MiddleCsharp.Application/Abstractions/Persistence`, реализации — в `MiddleCsharp.Infrastructure/Persistence`. Лок резервирования мест — порт `ISeatReservationLock` в `Application/Concurrency`, реализация регистрируется в DI как singleton.
- `IEventService` не выпускает доменный агрегат: чтение возвращает `EventResponse` и `PaginatedResult<EventResponse>`, запись принимает `CreateEventCommand`/`UpdateEventCommand`. Presentation не видит `Domain.Entities` и не может обойти правила сущности.
- `Presentation` ссылается на `Domain` явно: middleware маппит доменные исключения (`EventNotFoundException`, `NoAvailableSeatsException` и другие). Это единственная причина, по которой слой знает домен; на `Domain.Entities` Presentation не завязан.
- Регистрация зависимостей — в `Program.cs`: `AddApplicationServices(builder.Configuration)` и `AddInfrastructureServices(builder.Configuration)`, миграции применяет `await app.Services.MigrateDatabaseAsync()`. Presentation не создаёт `AppDbContext` и не знает его тип.
- `BookingStatus` сериализуется строкой через глобальный `JsonStringEnumConverter`, настроенный в Presentation, а не атрибутом на доменном перечислении.

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

`StartAt`/`EndAt` события хранятся в PostgreSQL как `timestamp without time zone`: API принимает `DateTime` без часового пояса и сохраняет настенное время. Смещение (`Z` или `+03:00`) при разборе отбрасывается. `CreatedAt`/`ProcessedAt` броней — это UTC-моменты, они хранятся как `timestamp with time zone` и возвращаются с `Z`.

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
- `totalPages` — общее количество страниц (`0`, если ничего не найдено); вычисляется в `PaginatedResult<T>` из `totalCount` и `pageSize`, адаптер эти два поля только заполняет;
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

Брони хранятся в таблице `bookings` и связаны с событием внешним ключом `event_id` (свойство `EventId`). При удалении события его брони удаляются каскадно (`ON DELETE CASCADE`): после `DELETE /events/{id}` запрос `GET /bookings/{id}` для такой брони возвращает `404`.

### Статусы `BookingStatus`

| Статус | Значение |
|---|---|
| `Pending` | Бронь создана и ожидает обработки |
| `Confirmed` | Бронь подтверждена фоновым сервисом |
| `Rejected` | Бронь отклонена при ошибке обработки; зарезервированное место возвращено в событие |

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

`BookingProcessingService` — реализация `BackgroundService`, зарегистрированная через `AddHostedService`. `BackgroundService` — синглтон, а `AppDbContext` — scoped, поэтому сервис получает `IServiceScopeFactory` и не зависит от scoped-сервисов напрямую. Сервис:

- раз в секунду опрашивает базу на наличие броней в статусе `Pending` (`BookingProcessing:PollingInterval`, по умолчанию 1 секунда);
- в коротком scope получает `IBookingRepository`, извлекает id `Pending`-броней и закрывает scope;
- обрабатывает брони **параллельно**: для каждой запускается отдельная задача, результаты собираются через `Task.WhenAll`;
- для каждой брони выдерживает искусственную задержку 2 секунды (`BookingProcessing:ProcessingDelay`) — имитация обращения к внешней системе. Задержка выполняется параллельно для всей пачки;
- после задержки создаёт **отдельный scope** на каждую бронь, получает из него scoped `BookingProcessor` с собственным `DbContext` и переводит бронь в `Confirmed` с заполнением `ProcessedAt`;
- если событие удалено, его брони удаляются каскадно; если к моменту обработки бронь уже исчезла, она пропускается;
- если при обработке произошла непредвиденная ошибка, бронь переводится в `Rejected`, а зарезервированное место возвращается в событие;
- логирует старт пачки с количеством и id броней, подтверждение, отказ и ошибки через `ILogger`; сбой одной брони не останавливает остальные;
- корректно завершается по `CancellationToken` при остановке приложения: незавершённые брони остаются `Pending`.

Настройки можно переопределить в `appsettings.json`:

```json
"BookingProcessing": {
  "PollingInterval": "00:00:01",
  "ProcessingDelay": "00:00:02"
}
```

Некорректные значения (`PollingInterval` ≤ 0 или `ProcessingDelay` < 0) останавливают запуск приложения с ошибкой валидации настроек.

### Синхронизация и защита от овербукинга

`BookingService` — scoped-сервис, но все его экземпляры в процессе используют один общий `static SemaphoreSlim(1, 1)`. Внутри критической секции:

1. через `IEventRepository.GetByIdForUpdateAsync` загружается событие;
2. проверяются свободные места и вызывается `TryReserveSeats`;
3. создаётся бронь и добавляется через `IBookingRepository.AddAsync`;
4. один `IUnitOfWork.SaveChangesAsync` сохраняет и новую бронь, и уменьшенный `AvailableSeats` в одной транзакции.

Если мест нет, `TryReserveSeats` возвращает `false`, и сервис выбрасывает `NoAvailableSeatsException`, которую `ExceptionHandlingMiddleware` превращает в `409 Conflict`. Чтение брони (`GET /bookings/{id}`) не блокируется. Если `SaveChangesAsync` завершился ошибкой, изменения очищаются через `IUnitOfWork.ClearChanges`, и место не остаётся занятым.

Тот же семафор используется при возврате места в `BookingProcessor`, если бронь отклонена из-за ошибки обработки. Это защищает пару «прочитать свободные места → изменить их» от lost update в рамках одного процесса.

Почему семафор, а не `lock`: внутри критической секции теперь есть `await` (запросы EF Core и `SaveChangesAsync`), а `lock` нельзя удерживать через `await`. `SemaphoreSlim.WaitAsync` умеет асинхронно ждать.

Ограничение: семафор синхронизирует только один экземпляр приложения. Для нескольких экземпляров нужна блокировка на уровне PostgreSQL или optimistic concurrency; это остаётся вне рамок учебного проекта.

Сценарий овербукинга: событие на 3 места, 5 одновременных запросов на бронь. Ровно 3 запроса получают `202 Accepted`, остальные 2 — `409 Conflict`; `AvailableSeats` в итоге равно 0, а количество броней не превышает `TotalSeats`.

Пример для bash (Git Bash / WSL; id события возьмите из ответа `POST /events`):

```bash
EVENT_ID=3fa85f64-5717-4562-b3fc-2c963f66afa6  # подставьте id из ответа POST /events

for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST "http://localhost:5027/events/$EVENT_ID/book" &
done
wait

curl -s "http://localhost:5027/events/$EVENT_ID"
```

Ожидаемо: три ответа `202`, два — `409` (порядок вывода может отличаться), в событии `"availableSeats": 0`.

### Сценарий целиком

1. `POST /events` — создать событие с `totalSeats`, запомнить `id`.
2. `POST /events/{id}/book` — получить `202 Accepted`, тело брони и `Location`.
3. Сразу выполнить `GET /bookings/{bookingId}` — статус `Pending`, `processedAt` пустой.
4. Подождать 2–3 секунды и повторить запрос — статус `Confirmed`, `processedAt` заполнен.
5. Если удалить событие, его брони удалятся каскадно; последующий `GET /bookings/{id}` вернёт `404`.
6. Если мест не осталось, очередной `POST /events/{id}/book` вернёт `409 Conflict`.

## Формат ошибок

Все ошибки отдаются в формате [Problem Details (RFC 7807)](https://www.rfc-editor.org/rfc/rfc9457) с `Content-Type: application/problem+json`. Исключения контроллеров и сервисов перехватывает глобальный middleware `ExceptionHandlingMiddleware`, ответы без тела (404 для неизвестного маршрута, 405 для неподдерживаемого метода, 413 для слишком большого тела) дополняются через `AddProblemDetails()` и `UseStatusCodePages()`, а `415`, который формирует MVC, приводится к тому же формату фильтром `ProblemDetailsContentTypeFilter`.

| Код | Когда | Пример `title` |
|---|---|---|
| `400 Bad Request` | невалидная модель, query-параметры или фильтр `from > to` | `One or more validation errors occurred.` / `Validation failed` |
| `404 Not Found` | событие или бронь с указанным id не найдены, либо маршрут не совпал | `Resource not found` / `Not Found` |
| `405 Method Not Allowed` | неподдерживаемый HTTP-метод | `Method Not Allowed` |
| `409 Conflict` | на событии не осталось свободных мест | `Conflict` |
| `413 Payload Too Large` | тело запроса превышает лимит Kestrel | `Payload too large` |
| `415 Unsupported Media Type` | неподдерживаемый `Content-Type` тела запроса | `Unsupported Media Type` |
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

- `Title` — обязательное поле, не длиннее 200 символов;
- `Description` — необязательное поле, не длиннее 2000 символов;
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

## База данных

Данные хранятся в PostgreSQL; доступ идёт через Entity Framework Core. In-memory хранилища и `ConcurrentDictionary` больше не используются. После разбиения на слои `AppDbContext`, конфигурации и миграции живут в `MiddleCsharp.Infrastructure`; схема и идентификатор миграции `InitialCreate` не менялись, поэтому существующую базу пересоздавать не нужно.

- `AppDbContext` содержит `DbSet<Event> Events` и `DbSet<Booking> Bookings`, конфигурации подключаются через `ApplyConfigurationsFromAssembly`.
- Маппинг описан в `src/MiddleCsharp.Infrastructure/Persistence/Configurations` через `IEntityTypeConfiguration<T>` (Fluent API): таблицы `events`/`bookings`, колонки в snake_case (`id`, `title`, `start_at`, `event_id` и т. д.), первичные ключи с `ValueGeneratedNever()`, обязательность и максимальные длины строк, `Status` как строка, внешний ключ `bookings.event_id → events.id` с каскадным удалением, индексы по `events.start_at` и `bookings.status, created_at`.
- `AppDbContext` — scoped. `EventService` и `BookingService` тоже scoped, но работают с данными только через репозитории.
- Схема управляется миграциями EF Core. При старте приложение выполняет `Database.MigrateAsync()` через `MigrateDatabaseAsync()` из Infrastructure; файлы миграций лежат в `src/MiddleCsharp.Infrastructure/Migrations`. `EnsureCreated()` больше не используется. Проверка `EF.IsDesignTime` внутри `MigrateDatabaseAsync()` не даёт инструментам `dotnet ef` подключаться к базе при генерации миграций, а нереляционные провайдеры (EF InMemory в юнит-тестах) миграции пропускают. Для `dotnet ef` в Infrastructure объявлен `IDesignTimeDbContextFactory<AppDbContext>` (`AppDbContextFactory`), поэтому команда работает без `--startup-project`.
- Миграция `InitialCreate` создаёт таблицы `events` и `bookings`, колонки в snake_case и внешний ключ `bookings.event_id → events.id` с `ON DELETE CASCADE`. Повторный запуск применяет только новые миграции; история хранится в `__EFMigrationsHistory`.
- Данные сохраняются после перезапуска приложения. Контейнер и volume можно остановить через `docker compose down` (данные сохранятся) или удалить через `docker compose down -v`.
- Старую базу, созданную в пятом спринте через `EnsureCreated()`, нужно один раз пересоздать: у неё нет истории миграций.
- Базу, созданную до переименования колонок в snake_case, тоже нужно пересоздать через `docker compose down -v`: в ней останутся прежние имена `EventId`, `StartAt` и другие.

## Репозитории и Unit of Work

Работа с `AppDbContext` инкапсулирована в слое `MiddleCsharp.Infrastructure/Persistence`: интерфейсы портов объявлены в `MiddleCsharp.Application/Abstractions/Persistence`, реализации — в `Persistence/Repositories` и `Persistence/UnitOfWork`.

- `IEventRepository` / `EventRepository` — чтение события, чтение для изменения, постраничный список с фильтрами, добавление и удаление; адаптер заполняет `TotalCount`, `Page`, `PageSize` и `Items`, а `TotalPages` вычисляет `PaginatedResult<T>`;
- `IBookingRepository` / `BookingRepository` — чтение брони, чтение для подтверждения без события, загрузка для отказа вместе с событием, список `Pending`-id, добавление;
- `IUnitOfWork` / `UnitOfWork` — единая точка сохранения изменений и очистки трекера.

Репозитории и `IUnitOfWork` зарегистрированы как `Scoped` и используют общий scoped `AppDbContext`. В `BookingService` это позволяет одним `SaveChangesAsync` атомарно сохранить новую бронь и уменьшенный `AvailableSeats`. В `BookingProcessingService` репозиторий получается из отдельного scope через `IServiceScopeFactory`; `BookingProcessor` не обращается к `AppDbContext` напрямую. При подтверждении процессор читает бронь без `Include(Event)`, а при отказе — вместе с событием, чтобы вернуть место. Сервисы по-прежнему отвечают за бизнес-правила и валидацию, а репозитории — только за доступ к данным.

Проверить таблицы можно так:

```bash
docker compose exec postgres psql -U postgres -d eventapi -c "\dt"
docker compose exec postgres psql -U postgres -d eventapi -c "select id, title, available_seats from events;"
```

## Соответствие заданию пятого спринта

| Требование | Реализация в проекте |
|---|---|
| PostgreSQL вместо памяти | `docker-compose.yml`, `UseNpgsql`; с шестого спринта схема — через миграции |
| EF Core | пакеты `Microsoft.EntityFrameworkCore` и `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `AppDbContext` и `DbSet` | `MiddleCsharp.Infrastructure/Persistence/AppDbContext.cs` |
| Fluent API | `MiddleCsharp.Infrastructure/Persistence/Configurations/EventConfiguration.cs`, `BookingConfiguration.cs` |
| Приватные конструкторы и навигации | `Event`/`Booking` с приватными конструкторами без параметров, `Event.Bookings`, `Booking.Event` |
| Сервисы работают через слой данных | В пятом спринте — напрямую с `AppDbContext`; с шестого — через репозитории и `IUnitOfWork` |
| Scoped-регистрации | `AddDbContext`, scoped `IEventService`/`IBookingService`/`BookingProcessor` |
| Фоновая обработка со scopes | `BookingProcessingService` + `IServiceScopeFactory`: отдельный scope на выборку id и на каждую бронь |
| Тесты | EF Core InMemory, уникальная база на тест, scope на конкурентный запрос |

`BookingProcessor` в проекте — scoped-обработчик одной брони; пачку и scopes координирует `BookingProcessingService`. В задании фоновый сервис назван `BookingBackgroundService` — эта роль у `BookingProcessingService`.

## Соответствие заданию шестого спринта

| Требование | Реализация в проекте |
|---|---|
| C# / .NET 8+, ASP.NET Core Web API | `net10.0`, ASP.NET Core Web API |
| Сборка и запуск | `dotnet build`, `dotnet run --project src/MiddleCsharp.Presentation` |
| Тесты | `dotnet test`: 125 юнит-тестов и 37 интеграционных (162 всего) |
| Swagger | Swagger UI и `swagger.json` доступны в Development |
| EF Core + PostgreSQL | `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `docker-compose.yml` |
| Схема через миграции | `src/MiddleCsharp.Infrastructure/Migrations`, `Database.MigrateAsync()` при старте; `EnsureCreated()` удалён |
| Начальная миграция | `InitialCreate`: таблицы `events`/`bookings`, snake_case-колонки, FK с `ON DELETE CASCADE`, индексы |
| Репозитории Event и Booking | порты — `MiddleCsharp.Application/Abstractions/Persistence` (`IEventRepository`, `IBookingRepository`), реализации — `MiddleCsharp.Infrastructure/Persistence/Repositories` (`EventRepository`, `BookingRepository`) |
| Сервисы не работают с `AppDbContext` | `EventService`, `BookingService`, `BookingProcessor` используют репозитории и `IUnitOfWork` |
| Scoped-регистрации | `AddDbContext`, репозитории, `IUnitOfWork`, сервисы и `BookingProcessor` — scoped |
| Фоновая обработка со scopes | `BookingProcessingService` получает `IBookingRepository` из scope через `IServiceScopeFactory` |
| Интеграционные тесты на реальной PostgreSQL | `tests/MiddleCsharp.IntegrationTests`, Testcontainers, образ `postgres:17-alpine` |
| Изоляция тестов | один контейнер на коллекцию, перед каждым тестом `EnsureDeleted()` + `Migrate()` |
| Нет захардкоженного порта | connection string берётся из `PostgreSqlContainer` |

## Соответствие заданию седьмого спринта

| Требование | Реализация в проекте |
|---|---|
| Четыре отдельных проекта | `MiddleCsharp.Domain`, `MiddleCsharp.Application`, `MiddleCsharp.Infrastructure`, `MiddleCsharp.Presentation` в `middle-csharp.sln` |
| Направление зависимостей | `Application → Domain`, `Infrastructure → Application + Domain`, `Presentation → Application + Domain + Infrastructure` через `<ProjectReference>` |
| `Application` не зависит от `Infrastructure` | у проекта один `ProjectReference` — на `Domain`; реализации портов подключаются через DI из Presentation |
| Доменные сущности и исключения в Domain | `Domain/Entities`, `Domain/Enums`, `Domain/Exceptions`; в `MiddleCsharp.Domain.csproj` нет `PackageReference` |
| Бизнес-логика в Application | `Application/Services` (`EventService`, `BookingService`), `Application/Processing` (`BookingProcessor`, `BookingProcessingService`) |
| Порты в Application | `Application/Abstractions/Persistence`: `IEventRepository`, `IBookingRepository`, `IUnitOfWork`; `Application/Concurrency`: `ISeatReservationLock` |
| Адаптеры в Infrastructure | `Infrastructure/Persistence`: `AppDbContext`, конфигурации Fluent API, репозитории, `UnitOfWork`, миграции, `AppDbContextFactory` для `dotnet ef` |
| Composition root в Presentation | `Program.cs`: `AddApplicationServices`, `AddInfrastructureServices`, MVC, Problem Details, Swagger; проверяется smoke-тестами на `WebApplicationFactory` |
| Тонкие контроллеры | `EventsController`/`BookingsController` только читают запрос, вызывают сервис и возвращают DTO; правила — в Application и Domain |
| Presentation не видит доменный агрегат | `IEventService` отдаёт `EventResponse`/`PaginatedResult<EventResponse>` и принимает `CreateEventCommand`/`UpdateEventCommand`; `Domain.Entities` остаётся внутри Application |
| Обработчик исключений в Presentation | `Presentation/Middleware/ExceptionHandlingMiddleware` маппит доменные исключения в `400`/`404`/`409`/`500`; ссылка на `Domain` объявлена явно |
| Тесты по слоям | `MiddleCsharp.Tests` → Domain + Application + Infrastructure; `MiddleCsharp.Presentation.Tests` → Presentation + Infrastructure + Domain (composition root); `MiddleCsharp.IntegrationTests` → Domain + Application + Infrastructure |
| README | этот раздел, «Слои приложения» и обновлённая «Структура проекта» |

## Тесты

Проекты на xUnit; каждый ссылается только на свои слои:

- `tests/MiddleCsharp.Tests` — юнит-тесты домена, Application и InMemory-персистентности (`Domain` + `Application` + `Infrastructure`);
- `tests/MiddleCsharp.Presentation.Tests` — middleware, фильтр `ProblemDetailsContentTypeFilter`, политика https-редиректа и smoke-тесты composition root (`Presentation` + `Infrastructure` + `Domain`);
- `tests/MiddleCsharp.IntegrationTests` — репозитории и миграции на реальной PostgreSQL через Testcontainers (`Domain` + `Application` + `Infrastructure`).

Юнит-тестами покрыто:

- CRUD через сервисы и репозитории: создание, чтение списка и по id, обновление, удаление; проверка, что чтение не оставляет сущности в трекере;
- неуспешные сценарии: неизвестный id для `GET`/`PUT`/`DELETE`, пустой `Title`, отсутствующие даты, `EndAt <= StartAt`, слишком длинные `Title`/`Description`;
- валидация DTO: обязательность дат и `TotalSeats`, `EndAt <= StartAt`, `StringLength`, валидный запрос;
- фильтрация: по названию (регистр, часть строки, отсутствие совпадений, пустая строка), `from`, `to`, диапазон дат, комбинированные фильтры, ошибка при `from > to`;
- пагинация: первая/средняя/последняя страница, страница за пределами, очень большой номер страницы, `pageSize` больше количества и больше допустимого максимума, порядок по `startAt`, невалидные `page`/`pageSize`, пустая база, вычисление `TotalPages` в DTO (включая нулевой `totalCount` и `pageSize`);
- middleware: `400`/`404`/`409`/`413`/`500`, `Content-Type: application/problem+json`, `status`/`title`/`detail`, отсутствие внутренних деталей в ответе `500`, отсутствие ответа при отмене запроса клиентом;
- фильтр `ProblemDetailsContentTypeFilter`: `415` от MVC отдаётся с `application/problem+json`, обычные `ObjectResult` не меняются;
- политика https-редиректа: настройка `HttpsRedirection:Enabled`, значение по умолчанию `true`, явное выключение через `HttpsRedirection__Enabled=false`;
- composition root: `GET /events` → `200` с `application/json`, `GET /events/{unknown}` → `404` с `application/problem+json`, `POST /events` → `201` и созданное событие читается по `Location`;
- сущность `Event`: конструктор, нормализация дат, резерв одного и нескольких мест, нехватка мест, возврат мест, ограничение сверху `TotalSeats`, ненулевое количество, `UpdateDetails` сохраняет места;
- `EventService` и места: `AvailableSeats == TotalSeats` при создании, `TotalSeats <= 0`, сохранение мест при `PUT`, каскадное удаление броней вместе с событием; сервис отдаёт DTO и не оставляет агрегат в трекере;
- сущность `Booking`: создание в статусе `Pending`, `Confirm`/`Reject`, заполнение `ProcessedAt`, запрет повторной обработки, проверка обязательных дат;
- `BookingService`: создание брони для существующего события, уменьшение `AvailableSeats`, бронирование до лимита мест, `NoAvailableSeatsException` после исчерпания, уникальные `Id`, чтение по `Id` без расхода места, отражение смены статуса, ошибки для несуществующего и удалённого события, неизвестной брони;
- конкурентность `BookingService`: 5 мест и 20 параллельных запросов, каждый в отдельном scope, дают ровно 5 успешных броней и 15 отказов, `AvailableSeats = 0`; 10 мест и 10 параллельных запросов дают 10 уникальных `Id`;
- `BookingProcessor`: подтверждение `Pending`-броней, заполнение `ProcessedAt`, пропуск уже обработанных и отсутствующих броней, отказ и возврат места при ошибке подтверждения (через `SaveChangesInterceptor`), сохранение места при успешном подтверждении, отмена по `CancellationToken`;
- `BookingProcessingService`: пакетная обработка, параллельный запуск задержек (детерминированный счётчик одновременных задержек через `TimeProvider`, без измерения wall-clock), запуск опроса и корректная остановка по `CancellationToken`.

Интеграционными тестами покрыто:

- миграция `InitialCreate` применяется; через `information_schema` проверяются таблицы, snake_case-колонки и внешний ключ `bookings.event_id → events.id` с `ON DELETE CASCADE`;
- `EventRepository`: добавление, чтение, чтение для изменения, удаление, каскадное удаление броней, фильтры `title`/`from`/`to` и их комбинации (`From + To`, `Title + To`, `Title + From + To`), границы включительно, пагинация и сортировка;
- `BookingRepository`: добавление, чтение, чтение для подтверждения без события, загрузка с событием для отказа, выборка `Pending`-id с порядком, подтверждение, отклонение с возвратом места, нарушение внешнего ключа;
- каскадное удаление на уровне схемы PostgreSQL проверяется отдельным тестом.

Всего 162 теста: 125 юнит-тестов (110 в `MiddleCsharp.Tests` и 15 в `MiddleCsharp.Presentation.Tests`) и 37 интеграционных.

Юнит-тесты написаны по принципу AAA (Arrange-Act-Assert). Каждый тест получает `TestApplication` с уникальной InMemory-базой, поэтому тесты не влияют друг на друга. Конкурентные тесты создают отдельный scope и отдельный `AppDbContext` на каждый параллельный запрос. Тест параллельной обработки не измеряет миллисекунды: `DelayTrackingTimeProvider` считает одновременно запущенные задержки и проверяет пик.

Интеграционные тесты используют один контейнер `postgres:17-alpine` на всю коллекцию. Перед каждым тестом база приводится к чистому состоянию через `EnsureDeleted()` и `Migrate()`, поэтому тесты не зависят от порядка запуска. Connection string и порт берутся из контейнера Testcontainers. Для запуска нужен Docker; контейнер поднимается автоматически.

```bash
dotnet test
```

## Структура проекта

```
middle-csharp/
├── .config/
│   └── dotnet-tools.json
├── docker-compose.yml
├── src/
│   ├── MiddleCsharp.Domain/
│   │   ├── Entities/
│   │   │   ├── Booking.cs
│   │   │   ├── Event.cs
│   │   │   └── EventConstraints.cs
│   │   ├── Enums/
│   │   │   └── BookingStatus.cs
│   │   ├── Exceptions/
│   │   │   ├── BookingNotFoundException.cs
│   │   │   ├── EventNotFoundException.cs
│   │   │   ├── EventValidationException.cs
│   │   │   ├── NoAvailableSeatsException.cs
│   │   │   └── NotFoundException.cs
│   │   └── MiddleCsharp.Domain.csproj
│   ├── MiddleCsharp.Application/
│   │   ├── Abstractions/
│   │   │   └── Persistence/
│   │   │       ├── IBookingRepository.cs
│   │   │       ├── IEventRepository.cs
│   │   │       ├── IUnitOfWork.cs
│   │   ├── Commands/
│   │   │   ├── CreateEventCommand.cs
│   │   │   └── UpdateEventCommand.cs
│   │   ├── Concurrency/
│   │   │   ├── ISeatReservationLock.cs
│   │   │   └── SeatReservationLock.cs
│   │   ├── DTOs/
│   │   │   ├── BookingInfo.cs
│   │   │   ├── BookingMappingExtensions.cs
│   │   │   ├── CreateEventRequest.cs
│   │   │   ├── EventFilter.cs
│   │   │   ├── EventMappingExtensions.cs
│   │   │   ├── EventRequestBase.cs
│   │   │   ├── EventResponse.cs
│   │   │   ├── PaginatedResult.cs
│   │   │   ├── PaginationLimits.cs
│   │   │   └── UpdateEventRequest.cs
│   │   ├── Processing/
│   │   │   ├── BookingProcessingOptions.cs
│   │   │   ├── BookingProcessingService.cs
│   │   │   └── BookingProcessor.cs
│   │   ├── Services/
│   │   │   ├── BookingService.cs
│   │   │   ├── EventService.cs
│   │   │   ├── IBookingService.cs
│   │   │   └── IEventService.cs
│   │   ├── DependencyInjection.cs
│   │   └── MiddleCsharp.Application.csproj
│   ├── MiddleCsharp.Infrastructure/
│   │   ├── Migrations/
│   │   │   ├── 20261123161211_InitialCreate.cs
│   │   │   ├── 20261123161211_InitialCreate.Designer.cs
│   │   │   └── AppDbContextModelSnapshot.cs
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   ├── BookingConfiguration.cs
│   │   │   │   └── EventConfiguration.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── BookingRepository.cs
│   │   │   │   └── EventRepository.cs
│   │   │   ├── AppDbContext.cs
│   │   │   ├── AppDbContextFactory.cs
│   │   │   └── UnitOfWork.cs
│   │   ├── DependencyInjection.cs
│   │   └── MiddleCsharp.Infrastructure.csproj
│   └── MiddleCsharp.Presentation/
│       ├── Controllers/
│       │   ├── BookingsController.cs
│       │   └── EventsController.cs
│       ├── DTOs/
│       │   └── GetEventsQuery.cs
│       ├── Filters/
│       │   └── ProblemDetailsContentTypeFilter.cs
│       ├── Hosting/
│       │   └── HttpsRedirectionPolicy.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── appsettings.Development.json
│       ├── appsettings.json
│       ├── MiddleCsharp.Presentation.csproj
│       └── Program.cs
├── tests/
│   ├── MiddleCsharp.IntegrationTests/
│   │   ├── BookingRepositoryTests.cs
│   │   ├── EventRepositoryTests.cs
│   │   ├── MigrationTests.cs
│   │   ├── PostgresCollection.cs
│   │   ├── PostgresFixture.cs
│   │   ├── RepositoryTestBase.cs
│   │   └── MiddleCsharp.IntegrationTests.csproj
│   ├── MiddleCsharp.Presentation.Tests/
│   │   ├── Filters/
│   │   │   └── ProblemDetailsContentTypeFilterTests.cs
│   │   ├── Hosting/
│   │   │   ├── CompositionRootTests.cs
│   │   │   ├── HttpsRedirectionPolicyTests.cs
│   │   │   └── PresentationApplicationFactory.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddlewareTests.cs
│   │   └── MiddleCsharp.Presentation.Tests.csproj
│   └── MiddleCsharp.Tests/
│       ├── BackgroundServices/
│       │   ├── BookingProcessingServiceTests.cs
│       │   └── BookingProcessorTests.cs
│       ├── DTOs/
│       │   ├── EventRequestValidationTests.cs
│       │   └── PaginatedResultTests.cs
│       ├── Models/
│       │   ├── BookingTests.cs
│       │   └── EventTests.cs
│       ├── Services/
│       │   ├── BookingServiceTests.cs
│       │   ├── EventServiceCrudTests.cs
│       │   ├── EventServiceFailureTests.cs
│       │   ├── EventServiceQueryTests.cs
│       │   └── EventServiceSeatTests.cs
│       ├── DelayTrackingTimeProvider.cs
│       ├── FixedTimeProvider.cs
│       ├── TestApplication.cs
│       ├── TestData.cs
│       └── MiddleCsharp.Tests.csproj
├── middle-csharp.http
├── middle-csharp.sln
└── README.md
```
