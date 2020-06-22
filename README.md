# WebApi with .NET Core
Samples and resources of how to design WebApi with .NET Core

## Projects structures

## Inversion of Control and Dependency Injection

## Docker

## CI/CD

## Routing

## REST

## API Versioning

## API Testing

## Filters

## Middleware

## Logging

### General

#### Log Levels

By default in .NET Core there are six levels of logging (available through [LogLevel](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel?view=dotnet-plat-ext-3.1) enum):
- `Trace` (value `0`) - the most detailed and verbose information about the application flow, 
- `Debug` (`1`) - useful information during the development process (eg. local environment bug investigation),
- `Information` (`2`) - usually important information about the application flow that can be useful for diagnostics and flow, 
- `Warning` (`3`) - potential unexpected application event or error that's not blocking flow (eg. operation was successfully saved to database but notification failed) or transient error occurred but was succeeded after retry), 
- `Error` (`4`) - unexpected application error - eg. not record found to update, database timeout, argument exception etc.,
- `Critical` (`5`) - informing about critical events that require immediate action like application or system crash, end of disk space or database in irrecoverable state,
- `None` (`6`) - means no logs at all, used usually in the configuration to disable logging for selected category.

It's important to keep in mind that `Trace` and `Debug` should not be used on production, and should be used only for development/debugging purposes (`Trace` is by default disabled). 
Because of theirs characteristic they may contain sensitive application information to be effective (eg. system secrets, PII/GDPR Data). Because of that we need to be sure that on production environment they are disabled as that may end up with security leak. 
As they're also verbose, then keeping them on production system may increase significantly cost of logs storage. Plus too much logs make them unreadable and hard to read.

#### Log Categories

Each logger instance needs to have assigned category. Categories allows to group logs messages (as category will be added to each log entry).
By convention category should be passed as type parameter of [ILogger<T>](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1?view=dotnet-plat-ext-3.1). Usually it's the class that we're injecting logger, eg.

```csharp
[Route("api/Reservations")]
public class ReservationsController: Controller
{
    private readonly ILogger<ReservationsController> logger;

    public ReservationsController(ILogger<ReservationsController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
    {
        var reservationId = Guid.NewGuid();

        // (...) 

        logger.LogInformation("Created reservation with {ReservationId}", reservationId);
        
        
        return Created("api/Reservations", reservationId);
    }

}
```

Log category created with type parameter will contain full type name (so eg. `LoggingSamples.Controllers.ReservationController`).


It's also possible (however not recommended) to define that through [ILoggerFactory](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.iloggerfactory?view=dotnet-plat-ext-3.1) `CreateLogger(string categoryName)` method:

```csharp
[Route("api/Reservations")]
public class ReservationsController: Controller
{
    private readonly ILogger logger;

    public ReservationsController(ILoggerFactory loggerFactory)
    {
        this.logger = logger.CreateLogger("LoggingSamples.Controllers.ReservationController");
    }
}
```

Categories are useful for searching through logs and diagnose issues. 
As mentioned in previous section - it's also possible to define in different log levels for configuration.

Eg. if you have a default log level `Information` and you need to investigate issues ocurring in specific controller (eg. `ReservationsController`) then you can change the log level to `Debug` for dedicated category.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LoggingSamples.Controllers.ReservationController": "Debug"
    }
  }
}
```

Then for all categories but `LoggingSamples.Controllers.ReservationController` you'll have logs logged for Information and above (`Information`, `Warning`, `Error`, `Critical`) and for `LoggingSamples.Controllers.ReservationController` also `Debug`.

The other example is to disable logs from selected category - eg. 
- because you noticed that is logging some sensitive information and you need quickly to change that, 
- you want to mute some unimportant system logs,
- you want to make sure that logs from specific category (eg. `LoggingSamples.Controllers.AuthenticationController`) won't be ever logged on prod.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LoggingSamples.Controllers.AuthenticationController": "None"
    }
  }
}
```

