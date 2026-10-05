using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;

namespace SAWP.Web.Data;

/// <summary>
/// EF Core DbContext for SAWP (School Administration & Workflow Platform).
/// Enforces relational constraints, unique indexes, and audit configurations.
/// </summary>
public class SawpDbContext : DbContext
{
    public SawpDbContext(DbContextOptions<SawpDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<SchoolCalendarDay> SchoolCalendarDays => Set<SchoolCalendarDay>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<HomeroomAssignment> HomeroomAssignments => Set<HomeroomAssignment>();
    public DbSet<ClassAssignment> ClassAssignments => Set<ClassAssignment>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AdministrativeRequest> AdministrativeRequests => Set<AdministrativeRequest>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LunchServiceDay> LunchServiceDays => Set<LunchServiceDay>();
    public DbSet<LunchRegistration> LunchRegistrations => Set<LunchRegistration>();
    public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<ClassTransferDetail> ClassTransferDetails => Set<ClassTransferDetail>();
    public DbSet<DocumentRequestDetail> DocumentRequestDetails => Set<DocumentRequestDetail>();
    public DbSet<IssuedDocument> IssuedDocuments => Set<IssuedDocument>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<EmailOutbox> EmailOutboxes => Set<EmailOutbox>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<ReferenceData> ReferenceDataList => Set<ReferenceData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User & Role composite keys
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        // Unique indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Code)
            .IsUnique();

        modelBuilder.Entity<StaffProfile>()
            .HasIndex(s => s.StaffCode)
            .IsUnique();

        modelBuilder.Entity<Student>()
            .HasIndex(s => s.StudentCode)
            .IsUnique();

        modelBuilder.Entity<SchoolYear>()
            .HasIndex(s => s.Code)
            .IsUnique();

        modelBuilder.Entity<SchoolCalendarDay>()
            .HasIndex(d => new { d.SchoolYearId, d.Date })
            .IsUnique();

        modelBuilder.Entity<AdministrativeRequest>()
            .HasIndex(r => r.RequestCode)
            .IsUnique();

        modelBuilder.Entity<IssuedDocument>()
            .HasIndex(d => d.DocumentNumber)
            .IsUnique();

        modelBuilder.Entity<SystemSetting>()
            .HasIndex(s => s.Key)
            .IsUnique();

        modelBuilder.Entity<ReferenceData>()
            .HasIndex(r => new { r.Category, r.Code })
            .IsUnique();

        modelBuilder.Entity<LunchServiceDay>()
            .HasIndex(l => l.Date)
            .IsUnique();

        modelBuilder.Entity<AttendanceRecord>()
            .HasIndex(a => new { a.StudentId, a.Date })
            .IsUnique();

        modelBuilder.Entity<LunchRegistration>()
            .HasIndex(l => new { l.StudentId, l.Date })
            .IsUnique();

        modelBuilder.Entity<StudentGuardian>()
            .HasIndex(sg => new { sg.StudentId, sg.GuardianId })
            .IsUnique();

        // 1-to-1 relationships for AdministrativeRequest subtypes
        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(r => r.LeaveRequest)
            .WithOne(l => l.AdministrativeRequest)
            .HasForeignKey<LeaveRequest>(l => l.AdministrativeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(r => r.ClassTransferDetail)
            .WithOne(t => t.AdministrativeRequest)
            .HasForeignKey<ClassTransferDetail>(t => t.AdministrativeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(r => r.DocumentRequestDetail)
            .WithOne(d => d.AdministrativeRequest)
            .HasForeignKey<DocumentRequestDetail>(d => d.AdministrativeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(r => r.IssuedDocument)
            .WithOne(d => d.AdministrativeRequest)
            .HasForeignKey<IssuedDocument>(d => d.AdministrativeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Specific delete behaviors to avoid cycles
        modelBuilder.Entity<ClassTransferDetail>()
            .HasOne(ct => ct.CurrentClass)
            .WithMany()
            .HasForeignKey(ct => ct.CurrentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassTransferDetail>()
            .HasOne(ct => ct.TargetClass)
            .WithMany()
            .HasForeignKey(ct => ct.TargetClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(a => a.Class)
            .WithMany(c => c.AttendanceRecords)
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(a => a.Student)
            .WithMany(s => s.AttendanceRecords)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassAssignment>()
            .HasOne(ca => ca.Student)
            .WithMany(s => s.ClassAssignments)
            .HasForeignKey(ca => ca.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassAssignment>()
            .HasOne(ca => ca.Class)
            .WithMany(c => c.ClassAssignments)
            .HasForeignKey(ca => ca.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeroomAssignment>()
            .HasOne(ha => ha.Class)
            .WithMany(c => c.HomeroomAssignments)
            .HasForeignKey(ha => ha.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeroomAssignment>()
            .HasOne(ha => ha.StaffProfile)
            .WithMany(sp => sp.HomeroomAssignments)
            .HasForeignKey(ha => ha.StaffProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentGuardian>()
            .HasOne(sg => sg.Student)
            .WithMany(s => s.StudentGuardians)
            .HasForeignKey(sg => sg.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentGuardian>()
            .HasOne(sg => sg.Guardian)
            .WithMany(g => g.StudentGuardians)
            .HasForeignKey(sg => sg.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LunchRegistration>()
            .HasOne(lr => lr.Student)
            .WithMany(s => s.LunchRegistrations)
            .HasForeignKey(lr => lr.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LunchRegistration>()
            .HasOne(lr => lr.LunchServiceDay)
            .WithMany(lsd => lsd.Registrations)
            .HasForeignKey(lr => lr.LunchServiceDayId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(ar => ar.Student)
            .WithMany(s => s.AdministrativeRequests)
            .HasForeignKey(ar => ar.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AdministrativeRequest>()
            .HasOne(ar => ar.RequesterGuardian)
            .WithMany(g => g.Requests)
            .HasForeignKey(ar => ar.RequesterGuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
