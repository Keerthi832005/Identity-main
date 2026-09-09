using System.Net.Mail;

namespace Identity.Domain.Entities;

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    public static UserAccount Create(
        string employeeCode,
        string displayName,
        DateTime createdAt,
        string? email = null,
        long? managerUserId = null) => new()
        {
            EmployeeCode = DomainRules.Required(employeeCode, nameof(employeeCode), 50),
            DisplayName = DomainRules.Required(displayName, nameof(displayName), 200),
            Email = ValidateEmail(email),
            ManagerUserId = ValidateManagerId(managerUserId),
            IsActive = true,
            SecurityVersion = 1,
            CreatedAt = createdAt,
        };

    public bool UpdateProfile(string displayName, string? email, long? managerUserId, DateTime updatedAt)
    {
        var validName = DomainRules.Required(displayName, nameof(displayName), 200);
        var validEmail = ValidateEmail(email);
        var validManagerId = ValidateManagerId(managerUserId);
        if (validManagerId == UserId)
        {
            throw new ArgumentException("A user cannot be their own manager.", nameof(managerUserId));
        }

        if (DisplayName == validName && Email == validEmail && ManagerUserId == validManagerId)
        {
            return false;
        }

        DisplayName = validName;
        Email = validEmail;
        ManagerUserId = validManagerId;
        UpdatedAt = updatedAt;
        return true;
    }

    public bool SetOrganizationMapping(long? departmentId, long? teamId, long? branchId, DateTime updatedAt)
    {
        if (departmentId is <= 0 || teamId is <= 0 || branchId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(departmentId), "Department, team and branch IDs must be positive.");
        if (teamId.HasValue && !departmentId.HasValue)
            throw new ArgumentException("Select a department before assigning a team.", nameof(teamId));
        if (DepartmentId == departmentId && TeamId == teamId && BranchId == branchId) return false;
        DepartmentId = departmentId;
        TeamId = teamId;
        BranchId = branchId;
        UpdatedAt = updatedAt;
        return true;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var value = email.Trim();
        if (value.Length > 254 || value.Any(char.IsWhiteSpace)
            || !MailAddress.TryCreate(value, out var address)
            || !string.Equals(address.Address, value, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(address.DisplayName))
        {
            throw new ArgumentException("Email must be a valid address of at most 254 characters.", nameof(email));
        }

        return value;
    }

    private static long? ValidateManagerId(long? managerUserId) => managerUserId is <= 0
        ? throw new ArgumentOutOfRangeException(nameof(managerUserId), "Manager user ID must be positive.")
        : managerUserId;

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        SecurityVersion++;
        UpdatedAt = updatedAt;
    }

    public void InvalidateSecurity(DateTime updatedAt)
    {
        SecurityVersion++;
        UpdatedAt = updatedAt;
    }

    public void RecordFailedVerification(DateTime occurredAt, int maximumAttempts, TimeSpan lockout)
    {
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (lockout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lockout));
        FailedLoginCount++;
        if (FailedLoginCount >= maximumAttempts)
        {
            LockoutEndAt = occurredAt.Add(lockout);
            FailedLoginCount = 0;
        }
        UpdatedAt = occurredAt;
    }

    public void RecordSuccessfulVerification(DateTime occurredAt)
    {
        FailedLoginCount = 0;
        LockoutEndAt = null;
        LastLoginAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    public long UserId { get; private set; }
    public string EmployeeCode { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public long? ManagerUserId { get; private set; }
    public UserAccount? Manager { get; private set; }
    public long? DepartmentId { get; private set; }
    public Department? Department { get; private set; }
    public long? TeamId { get; private set; }
    public Team? Team { get; private set; }
    public long? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int SecurityVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
