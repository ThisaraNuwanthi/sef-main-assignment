using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Services;

/// <summary>
/// The only way an enrolment's status changes. It refuses illegal jumps
/// (e.g. Rejected -> Approved) and writes a history row for every move.
/// </summary>
public static class EnrolmentStateMachine
{
    private static readonly Dictionary<EnrolmentStatus, EnrolmentStatus[]> Allowed = new()
    {
        [EnrolmentStatus.Submitted] = new[] { EnrolmentStatus.AgentProcessing, EnrolmentStatus.Cancelled },
        [EnrolmentStatus.AgentProcessing] = new[] { EnrolmentStatus.PendingAdminApproval, EnrolmentStatus.Failed },
        [EnrolmentStatus.PendingAdminApproval] = new[]
        {
            EnrolmentStatus.Approved, EnrolmentStatus.Rejected, EnrolmentStatus.RevisionRequested, EnrolmentStatus.Cancelled
        },
        [EnrolmentStatus.RevisionRequested] = new[] { EnrolmentStatus.Submitted, EnrolmentStatus.Cancelled },
        [EnrolmentStatus.Failed] = new[] { EnrolmentStatus.Submitted, EnrolmentStatus.Cancelled }, // Submitted = retry
        [EnrolmentStatus.Approved] = new[] { EnrolmentStatus.Cancelled },                          // frees the seat
        [EnrolmentStatus.Rejected] = Array.Empty<EnrolmentStatus>(),                                // final
        [EnrolmentStatus.Cancelled] = Array.Empty<EnrolmentStatus>()                                // final
    };

    public static bool CanMove(EnrolmentStatus from, EnrolmentStatus to) => Allowed[from].Contains(to);

    /// <param name="changedByUserId">Null when the system (agent workflow) makes the change.</param>
    public static void Move(Enrolment enrolment, EnrolmentStatus to, int? changedByUserId, string? note)
    {
        if (!CanMove(enrolment.Status, to))
            throw new ConflictException($"An enrolment cannot move from {enrolment.Status} to {to}.");

        enrolment.History.Add(new EnrolmentStatusHistory
        {
            FromStatus = enrolment.Status,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Note = note,
            ChangedAt = DateTime.UtcNow
        });
        enrolment.Status = to;
    }
}
