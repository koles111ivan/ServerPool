# 📚 Подробное руководство по изучению проекта Server Pool Management System

Этот документ поможет вам понять структуру проекта, порядок изучения файлов и назначение каждого компонента.

---

## ⚡ Краткое описание работы приложения

**Server Pool Management System** - это REST API для управления пулом вычислительных серверов.

### Как это работает:

1. **Добавление серверов** - Администратор добавляет серверы в пул через API, указывая характеристики (ОС, память, диск, CPU).

2. **Поиск серверов** - Пользователи ищут свободные серверы по критериям (ОС, минимальная память/диск/CPU).

3. **Аренда сервера** - Пользователь берет сервер в аренду:
   - Если сервер **Available** → сразу выдается
   - Если сервер **Offline** → автоматически запускается включение (5 минут)
   - Если сервер **PoweringOn** → ждет готовности

4. **Автоматические процессы** (фоновые задачи):
   - **Включение**: Серверы в статусе `PoweringOn` автоматически становятся `Available` через 5 минут
   - **Отключение**: Выданные серверы автоматически отключаются через 20 минут после выдачи

5. **Освобождение** - Пользователь может вручную освободить сервер, вернув его в пул.

### Технические детали:

- **Архитектура**: Clean Architecture (Core → Infrastructure → API)
- **База данных**: InMemory (по умолчанию) или SQL Server
- **Thread-safety**: Использует `SemaphoreSlim` для каждого сервера
- **API**: REST API с Swagger документацией
- **Фоновые задачи**: 2 фоновых сервиса работают параллельно с основным приложением

### Статусы сервера:
- **Offline** - выключен
- **PoweringOn** - включается (5 минут)
- **Available** - доступен для аренды
- **Allocated** - выдан пользователю

---

## 🚀 Как работает проект (подробно)

### Общее описание

**Server Pool Management System** - это система управления пулом вычислительных серверов, которая позволяет:
- Добавлять серверы с различными характеристиками в пул
- Искать свободные серверы по заданным критериям
- Выдавать серверы в аренду пользователям
- Автоматически управлять жизненным циклом серверов (включение/выключение)

Система работает как REST API сервис, который принимает HTTP запросы и управляет состоянием серверов в базе данных.

---

### Архитектура системы

Проект построен по принципам **Clean Architecture** и разделен на 4 основных слоя:

```
┌─────────────────────────────────────────┐
│         ServerPool.API                  │  ← Слой представления (REST API)
│  - Контроллеры                          │
│  - Настройка приложения                 │
│  - Swagger документация                 │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│    ServerPool.Infrastructure             │  ← Слой инфраструктуры
│  - Реализация сервисов                  │
│  - Entity Framework (БД)                │
│  - Фоновые задачи                       │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│         ServerPool.Core                 │  ← Доменный слой
│  - Модели данных                        │
│  - Интерфейсы                           │
│  - DTO                                  │
└─────────────────────────────────────────┘
```

**Принцип работы:**
- **API слой** получает HTTP запросы и передает их в **Infrastructure слой**
- **Infrastructure слой** реализует бизнес-логику через интерфейсы из **Core слоя**
- **Core слой** содержит чистые модели и контракты без зависимостей
- Все слои связаны через **Dependency Injection**

---

### Жизненный цикл сервера

Сервер в системе проходит через следующие состояния:

```
┌─────────┐
│ Offline │  ← Сервер выключен
└────┬────┘
     │ Запрос аренды
     ▼
┌─────────────┐
│ PoweringOn │  ← Сервер включается (5 минут)
└────┬───────┘
     │ Через 5 минут (автоматически)
     ▼
┌──────────┐
│Available │  ← Сервер доступен для аренды
└────┬─────┘
     │ Запрос аренды
     ▼
┌───────────┐
│Allocated │  ← Сервер выдан пользователю
└────┬──────┘
     │ Через 20 минут (автоматически) ИЛИ ручное освобождение
     ▼
┌──────────┐
│Available │  ← Возврат в пул
└────┬─────┘
     │ Через 20 минут после выдачи
     ▼
┌─────────┐
│ Offline │  ← Автоматическое отключение
└─────────┘
```

**Статусы сервера:**

1. **Offline** - Сервер выключен
   - Не может быть выдан в аренду
   - При попытке аренды автоматически переходит в `PoweringOn`

2. **PoweringOn** - Сервер включается
   - Процесс включения занимает 5 минут
   - Устанавливается `PowerOnRequestedAt` - время начала включения
   - Фоновая задача `ServerPowerOnService` автоматически переводит в `Available` через 5 минут

3. **Available** - Сервер доступен
   - Готов к выдаче в аренду
   - Может быть найден через поиск
   - Может быть выдан пользователю

4. **Allocated** - Сервер выдан в аренду
   - Установлены `AllocatedAt` (время выдачи) и `AllocatedTo` (пользователь)
   - Не может быть выдан другому пользователю
   - Фоновая задача `ServerAutoShutdownService` автоматически отключает через 20 минут

---

### Основные сценарии работы

#### Сценарий 1: Добавление нового сервера

```
1. Клиент отправляет POST /api/servers
   {
     "operatingSystem": "Windows Server 2022",
     "memoryGB": 32,
     "diskGB": 500,
     "cpuCores": 8,
     "isOnline": true
   }

2. ServersController.AddServer() получает запрос
   ↓
3. Вызывает ServerService.AddServerAsync()
   ↓
4. ServerService создает новый Server:
   - Генерирует уникальный GUID
   - Если isOnline = true → Status = Available
   - Если isOnline = false → Status = PoweringOn, PowerOnRequestedAt = Now
   ↓
5. Сохраняет в БД через ServerPoolDbContext
   ↓
6. Возвращает созданный сервер клиенту (201 Created)
```

#### Сценарий 2: Поиск свободных серверов

```
1. Клиент отправляет POST /api/servers/search
   {
     "operatingSystem": "Windows",
     "minMemoryGB": 16,
     "minDiskGB": 250,
     "minCpuCores": 4
   }

2. ServersController.SearchServers() получает запрос
   ↓
3. Вызывает ServerService.SearchAvailableServersAsync()
   ↓
4. ServerService выполняет запрос к БД:
   - Фильтрует только Available серверы
   - ИЛИ PoweringOn серверы, у которых прошло ≥ 5 минут
   - Применяет фильтры:
     * OperatingSystem.Contains("Windows")
     * MemoryGB >= 16
     * DiskGB >= 250
     * CpuCores >= 4
   ↓
5. Возвращает список подходящих серверов
```

#### Сценарий 3: Аренда доступного сервера

