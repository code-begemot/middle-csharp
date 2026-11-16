# Middle C# — Events API

Учебный проект: REST API для управления событиями на ASP.NET Core (.NET 10). Второй спринт добавил фильтрацию и пагинацию событий, глобальную обработку ошибок и юнит-тесты.

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

## Формат ошибок

Все ошибки отдаются в формате [Problem Details (RFC 7807)](https://www.rfc-editor.org/rfc/rfc9457) с `Content-Type: application/problem+json`. Исключения контроллеров и сервисов перехватывает глобальный middleware `ExceptionHandlingMiddleware`, а ответы без тела (404 для неизвестного маршрута, 405 для неподдерживаемого метода, 413 для слишком большого тела) дополняются через `AddProblemDetails()` и `UseStatusCodePages()`.

| Код | Когда | Пример `title` |
|---|---|---|
| `400 Bad Request` | невалидная модель, query-параметры или фильтр `from > to` | `One or more validation errors occurred.` / `Validation failed` |
| `404 Not Found` | событие с указанным id не найдено или маршрут не совпал | `Resource not found` / `Not Found` |
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

In-memory хранилище на `ConcurrentDictionary`. Данные сохраняются только на время работы процесса и очищаются при перезапуске.

## Тесты

Отдельный проект `tests/MiddleCsharp.Tests` на xUnit, ссылается на `src/MiddleCsharp`. Покрыто:

- CRUD: создание (включая игнорирование `id` из запроса), чтение списка и по id, обновление, удаление; возвращаемые объекты изолированы от внутреннего хранилища;
- неуспешные сценарии: неизвестный id для `GET`/`PUT`/`DELETE`, пустой `Title`, отсутствующие даты, `EndAt <= StartAt`;
- валидация DTO: обязательность дат, `EndAt <= StartAt`, валидный запрос;
- фильтрация: по названию (регистр, часть строки, отсутствие совпадений, пустая строка), `from`, `to`, диапазон дат, комбинированные фильтры, ошибка при `from > to`;
- пагинация: первая/средняя/последняя страница, страница за пределами, очень большой номер страницы, `pageSize` больше количества и больше допустимого максимума, порядок по `startAt`, невалидные `page`/`pageSize`, пустое хранилище;
- middleware: `400`/`404`/`413`/`500`, `Content-Type: application/problem+json`, `status`/`title`/`detail`, отсутствие внутренних деталей в ответе `500`, отсутствие ответа при отмене запроса клиентом.

Тесты написаны по принципу AAA (Arrange-Act-Assert); тесты сервиса работают со свежим экземпляром `EventService`:

```bash
dotnet test
```

## Структура проекта

```
middle-csharp/
├── src/
│   └── MiddleCsharp/
│       ├── Controllers/
│       │   └── EventsController.cs
│       ├── DTOs/
│       │   ├── CreateEventRequest.cs
│       │   ├── UpdateEventRequest.cs
│       │   ├── EventResponse.cs
│       │   ├── EventRequestBase.cs
│       │   ├── GetEventsQuery.cs
│       │   ├── PaginatedResult.cs
│       │   └── EventMappingExtensions.cs
│       ├── Exceptions/
│       │   ├── EventNotFoundException.cs
│       │   └── EventValidationException.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Models/
│       │   └── Event.cs
│       ├── Services/
│       │   ├── IEventService.cs
│       │   ├── EventService.cs
│       │   ├── EventFilter.cs
│       │   └── PaginationLimits.cs
│       ├── Program.cs
│       └── middle-csharp.csproj
├── tests/
│   └── MiddleCsharp.Tests/
│       ├── DTOs/
│       │   └── EventRequestValidationTests.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddlewareTests.cs
│       ├── Services/
│       │   ├── EventServiceCrudTests.cs
│       │   ├── EventServiceFailureTests.cs
│       │   └── EventServiceQueryTests.cs
│       ├── TestData.cs
│       └── MiddleCsharp.Tests.csproj
├── middle-csharp.http
├── middle-csharp.sln
└── README.md
```
