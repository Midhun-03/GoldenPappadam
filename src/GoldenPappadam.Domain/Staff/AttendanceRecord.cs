using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// One employee on one day. At most one per employee per date. A day with no record earns nothing.
///
/// Editable, even in a week already paid: the paid week never changes, and the difference is carried
/// to the employee's next unpaid week as a correction line (see <see cref="WagePaymentLine"/>).
/// </summary>
public class AttendanceRecord : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>The IST calendar date.</summary>
    public DateOnly WorkDate { get; set; }

    public Guid AttendanceStatusId { get; set; }
    public AttendanceStatus? AttendanceStatus { get; set; }
}