```
1. Клиент отправляет POST /api/servers/allocate
   {
     "serverId": "guid-сервера",
     "allocatedTo": "user1"
   }

2. ServersController.AllocateServer() получает запрос
   ↓
3. Вызывает ServerService.AllocateServerAsync()
   ↓
4. ServerService:
   a) Получает семафор для этого сервера (thread-safety)
   b) Загружает сервер из БД
   c) Проверяет статус:
      - Если Available:
        * Меняет Status → Allocated
        * Устанавливает AllocatedAt = Now
        * Устанавливает AllocatedTo = "user1"
        * Сохраняет в БД
        * Возвращает сервер
      - Если Offline:
        * Меняет Status → PoweringOn
        * Устанавливает PowerOnRequestedAt = Now
        * Сохраняет в БД
        * Возвращает null (сервер еще не готов)
      - Если PoweringOn и прошло < 5 минут:
        * Возвращает null (сервер еще не готов)
      - Если PoweringOn и прошло ≥ 5 минут:
        * Меняет Status → Available → Allocated
        * Устанавливает AllocatedAt и AllocatedTo
        * Сохраняет в БД
        * Возвращает сервер
   d) Освобождает семафор
   ↓
5. Возвращает результат клиенту (200 OK или 404 NotFound)
```

#### Сценарий 4: Аренда выключенного сервера

```
1. Клиент пытается взять в аренду сервер со статусом Offline
   ↓
2. ServerService.AllocateServerAsync() обнаруживает Offline
   ↓
3. Автоматически инициирует включение:
   - Status → PoweringOn
   - PowerOnRequestedAt = DateTime.UtcNow
   - Сохраняет в БД
   - Возвращает null (сервер еще не готов)
   ↓
4. Фоновая задача ServerPowerOnService:
   - Каждые 30 секунд проверяет серверы в PoweringOn
   - Если прошло ≥ 5 минут с PowerOnRequestedAt:
     * Status → Available
     * Сохраняет в БД
   ↓
5. Клиент может проверить готовность через GET /api/servers/{id}/ready
   ↓
6. Когда сервер Available, клиент может повторить запрос аренды
```

#### Сценарий 5: Автоматическое отключение сервера

```
1. Сервер выдан в аренду (Status = Allocated, AllocatedAt = время выдачи)
   ↓
2. Фоновая задача ServerAutoShutdownService:
   - Каждую минуту проверяет все серверы со статусом Allocated
   - Для каждого сервера вычисляет: DateTime.UtcNow - AllocatedAt
   - Если прошло ≥ 20 минут:
     * Status → Offline
     * AllocatedAt = null
     * AllocatedTo = null
     * PowerOnRequestedAt = null
     * Сохраняет в БД
   ↓
3. Сервер теперь Offline и не может быть выдан без включения
```

#### Сценарий 6: Ручное освобождение сервера

```
1. Клиент отправляет POST /api/servers/{id}/release
   ↓
2. ServersController.ReleaseServer() получает запрос
   ↓
3. Вызывает ServerService.ReleaseServerAsync()
   ↓
4. ServerService:
   a) Получает семафор для thread-safety
   b) Загружает сервер из БД
   c) Проверяет что Status = Allocated
   d) Меняет Status → Available
   e) Очищает AllocatedAt и AllocatedTo
   f) Сохраняет в БД
   g) Освобождает семафор
   ↓
5. Возвращает 204 NoContent клиенту
```

---

### Взаимодействие компонентов

#### При запуске приложения:

```
1. Program.cs выполняется
   ↓
2. Регистрируются сервисы в DI контейнере:
   - ServerPoolDbContext (с InMemory или SQL Server)
   - IServerService → ServerService
   - ServerPowerOnService (HostedService)
   - ServerAutoShutdownService (HostedService)
   ↓
3. Настраивается middleware:
   - Swagger (в Development режиме)
   - HTTPS редирект
   - Авторизация
   - Контроллеры
   ↓
4. Создается БД (EnsureCreated)
   ↓
5. Загружаются начальные данные (если БД пустая)
   ↓
6. Запускаются фоновые задачи:
   - ServerPowerOnService (каждые 30 сек)
   - ServerAutoShutdownService (каждую минуту)
   ↓
7. Приложение готово принимать HTTP запросы
```

#### При обработке HTTP запроса:

```
HTTP Request
    ↓
ASP.NET Core Middleware
    ↓
ServersController (API слой)
    ↓
IServerService (интерфейс из Core)
    ↓
ServerService (реализация из Infrastructure)
    ↓
ServerPoolDbContext (Entity Framework)
    ↓
База данных (InMemory или SQL Server)
    ↓
Результат возвращается обратно через все слои
    ↓
HTTP Response (JSON)
```

#### Фоновые задачи работают параллельно:

```
┌─────────────────────────────────────┐
│  HTTP Request Processing             │  ← Основной поток
│  (ServersController)                │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  ServerPowerOnService               │  ← Фоновая задача 1
│  (каждые 30 секунд)                  │
│  - Проверяет PoweringOn серверы     │
│  - Переводит в Available через 5 мин│
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  ServerAutoShutdownService           │  ← Фоновая задача 2
│  (каждую минуту)                     │
│  - Проверяет Allocated серверы      │
│  - Отключает через 20 минут         │
└─────────────────────────────────────┘
```

---

### Thread-Safety и конкурентные запросы

Система использует механизм блокировок для обеспечения корректной работы при одновременных запросах:

**Проблема:** Если два пользователя одновременно пытаются взять один и тот же сервер в аренду, оба могут получить его.

**Решение:** Использование `SemaphoreSlim` для каждого сервера:

```csharp
// ConcurrentDictionary хранит семафор для каждого сервера
private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks;

// При операции с сервером:
var semaphore = GetLock(serverId);  // Получаем семафор для этого сервера
await semaphore.WaitAsync();        // Ждем пока освободится
try {
    // Выполняем операцию (только один поток может быть здесь)
    // ...
} finally {
    semaphore.Release();             // Освобождаем семафор
}
```

**Как это работает:**
- Каждый сервер имеет свой собственный семафор
- Только один поток может выполнять операцию с конкретным сервером одновременно
- Разные серверы могут обрабатываться параллельно (нет глобальной блокировки)
- Это обеспечивает thread-safety без потери производительности

**Пример:**
```
Поток 1: Запрос на аренду Server-A → Получает семафор Server-A → Выполняет операцию
Поток 2: Запрос на аренду Server-A → Ждет освобождения семафора Server-A
Поток 3: Запрос на аренду Server-B → Получает семафор Server-B → Выполняет параллельно с Потоком 1
```

