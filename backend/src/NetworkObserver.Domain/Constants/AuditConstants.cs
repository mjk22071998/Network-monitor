namespace NetworkObserver.Domain.Constants;

/// <summary>
/// Standardized dictionary of 20+ immutable audit action codes complying with
/// security_and_rbac_specification.md Section 8.2.
/// </summary>
public static class AuditActionCodes
{
    // Authentication & Session Lifecycle
    public const string AuthLogin = "AUTH_LOGIN";
    public const string AuthLogout = "AUTH_LOGOUT";
    public const string AuthFailure = "AUTH_FAILURE";
    public const string PasswordChange = "PASSWORD_CHANGE";

    // User Governance (Admin Only)
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserDelete = "USER_DELETE";

    // Device Management & Operational Mode
    public const string DeviceCreate = "DEVICE_CREATE";
    public const string DeviceUpdate = "DEVICE_UPDATE";
    public const string DeviceDelete = "DEVICE_DELETE";
    public const string MaintenanceToggle = "MAINTENANCE_TOGGLE";

    // Group & Subgroup Hierarchy
    public const string GroupCreate = "GROUP_CREATE";
    public const string GroupUpdate = "GROUP_UPDATE";
    public const string GroupDelete = "GROUP_DELETE";

    // Enclave Site & Polling Configuration
    public const string SiteUpdate = "SITE_UPDATE";
    public const string ConfigUpdate = "CONFIG_UPDATE";

    // Subnet Discovery Sweeps
    public const string ScanTrigger = "SCAN_TRIGGER";
    public const string ScanCancel = "SCAN_CANCEL";

    // Bulk File Ingestion Pipeline
    public const string BatchUpload = "BATCH_UPLOAD";
    public const string BatchEdit = "BATCH_EDIT";
    public const string BatchCommit = "BATCH_COMMIT";
    public const string BatchDiscard = "BATCH_DISCARD";
}

/// <summary>
/// Standardized audit target entities matching database schema and specification.
/// </summary>
public static class AuditTargetEntities
{
    public const string User = "USER";
    public const string Device = "DEVICE";
    public const string Group = "GROUP";
    public const string Site = "SITE";
    public const string Network = "NETWORK";
    public const string Batch = "BATCH";
}
