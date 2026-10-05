using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAWP.Web.Domain;

/// <summary>
/// User login account (SRS Entity 1).
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(100), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(255)]
    public string? AvatarUrl { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public bool MustChangePassword { get; set; } = false;

    [MaxLength(10)]
    public string? OtpCode { get; set; }

    public DateTime? OtpExpiry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigations
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public StaffProfile? StaffProfile { get; set; }
    public Guardian? Guardian { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

/// <summary>
/// Defined SAWP roles (SRS Entity 2).
/// </summary>
public class Role
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public bool IsSystem { get; set; } = true;

    // Navigations
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Granular authorization capability assigned to roles (SRS Entity 3).
/// </summary>
public class Permission
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    // Navigations
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Associative entity linking users to roles (SRS Entity 4).
/// </summary>
public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>
/// Associative entity linking roles to permissions.
/// </summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

/// <summary>
/// School staff identity used for teacher/administrative role linkage (SRS Entity 5).
/// </summary>
public class StaffProfile
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(30)]
    public string StaffCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigations
    public ICollection<HomeroomAssignment> HomeroomAssignments { get; set; } = new List<HomeroomAssignment>();
}

/// <summary>
/// Parent/guardian identity and contact information (SRS Entity 6).
/// </summary>
public class Guardian
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? IdentityNumber { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Occupation { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigations
    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public ICollection<AdministrativeRequest> Requests { get; set; } = new List<AdministrativeRequest>();
}

/// <summary>
/// Central record for a student already enrolled in the school (SRS Entity 7).
/// </summary>
public class Student
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string StudentCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public DateTime AdmissionDate { get; set; }

    [MaxLength(100)]
    public string? PlaceOfBirth { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? HealthNotes { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Active"; // Active, Transferred, Graduated, Withdrawn

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigations
    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public ICollection<ClassAssignment> ClassAssignments { get; set; } = new List<ClassAssignment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<LunchRegistration> LunchRegistrations { get; set; } = new List<LunchRegistration>();
    public ICollection<AdministrativeRequest> AdministrativeRequests { get; set; } = new List<AdministrativeRequest>();
}

/// <summary>
/// Relationship between Student and Guardian (SRS Entity 8).
/// </summary>
public class StudentGuardian
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    public GuardianRelationship Relationship { get; set; }

    public bool IsPrimaryContact { get; set; } = false;

    public bool CanPickup { get; set; } = true;
}

/// <summary>
/// School-year name, start/end dates, and active/archive state (SRS Entity 9).
/// </summary>
public class SchoolYear
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty; // e.g. "2026-2027"

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public SchoolYearStatus Status { get; set; } = SchoolYearStatus.Planned;

    // Navigations
    public ICollection<SchoolCalendarDay> CalendarDays { get; set; } = new List<SchoolCalendarDay>();
    public ICollection<SchoolClass> Classes { get; set; } = new List<SchoolClass>();
}

/// <summary>
/// School calendar day (SRS Entity 10).
/// </summary>
public class SchoolCalendarDay
{
    public int Id { get; set; }

    public int SchoolYearId { get; set; }
    public SchoolYear SchoolYear { get; set; } = null!;

    public DateTime Date { get; set; }

    public CalendarDayType DayType { get; set; } = CalendarDayType.SchoolDay;

    [MaxLength(200)]
    public string? Note { get; set; }
}

/// <summary>
/// Class within a school year (SRS Entity 11, named SchoolClass to prevent C# keyword conflict).
/// </summary>
public class SchoolClass
{
    public int Id { get; set; }

    public int SchoolYearId { get; set; }
    public SchoolYear SchoolYear { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty; // e.g. "1A", "2B"

    public int Grade { get; set; } // 1, 2, 3, 4, 5

    [MaxLength(50)]
    public string? RoomNumber { get; set; }

    public int Capacity { get; set; } = 35;

    public bool IsActive { get; set; } = true;

    // Navigations
    public ICollection<HomeroomAssignment> HomeroomAssignments { get; set; } = new List<HomeroomAssignment>();
    public ICollection<ClassAssignment> ClassAssignments { get; set; } = new List<ClassAssignment>();
    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

/// <summary>
/// Assignment of Homeroom Teacher to Class (SRS Entity 12).
/// </summary>
public class HomeroomAssignment
{
    public int Id { get; set; }

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public int StaffProfileId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string? Note { get; set; }
}

/// <summary>
/// Assignment of Student to Class preserving history (SRS Entity 13).
/// </summary>
public class ClassAssignment
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public int? RollNumber { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string? TransferNote { get; set; }
}

/// <summary>
/// Weekly schedule entry for a Class (SRS Entity 14).
/// </summary>
public class ClassSchedule
{
    public int Id { get; set; }

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public int PeriodNumber { get; set; } // 1..8

    [Required, MaxLength(100)]
    public string SubjectName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TeacherName { get; set; }

    [MaxLength(50)]
    public string? Room { get; set; }
}

/// <summary>
/// Daily attendance record for one student on one date (SRS Entity 15).
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public DateTime Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public int? MarkedByStaffId { get; set; }
    public StaffProfile? MarkedByStaff { get; set; }

    public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    public string? Note { get; set; }

    public int? CorrectedByStaffId { get; set; }
    public StaffProfile? CorrectedByStaff { get; set; }

    public DateTime? CorrectedAt { get; set; }

    [MaxLength(255)]
    public string? CorrectionReason { get; set; }
}

/// <summary>
/// Common request header for workflows (SRS Entity 19).
/// </summary>
public class AdministrativeRequest
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string RequestCode { get; set; } = string.Empty; // e.g. REQ-2026-0001

