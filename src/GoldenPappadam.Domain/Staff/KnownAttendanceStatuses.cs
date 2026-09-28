namespace GoldenPappadam.Domain.Staff;

/// <summary>The four statuses, seeded with fixed ids like the units.</summary>
public static class KnownAttendanceStatuses
{
    public static readonly Guid PresentId = Guid.Parse("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000001");
    public static readonly Guid HalfDayId = Guid.Parse("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000002");
    public static readonly Guid AbsentId = Guid.Parse("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000003");
    public static readonly Guid LeaveId = Guid.Parse("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000004");
}