---

### База данных

Система поддерживает два режима работы с БД:

#### 1. InMemory Database (по умолчанию)
- Используется для разработки и тестирования
- Данные хранятся в памяти процесса
- Данные теряются при перезапуске приложения
- Не требует настройки
- Быстрая для разработки

**Настройка:** Оставить `ConnectionStrings.DefaultConnection` пустым в `appsettings.json`

#### 2. SQL Server (для продакшена)
- Постоянное хранилище данных
- Данные сохраняются между перезапусками
- Требует установки SQL Server
- Нужна строка подключения

**Настройка:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ServerPool;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

**Структура таблицы Servers:**
- `Id` (Guid, PK) - Уникальный идентификатор
- `OperatingSystem` (string, required, max 200) - ОС
- `MemoryGB` (int, required) - Память
- `DiskGB` (int, required) - Диск
- `CpuCores` (int, required) - Ядра
- `Status` (enum, required) - Статус
- `AllocatedAt` (DateTime?, nullable) - Время выдачи
- `PowerOnRequestedAt` (DateTime?, nullable) - Время запроса включения
- `AllocatedTo` (string?, nullable, max 200) - Пользователь

---

### Логирование

Все операции в системе логируются через `ILogger`:

**Уровни логирования:**
- **Information** - Обычные операции (добавление, поиск, аренда)
- **Warning** - Предупреждения (сервер недоступен, не найден)
- **Error** - Ошибки (исключения в фоновых задачах)

**Где логируется:**
- `ServerService` - все операции с серверами
- `ServersController` - HTTP запросы (опционально)
- `ServerPowerOnService` - автоматическое включение
- `ServerAutoShutdownService` - автоматическое отключение

**Настройка:** В `appsettings.json` и `appsettings.Development.json`

---

### API Endpoints

