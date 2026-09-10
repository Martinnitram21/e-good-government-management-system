using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GoodGovernanceApp.Models;

namespace GoodGovernanceApp.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("password")]
    public string Password { get; set; } = string.Empty;

    [Column("phone")]
    public long? Phone { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("about_me")]
    public string? AboutMe { get; set; }

    [Column("profile_photo")]
    public string? ProfilePhoto { get; set; }

    /// <summary>Roles: mayor, admin, user, super_admin, encoder</summary>
    [Column("role")]
    public string Role { get; set; } = "user";

    /// <summary>Status: active, inactive, suspended</summary>
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("office_type")]
    public string? OfficeType { get; set; }

    [Column("office_id")]
    public long? OfficeId { get; set; }

    [Column("remember_token")]
    public string? RememberToken { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("constituent_id")]
    public long? ConstituentId { get; set; }

    // Department/Office linkage
    public virtual Office? Office { get; set; }

    // NOTE: department_role_id does not exist in the remote database schema.
    // Marked as NotMapped to prevent EF from querying a non-existent column.
    [NotMapped]
    public int? DepartmentRoleId { get; set; }
    [NotMapped]
    public virtual DepartmentRole? DepartmentRole { get; set; }

    // User ↔ ValidateUser link
    public virtual ValidateUser? ValidationInfo { get; set; }

    // Navigation helpers
    [NotMapped]
    public string FullName => Name;

    [NotMapped]
    public bool IsActive
    {
        get => Status == "active";
        set => Status = value ? "active" : "inactive";
    }
    [Column("SyncId")]
    public Guid SyncId { get; set; } = Guid.NewGuid();

}
