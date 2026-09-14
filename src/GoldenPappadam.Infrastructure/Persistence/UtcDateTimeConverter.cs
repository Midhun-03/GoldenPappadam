using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GoldenPappadam.Infrastructure.Persistence;

/// <summary>
/// Keeps every stored DateTime in UTC and marks it as UTC when it is read back,
/// so JSON carries the "Z" and clients convert to IST themselves.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
