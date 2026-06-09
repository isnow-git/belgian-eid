using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BelgianEid.Implementations;

/// <summary>
/// Verifies the cardholder's PIN code and exposes its current status.
/// </summary>
public sealed class EidPinService : IEidPinService
{
    private readonly ILogger<EidPinService> _logger;

    public EidPinService(ILogger<EidPinService>? logger = null)
        => _logger = logger ?? NullLogger<EidPinService>.Instance;

    /// <inheritdoc/>
    public void VerifyPin(IEidSession session, string pin)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrEmpty(pin))
            throw new EidPinIncorrectException(session.GetPinStatus().RemainingAttempts);

        // C_Login automatically raises EidPinIncorrectException / EidPinBlockedException.
        session.Login(pin);
        _logger.LogInformation("PIN code verified successfully.");
    }

    /// <inheritdoc/>
    public EidPinStatus GetPinStatus(IEidSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.GetPinStatus();
    }
}
