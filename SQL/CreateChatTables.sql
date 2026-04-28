-- ══════════════════════════════════════════════════════════
--  DMSPRO Reporting — Chat & Survey tables
--  Chạy script này một lần trên database
-- ══════════════════════════════════════════════════════════

-- ── 1. Lịch sử chat theo user / ngày ─────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE name = 'DMSChatHistory' AND type = 'U')
BEGIN
    CREATE TABLE DMSChatHistory (
        Id        BIGINT        IDENTITY(1,1) PRIMARY KEY,
        UserId    NVARCHAR(100) NOT NULL,
        ChatDate  DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        Role      NVARCHAR(20)  NOT NULL,          -- 'user' | 'assistant'
        Content   NVARCHAR(MAX) NOT NULL,
        NavJson   NVARCHAR(MAX) NULL,              -- JSON array nav actions
        CreatedAt DATETIME2     NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX IX_ChatHistory_User_Date
        ON DMSChatHistory (UserId, ChatDate DESC);
    PRINT 'Created DMSChatHistory';
END

-- ── 2. Khảo sát trải nghiệm người dùng theo ngày ─────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE name = 'DMSUserExperience' AND type = 'U')
BEGIN
    CREATE TABLE DMSUserExperience (
        Id          INT           IDENTITY(1,1) PRIMARY KEY,
        UserId      NVARCHAR(100) NOT NULL,
        SurveyDate  DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        Score       TINYINT       NULL,    -- 1-5 sao tổng thể
        Feedback    NVARCHAR(500) NULL,    -- góp ý tự do
        IsCompleted BIT           NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
        CompletedAt DATETIME2     NULL
    );
    CREATE UNIQUE INDEX IX_UserExperience_User_Date
        ON DMSUserExperience (UserId, SurveyDate);
    PRINT 'Created DMSUserExperience';
END