Система предоставляет REST API для всех операций:

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/servers` | Получить все серверы |
| POST | `/api/servers` | Добавить новый сервер |
| POST | `/api/servers/search` | Поиск свободных серверов |
| POST | `/api/servers/allocate` | Взять сервер в аренду |
| POST | `/api/servers/{id}/release` | Освободить сервер |
| GET | `/api/servers/{id}` | Получить информацию о сервере |
| GET | `/api/servers/{id}/ready` | Проверить готовность сервера |

**Swagger UI:** Доступен по адресу `/swagger` в режиме разработки

---

### Тестирование

Проект включает unit тесты для проверки корректности работы:

**Тестируется:**
- Бизнес-логика (`ServerService`)
- HTTP обработка (`ServersController`)
- Thread-safety (конкурентные запросы)
- Граничные случаи (офлайн серверы, недоступные серверы)

**Используется:**
- **xunit** - тестовый фреймворк
- **Moq** - мокирование зависимостей
- **FluentAssertions** - читаемые assertions
- **InMemory БД** - изоляция тестов

**Запуск:** `dotnet test`

---

## 🎯 Порядок изучения проекта

Изучайте файлы в следующем порядке для лучшего понимания архитектуры:

### **Этап 1: Основные модели и структуры данных** (ServerPool.Core)
### **Этап 2: Интерфейсы и контракты** (ServerPool.Core)
### **Этап 3: Инфраструктура и реализация** (ServerPool.Infrastructure)
### **Этап 4: API слой** (ServerPool.API)
### **Этап 5: Тесты** (ServerPool.Tests)

---

## 📁 Этап 1: Основные модели и структуры данных

### 1.1. Модель сервера
**Файл:** [`ServerPool.Core/Models/Server.cs`](ServerPool.Core/Models/Server.cs)

**Назначение:** Основная модель данных, представляющая сервер в пуле.

**Содержимое:**
```csharp
public class Server
{
    public Guid Id { get; set; }                    // Уникальный идентификатор сервера
    public string OperatingSystem { get; set; }     // Операционная система (Windows, Linux и т.д.)
    public int MemoryGB { get; set; }               // Объем оперативной памяти в ГБ
    public int DiskGB { get; set; }                 // Объем дискового пространства в ГБ
    public int CpuCores { get; set; }              // Количество ядер процессора
    public ServerStatus Status { get; set; }        // Текущий статус сервера
    public DateTime? AllocatedAt { get; set; }     // Время выдачи сервера в аренду (null если свободен)
    public DateTime? PowerOnRequestedAt { get; set; } // Время запроса на включение (для PoweringOn)
    public string? AllocatedTo { get; set; }        // Имя пользователя, которому выдан сервер
}
```

**Enum ServerStatus:**
```csharp
public enum ServerStatus
{
    Available,      // Сервер включен и доступен для аренды
    Allocated,      // Сервер выдан в аренду пользователю
    PoweringOn,     // Сервер включается (процесс занимает 5 минут)
    Offline         // Сервер выключен
}
```

**Переменные:**
- `Id` - Уникальный идентификатор (GUID), используется для поиска и операций с сервером
- `OperatingSystem` - Название ОС, используется для фильтрации при поиске
- `MemoryGB`, `DiskGB`, `CpuCores` - Характеристики сервера, используются для фильтрации
- `Status` - Текущее состояние сервера, определяет доступность для операций
- `AllocatedAt` - Время выдачи, используется для автоматического отключения через 20 минут
- `PowerOnRequestedAt` - Время запроса включения, используется для проверки готовности (5 минут)
- `AllocatedTo` - Имя пользователя, для отслеживания кому выдан сервер

---

### 1.2. DTO для добавления сервера
**Файл:** [`ServerPool.Core/DTOs/AddServerRequest.cs`](ServerPool.Core/DTOs/AddServerRequest.cs)

**Назначение:** Объект передачи данных для запроса на добавление нового сервера в пул.

**Содержимое:**
```csharp
public class AddServerRequest
{
    public string OperatingSystem { get; set; } = string.Empty;  // ОС сервера
    public int MemoryGB { get; set; }                            // Память в ГБ
    public int DiskGB { get; set; }                              // Диск в ГБ
    public int CpuCores { get; set; }                            // Количество ядер
    public bool IsOnline { get; set; } = true;                   // Онлайн ли сервер при добавлении
}
```

**Переменные:**
- `OperatingSystem` - Название операционной системы
- `MemoryGB` - Объем оперативной памяти
- `DiskGB` - Объем дискового пространства
- `CpuCores` - Количество ядер процессора
- `IsOnline` - Если `true`, сервер создается со статусом `Available`, если `false` - `PoweringOn`

**Использование:** Принимается в POST `/api/servers` для добавления нового сервера.

---

### 1.3. DTO для поиска серверов
**Файл:** [`ServerPool.Core/DTOs/SearchServersRequest.cs`](ServerPool.Core/DTOs/SearchServersRequest.cs)

**Назначение:** Объект передачи данных для запроса поиска свободных серверов по критериям.

**Содержимое:**
```csharp
public class SearchServersRequest
{
    public string? OperatingSystem { get; set; }  // Фильтр по ОС (частичное совпадение)
    public int? MinMemoryGB { get; set; }         // Минимальная память
    public int? MinDiskGB { get; set; }           // Минимальный диск
    public int? MinCpuCores { get; set; }         // Минимальное количество ядер
}
```

**Переменные:**
- `OperatingSystem` - Опциональный фильтр по ОС (используется `Contains` для частичного совпадения)
- `MinMemoryGB` - Минимальный объем памяти (серверы с меньшей памятью исключаются)
- `MinDiskGB` - Минимальный объем диска
- `MinCpuCores` - Минимальное количество ядер

**Использование:** Принимается в POST `/api/servers/search` для поиска подходящих серверов.

---

### 1.4. DTO для аренды сервера
**Файл:** [`ServerPool.Core/DTOs/AllocateServerRequest.cs`](ServerPool.Core/DTOs/AllocateServerRequest.cs)

**Назначение:** Объект передачи данных для запроса на аренду сервера.

**Содержимое:**
```csharp
public class AllocateServerRequest
{
    public Guid ServerId { get; set; }           // ID сервера для аренды
    public string AllocatedTo { get; set; } = string.Empty;  // Имя пользователя
}
```

**Переменные:**
- `ServerId` - Уникальный идентификатор сервера, который нужно взять в аренду
- `AllocatedTo` - Имя пользователя, которому выдается сервер

**Использование:** Принимается в POST `/api/servers/allocate` для взятия сервера в аренду.

---

### 1.5. DTO для ответа API
**Файл:** [`ServerPool.Core/DTOs/ServerResponse.cs`](ServerPool.Core/DTOs/ServerResponse.cs)

**Назначение:** Объект передачи данных для ответа API, содержит информацию о сервере.

**Содержимое:**
```csharp
public class ServerResponse
{
    public Guid Id { get; set; }                    // ID сервера
    public string OperatingSystem { get; set; }     // ОС
    public int MemoryGB { get; set; }               // Память
    public int DiskGB { get; set; }                 // Диск
    public int CpuCores { get; set; }              // Ядра
    public string Status { get; set; }              // Статус (строка)
    public DateTime? AllocatedAt { get; set; }      // Время выдачи
    public string? AllocatedTo { get; set; }        // Пользователь
    public bool IsReady { get; set; }              // Готов ли к выдаче
    public DateTime? EstimatedReadyAt { get; set; } // Примерное время готовности
}
```

**Переменные:**
- `Id` - Идентификатор сервера
- `OperatingSystem`, `MemoryGB`, `DiskGB`, `CpuCores` - Характеристики сервера
- `Status` - Статус в виде строки (для JSON сериализации)
- `AllocatedAt` - Время выдачи в аренду
- `AllocatedTo` - Пользователь, которому выдан сервер
- `IsReady` - `true` если сервер готов к выдаче (Available или PoweringOn прошло 5 минут)
- `EstimatedReadyAt` - Если сервер в статусе PoweringOn, показывает когда он будет готов

**Использование:** Возвращается во всех GET и POST запросах API для представления сервера.

---

## 📁 Этап 2: Интерфейсы и контракты

### 2.1. Интерфейс сервиса серверов
**Файл:** [`ServerPool.Core/Interfaces/IServerService.cs`](ServerPool.Core/Interfaces/IServerService.cs)

**Назначение:** Определяет контракт (интерфейс) для работы с серверами. Содержит все методы, которые должен реализовать сервис.

**Содержимое:**
```csharp
public interface IServerService
{
    Task<Server> AddServerAsync(AddServerRequest request);                    // Добавить сервер
    Task<IEnumerable<Server>> SearchAvailableServersAsync(SearchServersRequest request); // Поиск
    Task<Server?> AllocateServerAsync(Guid serverId, string allocatedTo);     // Аренда
    Task<bool> ReleaseServerAsync(Guid serverId);                             // Освобождение
    Task<Server?> GetServerByIdAsync(Guid serverId);                          // Получить по ID
    Task<bool> IsServerReadyAsync(Guid serverId);                             // Проверка готовности
    Task<IEnumerable<Server>> GetAllServersAsync();                            // Получить все
}
```

**Методы:**
- `AddServerAsync` - Добавляет новый сервер в пул, возвращает созданный сервер
- `SearchAvailableServersAsync` - Ищет свободные серверы по критериям, возвращает список
- `AllocateServerAsync` - Выдает сервер в аренду, возвращает сервер или `null` если недоступен
- `ReleaseServerAsync` - Освобождает сервер из аренды, возвращает `true` при успехе
- `GetServerByIdAsync` - Получает сервер по ID, возвращает `null` если не найден
- `IsServerReadyAsync` - Проверяет готовность сервера к выдаче, возвращает `true/false`
- `GetAllServersAsync` - Возвращает все серверы в пуле

**Использование:** Реализуется в `ServerService` и используется в контроллерах через dependency injection.

---

## 📁 Этап 3: Инфраструктура и реализация

### 3.1. Контекст базы данных
**Файл:** [`ServerPool.Infrastructure/Data/ServerPoolDbContext.cs`](ServerPool.Infrastructure/Data/ServerPoolDbContext.cs)

**Назначение:** Entity Framework контекст для работы с базой данных. Определяет структуру таблиц и связи.

**Содержимое:**
```csharp
public class ServerPoolDbContext : DbContext
{
    public ServerPoolDbContext(DbContextOptions<ServerPoolDbContext> options) : base(options)
    
