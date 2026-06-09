using System.Text;
using System.Security.Cryptography;
using BelgianEid.Abstractions;
using BelgianEid.DependencyInjection;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// =====================================================================
//  BelgianEid library usage sample
//
//  Demonstrates:
//    1. Card detection
//    2. PIN verification
//    3. Challenge signing + OCSP certificate validation
//    4. Personal data reading
// =====================================================================

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== Belgian eID library demo ===\n");

using ServiceProvider provider = new ServiceCollection()
    .AddLogging(builder => builder
        .AddSimpleConsole(o => o.SingleLine = true)
        .SetMinimumLevel(LogLevel.Information))
    .AddBelgianEid()
    .BuildServiceProvider();

var client = provider.GetRequiredService<IEidClient>();
var pinService = provider.GetRequiredService<IEidPinService>();

try
{
    // --- 1. Detect readers -----------------------------------------------
    Console.WriteLine("[1] Detecting readers...");
    IReadOnlyList<EidReader> readers = client.GetReaders();
    if (readers.Count == 0)
    {
        Console.WriteLine("    No card reader detected. Connect a reader and try again.");
        return;
    }

    foreach (EidReader r in readers)
        Console.WriteLine($"    - {r}");

    using IEidSession session = client.OpenSession();
    Console.WriteLine($"    Session opened on: {session.Reader.Name}\n");

    // --- 2. Verify PIN ---------------------------------------------------
    Console.WriteLine("[2] PIN verification");
    EidPinStatus status = pinService.GetPinStatus(session);
    Console.WriteLine($"    Remaining attempts: {status.RemainingAttempts}");

    Console.Write("    Enter the card PIN: ");
    string pin = ReadHiddenInput();

    try
    {
        pinService.VerifyPin(session, pin);
        Console.WriteLine("    PIN correct.\n");
    }
    catch (EidPinIncorrectException ex)
    {
        Console.WriteLine($"    Wrong PIN. Remaining attempts: {ex.RemainingAttempts}");
        return;
    }
    catch (EidPinBlockedException)
    {
        Console.WriteLine("    PIN is blocked. Visit your municipality to unblock it.");
        return;
    }

    // --- 3. Sign challenge + validate certificate via OCSP ---------------
    Console.WriteLine("[3] Challenge authentication + OCSP validation");
    byte[] challenge = RandomNumberGenerator.GetBytes(32);

    AuthenticationResult auth = await client.AuthenticateAsync(session, challenge, pin);
    Console.WriteLine($"    Challenge signed ({auth.Signature.Length} bytes).");
    Console.WriteLine($"    Authentication certificate: {auth.AuthenticationCertificate.Subject}");

    OcspResult result = await client.ValidateCertificateAsync(
        session, EidCertificateKind.Authentication);
    Console.WriteLine($"    OCSP certificate status at {result.ProducedAt} " +
                      $"(valid: {(result.IsValid ? "yes" : "no")})\n");

    // --- 4. Read personal data -------------------------------------------
    Console.WriteLine("[4] Reading personal data");
    EidCardData card = client.ReadCardData(session, includePhoto: true);

    Console.WriteLine($"    Last name   : {card.Identity.LastName}");
    Console.WriteLine($"    First names : {card.Identity.FirstNames}");
    Console.WriteLine($"    Born        : {card.Identity.BirthDate:d} in {card.Identity.BirthLocation}");
    Console.WriteLine($"    Nationality : {card.Identity.Nationality}");
    Console.WriteLine($"    NRN         : {card.Identity.NationalNumber}");
    Console.WriteLine($"    Address     : {card.Address.Street}, {card.Address.ZipCode} {card.Address.Municipality}");
    Console.WriteLine($"    Photo       : {(card.Photo is null ? "absent" : $"{card.Photo.Data.Length} bytes JPEG")}");

    string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "photo.jpeg");
    if (card.Photo?.SaveToFile(photoPath) == true)
        Console.WriteLine($"    Photo saved : {photoPath}");

    Console.WriteLine("\n=== Demo completed successfully ===");
}
catch (EidReaderNotFoundException)
{
    Console.WriteLine("Error: no card reader available.");
}
catch (EidCardNotPresentException)
{
    Console.WriteLine("Error: no eID card inserted.");
}
catch (EidConfigurationException ex)
{
    Console.WriteLine($"Configuration error: {ex.Message}");
    Console.WriteLine("Verify that beidpkcs11.dll is accessible (see the README).");
}
catch (EidException ex)
{
    Console.WriteLine($"eID error: {ex.Message}");
}

return;

static string ReadHiddenInput()
{
    var sb = new StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
            sb.Length--;
        else if (!char.IsControl(key.KeyChar))
            sb.Append(key.KeyChar);
    }
    Console.WriteLine();
    return sb.ToString();
}
