-- ============================================================
-- S3: Audit Log Table — ISO 27001 A.12.4.1
-- Luu vao DB URCeTools, giu 3 nam
-- Tao table neu chua co
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppAuditLog')
BEGIN
    CREATE TABLE dbo.AppAuditLog (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        EventTime   DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UserName    NVARCHAR(50)  NOT NULL,
        FullName    NVARCHAR(100) NULL,
        Role        NVARCHAR(50)  NULL,
        EventType   NVARCHAR(50)  NOT NULL,  -- LOGIN, LOGOUT, VIEW_REPORT, EXPORT, CONFIG_CHANGE, SESSION_TIMEOUT
        Page        NVARCHAR(200) NULL,
        Detail      NVARCHAR(500) NULL,
        IpAddress   NVARCHAR(50)  NULL,
        UserAgent   NVARCHAR(500) NULL,
        IsSuccess   BIT           NOT NULL DEFAULT 1
    );

    -- Index de query nhanh theo user va thoi gian
    CREATE NONCLUSTERED INDEX IX_AuditLog_User_Time
        ON dbo.AppAuditLog (UserName, EventTime DESC);

    CREATE NONCLUSTERED INDEX IX_AuditLog_EventType_Time
        ON dbo.AppAuditLog (EventType, EventTime DESC);

    PRINT 'AppAuditLog table created.';
END
ELSE
    PRINT 'AppAuditLog table already exists.';
GO

-- Xoa log cu hon 3 nam (chay bang SQL Agent Job hang ngay)
-- EXEC sp_CleanAuditLog  -- tao job goi procedure nay
CREATE OR ALTER PROCEDURE dbo.sp_CleanAuditLog
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.AppAuditLog
    WHERE EventTime < DATEADD(YEAR, -3, SYSUTCDATETIME());
    PRINT CONCAT('Deleted ', @@ROWCOUNT, ' old audit records.');
END
GO