    public DbSet<Server> Servers { get; set; }  // Таблица серверов
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация модели Server
        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasKey(e => e.Id);                                    // Первичный ключ
            entity.Property(e => e.OperatingSystem).IsRequired().HasMaxLength(200);  // Обязательное, макс 200
            entity.Property(e => e.MemoryGB).IsRequired();              // Обязательное
            entity.Property(e => e.DiskGB).IsRequired();                 // Обязательное
            entity.Property(e => e.CpuCores).IsRequired();               // Обязательное
            entity.Property(e => e.Status).IsRequired();                  // Обязательное
            entity.Property(e => e.AllocatedTo).HasMaxLength(200);       // Опциональное, макс 200
        });
    }
}
```

**Переменные:**
- `Servers` - DbSet для работы с таблицей серверов в БД
- `OnModelCreating` - Метод конфигурации модели, определяет ограничения и правила

**Настройки:**
- `Id` - Первичный ключ (GUID)
- `OperatingSystem` - Обязательное поле, максимум 200 символов
- `MemoryGB`, `DiskGB`, `CpuCores`, `Status` - Обязательные поля
- `AllocatedTo` - Опциональное поле, максимум 200 символов

**Использование:** Используется во всех сервисах для доступа к данным через Entity Framework.

---

### 3.2. Реализация сервиса серверов
**Файл:** [`ServerPool.Infrastructure/Services/ServerService.cs`](ServerPool.Infrastructure/Services/ServerService.cs)

**Назначение:** Основная бизнес-логика приложения. Реализует все операции с серверами.

**Ключевые переменные:**
```csharp
private readonly ServerPoolDbContext _context;  // Контекст БД для работы с данными
private readonly ILogger<ServerService> _logger;  // Логгер для записи операций
private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks;  // Блокировки для thread-safety
```

**Методы:**

#### `GetLock(Guid serverId)`
- **Назначение:** Получает или создает семафор для блокировки операций с конкретным сервером
- **Переменные:**
  - `serverId` - ID сервера для которого нужна блокировка
- **Возвращает:** `SemaphoreSlim` - семафор для синхронизации

#### `AddServerAsync(AddServerRequest request)`
- **Назначение:** Добавляет новый сервер в пул
- **Параметры:**
  - `request` - Данные нового сервера
- **Логика:**
  - Создает новый объект `Server` с уникальным GUID
  - Если `IsOnline = true`, статус `Available`, иначе `PoweringOn` с установкой `PowerOnRequestedAt`
  - Сохраняет в БД
- **Возвращает:** Созданный сервер

#### `SearchAvailableServersAsync(SearchServersRequest request)`
- **Назначение:** Ищет свободные серверы по критериям
- **Параметры:**
  - `request` - Критерии поиска
- **Логика:**
  - Фильтрует только `Available` или `PoweringOn` (если прошло 5 минут)
  - Применяет фильтры по ОС (Contains), памяти, диску, ядрам
- **Возвращает:** Список подходящих серверов

#### `AllocateServerAsync(Guid serverId, string allocatedTo)`
- **Назначение:** Выдает сервер в аренду
- **Параметры:**
  - `serverId` - ID сервера
  - `allocatedTo` - Имя пользователя
- **Логика:**
  - Использует семафор для thread-safety
  - Если сервер `Offline` → переводит в `PoweringOn`, устанавливает `PowerOnRequestedAt`, возвращает `null`
  - Если `PoweringOn` и прошло < 5 минут → возвращает `null`
  - Если `PoweringOn` и прошло ≥ 5 минут → переводит в `Available`
  - Если `Available` → переводит в `Allocated`, устанавливает `AllocatedAt` и `AllocatedTo`
- **Возвращает:** Сервер или `null` если недоступен

#### `ReleaseServerAsync(Guid serverId)`
- **Назначение:** Освобождает сервер из аренды
- **Параметры:**
  - `serverId` - ID сервера
- **Логика:**
  - Использует семафор для thread-safety
  - Проверяет что сервер в статусе `Allocated`
  - Переводит в `Available`, очищает `AllocatedAt` и `AllocatedTo`
- **Возвращает:** `true` при успехе, `false` если сервер не найден или не выделен

#### `GetServerByIdAsync(Guid serverId)`
- **Назначение:** Получает сервер по ID
- **Возвращает:** Сервер или `null`

#### `IsServerReadyAsync(Guid serverId)`
- **Назначение:** Проверяет готовность сервера к выдаче
- **Логика:**
  - Если `Available` → `true`
  - Если `PoweringOn` и прошло ≥ 5 минут → переводит в `Available`, возвращает `true`
  - Иначе → `false`
- **Возвращает:** `true/false`

#### `GetAllServersAsync()`
- **Назначение:** Получает все серверы
- **Возвращает:** Список всех серверов

**Особенности:**
- Использует `ConcurrentDictionary<Guid, SemaphoreSlim>` для thread-safe операций
- Каждый сервер имеет свой семафор, что позволяет параллельно обрабатывать разные серверы
- Все операции логируются через `ILogger`

---

### 3.3. Сервис автоматического включения
**Файл:** [`ServerPool.Infrastructure/Services/ServerPowerOnService.cs`](ServerPool.Infrastructure/Services/ServerPowerOnService.cs)

**Назначение:** Фоновая задача, которая автоматически переводит серверы из статуса `PoweringOn` в `Available` через 5 минут.

**Ключевые переменные:**
```csharp
private readonly IServiceProvider _serviceProvider;  // Провайдер сервисов для создания scope
private readonly ILogger<ServerPowerOnService> _logger;  // Логгер
private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);  // Интервал проверки (30 сек)
private readonly TimeSpan _powerOnDuration = TimeSpan.FromMinutes(5);  // Время включения (5 мин)
```

**Методы:**

#### `ExecuteAsync(CancellationToken stoppingToken)`
- **Назначение:** Основной цикл фоновой задачи
- **Логика:**
  - Бесконечный цикл пока не отменен
  - Каждые 30 секунд вызывает `ProcessPowerOnRequestsAsync()`
  - Обрабатывает исключения и логирует ошибки

#### `ProcessPowerOnRequestsAsync()`
- **Назначение:** Обрабатывает серверы, которые готовы к переводу в `Available`
- **Логика:**
  - Создает scope для получения DbContext
  - Находит серверы со статусом `PoweringOn`, у которых прошло ≥ 5 минут с `PowerOnRequestedAt`
  - Переводит их в статус `Available`
  - Сохраняет изменения в БД
- **Выполняется:** Каждые 30 секунд

**Использование:** Регистрируется как `HostedService` в `Program.cs`, запускается автоматически при старте приложения.

---

### 3.4. Сервис автоматического отключения
**Файл:** [`ServerPool.Infrastructure/Services/ServerAutoShutdownService.cs`](ServerPool.Infrastructure/Services/ServerAutoShutdownService.cs)

**Назначение:** Фоновая задача, которая автоматически отключает серверы через 20 минут после выдачи в аренду.

**Ключевые переменные:**
```csharp
private readonly IServiceProvider _serviceProvider;  // Провайдер сервисов
private readonly ILogger<ServerAutoShutdownService> _logger;  // Логгер
private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);  // Интервал проверки (1 мин)
private readonly TimeSpan _shutdownAfter = TimeSpan.FromMinutes(20);  // Время до отключения (20 мин)
```

**Методы:**

#### `ExecuteAsync(CancellationToken stoppingToken)`
- **Назначение:** Основной цикл фоновой задачи
- **Логика:**
  - Бесконечный цикл пока не отменен
  - Каждую минуту вызывает `CheckAndShutdownServersAsync()`
  - Обрабатывает исключения

#### `CheckAndShutdownServersAsync()`
- **Назначение:** Отключает серверы, которые были выданы более 20 минут назад
- **Логика:**
  - Создает scope для получения DbContext
  - Находит серверы со статусом `Allocated`, у которых прошло ≥ 20 минут с `AllocatedAt`
  - Переводит их в статус `Offline`
  - Очищает `AllocatedAt`, `AllocatedTo`, `PowerOnRequestedAt`
  - Сохраняет изменения
- **Выполняется:** Каждую минуту

**Использование:** Регистрируется как `HostedService` в `Program.cs`, работает параллельно с `ServerPowerOnService`.

---

## 📁 Этап 4: API слой

### 4.1. Точка входа приложения
**Файл:** [`ServerPool.API/Program.cs`](ServerPool.API/Program.cs)

**Назначение:** Главный файл приложения, настраивает все сервисы, middleware и запускает приложение.

**Ключевые части:**

#### Регистрация сервисов:
```csharp
builder.Services.AddControllers();                    // Добавляет контроллеры
builder.Services.AddEndpointsApiExplorer();           // Для Swagger
builder.Services.AddSwaggerGen();                     // Генерация Swagger документации
```

#### Настройка базы данных:
```csharp
builder.Services.AddDbContext<ServerPoolDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        options.UseInMemoryDatabase("ServerPoolDb");  // InMemory если нет строки подключения
    }
    else
    {
        options.UseSqlServer(connectionString);        // SQL Server если есть строка
    }
});
```

#### Регистрация бизнес-сервисов:
```csharp
builder.Services.AddScoped<IServerService, ServerService>();  // Регистрация сервиса
```

#### Регистрация фоновых задач:
```csharp
builder.Services.AddHostedService<ServerAutoShutdownService>();  // Автоотключение
builder.Services.AddHostedService<ServerPowerOnService>();       // Автовключение
```

#### Настройка логирования:
```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();    // Логи в консоль
builder.Logging.AddDebug();      // Логи в Debug output
```

#### Настройка middleware:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // Swagger UI
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();  // Редирект на HTTPS
app.UseAuthorization();      // Авторизация
app.MapControllers();        // Маппинг контроллеров
```

