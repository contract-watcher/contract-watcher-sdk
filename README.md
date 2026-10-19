![build](https://github.com/contract-watcher/contract-watcher-sdk/actions/workflows/build.yml/badge.svg)
![release](https://github.com/contract-watcher/contract-watcher-sdk/actions/workflows/release.yml/badge.svg)
[![Codecov](https://codecov.io/gh/contract-watcher/contract-watcher-sdk/branch/main/graph/badge.svg)](https://codecov.io/gh/contract-watcher/contract-watcher-sdk)
[![NuGet](https://img.shields.io/nuget/v/ContractWatcher.SDK.svg)](https://www.nuget.org/packages/ContractWatcher.SDK)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

# ContractWatcher SDK

ContractWatcher SDK подключает .NET-приложение к ContractWatcher: получает опубликованные контракты из Core, проверяет входящий JSON и отправляет отчёты о результате проверки.

Можно начать с готовой конфигурации, а затем заменить отдельные компоненты: источник контрактов, хранилище, правила проверки, обработчики отчётов или весь pipeline.

SDK предназначен для серверного .NET-приложения. Фронтенд продолжает обращаться к вашему API обычными HTTP-запросами.

## Содержание

- [Quick start: ASP.NET Core](#quick-start-aspnet-core)
- [Как выбрать сценарий](#как-выбрать-сценарий)
- [Конфигурация](#конфигурация)
- [Проверка по атрибуту](#проверка-по-атрибуту)
- [Ручной вызов с получением контракта и отправкой отчёта](#ручной-вызов-с-получением-контракта-и-отправкой-отчёта)
- [Локальная проверка через IContractValidator](#локальная-проверка-через-icontractvalidator)
- [Quick start для фронтенда](#quick-start-для-фронтенда)
- [Контракты и кеширование](#контракты-и-кеширование)
- [Правила и порядок проверки](#правила-и-порядок-проверки)
- [Execution pipeline](#execution-pipeline)
- [Отчёты и их отправка](#отчёты-и-их-отправка)
- [Замена компонентов и время жизни](#замена-компонентов-и-время-жизни)
- [Поведение при ошибках и ограничения](#поведение-при-ошибках-и-ограничения)

## Quick start: ASP.NET Core

### 1. Подключите пакет

Требуется .NET 10. В директории вашего серверного проекта выполните:

```bash
dotnet add package ContractWatcher.SDK
```

Для полного сценария нужен работающий Core, ключ интеграции и опубликованный контракт. В примере используется slug `create-product` с двумя обязательными полями: `name` типа `String` и `price` типа `Number`; `Nullable = false` для обоих.

Slug в атрибуте должен совпадать со slug опубликованного контракта. SDK не создаёт контракт автоматически из DTO или атрибута.

### 2. Укажите настройки

В `appsettings.json` добавьте адрес Core и ключ интеграции, полученный на фронтенде:

```json
{
  "ContractWatcher": {
    "CoreUrl": "http://localhost:8080",
    "IntegrationKey": "YOUR_INTEGRATION_KEY"
  }
}
```

### 3. Зарегистрируйте SDK

В `Program.cs` подключите стандартные модули и MVC-фильтр:

```csharp
using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<ContractWatcherOptions>()
    .Bind(builder.Configuration.GetSection("ContractWatcher"));

builder.Services
    .AddContractWatcher()
    .AddDefaults();

builder.Services
    .AddControllers()
    .AddContractWatcher();

var app = builder.Build();

app.MapControllers();
app.Run();
```

Здесь два разных подключения: `Services.AddContractWatcher()` регистрирует SDK, а `AddControllers().AddContractWatcher()` включает проверку помеченных MVC actions.

### 4. Пометьте метод контроллера

Создайте `ProductsController.cs`:

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
        // Здесь выполняется бизнес-логика вашего приложения.
        return Ok(new { accepted = true, name = request.Name });
    }
}

public sealed record CreateProductRequest(string Name, decimal Price);
```

### 5. Отправьте запрос

Запустите приложение и отправьте JSON на его адрес:

```bash
curl -X POST http://localhost:5000/api/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Keyboard","price":99.90}'
```

Порт `5000` приведён для примера: используйте адрес, который приложение вывело при запуске. SDK загрузит контракт, проверит JSON и отправит отчёт в Core.

**Атрибут работает в режиме наблюдения:** результат проверки сам по себе не останавливает action и не меняет HTTP-ответ. Если нужно отклонять запросы или возвращать нарушения клиенту, используйте [ручной вызов](#ручной-вызов-с-получением-контракта-и-отправкой-отчёта).

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

Проверка и отправка отчёта — отдельные компоненты. `AddDefaultReporting()` добавляет сборку отчёта, а `UseCorePublisher()` — его публикацию в Core.

Стандартный отчёт содержит `ReportId`, `Slug`, `ContractVersion`, `Status` и `Violations`. Исходный JSON и заголовки пользовательского запроса в него не включаются. При доступном контракте отчёт создаётся и для успешной, и для неуспешной валидации.

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