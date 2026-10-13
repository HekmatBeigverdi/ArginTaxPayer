using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityProtectionTests
{
    [Fact]
    public void FifthInvalidAttemptLocksNormalizedAccountWithoutStoringCredentials()
    {
        var protection = new IdentityProtectionService(NullLogger<IdentityProtectionService>.Instance);
        var start = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 4; i++)
        {
            Assert.False(protection.IsBlocked("USER@example.invalid", start));
            protection.RecordFailure(" user@example.invalid ", start, "login");
        }
        protection.RecordFailure("USER@example.invalid", start, "login");
        Assert.True(protection.IsBlocked("user@example.invalid", start.AddMinutes(1)));
        Assert.False(protection.IsBlocked("user@example.invalid", start.AddMinutes(16)));
        Assert.False(protection.IsBlocked("other@example.invalid", start));
    }

    [Fact]
    public void SecurityLogOmitsAccountAndSecret()
    {
        var logger = new CaptureLogger();
        var protection = new IdentityProtectionService(logger);
        var email = "private-account@example.invalid";
        var password = "private-password-DO-NOT-LOG";
        protection.RecordFailure(email, DateTime.UtcNow, "login");
        protection.RecordSuccess(email, "login");
        var text = string.Join(" ", logger.Messages);
        Assert.DoesNotContain(email, text);
        Assert.DoesNotContain(password, text);
        Assert.Contains("rejected", text);
        Assert.Contains("accepted", text);
    }

    private sealed class CaptureLogger : ILogger<IdentityProtectionService>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public void SuccessfulAuthenticationClearsPreviousFailureBucket()
    {
        var protection = new IdentityProtectionService(NullLogger<IdentityProtectionService>.Instance);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
            protection.RecordFailure("test@example.invalid", now, "login");
        Assert.True(protection.IsBlocked("test@example.invalid", now));
        protection.RecordSuccess(" TEST@example.invalid ", "login");
        Assert.False(protection.IsBlocked("test@example.invalid", now));
    }
}
