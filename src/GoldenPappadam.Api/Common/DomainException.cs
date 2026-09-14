namespace GoldenPappadam.Api.Common;

/// <summary>A business rule stopped the request. Returned to the client as 400.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Something the request referred to does not exist. Returned as 404.</summary>
public class NotFoundException(string what) : DomainException($"{what} was not found.");
