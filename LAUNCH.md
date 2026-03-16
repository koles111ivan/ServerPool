# Инструкция по запуску

## Быстрый старт

1. Откройте проект в Visual Studio 2022 или VS Code
2. Восстановите зависимости:
   ```bash
   dotnet restore
   ```
3. Запустите проект:
   ```bash
   cd ServerPool.API
   dotnet run
   ```
4. Откройте браузер и перейдите на `https://localhost:5001/swagger` (или http://localhost:5000/swagger)

## Запуск тестов

```bash
dotnet test
```

## Использование SQL Server (опционально)

По умолчанию используется InMemory база данных. Для использования SQL Server:

1. Установите SQL Server
2. Обновите `ServerPool.API/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=ServerPool;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```
3. Примените миграции (если используете EF Migrations):
   ```bash
   dotnet ef migrations add InitialCreate --project ServerPool.Infrastructure --startup-project ServerPool.API
   dotnet ef database update --project ServerPool.Infrastructure --startup-project ServerPool.API
   ```

## Примеры использования API

### 1. Получить все серверы
```
GET /api/servers
```

### 2. Добавить новый сервер
```
POST /api/servers
Content-Type: application/json

{
  "operatingSystem": "Windows Server 2022",
  "memoryGB": 32,
  "diskGB": 500,
  "cpuCores": 8,
  "isOnline": true
}
```

### 3. Поиск свободных серверов
```
POST /api/servers/search
Content-Type: application/json

{
  "operatingSystem": "Windows",
  "minMemoryGB": 16,
  "minDiskGB": 250,
  "minCpuCores": 4
}
```

### 4. Взять сервер в аренду
```
POST /api/servers/allocate
Content-Type: application/json

{
  "serverId": "guid-сервера",
  "allocatedTo": "user1"
}
```

### 5. Освободить сервер
```
POST /api/servers/{id}/release
```

### 6. Проверить готовность сервера
```
GET /api/servers/{id}/ready
```

## Особенности реализации

- **Конкурентные запросы**: Используется `SemaphoreSlim` для каждого сервера, обеспечивая thread-safe операции
- **Автоматическое включение**: Выключенные серверы автоматически включаются через 5 минут при попытке аренды
- **Автоматическое отключение**: Серверы автоматически отключаются через 20 минут после выдачи
- **Логирование**: Все операции логируются в консоль
- **База данных**: Поддержка InMemory (по умолчанию) и SQL Server
