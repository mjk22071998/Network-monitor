namespace NetworkObserver.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class DeviceNotFoundException : DomainException
{
    public DeviceNotFoundException(int deviceId) 
        : base($"Device with ID {deviceId} was not found.", "DEVICE_NOT_FOUND") { }

    public DeviceNotFoundException(string identifier) 
        : base($"Device with identifier '{identifier}' was not found.", "DEVICE_NOT_FOUND") { }
}

public class MacAddressImmutableException : DomainException
{
    public MacAddressImmutableException(string currentMac, string requestedMac) 
        : base($"MAC address is immutable. Cannot modify '{currentMac}' to '{requestedMac}'.", "DEVICE_MAC_IMMUTABLE") { }
}

public class UnauthorizedRoleException : DomainException
{
    public UnauthorizedRoleException(string message = "User does not have sufficient role permissions.") 
        : base(message, "AUTH_FORBIDDEN") { }
}

public class UnauthenticatedException : DomainException
{
    public UnauthenticatedException(string message = "Authentication token missing or invalid.") 
        : base(message, "AUTH_UNAUTHORIZED") { }
}

public class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException(string message = "Invalid username or password.") 
        : base(message, "INVALID_CREDENTIALS") { }
}

public class AccountDeactivatedException : DomainException
{
    public AccountDeactivatedException(string message = "Operator account is deactivated. Contact network administrator.") 
        : base(message, "ACCOUNT_DEACTIVATED") { }
}

public class PasswordComplexityException : DomainException
{
    public PasswordComplexityException(string message = "Password must be at least 12 characters and include uppercase, lowercase, digit, and special character.") 
        : base(message, "PASSWORD_TOO_WEAK") { }
}

public class PasswordReuseException : DomainException
{
    public PasswordReuseException(string message = "New password cannot be identical to the current password.") 
        : base(message, "PASSWORD_REUSE") { }
}

public class UserNotFoundException : DomainException
{
    public UserNotFoundException(int userId) 
        : base($"User with ID {userId} was not found.", "USER_NOT_FOUND") { }

    public UserNotFoundException(string username) 
        : base($"User with username '{username}' was not found.", "USER_NOT_FOUND") { }
}

public class SelfDeletionException : DomainException
{
    public SelfDeletionException(string message = "Administrative security policy prohibits self-deletion.") 
        : base(message, "CANNOT_DELETE_SELF") { }
}

public class InvalidUsernameException : DomainException
{
    public InvalidUsernameException(string message = "Username is required and cannot be empty.") 
        : base(message, "INVALID_USERNAME") { }
}

public class DuplicateUsernameException : DomainException
{
    public DuplicateUsernameException(string username) 
        : base($"Username '{username}' already exists in enclave database.", "DUPLICATE_USERNAME") { }
}

public class InvalidRoleException : DomainException
{
    public InvalidRoleException(short roleId) 
        : base($"Role ID {roleId} is not a valid operational role.", "INVALID_ROLE") { }
}

public class InvalidSiteException : DomainException
{
    public InvalidSiteException(int siteId) 
        : base($"Site ID {siteId} does not exist.", "INVALID_SITE") { }
}

public class SiteNotFoundException : DomainException
{
    public SiteNotFoundException(string message = "No active site configuration found in enclave database.") 
        : base(message, "SITE_NOT_FOUND") { }
}

public class SiteValidationException : DomainException
{
    public SiteValidationException(string message) 
        : base(message, "SITE_VALIDATION_ERROR") { }
}

public class GroupNotFoundException : DomainException
{
    public GroupNotFoundException(int groupId) 
        : base($"Group with ID {groupId} was not found.", "GROUP_NOT_FOUND") { }
}

public class InvalidParentGroupException : DomainException
{
    public InvalidParentGroupException(int parentGroupId) 
        : base($"Parent group with ID {parentGroupId} does not exist.", "INVALID_PARENT_GROUP") { }
}

public class CircularGroupHierarchyException : DomainException
{
    public CircularGroupHierarchyException(string message = "Cannot reparent group: cyclic parent-child hierarchy detected.") 
        : base(message, "CIRCULAR_HIERARCHY") { }
}

public class ProtectedGroupException : DomainException
{
    public ProtectedGroupException(string message = "System root Group 1 (Unassigned Group) is protected and cannot be deleted or modified.") 
        : base(message, "CANNOT_MODIFY_ROOT_GROUP") { }
}

public class GroupValidationException : DomainException
{
    public GroupValidationException(string message) 
        : base(message, "GROUP_VALIDATION_ERROR") { }
}
