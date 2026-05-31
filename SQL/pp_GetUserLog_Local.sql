-- ============================================================
-- SP: pp_GetUserLog_Local
-- Thay the: pp_ReportUserLog (dung NhatNhatDMS..DMSHoliday linked server)
-- Nguon du lieu: UserActionLog + UserProfile + Feature (URCeTools local)
-- Bo linked server, tinh ngay lam viec don gian (tru Chu Nhat)
-- Ngay tao: 2026-05-30
-- ============================================================
IF OBJECT_ID('pp_GetUserLog_Local','P') IS NOT NULL DROP PROCEDURE pp_GetUserLog_Local;
GO
CREATE PROCEDURE pp_GetUserLog_Local
    @FromDate DATE,
    @ToDate   DATE,
    @RegionID NVARCHAR(20) = '',
    @UserName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        UP.UserName,
        UPI.FullName,
        UPI.Email,
        UPI.Phone,
        RO.RoleName,
        UL.Page,
        UL.Action,
        [Date] = CONVERT(DATE, UL.Date),
        Click  = COUNT(*)
    FROM dbo.UserActionLog UL WITH (NOLOCK)
    JOIN dbo.UserProfile UP WITH (NOLOCK) ON UP.UserId = UL.UserId
    LEFT JOIN dbo.UserProfileInfo UPI WITH (NOLOCK) ON UPI.LoginID = UP.UserName
    LEFT JOIN dbo.RoleUser RU WITH (NOLOCK) ON RU.UserID = UP.UserId
    LEFT JOIN dbo.Role RO WITH (NOLOCK) ON RO.ID = RU.RoleID
    WHERE CONVERT(DATE, UL.Date) >= @FromDate
      AND CONVERT(DATE, UL.Date) <= @ToDate
      AND (@RegionID = '' OR EXISTS (
            SELECT 1 FROM dbo.UserTerritory UT
            WHERE UT.UserName = UP.UserName AND UT.RegionID = @RegionID))
    GROUP BY UP.UserName, UPI.FullName, UPI.Email, UPI.Phone, RO.RoleName,
             UL.Page, UL.Action, CONVERT(DATE, UL.Date)
    ORDER BY CONVERT(DATE, UL.Date) DESC, UP.UserName, UL.Page;
END;
GO
-- Test: EXEC pp_GetUserLog_Local @FromDate='2025-05-01', @ToDate='2025-05-30', @UserName='admin'
