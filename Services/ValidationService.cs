using System;
using System.Data;
using System.Threading.Tasks;
using GoodGovernanceApp.Data;
using Microsoft.Data.Sqlite;

namespace GoodGovernanceApp.Services;

/// <summary>
/// Manages the validate_users validation workflow: Accept, Reject, Resubmit.
/// All actions are automatically logged to audit_trails.
/// </summary>
public class ValidationService
{
    private readonly DatabaseHelper _db;

    public ValidationService(DatabaseHelper db)
    {
        _db = db;
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all validate_users records, optionally filtered by status.
    /// </summary>
    /// <param name="filter">One of: "all", "pending", "accepted", "rejected"</param>
    public async Task<DataTable> GetAllAsync(string filter = "all")
    {
        string whereClause = filter.ToLower() switch
        {
            "pending"  => "WHERE status = 'pending'",
            "accepted" => "WHERE status = 'accepted'",
            "rejected" => "WHERE status = 'rejected'",
            _          => ""
        };

        string query = $@"
            SELECT
                vu.id,
                vu.civil_registryid,
                vu.lastname,
                vu.firstname,
                vu.middlename,
                COALESCE(vu.firstname,'') || ' ' || COALESCE(vu.middlename,'') || ' ' || COALESCE(vu.lastname,'') AS full_name,
                vu.address,
                vu.status,
                vu.rejection_reason,
                vu.validated_by,
                vu.validated_at,
                vu.user_id,
                vu.email,
                vu.phone,
                vu.photo,
                vu.created_at,
                vu.updated_at,
                u.name AS validated_by_name
            FROM validate_users vu
            LEFT JOIN users u ON u.id = vu.validated_by
            {whereClause}
            ORDER BY vu.created_at DESC";

        return await _db.ExecuteQueryAsync(query);
    }

    /// <summary>
    /// Returns a single validate_users record by ID.
    /// </summary>
    public async Task<DataTable> GetByIdAsync(int id)
    {
        string query = @"
            SELECT
                vu.*,
                u.name AS validated_by_name
            FROM validate_users vu
            LEFT JOIN users u ON u.id = vu.validated_by
            WHERE vu.id = @id
            LIMIT 1";

        return await _db.ExecuteQueryAsync(query,
            new SqliteParameter("@id", id));
    }

    /// <summary>
    /// Searches validate_users by name or civil registry ID.
    /// </summary>
    public async Task<DataTable> SearchAsync(string keyword, string filter = "all")
    {
        string whereClause = filter.ToLower() switch
        {
            "pending"  => "AND vu.status = 'pending'",
            "accepted" => "AND vu.status = 'accepted'",
            "rejected" => "AND vu.status = 'rejected'",
            _          => ""
        };

        string query = $@"
            SELECT
                vu.id,
                vu.civil_registryid,
                vu.lastname,
                vu.firstname,
                vu.middlename,
                COALESCE(vu.firstname,'') || ' ' || COALESCE(vu.middlename,'') || ' ' || COALESCE(vu.lastname,'') AS full_name,
                vu.address,
                vu.status,
                vu.rejection_reason,
                vu.validated_by,
                vu.validated_at,
                vu.email,
                vu.phone,
                vu.photo,
                vu.created_at
            FROM validate_users vu
            WHERE (
                vu.civil_registryid LIKE @kw
                OR vu.firstname     LIKE @kw
                OR vu.lastname      LIKE @kw
                OR vu.middlename    LIKE @kw
            )
            {whereClause}
            ORDER BY vu.created_at DESC";

        return await _db.ExecuteQueryAsync(query,
            new SqliteParameter("@kw", $"%{keyword}%"));
    }

    // ── Actions ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts a validate_user record and logs the action in audit_trails.
    /// </summary>
    public async Task<bool> AcceptAsync(int id, long adminUserId)
    {
        var now = DateTime.UtcNow;

        int rows = await _db.ExecuteNonQueryAsync(@"
            UPDATE validate_users
            SET status       = 'accepted',
                validated_by = @adminId,
                validated_at = @now,
                updated_at   = @now
            WHERE id = @id",
            new SqliteParameter("@adminId", adminUserId),
            new SqliteParameter("@now",     now),
            new SqliteParameter("@id",      id));

        if (rows > 0)
        {
            await LogAuditAsync(adminUserId, "accept", "validate_users", id,
                $"Validation record #{id} was accepted.", now);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Rejects a validate_user record with a required reason and logs the action.
    /// </summary>
    public async Task<bool> RejectAsync(int id, long adminUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        var now = DateTime.UtcNow;

        int rows = await _db.ExecuteNonQueryAsync(@"
            UPDATE validate_users
            SET status           = 'rejected',
                rejection_reason = @reason,
                validated_by     = @adminId,
                validated_at     = @now,
                updated_at       = @now
            WHERE id = @id",
            new SqliteParameter("@reason",  reason),
            new SqliteParameter("@adminId", adminUserId),
            new SqliteParameter("@now",     now),
            new SqliteParameter("@id",      id));

        if (rows > 0)
        {
            await LogAuditAsync(adminUserId, "reject", "validate_users", id,
                $"Validation record #{id} was rejected. Reason: {reason}", now);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resets a rejected record back to pending and clears the rejection reason.
    /// </summary>
    public async Task<bool> ResubmitAsync(int id)
    {
        var now = DateTime.UtcNow;

        int rows = await _db.ExecuteNonQueryAsync(@"
            UPDATE validate_users
            SET status           = 'pending',
                rejection_reason = NULL,
                validated_by     = NULL,
                validated_at     = NULL,
                updated_at       = @now
            WHERE id = @id",
            new SqliteParameter("@now", now),
            new SqliteParameter("@id",  id));

        if (rows > 0)
        {
            await LogAuditAsync(0, "resubmit", "validate_users", id,
                $"Validation record #{id} was reset to pending (resubmitted).", now);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates basic fields of a rejected record (used in the resubmit form).
    /// </summary>
    public async Task<bool> UpdateRecordAsync(int id, string firstname, string middlename,
        string lastname, string address, string? email, string? phone)
    {
        var now = DateTime.UtcNow;

        int rows = await _db.ExecuteNonQueryAsync(@"
            UPDATE validate_users
            SET firstname  = @firstname,
                middlename = @middlename,
                lastname   = @lastname,
                address    = @address,
                email      = @email,
                phone      = @phone,
                status     = 'pending',
                rejection_reason = NULL,
                validated_by     = NULL,
                validated_at     = NULL,
                updated_at = @now
            WHERE id = @id",
            new SqliteParameter("@firstname",  firstname),
            new SqliteParameter("@middlename", middlename),
            new SqliteParameter("@lastname",   lastname),
            new SqliteParameter("@address",    address),
            new SqliteParameter("@email",      (object?)email  ?? DBNull.Value),
            new SqliteParameter("@phone",      (object?)phone  ?? DBNull.Value),
            new SqliteParameter("@now",        now),
            new SqliteParameter("@id",         id));

        return rows > 0;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task LogAuditAsync(long userId, string action, string modelType,
        int modelId, string description, DateTime timestamp)
    {
        await _db.ExecuteNonQueryAsync(@"
            INSERT INTO audit_trails
                (user_id, action, model_type, model_id, description, created_at, updated_at, SyncId)
            VALUES
                (@userId, @action, @modelType, @modelId, @desc, @ts, @ts, @syncId)",
            new SqliteParameter("@userId",    userId),
            new SqliteParameter("@action",    action),
            new SqliteParameter("@modelType", modelType),
            new SqliteParameter("@modelId",   modelId),
            new SqliteParameter("@desc",      description),
            new SqliteParameter("@ts",        timestamp),
            new SqliteParameter("@syncId",    Guid.NewGuid().ToString()));
    }
}
