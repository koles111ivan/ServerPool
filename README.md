# Server Pool Management System

Система управления пулом вычислительных серверов на C# и .NET 8.

## Возможности

- ✅ Добавление новых серверов в пул
- ✅ Поиск свободных серверов по параметрам (ОС, память, диск, ядра)
- ✅ Взятие сервера в аренду
- ✅ Освобождение сервера
- ✅ Автоматическое включение выключенных серверов (5 минут)
- ✅ Автоматическое отключение серверов через 20 минут после выдачи
- ✅ Проверка готовности сервера к выдаче
- ✅ Логирование всех операций
- ✅ Хранение данных в базе данных (InMemory или SQL Server)
- ✅ Обработка конкурентных запросов (thread-safe операции)
- ✅ Unit тесты

## Структура проекта

- **ServerPool.Core** - Модели данных и интерфейсы
- **ServerPool.Infrastructure** - Реализация сервисов, Entity Framework, фоновые задачи
- **ServerPool.API** - ASP.NET Core Web API контроллеры
- **ServerPool.Tests** - Unit тесты

## Требования

- .NET 8.0 SDK
- Visual Studio 2022 или VS Code

## Запуск приложения

1. Восстановите зависимости:
```bash
dotnet restore
```

2. Запустите API:
```bash
cd ServerPool.API
dotnet run
```

3. Откройте Swagger UI: `https://localhost:5001/swagger` (или http://localhost:5000/swagger)

## API Endpoints

### GET /api/servers
Получить все серверы

### POST /api/servers
Добавить новый сервер в пул
```json
{
  "operatingSystem": "Windows Server 2022",
  "memoryGB": 32,
  "diskGB": 500,
  "cpuCores": 8,
  "isOnline": true
}
```

### POST /api/servers/search
Поиск свободных серверов
```json
{
  "operatingSystem": "Windows",
  "minMemoryGB": 16,
  "minDiskGB": 250,
  "minCpuCores": 4
}
```

### POST /api/servers/allocate
Взять сервер в аренду
```json
{
  "serverId": "guid",
  "allocatedTo": "user1"
}
```

### POST /api/servers/{id}/release
Освободить сервер

### GET /api/servers/{id}
Получить информацию о сервере

### GET /api/servers/{id}/ready
Проверить готовность сервера к выдаче

## База данных

По умолчанию используется InMemory база данных. Для использования SQL Server:

1. Установите SQL Server
2. Обновите `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ServerPool;Trusted_Connection=True;"
  }
}
```

3. Примените миграции:
```bash
dotnet ef migrations add InitialCreate --project ServerPool.Infrastructure --startup-project ServerPool.API
dotnet ef database update --project ServerPool.Infrastructure --startup-project ServerPool.API
```

## Запуск тестов

```bash
dotnet test
```

## Логирование

Все операции логируются в консоль. Уровень логирования настраивается в `appsettings.json`.

## Обработка конкурентных запросов

Система использует `SemaphoreSlim` для каждого сервера, чтобы обеспечить thread-safe операции при конкурентных запросах на один и тот же сервер.

## Фоновые задачи

- **ServerPowerOnService** - проверяет каждые 30 секунд серверы, которые включаются, и переводит их в статус Available через 5 минут
- **ServerAutoShutdownService** - проверяет каждую минуту выделенные серверы и автоматически отключает их через 20 минут после выдачи
