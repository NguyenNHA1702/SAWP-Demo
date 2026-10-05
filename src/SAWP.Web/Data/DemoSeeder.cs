using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Services;

namespace SAWP.Web.Data;

/// <summary>
/// Seeds realistic, interconnected demo data for SAWP primary school platform.
/// Idempotent: only seeds if data is missing.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(SawpDbContext db, IPasswordService passwordService, ILogger logger)
    {
        logger.LogInformation("[DemoSeeder] Checking and seeding database...");

        // 1. Roles
        var roleNames = new[]
        {
            (nameof(SystemRole.Admin), "Quản trị hệ thống", "Toàn quyền quản trị kỹ thuật và cấu hình."),
            (nameof(SystemRole.Board), "Ban Giám hiệu", "Hiệu trưởng/Phó hiệu trưởng duyệt yêu cầu và báo cáo toàn trường."),
            (nameof(SystemRole.Office), "Giáo vụ", "Quản lý hồ sơ học sinh, phân lớp, duyệt giấy tờ và điều chỉnh điểm danh."),
            (nameof(SystemRole.Teacher), "Giáo viên chủ nhiệm", "Điểm danh hàng ngày, duyệt nghỉ học thường nhật, quản lý lớp."),
            (nameof(SystemRole.Service), "Nhân viên bếp ăn", "Quản lý suất ăn bán trú, chốt danh sách và điều chỉnh bữa ăn."),
            (nameof(SystemRole.Parent), "Phụ huynh", "Xem hồ sơ con, nộp đơn nghỉ học, chuyển lớp, đăng ký ăn trưa.")
        };

        var roleMap = new Dictionary<string, Role>();
        foreach (var (rName, dName, desc) in roleNames)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == rName);
            if (role == null)
            {
                role = new Role { Name = rName, DisplayName = dName, Description = desc, IsSystem = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }
            roleMap[rName] = role;
        }

        // 2. Settings (UC-59)
        var defaultSettings = new[]
        {
            ("Leave.MaxDaysForTeacher", "3", "Số ngày nghỉ tối đa do GVCN duyệt. Trên số ngày này sẽ chuyển lên Ban Giám hiệu.", "Workflow"),
            ("Lunch.CutoffTime", "08:00", "Thời hạn đăng ký và hủy suất ăn trong ngày.", "Service"),
            ("Document.DefaultProcessingDays", "3", "Thời gian xử lý cấp phát tài liệu tiêu chuẩn.", "Workflow"),
            ("Attendance.TeacherEditWindowHours", "24", "Thời gian cho phép GVCN tự sửa điểm danh (giờ).", "Attendance")
        };

        foreach (var (key, val, desc, cat) in defaultSettings)
        {
            if (!await db.SystemSettings.AnyAsync(s => s.Key == key))
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = val,
                    Description = desc,
                    Category = cat,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "Seeder"
                });
            }
        }

        // 3. Reference Data (UC-56)
        var referenceData = new[]
        {
            ("DocumentType", "STUDY_CONFIRMATION", "Giấy xác nhận đang học tại trường", 1),
            ("DocumentType", "ATTENDANCE_CONFIRMATION", "Giấy xác nhận chuyên cần", 2),
            ("DocumentType", "TRANSCRIPT_COPY", "Bản sao học bạ / Hồ sơ chuyển trường", 3),
            ("Relationship", "FATHER", "Bố", 1),
            ("Relationship", "MOTHER", "Mẹ", 2),
            ("Relationship", "GUARDIAN", "Người giám hộ", 3)
        };

        foreach (var (cat, code, name, sort) in referenceData)
        {
            if (!await db.ReferenceDataList.AnyAsync(r => r.Category == cat && r.Code == code))
            {
                db.ReferenceDataList.Add(new ReferenceData
                {
                    Category = cat,
                    Code = code,
                    Name = name,
                    SortOrder = sort,
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync();

        // 4. Demo Users
        var demoAccounts = new[]
        {
            ("admin", "admin@sawp.edu.vn", "Nguyễn Quản Trị", nameof(SystemRole.Admin), "0901000001"),
            ("bgh.hung", "bgh.hung@sawp.edu.vn", "Nguyễn Văn Hùng", nameof(SystemRole.Board), "0901000002"),
            ("giaovu.mai", "giaovu.mai@sawp.edu.vn", "Trần Thị Mai", nameof(SystemRole.Office), "0901000003"),
            ("gv.lan", "gv.lan@sawp.edu.vn", "Lê Thị Lan", nameof(SystemRole.Teacher), "0901000004"),
            ("bep.tuan", "bep.tuan@sawp.edu.vn", "Phạm Văn Tuấn", nameof(SystemRole.Service), "0901000005"),
            ("ph.nam", "ph.nam@sawp.edu.vn", "Hoàng Văn Nam", nameof(SystemRole.Parent), "0901000006")
        };

        var userMap = new Dictionary<string, User>();
        foreach (var (uname, email, fname, rname, phone) in demoAccounts)
        {
            var user = await db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Username == uname);

            if (user == null)
            {
                user = new User
                {
                    Username = uname,
                    Email = email,
                    FullName = fname,
                    PhoneNumber = phone,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = passwordService.HashPassword(user, "Demo@123");
                db.Users.Add(user);
                await db.SaveChangesAsync();

                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleMap[rname].Id
                });
                await db.SaveChangesAsync();
            }
            userMap[uname] = user;
        }

        // 5. Staff Profiles
        var staffData = new[]
        {
            ("bgh.hung", "CB-001", "Ban Giám hiệu", "Hiệu trưởng"),
            ("giaovu.mai", "CB-002", "Phòng Giáo vụ", "Chuyên viên Giáo vụ"),
            ("gv.lan", "GV-001", "Tổ Khối 1", "Giáo viên chủ nhiệm"),
            ("bep.tuan", "NV-001", "Tổ Bếp & Dịch vụ", "Bếp trưởng")
        };

        var staffMap = new Dictionary<string, StaffProfile>();
        foreach (var (uname, code, dept, pos) in staffData)
        {
            var user = userMap[uname];
            var staff = await db.StaffProfiles.FirstOrDefaultAsync(s => s.StaffCode == code);
            if (staff == null)
            {
                staff = new StaffProfile
                {
                    UserId = user.Id,
                    StaffCode = code,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Department = dept,
                    Position = pos,
                    IsActive = true
                };
                db.StaffProfiles.Add(staff);
                await db.SaveChangesAsync();
            }
            staffMap[uname] = staff;
        }

        // 6. Guardian Profile for ph.nam
        var phUser = userMap["ph.nam"];
        var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.UserId == phUser.Id);
        if (guardian == null)
        {
            guardian = new Guardian
            {
                UserId = phUser.Id,
                FullName = phUser.FullName,
                PhoneNumber = phUser.PhoneNumber!,
                Email = phUser.Email,
                IdentityNumber = "001200001234",
                Address = "Số 15 phố Cầu Giấy, Hà Nội",
                Occupation = "Kỹ sư",
                IsActive = true
            };
            db.Guardians.Add(guardian);
            await db.SaveChangesAsync();
        }

        // 7. School Year (2026 - 2027)
        var schoolYear = await db.SchoolYears.FirstOrDefaultAsync(y => y.Code == "2026-2027");
        if (schoolYear == null)
        {
            schoolYear = new SchoolYear
            {
                Code = "2026-2027",
                Name = "Năm học 2026 - 2027",
                StartDate = new DateTime(2026, 9, 1),
                EndDate = new DateTime(2027, 5, 31),
                Status = SchoolYearStatus.Active
            };
            db.SchoolYears.Add(schoolYear);
            await db.SaveChangesAsync();
        }

        // 8. Classes (1A, 1B, 2A, 3A, 4A, 5A)
        var classNames = new[]
        {
            ("1A", 1, "P.101", 35),
            ("1B", 1, "P.102", 35),
            ("2A", 2, "P.201", 35),
            ("3A", 3, "P.301", 35),
            ("4A", 4, "P.401", 35),
            ("5A", 5, "P.501", 35)
        };

        var classMap = new Dictionary<string, SchoolClass>();
        foreach (var (cName, grade, room, cap) in classNames)
        {
            var cls = await db.SchoolClasses.FirstOrDefaultAsync(c => c.SchoolYearId == schoolYear.Id && c.Name == cName);
            if (cls == null)
            {
                cls = new SchoolClass
                {
                    SchoolYearId = schoolYear.Id,
                    Name = cName,
                    Grade = grade,
                    RoomNumber = room,
                    Capacity = cap,
                    IsActive = true
                };
                db.SchoolClasses.Add(cls);
                await db.SaveChangesAsync();
            }
            classMap[cName] = cls;
        }

        // 9. Assign GVCN gv.lan to Class 1A
        var class1A = classMap["1A"];
        var teacherLan = staffMap["gv.lan"];
        var hrLan = await db.HomeroomAssignments.FirstOrDefaultAsync(h => h.ClassId == class1A.Id && h.StaffProfileId == teacherLan.Id);
        if (hrLan == null)
        {
            hrLan = new HomeroomAssignment
            {
                ClassId = class1A.Id,
                StaffProfileId = teacherLan.Id,
                StartDate = new DateTime(2026, 9, 1),
                IsActive = true,
                Note = "Phân công đầu năm học 2026-2027"
            };
            db.HomeroomAssignments.Add(hrLan);
            await db.SaveChangesAsync();
        }

        // 10. Students & Guardians
        // Student 1: Hoàng Minh Đức (Class 1A, linked to ph.nam)
        var studentDuc = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == "HS2601001");
        if (studentDuc == null)
        {
            studentDuc = new Student
            {
                StudentCode = "HS2601001",
                FullName = "Hoàng Minh Đức",
                DateOfBirth = new DateTime(2020, 3, 15),
                Gender = Gender.Male,
                AdmissionDate = new DateTime(2026, 8, 15),
                PlaceOfBirth = "Hà Nội",
                Address = "Số 15 phố Cầu Giấy, Hà Nội",
                HealthNotes = "Không có tiền sử dị ứng thực phẩm",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.Students.Add(studentDuc);
            await db.SaveChangesAsync();

            // Link to guardian
            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = studentDuc.Id,
                GuardianId = guardian.Id,
                Relationship = GuardianRelationship.Father,
                IsPrimaryContact = true,
                CanPickup = true
            });

            // Assign to 1A
            db.ClassAssignments.Add(new ClassAssignment
            {
                StudentId = studentDuc.Id,
                ClassId = class1A.Id,
                RollNumber = 1,
                StartDate = new DateTime(2026, 9, 1),
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        // Student 2: Hoàng Mai Anh (Class 3A, linked to ph.nam)
        var studentMaiAnh = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == "HS2403001");
        if (studentMaiAnh == null)
        {
            var class3A = classMap["3A"];
            studentMaiAnh = new Student
            {
                StudentCode = "HS2403001",
                FullName = "Hoàng Mai Anh",
                DateOfBirth = new DateTime(2018, 7, 20),
                Gender = Gender.Female,
                AdmissionDate = new DateTime(2024, 8, 15),
                PlaceOfBirth = "Hà Nội",
                Address = "Số 15 phố Cầu Giấy, Hà Nội",
                HealthNotes = "Bình thường",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.Students.Add(studentMaiAnh);
            await db.SaveChangesAsync();

            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = studentMaiAnh.Id,
                GuardianId = guardian.Id,
                Relationship = GuardianRelationship.Father,
                IsPrimaryContact = true,
                CanPickup = true
            });

            db.ClassAssignments.Add(new ClassAssignment
            {
                StudentId = studentMaiAnh.Id,
                ClassId = class3A.Id,
                RollNumber = 5,
                StartDate = new DateTime(2026, 9, 1),
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        // Classmates in 1A
        var classmates1A = new[]
        {
            ("HS2601002", "Bùi Quỳnh Chi", Gender.Female, new DateTime(2020, 5, 12), 2),
            ("HS2601003", "Trần Đăng Khoa", Gender.Male, new DateTime(2020, 8, 22), 3),
            ("HS2601004", "Vũ Gia Bảo", Gender.Male, new DateTime(2020, 1, 10), 4),
            ("HS2601005", "Đỗ Bảo Trâm", Gender.Female, new DateTime(2020, 11, 4), 5),
            ("HS2601006", "Nguyễn Tuấn Kiệt", Gender.Male, new DateTime(2020, 4, 18), 6),
            ("HS2601007", "Phạm Ngọc Ánh", Gender.Female, new DateTime(2020, 9, 30), 7),
            ("HS2601008", "Lê Hải Đăng", Gender.Male, new DateTime(2020, 6, 14), 8),
            ("HS2601009", "Đặng Thùy Dương", Gender.Female, new DateTime(2020, 2, 28), 9),
            ("HS2601010", "Ngô Thành Nam", Gender.Male, new DateTime(2020, 10, 8), 10)
        };

        foreach (var (code, name, gender, dob, roll) in classmates1A)
        {
            if (!await db.Students.AnyAsync(s => s.StudentCode == code))
            {
                var classmate = new Student
                {
                    StudentCode = code,
                    FullName = name,
                    Gender = gender,
                    DateOfBirth = dob,
                    AdmissionDate = new DateTime(2026, 8, 15),
                    PlaceOfBirth = "Hà Nội",
                    Address = "Hà Nội",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                db.Students.Add(classmate);
                await db.SaveChangesAsync();

                db.ClassAssignments.Add(new ClassAssignment
                {
                    StudentId = classmate.Id,
                    ClassId = class1A.Id,
                    RollNumber = roll,
                    StartDate = new DateTime(2026, 9, 1),
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }
        }

        // 11. Schedule for Class 1A (Monday to Friday, periods 1..4 morning)
        if (!await db.ClassSchedules.AnyAsync(s => s.ClassId == class1A.Id))
        {
            var scheduleSlots = new[]
            {
                (DayOfWeek.Monday, 1, "Chào cờ / Sinh hoạt dưới cờ", "Lê Thị Lan"),
                (DayOfWeek.Monday, 2, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Monday, 3, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Monday, 4, "Toán", "Lê Thị Lan"),
                (DayOfWeek.Tuesday, 1, "Toán", "Lê Thị Lan"),
                (DayOfWeek.Tuesday, 2, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Tuesday, 3, "Đạo đức", "Lê Thị Lan"),
                (DayOfWeek.Tuesday, 4, "Giáo dục thể chất", "Vũ Thể Dục"),
                (DayOfWeek.Wednesday, 1, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Wednesday, 2, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Wednesday, 3, "Toán", "Lê Thị Lan"),
                (DayOfWeek.Wednesday, 4, "Âm nhạc", "Trần Âm Nhạc"),
                (DayOfWeek.Thursday, 1, "Toán", "Lê Thị Lan"),
                (DayOfWeek.Thursday, 2, "Tự nhiên & Xã hội", "Lê Thị Lan"),
                (DayOfWeek.Thursday, 3, "Mỹ thuật", "Nguyễn Mỹ Thuật"),
                (DayOfWeek.Thursday, 4, "Hoạt động trải nghiệm", "Lê Thị Lan"),
                (DayOfWeek.Friday, 1, "Tiếng Việt", "Lê Thị Lan"),
                (DayOfWeek.Friday, 2, "Toán (Ôn tập)", "Lê Thị Lan"),
                (DayOfWeek.Friday, 3, "Tiếng Anh", "Ms. Jenny"),
                (DayOfWeek.Friday, 4, "Sinh hoạt lớp", "Lê Thị Lan")
            };

            foreach (var (dow, period, subject, teacher) in scheduleSlots)
            {
                db.ClassSchedules.Add(new ClassSchedule
                {
                    ClassId = class1A.Id,
                    DayOfWeek = dow,
                    PeriodNumber = period,
                    SubjectName = subject,
                    TeacherName = teacher,
                    Room = class1A.RoomNumber
                });
            }
            await db.SaveChangesAsync();
        }

        // 12. Lunch Service Days for October 2026
        var today = DateTime.Today;
        for (int i = -3; i <= 7; i++)
        {
            var date = today.AddDays(i);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                var lunchDay = await db.LunchServiceDays.FirstOrDefaultAsync(l => l.Date == date);
                if (lunchDay == null)
                {
                    lunchDay = new LunchServiceDay
                    {
                        Date = date,
                        IsAvailable = true,
                        CutoffTime = new TimeSpan(8, 0, 0),
                        MenuDescription = "Cơm trắng, Sườn xào chua ngọt, Canh ngao mồng tơi, Tráng miệng thanh long",
                        Price = 35000,
                        Notes = "Thực đơn tiêu chuẩn bán trú trường Tiểu học"
                    };
                    db.LunchServiceDays.Add(lunchDay);
                    await db.SaveChangesAsync();
                }

                // Register studentDuc for these lunch days
                if (!await db.LunchRegistrations.AnyAsync(r => r.StudentId == studentDuc.Id && r.Date == date))
                {
                    db.LunchRegistrations.Add(new LunchRegistration
                    {
                        StudentId = studentDuc.Id,
                        LunchServiceDayId = lunchDay.Id,
                        Date = date,
                        Status = LunchRegistrationStatus.Registered,
                        RegisteredAt = DateTime.UtcNow.AddDays(-5)
                    });
                }
            }
        }
        await db.SaveChangesAsync();

        // 13. Sample Administrative Requests for Demo
        if (!await db.AdministrativeRequests.AnyAsync())
        {
            // Request 1: Leave request for Duc
            var req1 = new AdministrativeRequest
            {
                RequestCode = "REQ-2026-0001",
                RequestType = RequestType.Leave,
                StudentId = studentDuc.Id,
                RequesterGuardianId = guardian.Id,
                Status = RequestStatus.Approved,
                CurrentAssigneeRole = nameof(SystemRole.Teacher),
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                DecidedAt = DateTime.UtcNow.AddDays(-1),
                DecidedByUserId = userMap["gv.lan"].Id,
                Notes = "Đã duyệt nghỉ học lý do khám răng định kỳ"
            };
            req1.LeaveRequest = new LeaveRequest
            {
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(-1),
                TotalDays = 1,
                Reason = "Cháu bị đau răng cần đi kiểm tra và nhổ răng tại viện Răng Hàm Mặt.",
                IsEscalatedToBoard = false
            };
            req1.StatusHistories.Add(new RequestStatusHistory
            {
                FromStatus = RequestStatus.Submitted,
                ToStatus = RequestStatus.Approved,
                ChangedByUserId = userMap["gv.lan"].Id,
                ChangedAt = DateTime.UtcNow.AddDays(-1),
                ActionNotes = "GVCN Lê Thị Lan đã duyệt đơn xin nghỉ 1 ngày."
            });
            db.AdministrativeRequests.Add(req1);

            // Request 2: Document request (Giấy xác nhận đang học)
            var req2 = new AdministrativeRequest
            {
                RequestCode = "REQ-2026-0002",
                RequestType = RequestType.DocumentIssuance,
                StudentId = studentDuc.Id,
                RequesterGuardianId = guardian.Id,
                Status = RequestStatus.InReview,
                CurrentAssigneeRole = nameof(SystemRole.Office),
                SubmittedAt = DateTime.UtcNow.AddHours(-12),
                Notes = "Đang xử lý hồ sơ tại phòng Giáo vụ"
            };
            req2.DocumentRequestDetail = new DocumentRequestDetail
            {
                DocumentType = "Giấy xác nhận đang học tại trường",
                Quantity = 2,
                Purpose = "Bổ sung hồ sơ công tác và bảo hiểm y tế gia đình.",
                Notes = "Xin nhận bản cứng có dấu đỏ tại phòng Giáo vụ."
            };
            req2.StatusHistories.Add(new RequestStatusHistory
            {
                FromStatus = RequestStatus.Draft,
                ToStatus = RequestStatus.Submitted,
                ChangedByUserId = phUser.Id,
                ChangedAt = DateTime.UtcNow.AddHours(-12),
                ActionNotes = "Phụ huynh gửi yêu cầu cấp giấy xác nhận."
            });
            db.AdministrativeRequests.Add(req2);

            await db.SaveChangesAsync();
        }

        // 14. School Announcement (UC-47)
        if (!await db.Announcements.AnyAsync())
        {
            db.Announcements.Add(new Announcement
            {
                Title = "Thông báo kế hoạch bán trú và sinh hoạt ngoại khóa Tháng 10/2026",
                Content = "Kính gửi quý phụ huynh và cán bộ giáo viên, nhà trường triển khai thực đơn bán trú mùa thu cùng chương trình trải nghiệm kỹ năng sống khối 1-5 từ ngày 10/10/2026. Quý phụ huynh vui lòng cập nhật đăng ký ăn trưa trước 8:00 hàng ngày.",
                TargetAudience = AnnouncementAudience.All,
                PublishedByUserId = userMap["bgh.hung"].Id,
                PublishedAt = DateTime.UtcNow,
                IsPinned = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        logger.LogInformation("[DemoSeeder] Seeding completed successfully!");
    }
}