#### Инициализация БД:
```csharp
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ServerPoolDbContext>();
    context.Database.EnsureCreated();  // Создает БД если не существует
    
    await LoadInitialDataAsync(context);  // Загружает тестовые данные
}
```

#### `LoadInitialDataAsync(ServerPoolDbContext context)`
- **Назначение:** Загружает начальные тестовые данные при первом запуске
- **Логика:**
  - Проверяет есть ли серверы в БД
  - Если нет, создает 3 тестовых сервера:
    - Windows Server 2022 (Available)
    - Ubuntu 22.04 (Available)
    - CentOS 8 (PoweringOn)

**Переменные:**
- `builder` - WebApplicationBuilder для настройки приложения
- `app` - WebApplication для настройки middleware

---

### 4.2. Контроллер серверов
**Файл:** [`ServerPool.API/Controllers/ServersController.cs`](ServerPool.API/Controllers/ServersController.cs)

**Назначение:** REST API контроллер, обрабатывает HTTP запросы и вызывает бизнес-логику.

**Ключевые переменные:**
```csharp
private readonly IServerService _serverService;  // Сервис для бизнес-логики
private readonly ILogger<ServersController> _logger;  // Логгер
```

**API Endpoints:**

#### `GET /api/servers` - `GetAllServers()`
- **Назначение:** Получить все серверы
- **Возвращает:** Список всех серверов в формате `ServerResponse`

#### `POST /api/servers` - `AddServer([FromBody] AddServerRequest request)`
- **Назначение:** Добавить новый сервер
- **Параметры:** `request` - данные сервера из тела запроса
- **Валидация:** Проверяет `ModelState.IsValid`
- **Возвращает:** `201 Created` с созданным сервером или `400 BadRequest`

#### `POST /api/servers/search` - `SearchServers([FromBody] SearchServersRequest request)`
- **Назначение:** Поиск свободных серверов
- **Параметры:** `request` - критерии поиска
- **Возвращает:** Список подходящих серверов

#### `POST /api/servers/allocate` - `AllocateServer([FromBody] AllocateServerRequest request)`
- **Назначение:** Взять сервер в аренду
- **Параметры:** `request` - ID сервера и имя пользователя
- **Валидация:** Проверяет что `AllocatedTo` не пустое
- **Возвращает:** `200 OK` с сервером или `404 NotFound` если недоступен

#### `POST /api/servers/{id}/release` - `ReleaseServer(Guid id)`
- **Назначение:** Освободить сервер
- **Параметры:** `id` - ID сервера из URL
- **Возвращает:** `204 NoContent` при успехе или `404 NotFound`

#### `GET /api/servers/{id}` - `GetServerById(Guid id)`
- **Назначение:** Получить информацию о сервере
- **Параметры:** `id` - ID сервера из URL
- **Возвращает:** Сервер или `404 NotFound`

#### `GET /api/servers/{id}/ready` - `IsServerReady(Guid id)`
- **Назначение:** Проверить готовность сервера
- **Параметры:** `id` - ID сервера
- **Возвращает:** JSON объект:
  ```json
  {
    "IsReady": true/false,
    "Status": "Available|Allocated|PoweringOn|Offline",
    "EstimatedReadyAt": "2024-01-01T12:00:00Z" (или null)
  }
  ```

#### `MapToResponse(Server server)` - Приватный метод
- **Назначение:** Преобразует модель `Server` в DTO `ServerResponse`
- **Логика:**
  - Вычисляет `IsReady` (Available или PoweringOn прошло 5 минут)
  - Вычисляет `EstimatedReadyAt` (если PoweringOn, то PowerOnRequestedAt + 5 минут)
  - Маппит все поля

**Атрибуты:**
- `[ApiController]` - Включает автоматическую валидацию и обработку ошибок
- `[Route("api/[controller]")]` - Базовый маршрут `/api/servers`
- `[HttpGet]`, `[HttpPost]` - HTTP методы
- `[FromBody]` - Параметры из тела запроса

---

### 4.3. Конфигурационные файлы

