namespace SkcaEnrol.Api.Domain;

/// <summary>
/// Every table gets an Id plus audit timestamps. The timestamps are filled in
/// automatically by AppDbContext.SaveChangesAsync, so no service can forget them.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
