# Middle C# — Events API

Учебный проект: REST API для управления событиями на ASP.NET Core (.NET 9).

## Требования

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Git

## Запуск

```bash
git clone https://github.com/code-begemot/middle-csharp.git
cd middle-csharp
git checkout sprint-1
dotnet run
```

После запуска приложение доступно по адресу из вывода консоли:

- API: `https://localhost:5027`
- Swagger UI: `https://localhost:5027/swagger`

Если при первом запуске появляется предупреждение про HTTPS-сертификат:

```bash
dotnet dev-certs https --trust
```

## API

Базовый путь: `/events`

| Метод | Путь | Описание | Успех | Ошибки |
|---|---|---|---|---|
| GET | `/events` | Список всех событий | `200 OK` | — |
| GET | `/events/{id}` | Событие по id | `200 OK` | `404 Not Found` |
| POST | `/events` | Создать событие | `201 Created` | `400 Bad Request` |
| PUT | `/events/{id}` | Обновить событие целиком | `204 No Content` | `400`, `404` |
| DELETE | `/events/{id}` | Удалить событие | `204 No Content` | `404` |

### Модель `Event`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Meeting",
  "description": "Sprint planning",
  "startAt": "2025-01-10T10:00:00",
  "endAt": "2025-01-10T11:00:00"
}
```

### Правила валидации

- `Title` — обязательное поле;
- `StartAt` — обязательное поле;
- `EndAt` — обязательное поле, должно быть **позже** `StartAt`.

При нарушении возвращается `400 Bad Request` с описанием ошибок в формате `ValidationProblemDetails`.

### Пример запроса на создание события

```bash
curl -X POST https://localhost:5027/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Meeting",
    "description": "Sprint planning",
    "startAt": "2025-01-10T10:00:00",
    "endAt": "2025-01-10T11:00:00"
  }'
```

## Хранилище

На текущем этапе используется in-memory хранилище (`ConcurrentDictionary`). Данные сохраняются только на время работы процесса.

## Структура проекта

```
middle-csharp/
├── Controllers/
│   └── EventsController.cs
├── Models/
│   └── Event.cs
├── Services/
│   ├── IEventService.cs
│   └── EventService.cs
├── Program.cs
└── middle-csharp.csproj
```