#### `appsettings.json`
**Файл:** [`ServerPool.API/appsettings.json`](ServerPool.API/appsettings.json)

**Назначение:** Основной файл конфигурации приложения.

**Содержимое:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""  // Пустая строка = InMemory БД
  }
}
```

**Переменные:**
- `Logging.LogLevel.Default` - Уровень логирования по умолчанию (Information)
- `Logging.LogLevel.Microsoft.AspNetCore` - Уровень для ASP.NET (Warning)
- `AllowedHosts` - Разрешенные хосты (`*` = все)
- `ConnectionStrings.DefaultConnection` - Строка подключения к БД (пустая = InMemory)

**Для использования SQL Server:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ServerPool;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

---

#### `appsettings.Development.json`
**Файл:** [`ServerPool.API/appsettings.Development.json`](ServerPool.API/appsettings.Development.json)

**Назначение:** Конфигурация для режима разработки (переопределяет `appsettings.json`).

**Содержимое:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ServerPool": "Debug"  // Детальное логирование для нашего приложения
    }
  }
}
```

**Переменные:**
- `Logging.LogLevel.ServerPool` - Уровень логирования для нашего приложения (Debug = очень детально)

---

#### `launchSettings.json`
**Файл:** [`ServerPool.API/Properties/launchSettings.json`](ServerPool.API/Properties/launchSettings.json)

**Назначение:** Настройки запуска приложения (порты, переменные окружения).

**Содержимое:**
```json
{
  "profiles": {
    "ServerPool.API": {
      "commandName": "Project",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "https://localhost:60189;http://localhost:60190"
    }
  }
}
```

**Переменные:**
- `commandName` - Как запускать (`Project` = через dotnet run)
- `launchBrowser` - Открывать ли браузер при запуске
- `ASPNETCORE_ENVIRONMENT` - Режим работы (`Development` = разработка)
- `applicationUrl` - URL для HTTPS и HTTP

---

### 4.4. Файлы проектов (.csproj)

#### `ServerPool.API.csproj`
**Файл:** [`ServerPool.API/ServerPool.API.csproj`](ServerPool.API/ServerPool.API.csproj)

**Назначение:** Конфигурация проекта API, определяет зависимости и настройки компиляции.

**Содержимое:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>  // .NET 8.0
    <Nullable>enable</Nullable>                // Nullable reference types
    <ImplicitUsings>enable</ImplicitUsings>   // Автоматические using
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />  // Swagger
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\ServerPool.Core\ServerPool.Core.csproj" />
    <ProjectReference Include="..\ServerPool.Infrastructure\ServerPool.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

**Зависимости:**
- `Microsoft.EntityFrameworkCore.Design` - Инструменты EF Core (миграции)
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI документация
- Ссылки на проекты `Core` и `Infrastructure`

---

#### `ServerPool.Core.csproj`
**Файл:** [`ServerPool.Core/ServerPool.Core.csproj`](ServerPool.Core/ServerPool.Core.csproj)

**Назначение:** Конфигурация доменного слоя (только модели, без зависимостей).

**Содержимое:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**Особенность:** Нет внешних зависимостей, только чистые модели данных.

---

#### `ServerPool.Infrastructure.csproj`
**Файл:** [`ServerPool.Infrastructure/ServerPool.Infrastructure.csproj`](ServerPool.Infrastructure/ServerPool.Infrastructure.csproj)

**Назначение:** Конфигурация инфраструктурного слоя.

**Содержимое:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\ServerPool.Core\ServerPool.Core.csproj" />
  </ItemGroup>
</Project>
```

**Зависимости:**
- `Microsoft.EntityFrameworkCore` - Основной пакет EF Core
- `Microsoft.EntityFrameworkCore.SqlServer` - Провайдер для SQL Server
- `Microsoft.EntityFrameworkCore.InMemory` - InMemory БД для тестов
- Ссылка на проект `Core`

---

## 📁 Этап 5: Тесты

### 5.1. Тесты сервиса
**Файл:** [`ServerPool.Tests/Services/ServerServiceTests.cs`](ServerPool.Tests/Services/ServerServiceTests.cs)

**Назначение:** Unit тесты для `ServerService`, проверяют бизнес-логику.

**Ключевые переменные:**
```csharp
private readonly ServerPoolDbContext _context;  // InMemory БД для тестов
private readonly IServerService _serverService;  // Тестируемый сервис
private readonly ILogger<ServerService> _logger;  // Логгер
```

**Тесты:**

1. **`AddServerAsync_ShouldAddServerSuccessfully`**
   - Проверяет успешное добавление сервера
   - Проверяет что статус `Available` для онлайн сервера

2. **`AddServerAsync_OfflineServer_ShouldSetStatusToPoweringOn`**
   - Проверяет что офлайн сервер создается со статусом `PoweringOn`
   - Проверяет что `PowerOnRequestedAt` установлен

3. **`SearchAvailableServersAsync_ShouldReturnOnlyAvailableServers`**
   - Проверяет что поиск возвращает только доступные серверы
   - Исключает выделенные серверы

4. **`SearchAvailableServersAsync_ShouldFilterByMemory`**
   - Проверяет фильтрацию по минимальной памяти

5. **`AllocateServerAsync_ShouldAllocateAvailableServer`**
   - Проверяет успешную аренду доступного сервера
   - Проверяет установку статуса, `AllocatedAt`, `AllocatedTo`

6. **`AllocateServerAsync_ShouldNotAllocateAllocatedServer`**
   - Проверяет что выделенный сервер нельзя выделить повторно

7. **`ReleaseServerAsync_ShouldReleaseAllocatedServer`**
   - Проверяет успешное освобождение сервера
   - Проверяет очистку полей

8. **`IsServerReadyAsync_ShouldReturnTrueForAvailableServer`**
   - Проверяет готовность доступного сервера

9. **`AllocateServerAsync_ConcurrentRequests_ShouldHandleCorrectly`**
   - Проверяет thread-safety при конкурентных запросах
   - Симулирует 10 одновременных запросов на один сервер
   - Проверяет что только один успешен

10. **`AllocateServerAsync_OfflineServer_ShouldRequestPowerOn`**
    - Проверяет что офлайн сервер переводится в `PoweringOn`
    - Проверяет установку `PowerOnRequestedAt`

**Использование:**
- `FluentAssertions` - для читаемых assertions
- `xunit` - тестовый фреймворк
- InMemory БД для изоляции тестов

---

### 5.2. Тесты контроллера
**Файл:** [`ServerPool.Tests/Controllers/ServersControllerTests.cs`](ServerPool.Tests/Controllers/ServersControllerTests.cs)

**Назначение:** Unit тесты для `ServersController`, проверяют HTTP обработку.

**Ключевые переменные:**
```csharp
private readonly Mock<IServerService> _mockServerService;  // Мок сервиса
private readonly Mock<ILogger<ServersController>> _mockLogger;  // Мок логгера
private readonly ServersController _controller;  // Тестируемый контроллер
```

**Тесты:**

1. **`AddServer_ShouldReturnCreatedResult`**
   - Проверяет что POST `/api/servers` возвращает `201 Created`
   - Использует мок для `AddServerAsync`

2. **`AllocateServer_ShouldReturnOkResult`**
   - Проверяет что POST `/api/servers/allocate` возвращает `200 OK`
   - Проверяет успешную аренду

3. **`AllocateServer_WhenServerNotFound_ShouldReturnNotFound`**
   - Проверяет что возвращается `404 NotFound` если сервер недоступен

4. **`ReleaseServer_ShouldReturnNoContent`**
   - Проверяет что POST `/api/servers/{id}/release` возвращает `204 NoContent`

5. **`IsServerReady_ShouldReturnOkWithStatus`**
   - Проверяет что GET `/api/servers/{id}/ready` возвращает статус

**Использование:**
- `Moq` - для мокирования зависимостей
- `FluentAssertions` - для assertions
- Тестируется только логика контроллера, бизнес-логика мокируется

---

### 5.3. Файл проекта тестов
**Файл:** [`ServerPool.Tests/ServerPool.Tests.csproj`](ServerPool.Tests/ServerPool.Tests.csproj)

**Назначение:** Конфигурация проекта тестов.

**Содержимое:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>  // Не упаковывать как NuGet пакет
    <IsTestProject>true</IsTestProject>  // Это тестовый проект
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />  // Покрытие кода
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\ServerPool.Core\ServerPool.Core.csproj" />
    <ProjectReference Include="..\ServerPool.Infrastructure\ServerPool.Infrastructure.csproj" />
    <ProjectReference Include="..\ServerPool.API\ServerPool.API.csproj" />
  </ItemGroup>
</Project>
```

