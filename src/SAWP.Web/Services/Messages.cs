namespace SAWP.Web.Services;

/// <summary>
/// Central message repository for SAWP.
/// Implements SRS Section 4.2 Application Messages (MSG01 - MSG50).
/// UI text is strictly in Vietnamese.
/// </summary>
public static class Messages
{
    private static readonly Dictionary<string, string> MessageDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        { "MSG01", "Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ." },
        { "MSG02", "Tên đăng nhập hoặc mật khẩu không chính xác." },
        { "MSG03", "Tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên." },
        { "MSG04", "Mật khẩu mới chưa đáp ứng yêu cầu hoặc xác nhận mật khẩu không khớp." },
        { "MSG05", "Đăng nhập thành công." },
        { "MSG06", "Đã đăng xuất khỏi hệ thống." },
        { "MSG07", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." },
        { "MSG08", "Lưu thông tin thành công." },
        { "MSG09", "Không thể thực hiện thao tác với trạng thái hiện tại." },
        { "MSG10", "Bạn không có quyền truy cập hoặc thực hiện thao tác này." },
        { "MSG11", "Không tìm thấy hồ sơ học sinh." },
        { "MSG12", "Mã học sinh đã tồn tại trong hệ thống." },
        { "MSG13", "Đã cập nhật liên kết phụ huynh/người giám hộ." },
        { "MSG14", "Chưa có năm học đang hoạt động." },
        { "MSG15", "Lớp được chọn không còn phù hợp hoặc đã đạt giới hạn sĩ số." },
        { "MSG16", "Đã cập nhật phân lớp cho học sinh." },
        { "MSG17", "Thời khóa biểu bị trùng khung giờ. Vui lòng kiểm tra lại." },
        { "MSG18", "Đã lưu điểm danh." },
        { "MSG19", "Bản ghi điểm danh đã bị khóa. Vui lòng liên hệ Giáo vụ nếu cần điều chỉnh." },
        { "MSG20", "Bạn không được phân công phụ trách lớp này." },
        { "MSG21", "Đã gửi đơn xin nghỉ/vắng học." },
        { "MSG22", "Đơn xin nghỉ/vắng học đã được chấp thuận." },
        { "MSG23", "Đơn xin nghỉ/vắng học không được chấp thuận." },
        { "MSG24", "Khoảng thời gian xin nghỉ không hợp lệ hoặc đang trùng với yêu cầu khác." },
        { "MSG25", "Đăng ký ăn trưa thành công." },
        { "MSG26", "Đã hủy đăng ký ăn trưa." },
        { "MSG27", "Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này." },
        { "MSG28", "Dịch vụ ăn trưa không khả dụng cho ngày đã chọn." },
        { "MSG29", "Học sinh đã có đăng ký ăn trưa cho ngày này." },
        { "MSG30", "Đã gửi yêu cầu hành chính." },
        { "MSG31", "Yêu cầu đã được phê duyệt." },
        { "MSG32", "Yêu cầu không được phê duyệt." },
        { "MSG33", "Yêu cầu đã được xử lý hoàn tất." },
        { "MSG34", "Yêu cầu không thể hủy ở trạng thái hiện tại." },
        { "MSG35", "Đã cập nhật chuyển lớp cho học sinh." },
        { "MSG36", "Tài liệu/giấy xác nhận đã được xử lý và phát hành." },
        { "MSG37", "Đã đánh dấu thông báo là đã đọc." },
        { "MSG38", "Tạo tài khoản người dùng thành công." },
        { "MSG39", "Đã vô hiệu hóa tài khoản người dùng." },
        { "MSG40", "Đã cập nhật vai trò và quyền truy cập." },
        { "MSG41", "Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép." },
        { "MSG42", "Bạn không có quyền xem nhật ký hệ thống." },
        { "MSG43", "Không có dữ liệu phù hợp để hiển thị." },
        { "MSG44", "Đã lưu cấu hình hệ thống." },
        { "MSG45", "Bạn có chắc chắn muốn thực hiện thao tác này?" },
        { "MSG46", "Đổi mật khẩu thành công." },
        { "MSG47", "Cập nhật thông tin thành công." },
        { "MSG48", "Nhập dữ liệu hoàn tất. Vui lòng kiểm tra kết quả các bản ghi thành công và lỗi." },
        { "MSG49", "Một số dữ liệu không thể nhập. Vui lòng kiểm tra danh sách lỗi." },
        { "MSG50", "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." }
    };

    /// <summary>
    /// Gets message content by its code (MSG01 - MSG50).
    /// </summary>
    public static string Get(string code)
    {
        if (MessageDictionary.TryGetValue(code, out var message))
        {
            return message;
        }
        return code;
    }

    /// <summary>
    /// Determines whether the message code indicates a success notification.
    /// </summary>
    public static bool IsSuccess(string code)
    {
        return code is "MSG05" or "MSG06" or "MSG08" or "MSG13" or "MSG16" or "MSG18"
            or "MSG21" or "MSG22" or "MSG25" or "MSG26" or "MSG30" or "MSG31"
            or "MSG33" or "MSG35" or "MSG36" or "MSG37" or "MSG38" or "MSG40"
            or "MSG44" or "MSG46" or "MSG47" or "MSG48";
    }
}
