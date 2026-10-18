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
