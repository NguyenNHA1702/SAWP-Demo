namespace SAWP.Web.Domain;

/// <summary>
/// System roles supported in SAWP.
/// Parent (Phụ huynh), Teacher (GVCN), Office (Giáo vụ), Service (Nhân viên bếp ăn), Board (Ban Giám hiệu), Admin (Quản trị).
/// </summary>
public enum SystemRole
{
    Admin,
    Board,
    Office,
    Teacher,
    Service,
    Parent
}

/// <summary>
/// User account status.
/// </summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Locked = 3
}

/// <summary>
/// Gender representation.
/// </summary>
public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

/// <summary>
/// Relationship of guardian to student (BR-03, UC-10).
/// </summary>
public enum GuardianRelationship
{
    Father = 1,      // Bố
    Mother = 2,      // Mẹ
    Grandparent = 3, // Ông / Bà
    Guardian = 4,    // Người giám hộ hợp pháp
    Other = 5        // Khác
}

/// <summary>
/// School year lifecycle status (UC-13).
/// </summary>
public enum SchoolYearStatus
{
    Planned = 1,
    Active = 2,
    Archived = 3
}

/// <summary>
/// Types of calendar days (UC-20).
/// </summary>
public enum CalendarDayType
{
    SchoolDay = 1,
    Weekend = 2,
    Holiday = 3,
    ExceptionalClosure = 4
}

/// <summary>
/// Daily attendance statuses (UC-21, BR-10).
/// </summary>
public enum AttendanceStatus
{
    Present = 1,             // Có mặt
    AbsentWithLeave = 2,     // Nghỉ có phép
    AbsentWithoutLeave = 3,  // Nghỉ không phép
    Late = 4                 // Đi muộn
}

/// <summary>
/// Core types of administrative requests (UC-25, UC-36, UC-40).
/// </summary>
public enum RequestType
{
    Leave = 1,           // Xin nghỉ học
    ClassTransfer = 2,   // Chuyển lớp
    DocumentIssuance = 3 // Xin cấp giấy tờ/bản sao
}

/// <summary>
/// Common workflow statuses for administrative requests (BR-35, BR-36).
/// </summary>
public enum RequestStatus
{
    Draft = 1,
    Submitted = 2,
    InReview = 3,
    Escalated = 4,
    Approved = 5,
    Rejected = 6,
    Cancelled = 7,
    Completed = 8
}

/// <summary>
/// Daily lunch registration status (UC-29..34).
/// </summary>
public enum LunchRegistrationStatus
{
    Registered = 1,   // Đã đăng ký
    Cancelled = 2,    // Đã hủy (trước cutoff hoặc do GV/Bếp điều chỉnh)
    Adjusted = 3      // Điều chỉnh đặc biệt bởi giáo vụ/nhân viên bếp
}

/// <summary>
/// Target audience for announcements (UC-47).
/// </summary>
public enum AnnouncementAudience
{
    All = 1,
    Parents = 2,
    Teachers = 3,
    Staff = 4
}