**Зависимости:**
- `Microsoft.NET.Test.Sdk` - SDK для тестов
- `xunit` - тестовый фреймворк
- `xunit.runner.visualstudio` - интеграция с Visual Studio
- `coverlet.collector` - сбор данных о покрытии кода
- `Moq` - мокирование
- `FluentAssertions` - assertions
- `Microsoft.EntityFrameworkCore.InMemory` - InMemory БД для тестов

---

## 🔄 Поток выполнения приложения

### 1. Запуск приложения
1. `Program.cs` выполняется при старте
2. Регистрируются все сервисы (DI контейнер)
3. Настраивается БД (InMemory или SQL Server)
4. Запускаются фоновые задачи (`ServerPowerOnService`, `ServerAutoShutdownService`)
5. Создается БД и загружаются начальные данные (если нужно)
6. Приложение готово принимать HTTP запросы

### 2. Обработка HTTP запроса
1. Запрос приходит на контроллер (`ServersController`)
2. Контроллер вызывает `IServerService` (реализован в `ServerService`)
3. `ServerService` использует `ServerPoolDbContext` для работы с БД
4. Результат возвращается через контроллер в виде JSON

### 3. Фоновые задачи
- **ServerPowerOnService**: Каждые 30 секунд проверяет серверы в `PoweringOn`, переводит в `Available` через 5 минут
- **ServerAutoShutdownService**: Каждую минуту проверяет выделенные серверы, отключает через 20 минут

---

## 📝 Резюме

### Порядок изучения:
1. **Модели** (`Server.cs`) - понять структуру данных
2. **DTO** - понять формат запросов/ответов
3. **Интерфейсы** (`IServerService`) - понять контракты
4. **Реализация** (`ServerService`) - понять бизнес-логику
5. **Фоновые задачи** - понять автоматические процессы
6. **Контроллер** - понять API endpoints
7. **Program.cs** - понять настройку приложения
8. **Тесты** - понять как тестируется код

### Ключевые концепции:
- **Clean Architecture** - разделение на слои (Core, Infrastructure, API)
- **Dependency Injection** - все зависимости через конструктор
- **Thread-Safety** - использование `SemaphoreSlim` для конкурентных операций
- **Background Services** - автоматические процессы в фоне
- **Entity Framework** - ORM для работы с БД
- **REST API** - стандартные HTTP методы для операций

---

## 🔗 Быстрые ссылки на файлы

### Модели и DTO:
- [Server.cs](ServerPool.Core/Models/Server.cs)
- [AddServerRequest.cs](ServerPool.Core/DTOs/AddServerRequest.cs)
- [SearchServersRequest.cs](ServerPool.Core/DTOs/SearchServersRequest.cs)
- [AllocateServerRequest.cs](ServerPool.Core/DTOs/AllocateServerRequest.cs)
- [ServerResponse.cs](ServerPool.Core/DTOs/ServerResponse.cs)

### Интерфейсы:
- [IServerService.cs](ServerPool.Core/Interfaces/IServerService.cs)

### Инфраструктура:
- [ServerPoolDbContext.cs](ServerPool.Infrastructure/Data/ServerPoolDbContext.cs)
- [ServerService.cs](ServerPool.Infrastructure/Services/ServerService.cs)
- [ServerPowerOnService.cs](ServerPool.Infrastructure/Services/ServerPowerOnService.cs)
- [ServerAutoShutdownService.cs](ServerPool.Infrastructure/Services/ServerAutoShutdownService.cs)

### API:
- [Program.cs](ServerPool.API/Program.cs)
- [ServersController.cs](ServerPool.API/Controllers/ServersController.cs)
- [appsettings.json](ServerPool.API/appsettings.json)
- [appsettings.Development.json](ServerPool.API/appsettings.Development.json)
- [launchSettings.json](ServerPool.API/Properties/launchSettings.json)

### Тесты:
- [ServerServiceTests.cs](ServerPool.Tests/Services/ServerServiceTests.cs)
- [ServersControllerTests.cs](ServerPool.Tests/Controllers/ServersControllerTests.cs)

### Проекты:
- [ServerPool.API.csproj](ServerPool.API/ServerPool.API.csproj)
- [ServerPool.Core.csproj](ServerPool.Core/ServerPool.Core.csproj)
- [ServerPool.Infrastructure.csproj](ServerPool.Infrastructure/ServerPool.Infrastructure.csproj)
- [ServerPool.Tests.csproj](ServerPool.Tests/ServerPool.Tests.csproj)