#### Log Scopes

Besides categories it's possible to define logging scopes. They allow have add set of custom information to each log entry.

Scopes are disabled by default - if you'd like to use them then you need to toggle them on in configuration:

```json
{
  "Logging": {
    "IncludeScopes": true,
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

Having that you can use [ILogger.BeginScope method](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger.beginscope?view=dotnet-plat-ext-3.1) to define one or more logging scopes.

The first potential use case is to always add entity type and identifier to all logs in business logic to not need to add it in each entry. Eg. reservation id during its update. You can also create nested scopes.

```csharp
[HttpPut]
public async Task<IActionResult> Create(Guid id, [FromBody] UpdateReservationRequest request)
{
    using(logger.BeginScope("For {EntityType}", "Reservation")
    {
         using(logger.BeginScope("With {EntityId}", id)
         {    
              logger.LogInformation("Starting reservation update process for {request}", request);
              // (...)
         }
    }
    
    return OK();
}
```

You can create also scopes with aspect programming way - so eg. in middleware to inject scopes globally. 

Example would be injecting as logging scope information from request eg. client IP, user id.
 
Sample below shows how to inject [CorellationID](#correlationid) into logger scope.

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        this.next = next;
        logger = loggerFactory.CreateLogger<CorrelationIdMiddleware>();
    }

    public async Task Invoke(HttpContext context /* other scoped dependencies */)
    {
        var correlationID = Guid.NewGuid();

        using (logger.BeginScope($"CorrelationID: {CorrelationID}", correlationID))
        {
            await next(context);
        }
    }
}
```

#### Log Events

The other option for grouping logs are log events. They are used normally to group them eg. by purpose - eg. updating an entity, starting controller action, not finding entity etc.
To define them you need to provide a standardized list of int event ids. Eg.

```csharp
public class LogEvents
{
    public const int InvalidRequest = 911;
    public const int ConflictState = 112;
    public const int EntityNotFound = 1000;
}
```

Sample usage:

```csharp
[HttpPut]
public IActionResult Update([FromBody] UpdateReservation request)
{
    logger.LogInformation("Initiating reservation creation for {seatId}", request?.SeatId);

    if (request?.SeatId == null || request?.SeatId == Guid.Empty)
    {
        logger.LogWarning(LogEvents.InvalidRequest, "Invalid {SeatId}", request?.SeatId);

        return BadRequest("Invalid SeatId");
    }

    if (request?.ReservationId == null || request?.ReservationId == Guid.Empty)
    {
        logger.LogWarning(LogEvents.InvalidRequest, "Invalid {ReservationId}", request?.ReservationId);

        return BadRequest("Invalid ReservationId");
    }

    // (...)
    return Created("api/Reservations", reservation.Id);
}
```

#### Links
- [Microsoft Docs - Logging in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1)
- [Microsoft Docs - High-performance logging with LoggerMessage in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/loggermessage?view=aspnetcore-3.1)
- [Steve Gordon - High-Performance Logging in .NET Core](https://www.stevejgordon.co.uk/high-performance-logging-in-net-core)
- [Software Engineering StackExchange - Benefits of Structured Logging vs basic logging](https://softwareengineering.stackexchange.com/questions/312197/benefits-of-structured-logging-vs-basic-logging)
- [Message Templates](https://messagetemplates.org/)
- [Andre Newman - Tools and Techniques for Logging Microservices ](https://dzone.com/articles/tools-and-techniques-for-logging-microservices-1)
- [Siva Prasad Rao Janapati - Distributed Logging Architecture for Microservices](https://dzone.com/articles/distributed-logging-architecture-for-microservices)
- [Szymon Warda - Stop trying to mock the ILogger methods](https://indexoutofrange.com/Stop-trying-to-mock-the-ILogger-methods/)
- [Andrew Lock - How to include scopes when logging exceptions in ASP.NET Core](https://andrewlock.net/how-to-include-scopes-when-logging-exceptions-in-asp-net-core/)
- [Rico Suter - Logging with ILogger in .NET: Recommendations and best practices](https://blog.rsuter.com/logging-with-ilogger-recommendations-and-best-practices/)
- [Stephen Cleary - Microsoft.Extensions.Logging](https://blog.stephencleary.com/2018/06/microsoft-extensions-logging-part-2-types.html)
- [Stephen Cleary - A New Pattern for Exception Logging](https://blog.stephencleary.com/2020/06/a-new-pattern-for-exception-logging.html)

### Serilog

#### Links
- [Serilog Documentation](https://serilog.net/)
- [Nicholas Blumhardt - Setting up Serilog in ASP.NET Core 3](https://nblumhardt.com/2019/10/serilog-in-aspnetcore-3/)
- [Ben Foster - Serilog Best Practices](https://benfoster.io/blog/serilog-best-practices/)
- [Alfus Jaganathan - Scoped logging using Microsoft Logger with Serilog in .Net Core Application](https://www.initpals.com/net-core/scoped-logging-using-microsoft-logger-with-serilog-in-net-core-application/)

### NLog

#### Links
- [NLog Documentation](https://nlog-project.org/)
- [NLog Wiki - How to use structured logging](https://github.com/NLog/NLog/wiki/How-to-use-structured-logging)

### Elastic Stack - Kibana, LogStash etc.

#### Links
- [HumanKode - Logging with ElasticSearch, Kibana, ASP.NET Core and Docker](https://www.humankode.com/asp-net-core/logging-with-elasticsearch-kibana-asp-net-core-and-docker)
- [Than Le - Building logging system in Microservice Architecture with ELK stack and Serilog .NET Core](https://medium.com/@letienthanh0212/building-logging-system-in-microservice-architecture-with-elk-stack-and-serilog-net-core-part-1-8fe2dfcf9e6f)
- [Marco de Sanctis - Monitor ASP.NET Core in ELK through Docker and Azure Event Hubs](https://medium.com/@marcodesanctis2/monitor-asp-net-core-in-elk-through-docker-and-azure-event-hubs-6e519249af61)
- [Microsoft Docs - Logging with Elastic Stack](https://docs.microsoft.com/en-us/dotnet/architecture/cloud-native/logging-with-elastic-stack)
- [Ali Mselmi - Structured logging with Serilog and Seq and ElasticSearch under Docker ](https://dev.to/hasdrubal/structure-logging-with-serilog-and-seq-and-elasticsearch-under-docker-16dk)
- [Logz.io - Complete Guide to ELK Stack](https://logz.io/learn/complete-guide-elk-stack/#elasticsearch)
- [Logz.io - Best practices for managing ElasticSearch indices](https://logz.io/blog/managing-elasticsearch-indices/)
- [Andrew Lock - Writing logs to Elasticsearch with Fluentd using Serilog in ASP.NET Core](https://andrewlock.net/writing-logs-to-elasticsearch-with-fluentd-using-serilog-in-asp-net-core/)
- [Elastic Documentation - Install ElasticSearch with Docker](https://www.elastic.co/guide/en/elasticsearch/reference/current/docker.html)

## CorrelationId

#### Links
- [Steve Gordon - ASP.NET Core Correlation IDs](https://www.stevejgordon.co.uk/asp-net-core-correlation-ids)
- [Steve Gordon - CorrelationId NuGet Package](https://github.com/stevejgordon/CorrelationId)
- [Vicenç García - Capturing and forwarding correlation IDs in ASP.NET Core](https://vgaltes.com/post/forwarding-correlation-ids-in-aspnetcore/)
- [Vicenç García - Capturing and forwarding correlation IDs in ASP.NET Core, the easy way](https://vgaltes.com/post/forwarding-correlation-ids-in-aspnetcore-version-2/)

## Storage

### EntityFramework

### Dapper

## Caching

## GraphQL

## CQRS

## OAuth 


