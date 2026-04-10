# X.Web.Lockout

[![NuGet](https://img.shields.io/nuget/v/X.Web.Lockout.svg)](https://www.nuget.org/packages/X.Web.Lockout/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

A small .NET library that adds user account lockout functionality on top of [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) (`Microsoft.Extensions.Identity.Core`).

Pick a backing store that fits your application — an in-process dictionary, `IMemoryCache`, `IDistributedCache` (Redis, SQL Server, etc.), or your existing `IUserLockoutStore<TUser>` — and lock out users after too many failed authentication attempts.

## Features

- Two simple abstractions: `ILockoutService` (keyed by `userId`) and `IUserLockoutService<TUser>` (keyed by user instance)
- Multiple backing implementations:
  - `LockoutService` — `ConcurrentDictionary` (single-instance, no extra dependencies)
  - `MemoryLockoutService` — `IMemoryCache` (single-instance, with cache-managed eviction)
  - `DistributedLockoutService` — `IDistributedCache` (multi-instance, Redis/SQL/etc.)
  - `StoreLockoutService` / `StoreUserLockoutService` — wrap an existing `IUserLockoutStore<TUser>`
- `IUserLockoutStore<TUser>` decorators (`UserLockoutStore`, `DistributedUserLockoutStore`) for plugging into ASP.NET Identity's `UserManager` pipeline
- Auto-eviction of expired entries in the cache-based services
- `TimeProvider` injection for testable, deterministic time-based behavior
- Reuses `Microsoft.AspNetCore.Identity.LockoutOptions` (`MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan`) — no custom config

## Install

```sh
dotnet add package X.Web.Lockout
```

## Quick start

### 1. Pick an implementation and register it

**In-memory dictionary** (simplest, single-instance):

```csharp
using Microsoft.AspNetCore.Identity;
using X.Web.Lockout;
using X.Web.Lockout.Services;

var lockoutOptions = new LockoutOptions
{
    MaxFailedAccessAttempts = 5,
    DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)
};

builder.Services.AddSingleton(lockoutOptions);
builder.Services.AddSingleton<ILockoutService, LockoutService>();
```

**`IMemoryCache`-backed** (single-instance, with eviction):

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(lockoutOptions);
builder.Services.AddSingleton<ILockoutService, MemoryLockoutService>();
```

**`IDistributedCache`-backed** (multi-instance, Redis / SQL Server / etc.):

```csharp
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
builder.Services.AddSingleton(lockoutOptions);
builder.Services.AddSingleton<ILockoutService, DistributedLockoutService>();
```

### 2. Use it in your authentication flow

```csharp
public class LoginHandler
{
    private readonly ILockoutService _lockout;

    public LoginHandler(ILockoutService lockout) => _lockout = lockout;

    public async Task<bool> SignInAsync(string userId, string password)
    {
        if (await _lockout.GetLockoutEnabledAsync(userId))
        {
            throw new InvalidOperationException("Account is temporarily locked.");
        }

        if (!ValidatePassword(userId, password))
        {
            await _lockout.IncrementAccessFailedCountAsync(userId);
            return false;
        }

        await _lockout.ResetAccessFailedCountAsync(userId);
        return true;
    }
}
```

## API

### `ILockoutService`

```csharp
public interface ILockoutService
{
    Task<bool> GetLockoutEnabledAsync(string userId, CancellationToken cancellationToken = default);
    Task IncrementAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default);
    Task ResetAccessFailedCountAsync(string userId, CancellationToken cancellationToken = default);
}
```

### `IUserLockoutService<TUser>`

Strongly-typed variant for when you already have a user instance and don't want to look up by id:

```csharp
public interface IUserLockoutService<in TUser> where TUser : class
{
    Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken = default);
    Task IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default);
    Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default);
}
```

## Behavior

- **Failure tracking** — `IncrementAccessFailedCountAsync` increments a per-user counter. When it reaches `LockoutOptions.MaxFailedAccessAttempts`, the user is locked out for `LockoutOptions.DefaultLockoutTimeSpan`.
- **Lockout check** — `GetLockoutEnabledAsync` returns `true` only while the lockout window is in the future. Past lockouts are treated as inactive.
- **Reset** — `ResetAccessFailedCountAsync` clears both the failure counter and the lockout end date. Call it on a successful authentication.
- **Sliding window for failed attempts** — for the cache-based services (`MemoryLockoutService`, `DistributedLockoutService`), each failed attempt extends the entry lifetime by `DefaultLockoutTimeSpan`. An attacker who pauses longer than that window starts from a clean slate.
- **Self-eviction** — when a user is locked out, the cache entry's absolute expiration is set to the remaining lockout time, so expired lockouts are removed automatically without any cleanup job.

## Implementations

| Service | Backing | When to use |
|---|---|---|
| `LockoutService` | `ConcurrentDictionary` | Quick start, single-instance apps. State lost on restart. **No automatic eviction** — entries grow unbounded; not recommended for production with untrusted user ids. |
| `MemoryLockoutService` | `IMemoryCache` | Single-instance apps with auto-eviction. Configure `SizeLimit` on `MemoryCacheOptions` to bound memory. |
| `DistributedLockoutService` | `IDistributedCache` | Multi-instance / load-balanced apps. Works with any `IDistributedCache` provider (Redis, SQL Server, NCache, etc.). |
| `StoreLockoutService<TUser>` | `IUserLockoutStore<TUser>` | When lockout state should live in your existing user table (Entity Framework, Dapper, etc.). |
| `StoreUserLockoutService<TUser>` | `IUserLockoutStore<TUser>` | Same as above, but takes the `TUser` instance directly (no `FindByIdAsync` lookup). |

## `IUserLockoutStore<TUser>` decorators

If you want to plug lockout state into ASP.NET Identity's `UserManager<TUser>` pipeline, use one of the included decorators. Each wraps an inner `IUserStore<TUser>` and adds lockout-specific storage:

- `UserLockoutStore<TUser>` — keeps lockout state in `ConcurrentDictionary`
- `DistributedUserLockoutStore<TUser>` — keeps lockout state in `IDistributedCache`

Both delegate user CRUD (`CreateAsync`, `FindByIdAsync`, etc.) to the inner store.

## Testability

Every service accepts a `TimeProvider` via constructor injection. Combined with `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider`, lockout windows and expirations can be tested deterministically:

```csharp
var time = new FakeTimeProvider();
var service = new LockoutService(options, time);

await service.IncrementAccessFailedCountAsync("user1");
// ...
time.Advance(TimeSpan.FromMinutes(16));
Assert.False(await service.GetLockoutEnabledAsync("user1"));
```

## Build & test

```sh
dotnet build X.Web.Lockout.slnx
dotnet test X.Web.Lockout.slnx
```

## License

Apache-2.0 — see [LICENSE](LICENSE).
