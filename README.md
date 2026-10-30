![build](https://github.com/contract-watcher/contract-watcher-sdk/actions/workflows/build.yml/badge.svg)
![release](https://github.com/contract-watcher/contract-watcher-sdk/actions/workflows/release.yml/badge.svg)
[![Codecov](https://codecov.io/gh/contract-watcher/contract-watcher-sdk/branch/main/graph/badge.svg)](https://codecov.io/gh/contract-watcher/contract-watcher-sdk)
[![NuGet](https://img.shields.io/nuget/v/ContractWatcher.SDK.svg)](https://www.nuget.org/packages/ContractWatcher.SDK)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

# ContractWatcher SDK

ContractWatcher SDK подключает .NET-приложение к ContractWatcher: собирает контракты по моделям запросов, публикует их в Core, проверяет входящий JSON и отправляет отчёты.

Контракты, результаты сканирования и отчёты сохраняются локально в MicroDb. Если Core временно недоступен, SDK сохраняет ожидающие операции и повторяет отправку в фоне.

Можно использовать готовую конфигурацию или заменить отдельные компоненты: модули сканирования, источник контрактов, правила проверки, обработчики отчётов и отправителей.

SDK работает в серверном .NET-приложении. Фронтенд продолжает обращаться к вашему API обычными HTTP-запросами.

## Содержание

- [Quick start: ASP.NET Core](#quick-start-aspnet-core)
- [Как выбрать сценарий](#как-выбрать-сценарий)
- [Конфигурация](#конфигурация)
- [Сканирование и публикация контрактов](#сканирование-и-публикация-контрактов)
- [Локальное хранение](#локальное-хранение)
- [Проверка по атрибуту](#проверка-по-атрибуту)
- [Ручной вызов с получением контракта и отправкой отчёта](#ручной-вызов-с-получением-контракта-и-отправкой-отчёта)
- [Локальная проверка через IContractValidator](#локальная-проверка-через-icontractvalidator)
- [Правила и порядок проверки](#правила-и-порядок-проверки)
- [Execution pipeline](#execution-pipeline)
- [Отчёты и их отправка](#отчёты-и-их-отправка)
- [Замена компонентов и время жизни](#замена-компонентов-и-время-жизни)
- [Поведение при ошибках и ограничения](#поведение-при-ошибках-и-ограничения)

## Quick start: ASP.NET Core

### Пакет

Требуется .NET 10:

```bash
dotnet add package ContractWatcher.SDK
```

### Настройки

Укажите адрес Core в `appsettings.json`:

```json
{
  "ContractWatcher": {
    "CoreUrl": "http://localhost:8080",
    "Database": {
      "DirectoryPath": "./data/contract-watcher"
    }
  }
}
```

Ключ интеграции задайте через конфигурацию серверного окружения, например переменную `ContractWatcher__IntegrationKey`.

### Подключение

В `Program.cs` зарегистрируйте SDK, проверку запросов и публикацию контрактов:

```csharp
using System.Text.Json.Serialization;
using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Scanning.Enums;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<ContractWatcherOptions>()
    .Bind(builder.Configuration.GetSection("ContractWatcher"));

builder.Services
    .AddContractWatcher()
    .AddDefaults();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling =
            JsonNumberHandling.Strict;
    })
    .AddContractWatcher()
    .AddContractWatcherScanning(scanning => scanning
        .AddSynchronization(options =>
        {
            options.SourcePreference = ContractSourcePreference.Application;
        })
        .AddPublication());

var app = builder.Build();

app.MapControllers();
app.Run();
```

`AddDefaults()` подключает получение контрактов, локальное хранилище, валидацию и доставку отчётов. `AddContractWatcher()` у MVC включает проверку запросов, а `AddContractWatcherScanning()` — однократный сбор контрактов при запуске.

В этом примере источником правил выступает приложение: `AddPublication()` отправляет собранные контракты в Core.

`JsonNumberHandling.Strict` запрещает числовые строки вроде `"99.90"` для числовых свойств DTO. Это также позволяет представить числовые поля одним типом `Number` в контракте.

### Модель запроса

```csharp
public sealed record CreateProductRequest(
    string Name,
    decimal Price);
```

### Контроллер

```csharp
using ContractWatcher.SDK.AspNetCore.Attributes;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    [HttpPost]
    [ContractWatcher("create-product")]
    public IActionResult Create([FromBody] CreateProductRequest request)
    {
        return Ok(new { accepted = true, name = request.Name });
    }
}
```

При запуске SDK найдёт отмеченный action, построит контракт по `CreateProductRequest` и выполнит первоначальную попытку публикации. Если Core недоступен, доставка продолжится в фоне.

### Запрос

```bash
curl -X POST http://localhost:5000/api/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Keyboard","price":99.90}'
```

Используйте порт своего приложения.

**Атрибут работает в режиме наблюдения:** результат проверки сам по себе не останавливает action и не меняет HTTP-ответ. Если нужно отклонять запросы или возвращать нарушения клиенту, используйте [ручной вызов](#ручной-вызов-с-получением-контракта-и-отправкой-отчёта).

Сканирование запускается после старта приложения и не блокирует приём HTTP-запросов до завершения публикации. Пока опубликованного или сохранённого контракта нет, проверка может вернуть `ContractUnavailable`.

## Как выбрать сценарий

| Сценарий | Что использовать | Откуда берётся контракт | Отправка отчёта |
|---|---|---|---|
| Проверять запросы к MVC actions автоматически | `[ContractWatcher("slug")]` | Через `IContractProvider` | Да, при стандартной конфигурации |
| Проверять вручную и управлять реакцией приложения | `IContractWatcher.ValidateAsync(...)` | Через `IContractProvider` | Да, при стандартной конфигурации |
| Проверить JSON по уже имеющемуся контракту | `IContractValidator.Validate(...)` | Передаётся аргументом | Нет |
| Проверять без подключения к Core | `IContractValidator` либо собственные provider/publisher | Локальный контракт или ваш источник | По вашей конфигурации |

`ContractValidator` — внутренняя реализация SDK. В пользовательском приложении запрашивайте публичный интерфейс `IContractValidator` через DI.

Примеры регистрации ниже — альтернативы или явно обозначенные дополнения к Quick start. Не нужно одновременно копировать все варианты регистрации в один `Program.cs`.

## Конфигурация

### Параметры подключения

| Параметр | По умолчанию | Назначение |
|---|---|---|
| `CoreUrl` | Пустая строка | Базовый HTTP/HTTPS-адрес Core, например `https://core.example.com` |
| `IntegrationKey` | Пустая строка | Ключ интеграции; SDK передаёт его в заголовке `X-Api-Key` |
| `ContractRefreshInterval` | 5 минут | Период фонового обновления уже известных контрактов |

`CoreUrl` должен содержать только базовый адрес: без `/api`, другого пути, query и fragment. Адреса с завершающим `/` допустимы. При `AddCore()` проверяются URL и непустой ключ; при `AddRefreshWorker()` — положительный интервал. В обычном запущенном Host ошибки этих настроек выявляются при старте.

Полный раздел конфигурации может выглядеть так:

```json
{
  "ContractWatcher": {
    "CoreUrl": "https://core.example.com",
    "ContractRefreshInterval": "00:02:00"
  }
}
```

Ключ задавайте через конфигурацию серверного окружения, например переменную `ContractWatcher__IntegrationKey`. В браузер его передавать не нужно.

Вместо binding можно настроить SDK кодом:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher(options =>
    {
        options.CoreUrl = "https://core.example.com";
        options.IntegrationKey = builder.Configuration["ContractWatcher:IntegrationKey"]!;
        options.ContractRefreshInterval = TimeSpan.FromMinutes(2);
    })
    .AddDefaults();
```

### Что подключает AddDefaults

`AddContractWatcher()` регистрирует options, logging, `TimeProvider` и `IContractWatcher`. Полный рабочий набор подключает `AddDefaults()`:

| Метод | Что добавляет |
|---|---|
| `AddCore()` | HTTP-клиент Core и проверку настроек подключения |
| `AddCoreContracts()` | In-memory store, provider, refresher и фоновое обновление |
| `AddDefaultValidation()` | Валидатор и правила Required, Nullable, Type |
| `AddDefaultReporting()` | Стандартную сборку отчёта; отправитель отдельно не подключается |
| `AddReporting(r => r.UseCorePublisher())` | Отправителя отчётов в Core |
| `AddDefaultExecution()` | Стандартную последовательность middleware |

Развёрнутый эквивалент `AddDefaults()`:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddCore()
    .AddCoreContracts()
    .AddDefaultValidation()
    .AddDefaultReporting()
    .AddReporting(reporting => reporting.UseCorePublisher())
    .AddDefaultExecution();
```

Настройки в этом варианте должны быть привязаны через options, как в Quick start. MVC-фильтр по-прежнему подключается отдельно.

### Таймаут HTTP-клиента

После `AddDefaults()` можно дополнить конфигурацию типизированного клиента:

```csharp
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Interfaces;

builder.Services
    .AddHttpClient<IContractWatcherCoreClient, ContractWatcherCoreClient>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));
```

Этот таймаут относится и к получению контракта, и к публикации отчёта. Готовые автоматические retry и очередь отчётов SDK не подключает.
## Сканирование и публикация контрактов

Сканирование выполняется один раз при запуске процесса. SDK использует подготовленные метаданные MVC: находит actions с `[ContractWatcher]` и определяет модель, получаемую из тела запроса.

У отмеченного action должен быть ровно один body-параметр. Slug связывает endpoint с контрактом; разные actions не могут использовать один slug.

### Этапы

| Этап | Назначение |
|---|---|
| Discovery | Найти отмеченные actions, модели запросов и маршруты |
| Synchronization | Получить опубликованные контракты из Core с возможностью использовать локальную копию при ошибке |
| Schema | Построить JSON Schema по моделям запросов |
| Mapping | Преобразовать схемы в правила и определить изменения |
| Persistence | Сохранить результаты сканирования и ожидающие публикации в MicroDb |
| Publication | Выполнить первоначальную попытку доставки контрактов |

Стандартная цепочка включает этапы до Persistence. Публикация подключается отдельно через `AddPublication()`.

Модули обмениваются данными через `ScanningContext`. Последовательность определяется порядком регистрации, а `ScanningStage` обозначает зону ответственности модуля и используется при обработке ошибок.

### Источник правил

`SourcePreference` определяет, нужно ли публиковать изменения, найденные в приложении:

| Значение | Поведение |
|---|---|
| `Published` | Использовать опубликованные контракты; автоматически не отправлять правила из DTO |
| `Application` | Подготавливать изменения из DTO к публикации |

По умолчанию используется `Published`. Для автоматической публикации нужны одновременно `Application` и `AddPublication()`.

### Версии

SDK рассчитывает предварительную версию контракта, но окончательную версию получает из ответа Core.

Перед отправкой SDK сверяет актуальный контракт с Core. Если правила уже совпадают, повторная публикация не требуется. Порядок правил не считается изменением; сравниваются имя поля, тип, обязательность и допустимость null.

При публикации SDK отправляет POST на `/api/sdk/contracts`. Core возвращает `PublishedContract`:

- `201 Created` — создан контракт или новая версия;
- `200 OK` — правила совпадают с уже опубликованными;
- `409 Conflict` — конфликт параллельных изменений, допускающий повторную попытку.

SDK проверяет ответ и сохраняет версию Core вместо предварительной. Например, если SDK рассчитал версию `2`, а Core вернул `12`, локально сохраняется `12`. Последующие проверки используют подтверждённый контракт и его версию.

`202 Accepted` и ответ без опубликованного контракта не считаются подтверждением сохранения.

### Повторы модулей сканирования

Если модуль завершился ошибкой, повторяется этот модуль с тем же контекстом. Успешно завершённые предыдущие модули повторно не запускаются.

```csharp
.AddContractWatcherScanning(scanning => scanning
    .ConfigureRetry(options =>
    {
        options.MaxAttempts = 5;
        options.Delay = TimeSpan.FromSeconds(2);
    })
    .AddSynchronization(options =>
    {
        options.SourcePreference = ContractSourcePreference.Application;
    })
    .AddPublication())
```

`MaxAttempts` включает первоначальную попытку. После исчерпания попыток ошибка записывается в лог; scanning worker не останавливает приложение.

Контекст незавершённого сканирования не сохраняется между процессами. При следующем запуске сканирование начинается заново.

### Фоновая доставка контрактов

Повторы публикации работают отдельно от повторов модулей сканирования. Результат сканирования уже сохранён, поэтому worker продолжает доставку без повторного поиска endpoints и генерации схем.

```csharp
.AddPublication(options =>
{
    options.MaxAttempts = 5;
    options.InitialDelay = TimeSpan.FromSeconds(2);
    options.MaxDelay = TimeSpan.FromMinutes(1);
    options.PollInterval = TimeSpan.FromSeconds(2);
    options.BatchSize = 100;
})
```

В примере указаны значения по умолчанию.

`MaxAttempts` ограничивает число неудач одного этапа доставки; `0` отключает ограничение. При переходе к следующему этапу счётчик сбрасывается. Задержка повторов растёт экспоненциально до `MaxDelay`.

Состояние доставки сохраняется в MicroDb:

- если не удалось получить или опубликовать контракт, повтор продолжается с соответствующего этапа;
- перед повторным POST выполняется сверка с Core: предыдущий запрос мог сохраниться, даже если ответ потерялся;
- если подтверждение Core уже записано локально, но обновление хранилища контрактов завершилось ошибкой, повтор продолжается с локального сохранения.

После постоянной ошибки или исчерпания попыток автоматическая доставка останавливается. Запись остаётся в базе. Перезапуск с неизменившимися правилами сам по себе не сбрасывает этот лимит.

### Замена модулей

Для замены отдельного этапа используйте `ReplaceModule<TCurrent, TReplacement>()`: новый модуль займёт позицию старого.

`ClearModules()` позволяет собрать последовательность заново. При этом необходимо сохранить зависимости этапов: mapping требует результаты синхронизации и схемы, а публикация — сохранённые результаты mapping.

### Ограничения схем

Сейчас контракт описывает поля верхнего уровня:

- имя;
- тип;
- обязательность;
- допустимость null.

Для вложенных объектов и массивов сохраняются эти признаки, но правила их внутренней структуры не строятся.

Обязательность и nullable берутся из сгенерированной схемы, а не выводятся только из CLR-типа свойства. Составные схемы и объединения типов, которые нельзя представить текущей моделью правил, отклоняются.

Сканирование предназначено для MVC controllers. Сбор контрактов из Minimal API, Swagger или GraphQL не реализован.

## Локальное хранение

Стандартная конфигурация использует MicroDb. База хранит:

| Набор | Содержимое |
|---|---|
| Contracts | Опубликованные контракты для проверки запросов |
| ScannedContracts | Результаты сканирования и состояние ожидающих публикаций |
| ContractRetries | Этапы и расписание повторной доставки контрактов |
| Reports | Отчёты о проверках и состояние их доставки |
| ReportRetries | Расписание повторной отправки отчётов |

Результат сканирования хранится отдельно от подтверждённого контракта. Неуспешная публикация новых правил не подменяет ими последнюю опубликованную версию.

Путь задаётся через `ContractWatcherOptions.Database.DirectoryPath`. По умолчанию используется `data/contract-watcher` внутри `AppContext.BaseDirectory`.

Сохраняйте эту директорию между перезапусками. В контейнере подключите постоянный volume. Директория должна принадлежать одной интеграции и одному процессу-писателю; несколько экземпляров приложения не должны совместно записывать в неё.

Успешно отправленные отчёты остаются в Reports, а завершённые retry-записи удаляются. Автоматического удаления истории отчётов по сроку хранения нет.

## Проверка по атрибуту

`[ContractWatcher("create-product")]` применяется к методу контроллера. Глобальный resource filter находит атрибут в metadata выбранного endpoint, читает исходное тело запроса до model binding и вызывает `IContractWatcher`.

После проверки позиция потока сбрасывается, поэтому MVC может прочитать тело запроса обычным способом.

Особенности этого режима:

- actions без атрибута пропускаются;
- проверяется входящий JSON, а не HTTP-ответ;
- нарушения контракта не превращаются автоматически в HTTP 400;
- результат `ContractWatchResult` не добавляется в ответ клиенту;
- при синтаксически некорректном JSON фильтр пишет warning и передаёт запрос дальше; MVC может отдельно вернуть 400;
- фильтр ожидает JSON: используйте его для подходящих actions, а не для multipart/form-data;
- это интеграция с MVC controllers; отдельного подключения атрибута к Minimal API в текущем SDK нет.

Термин «проверка по атрибуту» здесь означает обработку запросов к помеченному action. Сканирование DTO, генерация схемы и её публикация в Core этим атрибутом не выполняются.

## Ручной вызов с получением контракта и отправкой отчёта

Если приложение должно принимать решение по результату проверки, внедрите `IContractWatcher`. Регистрация SDK остаётся такой же, как в Quick start.

Следующий контроллер задаёт собственную HTTP-политику: 200 при успешной проверке, 422 при нарушениях и 503, когда проверить запрос надёжно не удалось.

```csharp
using System.Text.Json;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/products/check")]
public sealed class ProductChecksController(IContractWatcher watcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Check(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var result = await watcher.ValidateAsync(
            "create-product", payload, cancellationToken);

        var response = new
        {
            status = result.Status.ToString(),
            violations = result.Violations.Select(violation => new
            {
                fieldName = violation.FieldName,
                type = violation.Type.ToString(),
                expectedType = violation.ExpectedType?.ToString(),
                actualType = violation.ActualType?.ToString()
            }).ToArray()
        };

        return result.Status switch
        {
            WatcherStatus.Passed => Ok(response),
            WatcherStatus.Failed => UnprocessableEntity(response),
            WatcherStatus.ContractUnavailable => StatusCode(503, response),
            WatcherStatus.TechnicalError => StatusCode(503, response),
            _ => StatusCode(500, response)
        };
    }
}
```

Не добавляйте к этому action ещё и `[ContractWatcher]`: иначе проверка и отправка отчёта выполнятся дважды. Endpoint выше только проверяет данные; создание продукта нужно реализовать отдельно.

### Результат проверки

| `WatcherStatus` | Значение |
|---|---|
| `Passed` | Валидация прошла, стандартный pipeline завершился успешно |
| `Failed` | Найдены нарушения контракта |
| `ContractUnavailable` | Контракт не найден или его не удалось получить из Core из-за обработанной ошибки запроса |
| `TechnicalError` | В стандартном pipeline возникла другая ошибка, в том числе при отправке отчёта |
| `Pending` | Начальное состояние; возможно, пользовательский pipeline не выполнил проверку или не назначил статус |

Проверяйте `Status`, а не только пустоту `Violations`. При недоступном контракте список нарушений тоже пуст, но проверка не считается успешной.

Статус относится ко всему выполнению: если проверка завершилась, но публикация отчёта упала, итог может быть `TechnicalError`, а найденные нарушения останутся в `Violations`.

## Локальная проверка через IContractValidator

Этот вариант подходит, когда контракт уже есть в приложении и нужны только нарушения. HTTP-клиент, кеш, worker и отправитель отчётов не требуются.

### 1. Опишите контракт

Создайте `ProductContract.cs`:

```csharp
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;

public static class ProductContract
{
    public static PublishedContract Create() => new()
    {
        Slug = "create-product",
        ContractVersion = 1,
        Rules =
        [
            new ContractRule
            {
                FieldName = "name",
                Type = ContractFieldType.String,
                Required = true,
                Nullable = false
            },
            new ContractRule
            {
                FieldName = "price",
                Type = ContractFieldType.Number,
                Required = true,
                Nullable = false
            }
        ]
    };
}
```

### 2. Подключите валидацию и выполните проверку

Например, в консольном приложении с подключённым пакетом:

```csharp
using System.Text.Json;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Validation.Interfaces;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddContractWatcher().AddDefaultValidation();

using var provider = services.BuildServiceProvider();
var validator = provider.GetRequiredService<IContractValidator>();

using var document = JsonDocument.Parse(
    """{"name":"Keyboard","price":"99.90"}""");

var result = validator.Validate(document.RootElement, ProductContract.Create());

Console.WriteLine($"Valid: {result.IsValid}");

foreach (var violation in result.Violations)
    Console.WriteLine($"{violation.FieldName}: {violation.Type}");
```

Результат — `IsValid == false` и `TypeMismatch` для `price`: строка с числом не является JSON-числом.

`Validate` выполняется синхронно и не отправляет отчёт. Корень JSON должен быть объектом; массив, строка или `null` в корне приводят к `ArgumentException`.

В веб-приложении регистрируйте валидацию через `builder.Services`, а `IContractValidator` внедряйте в контроллер или сервис. Создавать отдельный контейнер DI в каждом запросе не нужно.

### Режим наблюдения

Для action из Quick start запрос остаётся обычным:

```javascript
const response = await fetch('/api/products', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ name: 'Keyboard', price: 99.90 })
});

if (!response.ok) {
  throw new Error(`API returned HTTP ${response.status}`);
}

const productResponse = await response.json();
```

HTTP 200 здесь означает успешный ответ вашего action. Он не сообщает, прошла ли проверка контракта: атрибут отправляет результат в Core, но не включает его в HTTP-ответ.

### Обновление по требованию

При стандартной регистрации можно принудительно обновить один контракт:

```csharp
using ContractWatcher.SDK.Contracts.Interfaces;

public sealed class ContractUpdater(IContractRefresher refresher)
{
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        refresher.RefreshAsync("create-product", cancellationToken);
}
```

Для прямой работы с кешем доступны `IContractStore.Get`, `Set`, `Remove`, `Clear` и `GetSlugs`. Например, `Remove("create-product")` заставит следующий стандартный запрос provider снова обратиться к Core.

Если Core возвращает 404 при обновлении, текущий refresher возвращает `null`, но ранее сохранённый контракт из store не удаляет. Ошибка обновления тоже не очищает кеш. Это важно, если приложение должно строго прекращать работу с удалёнными или устаревшими контрактами: такую политику нужно реализовать отдельно.

### Свой источник контрактов

Для полного локального сценария можно использовать provider, который читает только store:

```csharp
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;

public sealed class LocalContractProvider(IContractStore store) : IContractProvider
{
    public Task<PublishedContract?> GetAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(store.Get(slug));
    }
}
```

Регистрация без Core с логированием отчётов (`LoggingReportPublisher` приведён ниже):

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddContracts(contracts => contracts
        .UseInMemoryStore()
        .UseProvider<LocalContractProvider>())
    .AddDefaultValidation()
    .AddDefaultReporting()
    .AddReporting(reporting => reporting.UsePublisher<LoggingReportPublisher>())
    .AddDefaultExecution();
```

После `builder.Build()` заполните store контрактом из локального примера, до `app.Run()`:

```csharp
using ContractWatcher.SDK.Contracts.Interfaces;

app.Services.GetRequiredService<IContractStore>().Set(ProductContract.Create());
```

Здесь нет refresh worker и обращений к Core. Для неизвестного slug стандартный execution pipeline вернёт `ContractUnavailable`.

### Обновление без фонового worker

Если обновлением управляет приложение, соберите contracts-модуль явно:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddCore()
    .AddContracts(contracts => contracts
        .UseInMemoryStore()
        .UseCoreRefresher()
        .UseDefaultProvider())
    .AddDefaultValidation()
    .AddDefaultReporting()
    .AddReporting(reporting => reporting.UseCorePublisher())
    .AddDefaultExecution();
```

При пустом кеше контракт всё ещё загружается по запросу. Периодического обновления нет, потому что не вызван `AddRefreshWorker()`.

## Правила и порядок проверки

Для каждого поля из `PublishedContract.Rules` валидатор находит одноимённое свойство JSON и запускает зарегистрированные `IValidationRule` в порядке регистрации. У каждого правила сначала вызывается `CanValidate`; если оно возвращает `true`, вызывается `Validate`.

Стандартный набор:

| Правило | Когда проверяет | Нарушение |
|---|---|---|
| Required | Поле обязательное, но отсутствует | `RequiredFieldMissing` |
| Nullable | Поле присутствует со значением `null`, а `Nullable = false` | `NullNotAllowed` |
| Type | Поле присутствует и не равно `null`, но тип не совпадает | `TypeMismatch` |

Поддерживаются типы `String`, `Number`, `Boolean`, `Object`, `Array`. `Required` проверяет присутствие свойства, а не непустоту строки: `""` и `" "` по умолчанию допустимы. Явный `null` обрабатывается отдельно от отсутствующего поля.

### Добавление своего правила

Например, договоримся считать пустую или пробельную строку незаполненным обязательным строковым полем:

```csharp
using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

public sealed class NonBlankRequiredStringRule : IValidationRule
{
    public bool CanValidate(ValidationContext context) =>
        context.Rule.Required &&
        context.Rule.Type == ContractFieldType.String &&
        context.Value is { ValueKind: JsonValueKind.String };

    public ValidationViolation? Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Value!.Value.GetString()))
            return null;

        return new ValidationViolation
        {
            FieldName = context.Rule.FieldName,
            Type = ViolationType.RequiredFieldMissing,
            ExpectedType = ContractFieldType.String,
            ActualType = ContractFieldType.String
        };
    }
}
```

Это пользовательская политика: она намеренно относит пустую строку к `RequiredFieldMissing`. В текущем Common нет отдельного типа нарушения для пустой строки; собственное правило должно использовать доступную модель `ValidationViolation`.

Добавить правило после стандартных можно после `AddDefaults()`:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddValidation(validation => validation.Configure(pipeline =>
        pipeline.AddRule<NonBlankRequiredStringRule>()));
```

### Изменение порядка

Чтобы своё правило выполнялось перед стандартными, пересоберите список:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddValidation(validation => validation.Configure(pipeline => pipeline
        .ClearRules()
        .AddRule<NonBlankRequiredStringRule>()
        .UseDefaultRules()));
```

`ClearRules()` удаляет все ранее зарегистрированные `IValidationRule`. Если не вызвать `UseDefaultRules()`, останутся только ваши правила. Повторный `AddRule<T>()` для того же типа не добавляет второй экземпляр регистрации и не перемещает существующую регистрацию.

Встроенные `RequiredRule`, `NullableRule`, `TypeRule` — `internal`. Публичный builder позволяет добавлять стандартный набор целиком, располагать свои правила до/после него или полностью заменять набор. Поимённо переставить встроенные классы через `AddRule<RequiredRule>()` из пользовательского проекта нельзя.

### Остановка на первом нарушении поля

Стандартный pipeline собирает все нарушения. Если нужно останавливаться на первой ошибке каждого поля, замените `IValidationPipeline`:

```csharp
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

public sealed class FirstViolationPipeline(IEnumerable<IValidationRule> rules)
    : IValidationPipeline
{
    private readonly IValidationRule[] _rules = rules.ToArray();

    public IReadOnlyCollection<ValidationViolation> Validate(ValidationContext context)
    {
        foreach (var rule in _rules)
        {
            if (!rule.CanValidate(context))
                continue;

            var violation = rule.Validate(context);
            if (violation is not null)
                return [violation];
        }

        return [];
    }
}
```

Подключение после стандартной регистрации:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddValidation(validation => validation.UsePipeline<FirstViolationPipeline>());
```

Остальные поля контракта валидатор продолжит проверять. Чтобы остановить проверку всего документа на первом нарушении, нужна своя реализация `IContractValidator`, подключаемая через `UseValidator<TValidator>()`.

## Execution pipeline

Execution pipeline управляет полным сценарием `IContractWatcher`: получением контракта, валидацией и отчётом. Его middleware вызываются в порядке регистрации, а код после `await next(...)` выполняется в обратном порядке при возвращении из цепочки.

| Порядок | Стандартный middleware | Назначение |
|---|---|---|
| 1 | TechnicalError | Перехватывает технические ошибки следующих middleware |
| 2 | ContractResolution | Получает контракт; прекращает цепочку при его недоступности |
| 3 | Validation | Проверяет JSON и назначает `Passed` или `Failed` |
| 4 | Report | Формирует отчёт |
| 5 | ReportPublishing | Отправляет отчёт и ожидает завершения отправки |

### Свой middleware

Например, измерим длительность полного выполнения:

```csharp
using System.Diagnostics;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using Microsoft.Extensions.Logging;

public sealed class TimingMiddleware(ILogger<TimingMiddleware> logger)
    : IWatcherMiddleware
{
    public async Task InvokeAsync(
        WatcherContext context,
        WatcherDelegate next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context, cancellationToken);
        }
        finally
        {
            logger.LogInformation(
                "Contract {Slug}: {Status}, elapsed {ElapsedMs} ms",
                context.ContractSlug,
                context.Status,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
```

Чтобы middleware оборачивал весь стандартный pipeline, поставьте его перед готовым набором:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddExecution(execution => execution.Configure(pipeline => pipeline
        .ClearMiddlewares()
        .AddMiddleware<TimingMiddleware>()
        .UseDefaultMiddlewares()));
```

Если просто вызвать `AddMiddleware<TimingMiddleware>()` после `AddDefaults()`, он окажется в конце цепочки и не измерит уже завершённую валидацию и отправку. Кроме того, конец цепочки не вызывается при раннем выходе, например при недоступном контракте.

Ваш внешний middleware находится вне стандартного TechnicalError middleware: исключение, возникшее в его собственном коде, не будет поймано внутренним обработчиком.

### Полная замена цепочки

`ClearMiddlewares()` удаляет все регистрации `IWatcherMiddleware`; затем `AddMiddleware<T>()` задаёт порядок ваших реализаций. Как и с правилами, повторная регистрация того же типа не создаёт второй вызов.

Встроенные middleware — `internal`. Их набор можно вернуть через `UseDefaultMiddlewares()`, но публичных методов для вставки между конкретными встроенными стадиями или перестановки этих классов нет. Для полностью своей последовательности реализуйте собственные middleware либо `IExecutionPipeline` и подключите его через `execution.UsePipeline<TPipeline>()`.

Если вы сохраняете стандартный смысл стадий, контракт должен появиться до валидации, результат валидации — до сборки отчёта, а отчёт — до отправки. Пустой pipeline не проверяет данные и оставляет `Pending`.

## Отчёты и их отправка

`AddDefaultReporting()` подключает формирование отчётов. `UseCorePublisher()` добавляет сохранение в MicroDb, первоначальную отправку и фоновый retry worker. Оба компонента уже подключены через `AddDefaults()`.

Перед HTTP-запросом отчёт сохраняется в Reports. При временной ошибке SDK оставляет его для повторной отправки и сохраняет расписание в ReportRetries. После перезапуска worker продолжает обработку сохранённых отчётов.

Временная недоступность Core сама по себе не превращает результат проверки в `TechnicalError`, если отчёт удалось сохранить для повторной отправки. Постоянные ошибки доставки и ошибки локального хранения передаются вызывающему pipeline.

Стандартный отчёт содержит `ReportId`, `Slug`, `ContractVersion`, `Status` и `Violations`. Исходный JSON и заголовки пользовательского запроса в него не включаются.

При повторах используется тот же `ReportId`. Если Core сохранил отчёт, но ответ потерялся, повторная отправка возможна; принимающая сторона должна учитывать повторные запросы.

### Настройка повторов

После `AddDefaults()` можно изменить настройки стандартного отправителя:

```csharp
builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddReporting(reporting => reporting.UseCorePublisher(options =>
    {
        options.PollInterval = TimeSpan.FromSeconds(5);
        options.InitialDelay = TimeSpan.FromSeconds(10);
        options.MaxDelay = TimeSpan.FromMinutes(10);
        options.MaxAttempts = 0;
        options.BatchSize = 100;
    }));
```

Показаны значения по умолчанию. `MaxAttempts = 0` означает отсутствие ограничения числа неудач. Задержка увеличивается экспоненциально со случайной добавкой, не превышая `MaxDelay`.

При постоянной ошибке или исчерпании лимита отчёт получает состояние Failed и перестаёт автоматически отправляться.

### Свой отправитель

Для локальной разработки можно записывать результат в лог:

```csharp
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Reporting.Interfaces;
using Microsoft.Extensions.Logging;

public sealed class LoggingReportPublisher(ILogger<LoggingReportPublisher> logger)
    : IReportPublisher
{
    public Task PublishAsync(
        ValidationReport report,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Report {ReportId}: contract {Slug} v{Version}, status {Status}, violations {Count}",
            report.ReportId,
            report.Slug,
            report.ContractVersion,
            report.Status,
            report.Violations.Count);

        return Task.CompletedTask;
    }
}
```

Замените только отправку, сохранив получение контрактов из Core:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddDefaults()
    .AddReporting(reporting => reporting.UsePublisher<LoggingReportPublisher>());
```

Контракты в таком варианте всё ещё загружаются из Core, но отчёты туда не отправляются. Для очереди или другого транспорта реализуйте тот же `IReportPublisher`.

### Обработчики сборки отчёта

`IReportBuildHandler` работает с `ReportBuildContext` и вызывает следующий обработчик через `ReportBuildDelegate`. Стандартный порядок: metadata, status, violations.

У стандартного обработчика status есть ранний выход: для успешной валидации он не вызывает следующий handler. Поэтому обработчик, просто добавленный в конец после `AddDefaultReporting()`, не выполнится для успешного результата.

Чтобы наблюдать оба исхода, поставьте свой handler перед стандартными и выполняйте нужную работу после `next`:

```csharp
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Models;
using Microsoft.Extensions.Logging;

public sealed class ReportAuditHandler(ILogger<ReportAuditHandler> logger)
    : IReportBuildHandler
{
    public void Handle(ReportBuildContext context, ReportBuildDelegate next)
    {
        next(context);

        logger.LogInformation(
            "Built report {ReportId} for {Slug}: {Status}",
            context.ReportId, context.Contract.Slug, context.Status);
    }
}
```

Для этого сценария соберите конфигурацию явно, вместо `AddDefaults()`:

```csharp
using ContractWatcher.SDK.Configuration.Extensions;

builder.Services
    .AddContractWatcher()
    .AddCore()
    .AddCoreContracts()
    .AddDefaultValidation()
    .AddReporting(reporting => reporting
        .AddBuildHandler<ReportAuditHandler>()
        .AddDefaultReportBuilder()
        .UseCorePublisher())
    .AddDefaultExecution();
```

Если стандартные handlers уже добавлены, повторный `AddDefaultReportBuilder()` не переставит их. У reporting builder нет отдельного `ClearHandlers()`: для полного пересоздания списка можно использовать `reporting.Services.RemoveAll<IReportBuildHandler>()` из `Microsoft.Extensions.DependencyInjection.Extensions`, затем зарегистрировать нужные handlers. Самостоятельный builder отчёта подключается через `UseReportBuilder<TBuilder>()`.

## Замена компонентов и время жизни

Методы `Use...<T>()` заменяют регистрацию соответствующего интерфейса. Вызывайте свои замены после подключения defaults. Модульные builders и корневой `ContractWatcherBuilder` также предоставляют `Services` для стандартной настройки DI.

| Компонент | Метод | Lifetime по умолчанию |
|---|---|---|
| `IContractStore` | `contracts.UseStore<T>()` | Singleton |
| `IContractProvider` | `contracts.UseProvider<T>()` | Transient |
| `IContractRefresher` | `contracts.UseRefresher<T>()` | Transient |
| `IValidationPipeline` | `validation.UsePipeline<T>()` | Transient |
| `IContractValidator` | `validation.UseValidator<T>()` | Transient |
| `IExecutionPipeline` | `execution.UsePipeline<T>()` | Transient |
| `IValidationReportBuilder` | `reporting.UseReportBuilder<T>()` | Transient |
| `IReportPublisher` | `reporting.UsePublisher<T>()` | Scoped |
| `IReportBuildHandler` | `reporting.AddBuildHandler<T>()` | Transient |
| `IValidationRule` | `pipeline.AddRule<T>()` | Transient, без параметра lifetime |
| `IWatcherMiddleware` | `pipeline.AddMiddleware<T>()` | Transient, без параметра lifetime |

Для методов с параметром lifetime его можно указать явно, например `reporting.UsePublisher<LoggingReportPublisher>(ServiceLifetime.Transient)`.

`IContractWatcher` зарегистрирован как transient, MVC resource filter — как scoped. При стандартном scoped publisher запрашивайте watcher внутри scope: HTTP-запрос MVC уже предоставляет его. В собственном `BackgroundService` создавайте scope через `IServiceScopeFactory`, а затем разрешайте watcher внутри него.

Refresh worker напрямую получает store и refresher в конструкторе. Пользовательские реализации этих зависимостей не должны требовать scoped-сервисы при сохранении стандартного worker; для такого сценария нужен собственный worker с управлением scope. Реализация общего store должна поддерживать конкурентный доступ.

`IContractWatcherCoreClient` тоже является публичной точкой расширения. Если нужна полная замена транспорта, зарегистрируйте свою реализацию через обычный DI вместо `AddCore()`, а остальные модули подключите явно.

## Поведение при ошибках и ограничения

- **Нет контракта:** стандартный watcher возвращает `ContractUnavailable`, валидация и отправка отчёта не выполняются.
- **Есть нарушения:** watcher возвращает `Failed`, отчёт отправляется; MVC-фильтр не блокирует action.
- **Ошибка публикации:** стандартный обработчик переводит итог в `TechnicalError`. Очереди и автоматической повторной отправки нет.
- **Отмена:** отмена переданного `CancellationToken` распространяется исключением, а не преобразуется в успешную проверку.
- **Ошибочная конфигурация DI:** отсутствие обязательного модуля может привести к ошибке разрешения сервисов ещё до начала pipeline. Используйте готовый набор или подключайте все зависимости выбранного сценария.
- **Проверка полей:** валидатор обходит правила контракта и ищет свойства верхнего уровня по точному имени, с учётом регистра. JSONPath и рекурсивный обход вложенных объектов не реализованы.
- **Дополнительные поля:** свойства JSON, для которых нет правила, не считаются нарушением.
- **Объекты и массивы:** стандартный Type rule проверяет только JSON-тип, но не структуру содержимого или тип каждого элемента.
- **Пустой набор правил:** JSON-объект не даст нарушений, если контракт не содержит правил. Пустой набор зарегистрированных validation rules также ничего не проверяет.
- **Публикация схем:** автоматическое извлечение контрактов из Swagger, GraphQL или DTO и публикация схем не входят в показанную реализацию SDK.