    public RequestType RequestType { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int RequesterGuardianId { get; set; }
    public Guardian RequesterGuardian { get; set; } = null!;

    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    [MaxLength(50)]
    public string? CurrentAssigneeRole { get; set; } // Teacher, Office, Board

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigations
    public LeaveRequest? LeaveRequest { get; set; }
    public ClassTransferDetail? ClassTransferDetail { get; set; }
    public DocumentRequestDetail? DocumentRequestDetail { get; set; }
    public IssuedDocument? IssuedDocument { get; set; }
    public ICollection<RequestStatusHistory> StatusHistories { get; set; } = new List<RequestStatusHistory>();
    public ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
}

/// <summary>
/// Leave/absence request details associated with an AdministrativeRequest (SRS Entity 16).
/// </summary>
public class LeaveRequest
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int TotalDays { get; set; } = 1;

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public bool IsEscalatedToBoard { get; set; } = false;

    [MaxLength(255)]
    public string? MedicalCertificateUrl { get; set; }
}

/// <summary>
/// Daily lunch availability and registration/cancellation cutoff (SRS Entity 17).
/// </summary>
public class LunchServiceDay
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public bool IsAvailable { get; set; } = true;

    public TimeSpan CutoffTime { get; set; } = new TimeSpan(8, 0, 0); // e.g. 08:00 AM

    [MaxLength(500)]
    public string? MenuDescription { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; } = 35000; // 35,000 VND

    [MaxLength(255)]
    public string? Notes { get; set; }

    // Navigations
    public ICollection<LunchRegistration> Registrations { get; set; } = new List<LunchRegistration>();
}

/// <summary>
/// Student lunch registration for one service date (SRS Entity 18).
/// </summary>
public class LunchRegistration
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int LunchServiceDayId { get; set; }
    public LunchServiceDay LunchServiceDay { get; set; } = null!;

    public DateTime Date { get; set; }

    public LunchRegistrationStatus Status { get; set; } = LunchRegistrationStatus.Registered;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    public int? AdjustedByStaffId { get; set; }
    public StaffProfile? AdjustedByStaff { get; set; }

    [MaxLength(255)]
    public string? AdjustmentReason { get; set; }
}

/// <summary>
/// Chronological status transitions and processing notes for an AdministrativeRequest (SRS Entity 20).
/// </summary>
public class RequestStatusHistory
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public RequestStatus FromStatus { get; set; }

    public RequestStatus ToStatus { get; set; }

    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ActionNotes { get; set; }
}

/// <summary>
/// File metadata linked to an AdministrativeRequest (SRS Entity 21).
/// </summary>
public class RequestAttachment
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Class-transfer-specific information linked one-to-one to AdministrativeRequest (SRS Entity 22).
/// </summary>
public class ClassTransferDetail
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public int CurrentClassId { get; set; }
    public SchoolClass CurrentClass { get; set; } = null!;

    public int TargetClassId { get; set; }
    public SchoolClass TargetClass { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? EffectiveDate { get; set; }

    public bool IsApplied { get; set; } = false;
}

/// <summary>
/// School-document-request-specific information linked one-to-one to AdministrativeRequest (SRS Entity 23).
/// </summary>
public class DocumentRequestDetail
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty; // e.g. "Giấy xác nhận đang học", "Giấy xác nhận chuyên cần", "Bản sao học bạ"

    public int Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Purpose { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Metadata about a school document issued in response to an approved request (SRS Entity 24).
/// </summary>
public class IssuedDocument
{
    public int Id { get; set; }

    public int AdministrativeRequestId { get; set; }
    public AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    [Required, MaxLength(50)]
    public string DocumentNumber { get; set; } = string.Empty; // e.g. "GXN-2026/0123"

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;

    public int? IssuedByStaffId { get; set; }
    public StaffProfile? IssuedByStaff { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? ContentSummary { get; set; }

    [MaxLength(500)]
    public string? FileAttachmentUrl { get; set; }
}

/// <summary>
/// Recipient-specific in-system notification and read state (SRS Entity 25).
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public int RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? LinkUrl { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "Workflow"; // Workflow, Announcement, System

    public bool IsRead { get; set; } = false;

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// School announcement content and target audience (SRS Entity 26).
/// </summary>
public class Announcement
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public AnnouncementAudience TargetAudience { get; set; } = AnnouncementAudience.All;

    public int PublishedByUserId { get; set; }
    public User PublishedByUser { get; set; } = null!;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpireAt { get; set; }

    public bool IsPinned { get; set; } = false;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Append-only security and administrative audit record (SRS Entity 27).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    [MaxLength(100)]
    public string ActorName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ActorRole { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, StatusChange, Login, PasswordReset, Assign, Export

    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EntityId { get; set; }

    [MaxLength(2000)]
    public string? Details { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(255)]
    public string? UserAgent { get; set; }
}

/// <summary>
/// Configurable key-value system settings (UC-59).
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Simulated outgoing email queue for verification and notifications (UC-01..05, Admin view).
/// </summary>
public class EmailOutbox
{
    public int Id { get; set; }

    [Required, MaxLength(100), EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? OtpCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Sent"; // Sent, Failed

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Configurable notification templates (UC-57).
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string BodyTemplate { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }
}

/// <summary>
/// Configurable reference data list (UC-56).
/// </summary>
public class ReferenceData
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // e.g. DocumentType, Relationship

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}
