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
