namespace ProjectManagement.Domain.Common;

/// <summary>
/// Extends BaseEntity with full audit trail (who created/updated and when).
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
