/*
PRN222_G5.sql
Database: SQL Server (Database First) for School Management (Student/Teacher/Admin)
Generated from Ass01.docx functional spec.
Notes:
- Default database name: SchoolManagementDb (change as needed).
- Enums are enforced via CHECK constraints.
- Some complex constraints (time conflict, prerequisite satisfaction, credit max) are enforced in application logic.
*/

SET NOCOUNT ON;
GO

/* ===== Create database ===== */
IF DB_ID(N'SchoolManagementDb') IS NULL
BEGIN
    CREATE DATABASE [SchoolManagementDb];
END
GO

USE [SchoolManagementDb];
GO

/* ===== Basic settings ===== */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* =========================
   1) Identity & Core catalog
   ========================= */

CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username        NVARCHAR(100) NOT NULL,
    PasswordHash    NVARCHAR(255) NOT NULL, -- BCrypt hash
    Email           NVARCHAR(255) NULL,
    FullName        NVARCHAR(200) NOT NULL,
    Role            NVARCHAR(20) NOT NULL,  -- STUDENT | TEACHER | ADMIN
    IsActive        BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.Users
ADD CONSTRAINT UQ_Users_Username UNIQUE (Username);
GO

ALTER TABLE dbo.Users
ADD CONSTRAINT CK_Users_Role CHECK (Role IN (N'STUDENT', N'TEACHER', N'ADMIN'));
GO

CREATE TABLE dbo.Programs (
    ProgramId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Programs PRIMARY KEY,
    ProgramCode NVARCHAR(50) NOT NULL,
    ProgramName NVARCHAR(200) NOT NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_Programs_IsActive DEFAULT (1)
);
GO

ALTER TABLE dbo.Programs
ADD CONSTRAINT UQ_Programs_ProgramCode UNIQUE (ProgramCode);
GO

CREATE TABLE dbo.Semesters (
    SemesterId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Semesters PRIMARY KEY,
    SemesterCode         NVARCHAR(50) NOT NULL,
    SemesterName         NVARCHAR(200) NOT NULL,
    StartDate            DATE NOT NULL,
    EndDate              DATE NOT NULL,
    IsActive             BIT NOT NULL CONSTRAINT DF_Semesters_IsActive DEFAULT (0),

    -- Registration windows / rules (from spec)
    RegistrationEndDate  DATE NULL,
    AddDropDeadline      DATE NULL,
    WithdrawalDeadline   DATE NULL,
    MaxCredits           INT NOT NULL CONSTRAINT DF_Semesters_MaxCredits DEFAULT (16),
    MinCredits           INT NOT NULL CONSTRAINT DF_Semesters_MinCredits DEFAULT (8),

    CreatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_Semesters_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.Semesters
ADD CONSTRAINT UQ_Semesters_SemesterCode UNIQUE (SemesterCode);
GO

ALTER TABLE dbo.Semesters
ADD CONSTRAINT CK_Semesters_DateRange CHECK (EndDate >= StartDate);
GO

CREATE TABLE dbo.Students (
    StudentId        INT NOT NULL CONSTRAINT PK_Students PRIMARY KEY, -- FK to Users(UserId)
    StudentCode      NVARCHAR(50) NOT NULL,
    ProgramId        INT NULL,
    CurrentSemesterId INT NULL,
    CreatedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_Students_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.Students
ADD CONSTRAINT FK_Students_Users FOREIGN KEY (StudentId) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.Students
ADD CONSTRAINT FK_Students_Programs FOREIGN KEY (ProgramId) REFERENCES dbo.Programs(ProgramId);
GO

ALTER TABLE dbo.Students
ADD CONSTRAINT FK_Students_CurrentSemester FOREIGN KEY (CurrentSemesterId) REFERENCES dbo.Semesters(SemesterId);
GO

ALTER TABLE dbo.Students
ADD CONSTRAINT UQ_Students_StudentCode UNIQUE (StudentCode);
GO

CREATE TABLE dbo.Teachers (
    TeacherId    INT NOT NULL CONSTRAINT PK_Teachers PRIMARY KEY, -- FK to Users(UserId)
    TeacherCode  NVARCHAR(50) NOT NULL,
    Department   NVARCHAR(200) NULL,
    CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Teachers_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.Teachers
ADD CONSTRAINT FK_Teachers_Users FOREIGN KEY (TeacherId) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.Teachers
ADD CONSTRAINT UQ_Teachers_TeacherCode UNIQUE (TeacherCode);
GO

CREATE TABLE dbo.Courses (
    CourseId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    CourseCode       NVARCHAR(50) NOT NULL,
    CourseName       NVARCHAR(200) NOT NULL,
    Credits          INT NOT NULL,
    Description      NVARCHAR(MAX) NULL,
    ContentHtml      NVARCHAR(MAX) NULL,      -- course content (optional)
    LearningPathJson NVARCHAR(MAX) NULL,      -- learning path (optional, JSON)
    IsActive         BIT NOT NULL CONSTRAINT DF_Courses_IsActive DEFAULT (1),
    CreatedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.Courses
ADD CONSTRAINT UQ_Courses_CourseCode UNIQUE (CourseCode);
GO

ALTER TABLE dbo.Courses
ADD CONSTRAINT CK_Courses_Credits CHECK (Credits > 0 AND Credits <= 10);
GO

CREATE TABLE dbo.CoursePrerequisites (
    CourseId             INT NOT NULL,
    PrerequisiteCourseId INT NOT NULL,
    CONSTRAINT PK_CoursePrerequisites PRIMARY KEY (CourseId, PrerequisiteCourseId)
);
GO

ALTER TABLE dbo.CoursePrerequisites
ADD CONSTRAINT FK_CoursePrerequisites_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO
ALTER TABLE dbo.CoursePrerequisites
ADD CONSTRAINT FK_CoursePrerequisites_Prereq FOREIGN KEY (PrerequisiteCourseId) REFERENCES dbo.Courses(CourseId);
GO

ALTER TABLE dbo.CoursePrerequisites
ADD CONSTRAINT CK_CoursePrerequisites_NoSelf CHECK (CourseId <> PrerequisiteCourseId);
GO

/* =========================
   2) Class sections & Enrollment
   ========================= */

CREATE TABLE dbo.ClassSections (
    ClassSectionId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassSections PRIMARY KEY,
    SemesterId         INT NOT NULL,
    CourseId           INT NOT NULL,
    TeacherId          INT NOT NULL,
    SectionCode        NVARCHAR(50) NOT NULL,      -- e.g. CS101-01
    IsOpen             BIT NOT NULL CONSTRAINT DF_ClassSections_IsOpen DEFAULT (1),
    MaxCapacity        INT NOT NULL CONSTRAINT DF_ClassSections_MaxCapacity DEFAULT (30),
    CurrentEnrollment  INT NOT NULL CONSTRAINT DF_ClassSections_CurrentEnrollment DEFAULT (0),
    Room               NVARCHAR(100) NULL,
    OnlineUrl          NVARCHAR(500) NULL,
    Notes              NVARCHAR(MAX) NULL,
    CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_ClassSections_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ClassSections
ADD CONSTRAINT FK_ClassSections_Semesters FOREIGN KEY (SemesterId) REFERENCES dbo.Semesters(SemesterId);
GO
ALTER TABLE dbo.ClassSections
ADD CONSTRAINT FK_ClassSections_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO
ALTER TABLE dbo.ClassSections
ADD CONSTRAINT FK_ClassSections_Teachers FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(TeacherId);
GO

ALTER TABLE dbo.ClassSections
ADD CONSTRAINT UQ_ClassSections_Sem_Course_Section UNIQUE (SemesterId, CourseId, SectionCode);
GO

ALTER TABLE dbo.ClassSections
ADD CONSTRAINT CK_ClassSections_Capacity CHECK (MaxCapacity > 0 AND CurrentEnrollment >= 0 AND CurrentEnrollment <= MaxCapacity);
GO

CREATE TABLE dbo.Enrollments (
    EnrollmentId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Enrollments PRIMARY KEY,
    StudentId       INT NOT NULL,
    ClassSectionId  INT NOT NULL,

    -- Denormalized keys for enforceable constraints & reporting
    SemesterId      INT NOT NULL,
    CourseId        INT NOT NULL,
    CreditsSnapshot INT NOT NULL,

    Status          NVARCHAR(20) NOT NULL,  -- ENROLLED | WAITLIST | DROPPED | WITHDRAWN | COMPLETED | CANCELED
    EnrolledAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Enrollments_EnrolledAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.Enrollments
ADD CONSTRAINT FK_Enrollments_Students FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO
ALTER TABLE dbo.Enrollments
ADD CONSTRAINT FK_Enrollments_ClassSections FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO
ALTER TABLE dbo.Enrollments
ADD CONSTRAINT FK_Enrollments_Semesters FOREIGN KEY (SemesterId) REFERENCES dbo.Semesters(SemesterId);
GO
ALTER TABLE dbo.Enrollments
ADD CONSTRAINT FK_Enrollments_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO

ALTER TABLE dbo.Enrollments
ADD CONSTRAINT CK_Enrollments_Status CHECK (Status IN (N'PENDING_APPROVAL',N'REJECTED', N'ENROLLED', N'WAITLIST', N'DROPPED', N'WITHDRAWN', N'COMPLETED', N'CANCELED'));
GO

ALTER TABLE dbo.Enrollments
ADD CONSTRAINT CK_Enrollments_CreditsSnapshot CHECK (CreditsSnapshot > 0 AND CreditsSnapshot <= 10);
GO

/* Anti-duplicate: one student cannot be ENROLLED or WITHDRAWN for same Course in same Semester */
CREATE UNIQUE INDEX UX_Enrollments_Student_Course_Sem_Active
ON dbo.Enrollments(StudentId, CourseId, SemesterId)
WHERE Status IN (N'PENDING_APPROVAL', N'ENROLLED', N'WAITLIST');
GO

CREATE INDEX IX_Enrollments_ClassSection ON dbo.Enrollments(ClassSectionId);
GO

/* =========================
   3) Gradebook
   ========================= */

CREATE TABLE dbo.GradeBooks (
    GradeBookId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeBooks PRIMARY KEY,
    ClassSectionId INT NOT NULL,
    Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_GradeBooks_Status DEFAULT (N'DRAFT'), -- DRAFT|PUBLISHED|LOCKED|ARCHIVED
    Version        INT NOT NULL CONSTRAINT DF_GradeBooks_Version DEFAULT (1),
    PublishedAt    DATETIME2(0) NULL,
    LockedAt       DATETIME2(0) NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_GradeBooks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt      DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.GradeBooks
ADD CONSTRAINT FK_GradeBooks_ClassSections FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO

ALTER TABLE dbo.GradeBooks
ADD CONSTRAINT UQ_GradeBooks_ClassSection UNIQUE (ClassSectionId);
GO

-- Bảng lưu lịch sử yêu cầu/duyệt (Optional nhưng recommend)
CREATE TABLE dbo.GradeBookApprovals (
    ApprovalId INT IDENTITY(1,1) PRIMARY KEY,
    GradeBookId INT NOT NULL,
    RequestBy INT NOT NULL, -- Teacher
    RequestAt DATETIME2(0) DEFAULT SYSUTCDATETIME(),
    RequestMessage NVARCHAR(500),
    
    ResponseBy INT NULL, -- Admin
    ResponseAt DATETIME2(0) NULL,
    ResponseMessage NVARCHAR(500), -- Lý do reject
    Outcome NVARCHAR(20) NULL -- APPROVED / REJECTED
);
GO

/* Phase 1 alignment: Gradebook workflow statuses + approval constraints/indexes */
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_GradeBooks_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.GradeBooks')
)
BEGIN
    ALTER TABLE dbo.GradeBooks DROP CONSTRAINT CK_GradeBooks_Status;
END
GO

ALTER TABLE dbo.GradeBooks
ADD CONSTRAINT CK_GradeBooks_Status
CHECK (Status IN (N'DRAFT', N'PENDING_APPROVAL', N'REJECTED', N'PUBLISHED', N'LOCKED', N'ARCHIVED'));
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_GradeBooks_Status_ClassSection'
      AND object_id = OBJECT_ID(N'dbo.GradeBooks')
)
BEGIN
    DROP INDEX IX_GradeBooks_Status_ClassSection ON dbo.GradeBooks;
END
GO

CREATE INDEX IX_GradeBooks_Status_ClassSection ON dbo.GradeBooks(Status, ClassSectionId);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_GradeBookApprovals_GradeBooks'
)
BEGIN
    ALTER TABLE dbo.GradeBookApprovals
    ADD CONSTRAINT FK_GradeBookApprovals_GradeBooks
        FOREIGN KEY (GradeBookId) REFERENCES dbo.GradeBooks(GradeBookId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_GradeBookApprovals_RequestBy'
)
BEGIN
    ALTER TABLE dbo.GradeBookApprovals
    ADD CONSTRAINT FK_GradeBookApprovals_RequestBy
        FOREIGN KEY (RequestBy) REFERENCES dbo.Users(UserId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_GradeBookApprovals_ResponseBy'
)
BEGIN
    ALTER TABLE dbo.GradeBookApprovals
    ADD CONSTRAINT FK_GradeBookApprovals_ResponseBy
        FOREIGN KEY (ResponseBy) REFERENCES dbo.Users(UserId);
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_GradeBookApprovals_Outcome'
      AND parent_object_id = OBJECT_ID(N'dbo.GradeBookApprovals')
)
BEGIN
    ALTER TABLE dbo.GradeBookApprovals DROP CONSTRAINT CK_GradeBookApprovals_Outcome;
END
GO

ALTER TABLE dbo.GradeBookApprovals
ADD CONSTRAINT CK_GradeBookApprovals_Outcome
CHECK (Outcome IS NULL OR Outcome IN (N'APPROVED', N'REJECTED'));
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_GradeBookApprovals_GradeBookId_RequestAt'
      AND object_id = OBJECT_ID(N'dbo.GradeBookApprovals')
)
BEGIN
    DROP INDEX IX_GradeBookApprovals_GradeBookId_RequestAt ON dbo.GradeBookApprovals;
END
GO

CREATE INDEX IX_GradeBookApprovals_GradeBookId_RequestAt
ON dbo.GradeBookApprovals(GradeBookId, RequestAt DESC);
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_GradeBookApprovals_Outcome'
      AND object_id = OBJECT_ID(N'dbo.GradeBookApprovals')
)
BEGIN
    DROP INDEX IX_GradeBookApprovals_Outcome ON dbo.GradeBookApprovals;
END
GO

CREATE INDEX IX_GradeBookApprovals_Outcome
ON dbo.GradeBookApprovals(Outcome);
GO

CREATE TABLE dbo.GradeItems (
    GradeItemId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeItems PRIMARY KEY,
    GradeBookId    INT NOT NULL,
    ItemName       NVARCHAR(200) NOT NULL,        -- e.g. Quiz 1
    MaxScore       DECIMAL(5,2) NOT NULL CONSTRAINT DF_GradeItems_MaxScore DEFAULT (10.00),
    Weight         DECIMAL(6,4) NULL,             -- e.g. 0.2000 (20%). NULL = not weighted/standalone
    IsRequired     BIT NOT NULL CONSTRAINT DF_GradeItems_IsRequired DEFAULT (0),
    SortOrder      INT NOT NULL CONSTRAINT DF_GradeItems_SortOrder DEFAULT (0),
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_GradeItems_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.GradeItems
ADD CONSTRAINT FK_GradeItems_GradeBooks FOREIGN KEY (GradeBookId) REFERENCES dbo.GradeBooks(GradeBookId);
GO

ALTER TABLE dbo.GradeItems
ADD CONSTRAINT CK_GradeItems_MaxScore CHECK (MaxScore > 0 AND MaxScore <= 100);
GO

ALTER TABLE dbo.GradeItems
ADD CONSTRAINT CK_GradeItems_Weight CHECK (Weight IS NULL OR (Weight >= 0 AND Weight <= 1));
GO

CREATE TABLE dbo.GradeEntries (
    GradeEntryId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeEntries PRIMARY KEY,
    GradeItemId    INT NOT NULL,
    EnrollmentId   INT NOT NULL,
    Score          DECIMAL(5,2) NULL,             -- NULL = not graded
    UpdatedBy      INT NULL,                      -- Users(UserId)
    UpdatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_GradeEntries_UpdatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.GradeEntries
ADD CONSTRAINT FK_GradeEntries_GradeItems FOREIGN KEY (GradeItemId) REFERENCES dbo.GradeItems(GradeItemId);
GO

ALTER TABLE dbo.GradeEntries
ADD CONSTRAINT FK_GradeEntries_Enrollments FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollments(EnrollmentId);
GO

ALTER TABLE dbo.GradeEntries
ADD CONSTRAINT FK_GradeEntries_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.GradeEntries
ADD CONSTRAINT CK_GradeEntries_Score CHECK (Score IS NULL OR (Score >= 0 AND Score <= 10));
GO

ALTER TABLE dbo.GradeEntries
ADD CONSTRAINT UQ_GradeEntries_Item_Enrollment UNIQUE (GradeItemId, EnrollmentId);
GO

CREATE TABLE dbo.GradeAuditLogs (
    GradeAuditLogId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeAuditLogs PRIMARY KEY,
    GradeEntryId    INT NOT NULL,
    ActorUserId     INT NOT NULL,
    OldScore        DECIMAL(5,2) NULL,
    NewScore        DECIMAL(5,2) NULL,
    Reason          NVARCHAR(500) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_GradeAuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.GradeAuditLogs
ADD CONSTRAINT FK_GradeAuditLogs_GradeEntries FOREIGN KEY (GradeEntryId) REFERENCES dbo.GradeEntries(GradeEntryId);
GO
ALTER TABLE dbo.GradeAuditLogs
ADD CONSTRAINT FK_GradeAuditLogs_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId);
GO

/* =============================================================
   Grade Appeals (minimal version to support final project)
   ============================================================= */
CREATE TABLE dbo.GradeAppeals (
    AppealId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeAppeals PRIMARY KEY,
    GradeBookId       INT NOT NULL,
    EnrollmentId      INT NOT NULL,
    StudentId         INT NOT NULL,
    GradeItemId       INT NULL,

    AppealContent     NVARCHAR(1000) NOT NULL,
    EvidenceNote      NVARCHAR(500) NULL,

    Status            NVARCHAR(20) NOT NULL CONSTRAINT DF_GradeAppeals_Status DEFAULT (N'SUBMITTED'),
    -- SUBMITTED|UNDER_REVIEW|APPROVED|REJECTED|CLOSED

    ResponseMessage   NVARCHAR(1000) NULL,
    ResolvedBy        INT NULL,
    SubmittedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_GradeAppeals_SubmittedAt DEFAULT (SYSUTCDATETIME()),
    ResolvedAt        DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT FK_GradeAppeals_GradeBooks
FOREIGN KEY (GradeBookId) REFERENCES dbo.GradeBooks(GradeBookId);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT FK_GradeAppeals_Enrollments
FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollments(EnrollmentId);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT FK_GradeAppeals_Students
FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT FK_GradeAppeals_GradeItems
FOREIGN KEY (GradeItemId) REFERENCES dbo.GradeItems(GradeItemId);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT FK_GradeAppeals_ResolvedBy
FOREIGN KEY (ResolvedBy) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT CK_GradeAppeals_Status
CHECK (Status IN (N'SUBMITTED', N'UNDER_REVIEW', N'APPROVED', N'REJECTED', N'CLOSED'));
GO

/* 1 sinh viên chỉ khiếu nại 1 lần cho 1 gradebook */
ALTER TABLE dbo.GradeAppeals
ADD CONSTRAINT UQ_GradeAppeals_GradeBook_Student
UNIQUE (GradeBookId, StudentId);
GO

/* =========================
   4) Chat (SignalR realtime)
   ========================= */

CREATE TABLE dbo.ChatRooms (
    RoomId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatRooms PRIMARY KEY,
    RoomType       NVARCHAR(20) NOT NULL,         -- COURSE | CLASS | GROUP | DM
    CourseId       INT NULL,
    ClassSectionId INT NULL,
    RoomName       NVARCHAR(200) NOT NULL,
    Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_ChatRooms_Status DEFAULT (N'ACTIVE'), -- ACTIVE|LOCKED|ARCHIVED|DELETED
    CreatedBy      INT NOT NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_ChatRooms_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ChatRooms
ADD CONSTRAINT FK_ChatRooms_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO
ALTER TABLE dbo.ChatRooms
ADD CONSTRAINT FK_ChatRooms_ClassSections FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO
ALTER TABLE dbo.ChatRooms
ADD CONSTRAINT FK_ChatRooms_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.ChatRooms
ADD CONSTRAINT CK_ChatRooms_RoomType CHECK (RoomType IN (N'COURSE', N'CLASS', N'GROUP', N'DM'));
GO
ALTER TABLE dbo.ChatRooms
ADD CONSTRAINT CK_ChatRooms_Status CHECK (Status IN (N'ACTIVE', N'LOCKED', N'ARCHIVED', N'DELETED'));
GO

CREATE TABLE dbo.ChatRoomMembers (
    RoomId          INT NOT NULL,
    UserId          INT NOT NULL,
    RoleInRoom      NVARCHAR(20) NOT NULL CONSTRAINT DF_ChatRoomMembers_Role DEFAULT (N'MEMBER'), -- MEMBER|MODERATOR|OWNER
    MemberStatus    NVARCHAR(20) NOT NULL CONSTRAINT DF_ChatRoomMembers_Status DEFAULT (N'JOINED'), -- JOINED|MUTED|READ_ONLY|BANNED|REMOVED
    LastReadMessageId BIGINT NULL,
    JoinedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_ChatRoomMembers_JoinedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ChatRoomMembers PRIMARY KEY (RoomId, UserId)
);
GO

ALTER TABLE dbo.ChatRoomMembers
ADD CONSTRAINT FK_ChatRoomMembers_Room FOREIGN KEY (RoomId) REFERENCES dbo.ChatRooms(RoomId);
GO
ALTER TABLE dbo.ChatRoomMembers
ADD CONSTRAINT FK_ChatRoomMembers_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.ChatRoomMembers
ADD CONSTRAINT CK_ChatRoomMembers_Role CHECK (RoleInRoom IN (N'MEMBER', N'MODERATOR', N'OWNER'));
GO
ALTER TABLE dbo.ChatRoomMembers
ADD CONSTRAINT CK_ChatRoomMembers_Status CHECK (MemberStatus IN (N'JOINED', N'MUTED', N'READ_ONLY', N'BANNED', N'REMOVED'));
GO

CREATE TABLE dbo.ChatMessages (
    MessageId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatMessages PRIMARY KEY,
    RoomId         INT NOT NULL,
    SenderId       INT NOT NULL,
    MessageType    NVARCHAR(20) NOT NULL CONSTRAINT DF_ChatMessages_Type DEFAULT (N'TEXT'), -- TEXT|SYSTEM
    Content        NVARCHAR(MAX) NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    EditedAt       DATETIME2(0) NULL,
    DeletedAt      DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.ChatMessages
ADD CONSTRAINT FK_ChatMessages_Room FOREIGN KEY (RoomId) REFERENCES dbo.ChatRooms(RoomId);
GO
ALTER TABLE dbo.ChatMessages
ADD CONSTRAINT FK_ChatMessages_Sender FOREIGN KEY (SenderId) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.ChatMessages
ADD CONSTRAINT CK_ChatMessages_Type CHECK (MessageType IN (N'TEXT', N'SYSTEM'));
GO

CREATE INDEX IX_ChatMessages_Room_CreatedAt ON dbo.ChatMessages(RoomId, CreatedAt DESC);
GO

CREATE TABLE dbo.ChatMessageAttachments (
    AttachmentId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatMessageAttachments PRIMARY KEY,
    MessageId      BIGINT NOT NULL,
    FileUrl        NVARCHAR(1000) NOT NULL,
    FileType       NVARCHAR(100) NOT NULL,
    FileSizeBytes  BIGINT NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_ChatMessageAttachments_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ChatMessageAttachments
ADD CONSTRAINT FK_ChatMessageAttachments_Message FOREIGN KEY (MessageId) REFERENCES dbo.ChatMessages(MessageId);
GO

CREATE TABLE dbo.ChatModerationLogs (
    ModerationLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatModerationLogs PRIMARY KEY,
    RoomId          INT NOT NULL,
    ActorUserId     INT NOT NULL,
    Action          NVARCHAR(50) NOT NULL,         -- LOCK|UNLOCK|REMOVE_MEMBER|DELETE_MESSAGE|REPORT
    TargetUserId    INT NULL,
    TargetMessageId BIGINT NULL,
    MetadataJson    NVARCHAR(MAX) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ChatModerationLogs_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ChatModerationLogs
ADD CONSTRAINT FK_ChatModerationLogs_Room FOREIGN KEY (RoomId) REFERENCES dbo.ChatRooms(RoomId);
GO
ALTER TABLE dbo.ChatModerationLogs
ADD CONSTRAINT FK_ChatModerationLogs_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId);
GO
ALTER TABLE dbo.ChatModerationLogs
ADD CONSTRAINT FK_ChatModerationLogs_TargetUser FOREIGN KEY (TargetUserId) REFERENCES dbo.Users(UserId);
GO
ALTER TABLE dbo.ChatModerationLogs
ADD CONSTRAINT FK_ChatModerationLogs_TargetMessage FOREIGN KEY (TargetMessageId) REFERENCES dbo.ChatMessages(MessageId);
GO

/* =========================
   5) Calendar / schedule
   ========================= */

CREATE TABLE dbo.Recurrences (
    RecurrenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Recurrences PRIMARY KEY,
    RRule        NVARCHAR(500) NOT NULL, -- iCal RRULE
    StartDate    DATE NOT NULL,
    EndDate      DATE NOT NULL,
    CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Recurrences_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.Recurrences
ADD CONSTRAINT CK_Recurrences_DateRange CHECK (EndDate >= StartDate);
GO

CREATE TABLE dbo.ScheduleEvents (
    ScheduleEventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleEvents PRIMARY KEY,
    ClassSectionId  INT NOT NULL,
    Title           NVARCHAR(200) NOT NULL,
    StartAt         DATETIME2(0) NOT NULL,         -- stored in UTC recommended
    EndAt           DATETIME2(0) NOT NULL,
    Timezone        NVARCHAR(100) NOT NULL CONSTRAINT DF_ScheduleEvents_Timezone DEFAULT (N'Asia/Ho_Chi_Minh'),
    Location        NVARCHAR(200) NULL,
    OnlineUrl       NVARCHAR(500) NULL,
    TeacherId       INT NULL,                      -- can override section teacher
    Status          NVARCHAR(20) NOT NULL CONSTRAINT DF_ScheduleEvents_Status DEFAULT (N'DRAFT'), -- DRAFT|PUBLISHED|RESCHEDULED|CANCELLED|COMPLETED|ARCHIVED
    RecurrenceId    INT NULL,
    CreatedBy       INT NOT NULL,
    UpdatedBy       INT NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ScheduleEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT FK_ScheduleEvents_ClassSection FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO
ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT FK_ScheduleEvents_Recurrence FOREIGN KEY (RecurrenceId) REFERENCES dbo.Recurrences(RecurrenceId);
GO
ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT FK_ScheduleEvents_Teacher FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(TeacherId);
GO
ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT FK_ScheduleEvents_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId);
GO
ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT FK_ScheduleEvents_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(UserId);
GO

ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT CK_ScheduleEvents_Time CHECK (EndAt > StartAt);
GO

ALTER TABLE dbo.ScheduleEvents
ADD CONSTRAINT CK_ScheduleEvents_Status CHECK (Status IN (N'DRAFT', N'PUBLISHED', N'RESCHEDULED', N'CANCELLED', N'COMPLETED', N'ARCHIVED'));
GO

CREATE INDEX IX_ScheduleEvents_ClassSection_StartAt ON dbo.ScheduleEvents(ClassSectionId, StartAt);
GO

CREATE TABLE dbo.ScheduleEventOverrides (
    OverrideId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleEventOverrides PRIMARY KEY,
    RecurrenceId    INT NOT NULL,
    OriginalDate    DATE NOT NULL,
    OverrideType    NVARCHAR(20) NOT NULL,         -- RESCHEDULE|CANCEL
    NewStartAt      DATETIME2(0) NULL,
    NewEndAt        DATETIME2(0) NULL,
    NewLocation     NVARCHAR(200) NULL,
    NewTeacherId    INT NULL,
    Reason          NVARCHAR(500) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ScheduleEventOverrides_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ScheduleEventOverrides
ADD CONSTRAINT FK_ScheduleEventOverrides_Recurrence FOREIGN KEY (RecurrenceId) REFERENCES dbo.Recurrences(RecurrenceId);
GO
ALTER TABLE dbo.ScheduleEventOverrides
ADD CONSTRAINT FK_ScheduleEventOverrides_Teacher FOREIGN KEY (NewTeacherId) REFERENCES dbo.Teachers(TeacherId);
GO

ALTER TABLE dbo.ScheduleEventOverrides
ADD CONSTRAINT CK_ScheduleEventOverrides_Type CHECK (OverrideType IN (N'RESCHEDULE', N'CANCEL'));
GO

CREATE TABLE dbo.ScheduleChangeLogs (
    ChangeLogId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleChangeLogs PRIMARY KEY,
    ScheduleEventId BIGINT NOT NULL,
    ActorUserId     INT NOT NULL,
    ChangeType      NVARCHAR(50) NOT NULL,         -- CREATE|UPDATE|CANCEL|PUBLISH|LOCK
    OldJson         NVARCHAR(MAX) NULL,
    NewJson         NVARCHAR(MAX) NULL,
    Reason          NVARCHAR(500) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ScheduleChangeLogs_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.ScheduleChangeLogs
ADD CONSTRAINT FK_ScheduleChangeLogs_Event FOREIGN KEY (ScheduleEventId) REFERENCES dbo.ScheduleEvents(ScheduleEventId);
GO
ALTER TABLE dbo.ScheduleChangeLogs
ADD CONSTRAINT FK_ScheduleChangeLogs_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId);
GO

/* =========================
   6) Notifications
   ========================= */

CREATE TABLE dbo.Notifications (
    NotificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    NotificationType NVARCHAR(50) NOT NULL,        -- SCHEDULE_CHANGED|GRADE_PUBLISHED|CHAT_MENTION|...
    PayloadJson     NVARCHAR(MAX) NOT NULL,
    Status          NVARCHAR(20) NOT NULL CONSTRAINT DF_Notifications_Status DEFAULT (N'PENDING'), -- PENDING|SENT|DELIVERED
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    SentAt          DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.Notifications
ADD CONSTRAINT CK_Notifications_Status CHECK (Status IN (N'PENDING', N'SENT', N'DELIVERED'));
GO

CREATE TABLE dbo.NotificationRecipients (
    NotificationId BIGINT NOT NULL,
    UserId         INT NOT NULL,
    DeliveredAt    DATETIME2(0) NULL,
    ReadAt         DATETIME2(0) NULL,
    CONSTRAINT PK_NotificationRecipients PRIMARY KEY (NotificationId, UserId)
);
GO

ALTER TABLE dbo.NotificationRecipients
ADD CONSTRAINT FK_NotificationRecipients_Notification FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications(NotificationId);
GO
ALTER TABLE dbo.NotificationRecipients
ADD CONSTRAINT FK_NotificationRecipients_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
GO

/* =========================
   7) AI Chatbot (logs & audit)
   ========================= */

CREATE TABLE dbo.AIChatSessions (
    ChatSessionId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AIChatSessions PRIMARY KEY,
    UserId         INT NOT NULL,
    Purpose        NVARCHAR(50) NOT NULL,          -- SCORE_SUMMARY|STUDY_PLAN|COURSE_SUGGESTION
    ModelName      NVARCHAR(100) NULL,             -- e.g. gemini-...
    State          NVARCHAR(30) NOT NULL CONSTRAINT DF_AIChatSessions_State DEFAULT (N'ACTIVE'),
    PromptVersion  NVARCHAR(50) NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_AIChatSessions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt    DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.AIChatSessions
ADD CONSTRAINT FK_AIChatSessions_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId);
GO

CREATE TABLE dbo.AIChatMessages (
    ChatMessageId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AIChatMessages PRIMARY KEY,
    ChatSessionId  BIGINT NOT NULL,
    SenderType     NVARCHAR(20) NOT NULL,          -- USER|ASSISTANT|SYSTEM
    Content        NVARCHAR(MAX) NOT NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_AIChatMessages_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.AIChatMessages
ADD CONSTRAINT FK_AIChatMessages_Session FOREIGN KEY (ChatSessionId) REFERENCES dbo.AIChatSessions(ChatSessionId);
GO
ALTER TABLE dbo.AIChatMessages
ADD CONSTRAINT CK_AIChatMessages_SenderType CHECK (SenderType IN (N'USER', N'ASSISTANT', N'SYSTEM'));
GO

CREATE TABLE dbo.AIToolCalls (
    ToolCallId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AIToolCalls PRIMARY KEY,
    ChatSessionId  BIGINT NOT NULL,
    ToolName       NVARCHAR(200) NOT NULL,
    RequestJson    NVARCHAR(MAX) NOT NULL,
    ResponseJson   NVARCHAR(MAX) NULL,
    Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_AIToolCalls_Status DEFAULT (N'OK'), -- OK|ERROR
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_AIToolCalls_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.AIToolCalls
ADD CONSTRAINT FK_AIToolCalls_Session FOREIGN KEY (ChatSessionId) REFERENCES dbo.AIChatSessions(ChatSessionId);
GO

ALTER TABLE dbo.AIToolCalls
ADD CONSTRAINT CK_AIToolCalls_Status CHECK (Status IN (N'OK', N'ERROR'));
GO

/* =========================
   8) Helpful views (optional)
   ========================= */

CREATE VIEW dbo.vw_ClassSectionSummary
AS
SELECT
    cs.ClassSectionId,
    cs.SectionCode,
    s.SemesterCode,
    c.CourseCode,
    c.CourseName,
    c.Credits,
    cs.IsOpen,
    cs.CurrentEnrollment,
    cs.MaxCapacity,
    u.FullName AS TeacherFullName
FROM dbo.ClassSections cs
JOIN dbo.Semesters s ON s.SemesterId = cs.SemesterId
JOIN dbo.Courses c ON c.CourseId = cs.CourseId
JOIN dbo.Teachers t ON t.TeacherId = cs.TeacherId
JOIN dbo.Users u ON u.UserId = t.TeacherId;
GO

--=========================
/* =============================================================
   MODULE 9: FINANCE & MOMO PAYMENT (COURSE REGISTRATION SUPPORT)
   Notes: 
   - Manages Tuition Fees based on Credits registered.
   - Integrates MoMo Payment Gateway.
   - Uses Wallet system to handle Add/Drop refunds easily.
   ============================================================= */

/* 1. Bảng Ví sinh viên (StudentWallets)
   Mục đích: Lưu số dư khả dụng để đóng tiền học. 
   Khi nạp MoMo -> Tiền vào đây -> Sinh viên bấm "Thanh toán học phí" -> Trừ tiền ở đây.
*/
CREATE TABLE dbo.StudentWallets (
    WalletId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentWallets PRIMARY KEY,
    StudentId       INT NOT NULL,
    Balance         DECIMAL(18,2) NOT NULL CONSTRAINT DF_StudentWallets_Balance DEFAULT (0),
    WalletStatus    NVARCHAR(20) NOT NULL CONSTRAINT DF_StudentWallets_Status DEFAULT (N'ACTIVE'), -- ACTIVE | LOCKED
    LastUpdated     DATETIME2(0) NOT NULL CONSTRAINT DF_StudentWallets_LastUpdated DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.StudentWallets
ADD CONSTRAINT FK_StudentWallets_Students FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO
ALTER TABLE dbo.StudentWallets
ADD CONSTRAINT UQ_StudentWallets_Student UNIQUE (StudentId);
GO

CREATE TABLE dbo.SemesterTuitionPolicies (
    PolicyId            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SemesterTuitionPolicies PRIMARY KEY,
    SemesterId          INT NOT NULL,
    AmountPerCredit     DECIMAL(18,2) NOT NULL,
    DefaultSurcharge    DECIMAL(18,2) NOT NULL CONSTRAINT DF_SemesterTuitionPolicies_Surcharge DEFAULT (0),
    CurrencyCode        NVARCHAR(10) NOT NULL CONSTRAINT DF_SemesterTuitionPolicies_Currency DEFAULT (N'VND'),
    IsActive            BIT NOT NULL CONSTRAINT DF_SemesterTuitionPolicies_IsActive DEFAULT (1),
    EffectiveFrom       DATETIME2(0) NOT NULL CONSTRAINT DF_SemesterTuitionPolicies_EffectiveFrom DEFAULT (SYSUTCDATETIME()),
    EffectiveTo         DATETIME2(0) NULL,
    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_SemesterTuitionPolicies_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.SemesterTuitionPolicies
ADD CONSTRAINT FK_SemesterTuitionPolicies_Semesters
FOREIGN KEY (SemesterId) REFERENCES dbo.Semesters(SemesterId);
GO

ALTER TABLE dbo.SemesterTuitionPolicies
ADD CONSTRAINT CK_SemesterTuitionPolicies_Amounts
CHECK (AmountPerCredit >= 0 AND DefaultSurcharge >= 0);
GO

/* 1 kỳ chỉ có 1 policy active */
CREATE UNIQUE INDEX UX_SemesterTuitionPolicies_ActiveSemester
ON dbo.SemesterTuitionPolicies(SemesterId)
WHERE IsActive = 1;
GO


/* 3. Cart header */
CREATE TABLE dbo.RegistrationCarts (
    CartId                   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrationCarts PRIMARY KEY,
    StudentId                INT NOT NULL,
    SemesterId               INT NOT NULL,
    CartStatus               NVARCHAR(20) NOT NULL CONSTRAINT DF_RegistrationCarts_Status DEFAULT (N'ACTIVE'), -- ACTIVE|CHECKED_OUT|EXPIRED|ABANDONED
    EstimatedSubTotalAmount  DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationCarts_SubTotal DEFAULT (0),
    EstimatedSurchargeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationCarts_Surcharge DEFAULT (0),
    EstimatedTotalAmount     DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationCarts_Total DEFAULT (0),
    CheckedOutAt             DATETIME2(0) NULL,
    ExpiresAt                DATETIME2(0) NULL,
    CreatedAt                DATETIME2(0) NOT NULL CONSTRAINT DF_RegistrationCarts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt                DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.RegistrationCarts
ADD CONSTRAINT FK_RegistrationCarts_Students
FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO

ALTER TABLE dbo.RegistrationCarts
ADD CONSTRAINT FK_RegistrationCarts_Semesters
FOREIGN KEY (SemesterId) REFERENCES dbo.Semesters(SemesterId);
GO

ALTER TABLE dbo.RegistrationCarts
ADD CONSTRAINT CK_RegistrationCarts_Status
CHECK (CartStatus IN (N'ACTIVE', N'CHECKED_OUT', N'EXPIRED', N'ABANDONED'));
GO

/* Mỗi student chỉ có 1 cart ACTIVE cho 1 semester */
CREATE UNIQUE INDEX UX_RegistrationCarts_Student_Semester_Active
ON dbo.RegistrationCarts(StudentId, SemesterId)
WHERE CartStatus = N'ACTIVE';
GO


/* 4. Cart items */
CREATE TABLE dbo.RegistrationCartItems (
    CartItemId                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrationCartItems PRIMARY KEY,
    CartId                      BIGINT NOT NULL,
    ClassSectionId              INT NOT NULL,
    CourseId                    INT NOT NULL,
    CreditsSnapshot             INT NOT NULL,
    EstimatedUnitPricePerCredit DECIMAL(18,2) NOT NULL,
    EstimatedLineAmount         DECIMAL(18,2) NOT NULL,
    AddedAt                     DATETIME2(0) NOT NULL CONSTRAINT DF_RegistrationCartItems_AddedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT FK_RegistrationCartItems_Carts
FOREIGN KEY (CartId) REFERENCES dbo.RegistrationCarts(CartId);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT FK_RegistrationCartItems_ClassSections
FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT FK_RegistrationCartItems_Courses
FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT CK_RegistrationCartItems_Credits
CHECK (CreditsSnapshot > 0 AND CreditsSnapshot <= 10);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT CK_RegistrationCartItems_Amounts
CHECK (EstimatedUnitPricePerCredit >= 0 AND EstimatedLineAmount >= 0);
GO

ALTER TABLE dbo.RegistrationCartItems
ADD CONSTRAINT UQ_RegistrationCartItems_Cart_ClassSection
UNIQUE (CartId, ClassSectionId);
GO


/* 5. Registration orders
   Một lần checkout tương ứng 1 order
*/
CREATE TABLE dbo.RegistrationOrders (
    OrderId            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrationOrders PRIMARY KEY,
    StudentId          INT NOT NULL,
    SemesterId         INT NOT NULL,
    CartId             BIGINT NULL,
    OrderCode          NVARCHAR(50) NOT NULL,
    PricingPolicyId    INT NULL,

    OrderStatus        NVARCHAR(30) NOT NULL CONSTRAINT DF_RegistrationOrders_Status DEFAULT (N'PENDING'), 
    -- PENDING|PAID|FAILED|CANCELLED|PARTIALLY_REFUNDED|REFUNDED

    SubTotalAmount     DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrders_SubTotal DEFAULT (0),
    SurchargeAmount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrders_Surcharge DEFAULT (0),
    DiscountAmount     DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrders_Discount DEFAULT (0),
    TotalAmount        DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrders_Total DEFAULT (0),
    PaidAmount         DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrders_Paid DEFAULT (0),

    FailureReason      NVARCHAR(500) NULL,
    PaidAt             DATETIME2(0) NULL,
    CancelledAt        DATETIME2(0) NULL,
    CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_RegistrationOrders_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt          DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT FK_RegistrationOrders_Students
FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT FK_RegistrationOrders_Semesters
FOREIGN KEY (SemesterId) REFERENCES dbo.Semesters(SemesterId);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT FK_RegistrationOrders_Carts
FOREIGN KEY (CartId) REFERENCES dbo.RegistrationCarts(CartId);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT FK_RegistrationOrders_PricingPolicy
FOREIGN KEY (PricingPolicyId) REFERENCES dbo.SemesterTuitionPolicies(PolicyId);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT UQ_RegistrationOrders_OrderCode UNIQUE (OrderCode);
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT CK_RegistrationOrders_Status
CHECK (OrderStatus IN (N'PENDING', N'PAID', N'FAILED', N'CANCELLED', N'PARTIALLY_REFUNDED', N'REFUNDED'));
GO

ALTER TABLE dbo.RegistrationOrders
ADD CONSTRAINT CK_RegistrationOrders_Amounts
CHECK (
    SubTotalAmount >= 0
    AND SurchargeAmount >= 0
    AND DiscountAmount >= 0
    AND TotalAmount >= 0
    AND PaidAmount >= 0
);
GO

CREATE INDEX IX_RegistrationOrders_Student_CreatedAt
ON dbo.RegistrationOrders(StudentId, CreatedAt DESC);
GO


/* 6. Registration order items
   Snapshot từng môn trong 1 lần checkout
*/
CREATE TABLE dbo.RegistrationOrderItems (
    OrderItemId           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrationOrderItems PRIMARY KEY,
    OrderId               BIGINT NOT NULL,
    ClassSectionId        INT NOT NULL,
    CourseId              INT NOT NULL,
    EnrollmentId          INT NULL,

    CreditsSnapshot       INT NOT NULL,
    UnitPricePerCredit    DECIMAL(18,2) NOT NULL,
    LineSubTotalAmount    DECIMAL(18,2) NOT NULL,
    LineSurchargeAmount   DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrderItems_Surcharge DEFAULT (0),
    LineTotalAmount       DECIMAL(18,2) NOT NULL,
    RefundAmount          DECIMAL(18,2) NOT NULL CONSTRAINT DF_RegistrationOrderItems_Refund DEFAULT (0),

    ItemStatus            NVARCHAR(30) NOT NULL CONSTRAINT DF_RegistrationOrderItems_Status DEFAULT (N'PENDING_APPROVAL'),
    -- PENDING_APPROVAL|ENROLLED|REJECTED|REFUNDED|CANCELLED

    CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_RegistrationOrderItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt             DATETIME2(0) NULL
);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT FK_RegistrationOrderItems_Orders
FOREIGN KEY (OrderId) REFERENCES dbo.RegistrationOrders(OrderId);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT FK_RegistrationOrderItems_ClassSections
FOREIGN KEY (ClassSectionId) REFERENCES dbo.ClassSections(ClassSectionId);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT FK_RegistrationOrderItems_Courses
FOREIGN KEY (CourseId) REFERENCES dbo.Courses(CourseId);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT FK_RegistrationOrderItems_Enrollments
FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollments(EnrollmentId);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT CK_RegistrationOrderItems_Credits
CHECK (CreditsSnapshot > 0 AND CreditsSnapshot <= 10);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT CK_RegistrationOrderItems_Amounts
CHECK (
    UnitPricePerCredit >= 0
    AND LineSubTotalAmount >= 0
    AND LineSurchargeAmount >= 0
    AND LineTotalAmount >= 0
    AND RefundAmount >= 0
);
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT CK_RegistrationOrderItems_Status
CHECK (ItemStatus IN (N'PENDING_APPROVAL', N'ENROLLED', N'REJECTED', N'REFUNDED', N'CANCELLED'));
GO

ALTER TABLE dbo.RegistrationOrderItems
ADD CONSTRAINT UQ_RegistrationOrderItems_Order_ClassSection
UNIQUE (OrderId, ClassSectionId);
GO

CREATE INDEX IX_RegistrationOrderItems_OrderId
ON dbo.RegistrationOrderItems(OrderId);
GO

/* 3. Bảng Giao dịch Cổng thanh toán (PaymentTransactions) - LOG MOMO
   Mục đích: Lưu lịch sử gọi API sang MoMo và kết quả trả về (IPN).
*/
CREATE TABLE dbo.PaymentTransactions (
    TransactionId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentTransactions PRIMARY KEY,
    StudentId       INT NOT NULL,
    PaymentMethod   NVARCHAR(50) NOT NULL CONSTRAINT DF_PaymentTransactions_Method DEFAULT (N'MOMO'), 
    
    -- Các trường quan trọng để map với MoMo API
    MoMoRequestId   NVARCHAR(100) NOT NULL, -- requestId (Unique per attempt)
    MoMoOrderId     NVARCHAR(100) NOT NULL, -- orderId (Unique per logical order)
    MoMoTransId     BIGINT NULL,            -- transId (Mã giao dịch phía MoMo trả về)
    
    Amount          DECIMAL(18,2) NOT NULL,
    OrderInfo       NVARCHAR(500) NULL,     -- Nội dung chuyển khoản (VD: Nap tien hoc phi SP26)
    
    -- Trạng thái xử lý
    Status          NVARCHAR(20) NOT NULL CONSTRAINT DF_PaymentTransactions_Status DEFAULT (N'PENDING'), -- PENDING | SUCCESS | FAILED | CANCELLED
    ErrorCode       INT NULL,               -- Mã lỗi MoMo (0 = Thành công)
    LocalMessage    NVARCHAR(500) NULL,     -- Ghi chú nội bộ
    
    RawResponse     NVARCHAR(MAX) NULL,     -- Lưu JSON response từ MoMo để debug khi cần
    PaymentDate     DATETIME2(0) NULL,      -- Thời điểm nhận tiền thành công
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_PaymentTransactions_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.PaymentTransactions
ADD CONSTRAINT FK_PaymentTransactions_Students FOREIGN KEY (StudentId) REFERENCES dbo.Students(StudentId);
GO
/* Đảm bảo OrderId không trùng lặp để đối soát */
CREATE UNIQUE INDEX UX_PaymentTransactions_MoMoOrderId ON dbo.PaymentTransactions(MoMoOrderId);
GO

/* 4. Bảng Lịch sử dòng tiền (refactor)
   Chỉ lưu các giao dịch đã làm thay đổi số dư ví.
*/
CREATE TABLE dbo.WalletTransactions (
    WalletTransId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletTransactions PRIMARY KEY,
    WalletId           INT NOT NULL,
    Amount             DECIMAL(18,2) NOT NULL, -- + tăng ví, - giảm ví
    TransactionType    NVARCHAR(50) NOT NULL,  -- DEPOSIT|CHECKOUT_PAYMENT|REFUND|ADJUSTMENT

    RelatedPaymentId   BIGINT NULL,            -- dùng cho MoMo deposit
    RelatedOrderId     BIGINT NULL,            -- dùng cho checkout/refund theo order

    BalanceBefore      DECIMAL(18,2) NULL,
    BalanceAfter       DECIMAL(18,2) NULL,

    Description        NVARCHAR(500) NULL,
    CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_WalletTransactions_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

ALTER TABLE dbo.WalletTransactions
ADD CONSTRAINT FK_WalletTransactions_Wallet
FOREIGN KEY (WalletId) REFERENCES dbo.StudentWallets(WalletId);
GO

ALTER TABLE dbo.WalletTransactions
ADD CONSTRAINT FK_WalletTransactions_Payment
FOREIGN KEY (RelatedPaymentId) REFERENCES dbo.PaymentTransactions(TransactionId);
GO

ALTER TABLE dbo.WalletTransactions
ADD CONSTRAINT FK_WalletTransactions_Order
FOREIGN KEY (RelatedOrderId) REFERENCES dbo.RegistrationOrders(OrderId);
GO

ALTER TABLE dbo.WalletTransactions
ADD CONSTRAINT CK_WalletTransactions_Type
CHECK (TransactionType IN (N'DEPOSIT', N'CHECKOUT_PAYMENT', N'REFUND', N'ADJUSTMENT'));
GO

ALTER TABLE dbo.WalletTransactions
ADD CONSTRAINT CK_WalletTransactions_OneSource
CHECK (
    NOT (RelatedPaymentId IS NOT NULL AND RelatedOrderId IS NOT NULL)
);
GO

CREATE INDEX IX_WalletTransactions_Wallet_CreatedAt
ON dbo.WalletTransactions(WalletId, CreatedAt DESC);
GO

CREATE INDEX IX_WalletTransactions_RelatedOrderId
ON dbo.WalletTransactions(RelatedOrderId)
WHERE RelatedOrderId IS NOT NULL;
GO

CREATE INDEX IX_WalletTransactions_RelatedPaymentId
ON dbo.WalletTransactions(RelatedPaymentId)
WHERE RelatedPaymentId IS NOT NULL;
GO
--===========================



INSERT INTO dbo.Programs (ProgramCode, ProgramName) VALUES
('SE', N'Kỹ thuật phần mềm'),
('AI', N'Trí tuệ nhân tạo'),
('GD', N'Thiết kế đồ họa');
GO

-- 3. Tạo dữ liệu Môn học (Courses)
INSERT INTO dbo.Courses (CourseCode, CourseName, Credits, Description) VALUES
('PRN211', N'Basic Cross-Platform App Programming', 3, N'C# basic and WinForms'),
('PRN222', N'Advanced Cross-Platform App Programming', 3, N'ASP.NET Core, EF Core, SignalR'),
('PRN231', N'Building Cross-Platform Web APIs', 3, N'RESTful API, OData, JWT'),
('SWP391', N'Software Development Project', 4, N'Capstone project for juniors');
GO

-- =============================================================
-- 4. TẠO USERS (1 Admin, 5 Teachers, 14 Students)
-- Lưu ý: PasswordHash ở đây là giả lập. Trong thực tế cần dùng BCrypt hash.
-- Mật khẩu mặc định ví dụ: '123456' (Hash giả: $2a$11$...)
-- =============================================================

-- 4.1 Tạo Admin (1 người)
INSERT INTO dbo.Users (Username, PasswordHash, Email, FullName, Role, IsActive) VALUES
('admin', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'admin@fpt.edu.vn', N'Quản Trị Viên Hệ Thống', 'ADMIN', 1);

-- 4.2 Tạo Teachers (5 người)
INSERT INTO dbo.Users (Username, PasswordHash, Email, FullName, Role, IsActive) VALUES
('teacher1', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'hungnv@fpt.edu.vn', N'Nguyễn Văn Hùng', 'TEACHER', 1),
('teacher2', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'hoangtm@fpt.edu.vn', N'Trần Minh Hoàng', 'TEACHER', 1),
('teacher3', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'lannt@fpt.edu.vn', N'Nguyễn Thị Lan', 'TEACHER', 1),
('teacher4', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'dungpa@fpt.edu.vn', N'Phạm Anh Dũng', 'TEACHER', 1),
('teacher5', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'huonglt@fpt.edu.vn', N'Lê Thị Hương', 'TEACHER', 1);

-- 4.3 Tạo Students (14 người)
INSERT INTO dbo.Users (Username, PasswordHash, Email, FullName, Role, IsActive) VALUES
('student1', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'namnv@he18.vn', N'Nguyễn Văn Nam', 'STUDENT', 1),
('student2', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'linhtt@he18.vn', N'Trần Thùy Linh', 'STUDENT', 1),
('student3', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'quanlm@he18.vn', N'Lê Minh Quân', 'STUDENT', 1),
('student4', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'maihtp@he18.vn', N'Hoàng Thị Phương Mai', 'STUDENT', 1),
('student5', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'duongtv@he18.vn', N'Trương Văn Dương', 'STUDENT', 1),
('student6', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'thanhhv@he18.vn', N'Hà Văn Thành', 'STUDENT', 1),
('student7', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'ngocdth@he18.vn', N'Đỗ Thị Hồng Ngọc', 'STUDENT', 1),
('student8', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'khoand@he18.vn', N'Nguyễn Đăng Khoa', 'STUDENT', 1),
('student9', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'mylt@he18.vn', N'Lý Thị Mỹ', 'STUDENT', 1),
('student10', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'phucbp@he18.vn', N'Bùi Phi Phúc', 'STUDENT', 1),
('student11', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'trangnt@he18.vn', N'Ngô Thùy Trang', 'STUDENT', 1),
('student12', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'vietpd@he18.vn', N'Phạm Đức Việt', 'STUDENT', 1),
('student13', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'anhtt@he18.vn', N'Trịnh Tuấn Anh', 'STUDENT', 1),
('student14', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'yennh@he18.vn', N'Nguyễn Hải Yến', 'STUDENT', 1);
GO

-- =============================================================
-- 5. LINK DỮ LIỆU VÀO BẢNG TEACHERS VÀ STUDENTS
-- =============================================================

-- 5.1 Insert vào bảng Teachers (Lấy UserId từ bảng Users có Role='TEACHER')
INSERT INTO dbo.Teachers (TeacherId, TeacherCode, Department)
SELECT 
    UserId, 
    'GV' + RIGHT('0000' + CAST(UserId AS VARCHAR(10)), 4), -- Tạo mã GV0002, GV0003...
    CASE 
        WHEN UserId % 2 = 0 THEN N'Kỹ thuật phần mềm' 
        ELSE N'Khoa học máy tính' 
    END
FROM dbo.Users 
WHERE Role = 'TEACHER';
GO

-- 5.2 Insert vào bảng Students (Lấy UserId từ bảng Users có Role='STUDENT')
-- Gán ngẫu nhiên vào ProgramId=1 (SE) và SemesterId=1 (SP26)
DECLARE @ProgramId INT = (SELECT TOP 1 ProgramId FROM dbo.Programs WHERE ProgramCode = 'SE');
DECLARE @SemesterId INT = (SELECT TOP 1 SemesterId FROM dbo.Semesters WHERE SemesterCode = 'SP26');

INSERT INTO dbo.Students (StudentId, StudentCode, ProgramId, CurrentSemesterId)
SELECT 
    UserId, 
    'HE18' + RIGHT('0000' + CAST(UserId AS VARCHAR(10)), 4), -- Tạo mã HE180007...
    @ProgramId,
    @SemesterId
FROM dbo.Users 
WHERE Role = 'STUDENT';
GO

/* =====================================================
   SEED DATA - 100 rows per table
   ===================================================== */

-- =====================================================
-- SEED: Users (100 rows total: 1 existing admin + 5 existing teachers + 14 existing students = 20 already inserted)
-- We add 19 more teachers (total 24) + 56 more students (total 70) + some admins = 80 new rows
-- Actually we insert fresh 100 additional seed users here (teachers + students)
-- =====================================================

-- 19 more Teachers (user rows)
INSERT INTO dbo.Users (Username, PasswordHash, Email, FullName, Role, IsActive) VALUES
('teacher6',  '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher6@fpt.edu.vn',  N'Võ Thị Thu', 'TEACHER', 1),
('teacher7',  '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher7@fpt.edu.vn',  N'Đinh Quang Minh', 'TEACHER', 1),
('teacher8',  '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher8@fpt.edu.vn',  N'Lưu Thị Hoa', 'TEACHER', 1),
('teacher9',  '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher9@fpt.edu.vn',  N'Phan Văn Đức', 'TEACHER', 1),
('teacher10', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher10@fpt.edu.vn', N'Bùi Thị Nga', 'TEACHER', 1),
('teacher11', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher11@fpt.edu.vn', N'Trịnh Văn Long', 'TEACHER', 1),
('teacher12', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher12@fpt.edu.vn', N'Ngô Thị Bích', 'TEACHER', 1),
('teacher13', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher13@fpt.edu.vn', N'Hồ Văn Phong', 'TEACHER', 1),
('teacher14', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher14@fpt.edu.vn', N'Đặng Thị Loan', 'TEACHER', 1),
('teacher15', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher15@fpt.edu.vn', N'Vũ Quốc Hùng', 'TEACHER', 1),
('teacher16', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher16@fpt.edu.vn', N'Lê Thị Kim Anh', 'TEACHER', 1),
('teacher17', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher17@fpt.edu.vn', N'Nguyễn Hữu Trung', 'TEACHER', 1),
('teacher18', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher18@fpt.edu.vn', N'Phạm Thị Yến', 'TEACHER', 1),
('teacher19', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher19@fpt.edu.vn', N'Cao Văn Tài', 'TEACHER', 1),
('teacher20', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher20@fpt.edu.vn', N'Trần Thị Mai', 'TEACHER', 1),
('teacher21', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher21@fpt.edu.vn', N'Lý Văn Sang', 'TEACHER', 1),
('teacher22', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher22@fpt.edu.vn', N'Dương Thị Hà', 'TEACHER', 1),
('teacher23', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher23@fpt.edu.vn', N'Mai Văn Khải', 'TEACHER', 1),
('teacher24', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher24@fpt.edu.vn', N'Hoàng Thị Nga', 'TEACHER', 1),
('teacher25', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher25@fpt.edu.vn', N'Nguyễn Quang Thuấn', 'TEACHER', 1),
('teacher26', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher26@fpt.edu.vn', N'Đỗ Thái An', 'TEACHER', 1),
('teacher27', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher27@fpt.edu.vn', N'Trần Đại Nam', 'TEACHER', 1),
('teacher28', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher28@fpt.edu.vn', N'Ngô Hữu Kiên', 'TEACHER', 1),
('teacher29', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher29@fpt.edu.vn', N'Hà Minh Tú', 'TEACHER', 1),
('teacher30', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 'teacher30@fpt.edu.vn', N'Vũ Gia Bách', 'TEACHER', 1);
GO

-- 81 more Students (user rows) to reach 100 total new rows in this seed block
INSERT INTO dbo.Users (Username, PasswordHash, Email, FullName, Role, IsActive) VALUES
('student15', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's15@he18.vn', N'Phùng Thị Lan', 'STUDENT', 1),
('student16', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's16@he18.vn', N'Tô Văn Bình', 'STUDENT', 1),
('student17', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's17@he18.vn', N'Đỗ Thị Hằng', 'STUDENT', 1),
('student18', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's18@he18.vn', N'Lê Văn Cường', 'STUDENT', 1),
('student19', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's19@he18.vn', N'Vũ Thị Hoa', 'STUDENT', 1),
('student20', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's20@he18.vn', N'Trần Văn Đạt', 'STUDENT', 1),
('student21', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's21@he18.vn', N'Nguyễn Thị Thảo', 'STUDENT', 1),
('student22', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's22@he18.vn', N'Phạm Văn Kiên', 'STUDENT', 1),
('student23', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's23@he18.vn', N'Hoàng Thị Nhung', 'STUDENT', 1),
('student24', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's24@he18.vn', N'Bùi Văn Tú', 'STUDENT', 1),
('student25', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's25@he18.vn', N'Lê Thị Phương', 'STUDENT', 1),
('student26', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's26@he18.vn', N'Đinh Văn Tùng', 'STUDENT', 1),
('student27', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's27@he18.vn', N'Trịnh Thị Huyền', 'STUDENT', 1),
('student28', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's28@he18.vn', N'Võ Văn Hải', 'STUDENT', 1),
('student29', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's29@he18.vn', N'Ngô Thị Bảo', 'STUDENT', 1),
('student30', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's30@he18.vn', N'Phan Văn Lộc', 'STUDENT', 1),
('student31', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's31@he18.vn', N'Lưu Thị Diểm', 'STUDENT', 1),
('student32', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's32@he18.vn', N'Hà Văn Quý', 'STUDENT', 1),
('student33', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's33@he18.vn', N'Đặng Thị Thu', 'STUDENT', 1),
('student34', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's34@he18.vn', N'Vương Văn Duy', 'STUDENT', 1),
('student35', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's35@he18.vn', N'Cao Thị Xuân', 'STUDENT', 1),
('student36', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's36@he18.vn', N'Lý Văn Hiếu', 'STUDENT', 1),
('student37', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's37@he18.vn', N'Dương Thị Ngân', 'STUDENT', 1),
('student38', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's38@he18.vn', N'Mai Văn Thắng', 'STUDENT', 1),
('student39', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's39@he18.vn', N'Hồ Thị Liên', 'STUDENT', 1),
('student40', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's40@he18.vn', N'Tống Văn Khoa', 'STUDENT', 1),
('student41', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's41@he18.vn', N'Trương Thị Minh', 'STUDENT', 1),
('student42', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's42@he18.vn', N'Đoàn Văn Phú', 'STUDENT', 1),
('student43', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's43@he18.vn', N'Lê Thị Oanh', 'STUDENT', 1),
('student44', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's44@he18.vn', N'Nguyễn Văn Quyết', 'STUDENT', 1),
('student45', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's45@he18.vn', N'Phạm Thị Hồng', 'STUDENT', 1),
('student46', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's46@he18.vn', N'Bùi Văn Tân', 'STUDENT', 1),
('student47', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's47@he18.vn', N'Hoàng Văn Sơn', 'STUDENT', 1),
('student48', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's48@he18.vn', N'Trần Thị Diệu', 'STUDENT', 1),
('student49', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's49@he18.vn', N'Đinh Văn Hải', 'STUDENT', 1),
('student50', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's50@he18.vn', N'Võ Thị Hạnh', 'STUDENT', 1),
('student51', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's51@he18.vn', N'Lưu Văn Cảnh', 'STUDENT', 1),
('student52', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's52@he18.vn', N'Ngô Thị Lan', 'STUDENT', 1),
('student53', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's53@he18.vn', N'Phan Văn Tài', 'STUDENT', 1),
('student54', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's54@he18.vn', N'Hà Thị Thúy', 'STUDENT', 1),
('student55', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's55@he18.vn', N'Lý Văn Dũng', 'STUDENT', 1),
('student56', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's56@he18.vn', N'Dương Thị Quỳnh', 'STUDENT', 1),
('student57', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's57@he18.vn', N'Cao Văn Nghĩa', 'STUDENT', 1),
('student58', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's58@he18.vn', N'Trịnh Thị Vân', 'STUDENT', 1),
('student59', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's59@he18.vn', N'Đặng Văn Thành', 'STUDENT', 1),
('student60', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's60@he18.vn', N'Vương Thị Bích', 'STUDENT', 1),
('student61', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's61@he18.vn', N'Hồ Văn Duy', 'STUDENT', 1),
('student62', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's62@he18.vn', N'Tô Thị Hà', 'STUDENT', 1),
('student63', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's63@he18.vn', N'Lê Văn Toàn', 'STUDENT', 1),
('student64', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's64@he18.vn', N'Nguyễn Thị Ngọc', 'STUDENT', 1),
('student65', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's65@he18.vn', N'Phạm Văn Long', 'STUDENT', 1),
('student66', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's66@he18.vn', N'Bùi Thị Mai', 'STUDENT', 1),
('student67', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's67@he18.vn', N'Võ Văn Khang', 'STUDENT', 1),
('student68', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's68@he18.vn', N'Trần Thị Thùy', 'STUDENT', 1),
('student69', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's69@he18.vn', N'Đinh Thị Loan', 'STUDENT', 1),
('student70', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's70@he18.vn', N'Lưu Văn Minh', 'STUDENT', 1),
('student71', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's71@he18.vn', N'Hoàng Văn Hải', 'STUDENT', 1),
('student72', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's72@he18.vn', N'Ngô Thị Hương', 'STUDENT', 1),
('student73', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's73@he18.vn', N'Phan Văn Đông', 'STUDENT', 1),
('student74', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's74@he18.vn', N'Đoàn Thị Phúc', 'STUDENT', 1),
('student75', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's75@he18.vn', N'Tống Văn Bảo', 'STUDENT', 1),
('student76', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's76@he18.vn', N'Vũ Thị Thu', 'STUDENT', 1),
('student77', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's77@he18.vn', N'Hà Văn Sỹ', 'STUDENT', 1),
('student78', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's78@he18.vn', N'Dương Thị Châu', 'STUDENT', 1),
('student79', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's79@he18.vn', N'Trương Văn Tín', 'STUDENT', 1),
('student80', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's80@he18.vn', N'Cao Thị Yến', 'STUDENT', 1),
('student81', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's81@he18.vn', N'Lý Văn Đức', 'STUDENT', 1),
('student82', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's82@he18.vn', N'Trịnh Thị Khánh', 'STUDENT', 1),
('student83', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's83@he18.vn', N'Đặng Văn Phong', 'STUDENT', 1),
('student84', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's84@he18.vn', N'Mai Thị Hồng', 'STUDENT', 1),
('student85', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's85@he18.vn', N'Hồ Văn Hòa', 'STUDENT', 1),
('student86', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's86@he18.vn', N'Tô Thị Lệ', 'STUDENT', 1),
('student87', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's87@he18.vn', N'Phùng Văn An', 'STUDENT', 1),
('student88', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's88@he18.vn', N'Lưu Thị Bình', 'STUDENT', 1),
('student89', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's89@he18.vn', N'Nguyễn Văn Trí', 'STUDENT', 1),
('student90', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's90@he18.vn', N'Phạm Thị Cúc', 'STUDENT', 1),
('student91', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's91@he18.vn', N'Hoàng Văn Linh', 'STUDENT', 1),
('student92', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's92@he18.vn', N'Trần Thị Bích', 'STUDENT', 1),
('student93', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's93@he18.vn', N'Đinh Văn Tuấn', 'STUDENT', 1),
('student94', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's94@he18.vn', N'Võ Thị Giang', 'STUDENT', 1),
('student95', '$2a$10$Buj5ZRDdXG/uQTQzgCmoDul02XwRUh83447c09UreThGSYbOA0wx.', 's95@he18.vn', N'Bùi Văn Quang', 'STUDENT', 1);
GO

-- Link new Teachers -> dbo.Teachers
INSERT INTO dbo.Teachers (TeacherId, TeacherCode, Department)
SELECT UserId,
       'GV' + RIGHT('0000' + CAST(UserId AS VARCHAR(10)), 4),
       CASE WHEN UserId % 3 = 0 THEN N'Kỹ thuật phần mềm'
            WHEN UserId % 3 = 1 THEN N'Khoa học máy tính'
            ELSE N'Hệ thống thông tin' END
FROM dbo.Users WHERE Role = 'TEACHER' AND UserId NOT IN (SELECT TeacherId FROM dbo.Teachers);
GO

-- Link new Students -> dbo.Students
DECLARE @PId INT = (SELECT TOP 1 ProgramId FROM dbo.Programs ORDER BY ProgramId);
DECLARE @SId INT = (SELECT TOP 1 SemesterId FROM dbo.Semesters WHERE IsActive = 1);
INSERT INTO dbo.Students (StudentId, StudentCode, ProgramId, CurrentSemesterId)
SELECT UserId,
       'HE18' + RIGHT('0000' + CAST(UserId AS VARCHAR(10)), 4),
       @PId, @SId
FROM dbo.Users WHERE Role = 'STUDENT' AND UserId NOT IN (SELECT StudentId FROM dbo.Students);
GO

-- =====================================================
-- SEED: Programs (97 more => total 100)
-- =====================================================
INSERT INTO dbo.Programs (ProgramCode, ProgramName, IsActive) VALUES
('IS',   N'Hệ thống thông tin', 1),
('CS',   N'Khoa học máy tính', 1),
('BA',   N'Kinh doanh và Quản trị', 1),
('MK',   N'Marketing số', 1),
('EC',   N'Kinh tế học', 1),
('DS',   N'Khoa học dữ liệu', 1),
('CN',   N'Mạng máy tính', 1),
('CEP',  N'Kỹ thuật máy tính', 1),
('EE',   N'Kỹ thuật điện', 1),
('ME',   N'Kỹ thuật cơ khí', 1),
('JP',   N'Ngôn ngữ Nhật', 1),
('KR',   N'Ngôn ngữ Hàn', 1),
('EN',   N'Ngôn ngữ Anh', 1),
('LA',   N'Luật kinh tế', 1),
('AC',   N'Kế toán', 1),
('FI',   N'Tài chính ngân hàng', 1),
('HR',   N'Quản trị nhân lực', 1),
('HT',   N'Quản trị khách sạn', 1),
('TN',   N'Du lịch lữ hành', 1),
('MT',   N'Truyền thông đa phương tiện', 1),
('FA',   N'Thiết kế nội thất', 1),
('FD',   N'Thiết kế thời trang', 1),
('PH',   N'Nhiếp ảnh và Phim ảnh', 1),
('MU',   N'Âm nhạc ứng dụng', 1),
('GA',   N'Thiết kế game', 1),
('AR',   N'Kiến trúc', 1),
('CI',   N'Kỹ thuật xây dựng', 1),
('BT',   N'Công nghệ sinh học', 1),
('FO',   N'Công nghệ thực phẩm', 1),
('EN2',  N'Kỹ thuật môi trường', 1),
('NF',   N'Dinh dưỡng học', 1),
('PY',   N'Tâm lý học', 1),
('SO',   N'Xã hội học', 1),
('PO',   N'Khoa học chính trị', 1),
('HI',   N'Lịch sử', 1),
('LI',   N'Thư viện thông tin', 1),
('PEP',  N'Giáo dục thể chất', 1),
('NC',   N'Điều dưỡng', 1),
('MD',   N'Y khoa', 1),
('PH2',  N'Dược học', 1),
('DEP',  N'Kinh tế phát triển', 1),
('RM',   N'Quản trị rủi ro', 1),
('SC',   N'Chuỗi cung ứng', 1),
('IE',   N'Kỹ thuật công nghiệp', 1),
('AU',   N'Kỹ thuật ô tô', 1),
('AV',   N'Kỹ thuật hàng không', 1),
('MA',   N'Toán học ứng dụng', 1),
('PH3',  N'Vật lý kỹ thuật', 1),
('CH',   N'Hóa học ứng dụng', 1),
('BI',   N'Sinh học ứng dụng', 1),
('GE',   N'Kỹ thuật địa chất', 1),
('SP',   N'Ngôn ngữ Tây Ban Nha', 1),
('FR',   N'Ngôn ngữ Pháp', 1),
('DE2',  N'Ngôn ngữ Đức', 1),
('RU',   N'Ngôn ngữ Nga', 1),
('ZH',   N'Ngôn ngữ Trung', 1),
('ID',   N'Quản lý công nghiệp', 1),
('LO',   N'Logistics', 1),
('PM',   N'Quản lý dự án', 1),
('EM',   N'Khởi nghiệp', 1),
('EA',   N'Kiểm toán', 1),
('TX',   N'Thuế', 1),
('RE',   N'Bất động sản', 1),
('INS',  N'Bảo hiểm', 1),
('BK',   N'Ngân hàng số', 1),
('CY',   N'An ninh mạng', 1),
('BL',   N'Chuỗi khối', 1),
('ML',   N'Học máy', 1),
('RO',   N'Robotics', 1),
('IOT',  N'Internet vạn vật', 1),
('CV',   N'Thị giác máy tính', 1),
('NLP',  N'Xử lý ngôn ngữ tự nhiên', 1),
('CG',   N'Đồ họa máy tính', 1),
('HCI',  N'Tương tác người-máy', 1),
('DM',   N'Khai phá dữ liệu', 1),
('BI2',  N'Kinh doanh thông minh', 1),
('OR',   N'Nghiên cứu vận trù', 1),
('ST',   N'Thống kê ứng dụng', 1),
('QA',   N'Đảm bảo chất lượng phần mềm', 1),
('SD',   N'Kiến trúc phần mềm', 1),
('DB',   N'Cơ sở dữ liệu nâng cao', 1),
('OSP',  N'Hệ điều hành nâng cao', 1),
('DC',   N'Điện toán phân tán', 1),
('CC',   N'Điện toán đám mây', 1),
('DV',   N'DevOps', 1),
('SA',   N'Kiến trúc hệ thống', 1),
('WD',   N'Phát triển web nâng cao', 1),
('MB',   N'Phát triển ứng dụng di động', 1),
('UX',   N'Thiết kế trải nghiệm người dùng', 1),
('GD2',  N'Thiết kế đồ họa nâng cao', 1),
('AN',   N'Hoạt hình kỹ thuật số', 1),
('VR',   N'Thực tế ảo và Thực tế tăng cường', 1);
GO

/* -------------------------
   1) Seed 15 semesters
   Start from SP25
   ------------------------- */
IF OBJECT_ID('tempdb..#SemSeed') IS NOT NULL DROP TABLE #SemSeed;
CREATE TABLE #SemSeed
(
    SemesterCode NVARCHAR(50) NOT NULL PRIMARY KEY,
    SemesterName NVARCHAR(200) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    IsActive BIT NOT NULL,
    RegistrationEndDate DATE NULL,
    AddDropDeadline DATE NULL
);

INSERT INTO #SemSeed (SemesterCode, SemesterName, StartDate, EndDate, IsActive, RegistrationEndDate, AddDropDeadline) VALUES
(N'SP25', N'Spring 2025', '2025-01-06', '2025-04-30', 0, '2024-12-31', '2025-01-15'),
(N'SU25', N'Summer 2025', '2025-05-12', '2025-08-30', 0, '2025-05-01', '2025-05-20'),
(N'FA25', N'Fall 2025',   '2025-09-08', '2025-12-20', 0, '2025-08-31', '2025-09-20'),

(N'SP26', N'Spring 2026', '2026-01-05', '2026-04-30', 1, '2025-12-31', '2026-01-15'),
(N'SU26', N'Summer 2026', '2026-05-11', '2026-08-30', 0, '2026-05-01', '2026-05-20'),
(N'FA26', N'Fall 2026',   '2026-09-07', '2026-12-20', 0, '2026-08-31', '2026-09-20'),

(N'SP27', N'Spring 2027', '2027-01-04', '2027-04-30', 0, '2026-12-31', '2027-01-15'),
(N'SU27', N'Summer 2027', '2027-05-10', '2027-08-30', 0, '2027-05-01', '2027-05-20'),
(N'FA27', N'Fall 2027',   '2027-09-06', '2027-12-20', 0, '2027-08-31', '2027-09-20'),

(N'SP28', N'Spring 2028', '2028-01-03', '2028-04-30', 0, '2027-12-31', '2028-01-15'),
(N'SU28', N'Summer 2028', '2028-05-08', '2028-08-30', 0, '2028-05-01', '2028-05-20'),
(N'FA28', N'Fall 2028',   '2028-09-04', '2028-12-20', 0, '2028-08-31', '2028-09-20'),

(N'SP29', N'Spring 2029', '2029-01-08', '2029-04-30', 0, '2028-12-31', '2029-01-15'),
(N'SU29', N'Summer 2029', '2029-05-14', '2029-08-30', 0, '2029-05-01', '2029-05-20'),
(N'FA29', N'Fall 2029',   '2029-09-10', '2029-12-20', 0, '2029-08-31', '2029-09-20');

MERGE dbo.Semesters AS tgt
USING #SemSeed AS src
ON tgt.SemesterCode = src.SemesterCode
WHEN MATCHED THEN
    UPDATE SET
        SemesterName = src.SemesterName,
        StartDate = src.StartDate,
        EndDate = src.EndDate,
        IsActive = src.IsActive,
        RegistrationEndDate = src.RegistrationEndDate,
        AddDropDeadline = src.AddDropDeadline
WHEN NOT MATCHED THEN
    INSERT (SemesterCode, SemesterName, StartDate, EndDate, IsActive, RegistrationEndDate, AddDropDeadline)
    VALUES (src.SemesterCode, src.SemesterName, src.StartDate, src.EndDate, src.IsActive, src.RegistrationEndDate, src.AddDropDeadline);
GO

/* ---------------------------------------------------------
   2) Seed course pools (10 courses each) for allowed semesters
   Allowed semesters: SP25, SU25, FA25, SP26, SU26, FA26, SP27, SU27
   --------------------------------------------------------- */
IF OBJECT_ID('tempdb..#CoursePool') IS NOT NULL DROP TABLE #CoursePool;
CREATE TABLE #CoursePool
(
    SemesterCode NVARCHAR(50) NOT NULL,
    CourseCode NVARCHAR(50) NOT NULL,
    CourseName NVARCHAR(200) NOT NULL,
    Credits INT NOT NULL,
    Description NVARCHAR(MAX) NULL,
    PRIMARY KEY (SemesterCode, CourseCode)
);

/* SP25 - 10 */
INSERT INTO #CoursePool VALUES
(N'SP25', N'PRN201', N'Object-Oriented Programming', 3, N'Covers classes, inheritance, polymorphism, interfaces, and clean OOP design in C#.'),
(N'SP25', N'DBI202', N'Database Systems', 3, N'Introduces relational data modeling, normalization, SQL querying, and transaction basics.'),
(N'SP25', N'WED201', N'Web Front-End Development', 3, N'Builds responsive web interfaces using HTML, CSS, JavaScript, and UI best practices.'),
(N'SP25', N'SWT201', N'Software Testing Fundamentals', 3, N'Focuses on test design techniques, unit testing, integration testing, and defect lifecycle.'),
(N'SP25', N'OSG202', N'Operating Systems Concepts', 3, N'Explains process management, memory, file systems, synchronization, and scheduling.'),
(N'SP25', N'PRF192', N'Programming Fundamentals', 3, N'Provides foundational programming logic, control flow, functions, and basic data structures.'),
(N'SP25', N'MAD101', N'Discrete Mathematics', 3, N'Covers logic, sets, combinatorics, graphs, and proofs for computing disciplines.'),
(N'SP25', N'NET201', N'Computer Networks Basics', 3, N'Introduces network models, routing, switching, TCP/IP, and network troubleshooting basics.'),
(N'SP25', N'SSL101', N'Study Skills for University', 2, N'Develops time management, effective learning habits, note-taking, and teamwork skills.'),
(N'SP25', N'PRJ101', N'Project Introduction', 2, N'Guides students through project planning, scope definition, and collaborative execution.');

/* SU25 - 10 */
INSERT INTO #CoursePool VALUES
(N'SU25', N'PRN211', N'Basic Cross-Platform App Programming', 3, N'C# basic and WinForms.'),
(N'SU25', N'DBI203', N'Advanced Database Programming', 3, N'Includes stored procedures, indexing strategies, views, and query performance tuning.'),
(N'SU25', N'WED202', N'Client-Side Frameworks', 3, N'Builds SPAs with modern JavaScript frameworks and reusable component architecture.'),
(N'SU25', N'SWE201', N'Software Requirements Engineering', 3, N'Focuses on elicitation, modeling, validation, and requirements management workflows.'),
(N'SU25', N'PRM301', N'Mobile Application Development', 3, N'Develops mobile applications with app lifecycle, UI patterns, and local data handling.'),
(N'SU25', N'SWD301', N'Software Design Patterns', 3, N'Applies creational, structural, and behavioral patterns in real-world software design.'),
(N'SU25', N'JPD123', N'Japanese for IT Professionals', 2, N'Builds practical Japanese communication skills for technical and workplace contexts.'),
(N'SU25', N'ACC101', N'Principles of Accounting', 2, N'Introduces accounting equations, financial statements, and basic business transactions.'),
(N'SU25', N'ECO201', N'Microeconomics for Engineers', 2, N'Explains market behavior, pricing, cost structures, and decision-making under constraints.'),
(N'SU25', N'SKT101', N'Soft Skills and Communication', 2, N'Improves communication, collaboration, presentation, and professional interaction skills.');

/* FA25 - 10 */
INSERT INTO #CoursePool VALUES
(N'FA25', N'PRN221', N'Cross-Platform UI Engineering', 3, N'Builds maintainable desktop UI with architecture patterns and reusable components.'),
(N'FA25', N'DBI204', N'Database Administration', 3, N'Covers backup, recovery, security, monitoring, and operational database maintenance.'),
(N'FA25', N'WED203', N'Web Accessibility and UX', 3, N'Applies accessibility standards, UX heuristics, and inclusive web design practices.'),
(N'FA25', N'SWT202', N'Automation Testing', 3, N'Implements automated tests using frameworks, CI integration, and test reporting.'),
(N'FA25', N'PRM302', N'Cross-Platform Mobile Development', 3, N'Builds cross-platform apps with shared codebase and native integration fundamentals.'),
(N'FA25', N'SEC201', N'Application Security Basics', 3, N'Introduces secure coding, OWASP risks, authentication, and common mitigation patterns.'),
(N'FA25', N'BIZ201', N'Business Process Analysis', 2, N'Analyzes workflows, identifies bottlenecks, and models process improvements.'),
(N'FA25', N'LAW101', N'IT Law and Ethics', 2, N'Discusses legal, ethical, and compliance issues in software and data management.'),
(N'FA25', N'PMA201', N'Project Management Basics', 2, N'Introduces scope, schedule, risk, stakeholder, and quality management practices.'),
(N'FA25', N'ENG201', N'Academic English for IT', 2, N'Enhances technical reading, writing, presentation, and documentation in English.');

/* SP26 - 10 */
INSERT INTO #CoursePool VALUES
(N'SP26', N'PRN222', N'Advanced Cross-Platform App Programming', 3, N'ASP.NET Core, EF Core, SignalR.'),
(N'SP26', N'PRN231', N'Building Cross-Platform Web APIs', 3, N'RESTful API, OData, JWT.'),
(N'SP26', N'SWP391', N'Software Development Project', 4, N'Capstone project for juniors.'),
(N'SP26', N'DBI205', N'NoSQL and Distributed Data', 3, N'Introduces document stores, key-value systems, consistency models, and scaling strategies.'),
(N'SP26', N'SWT203', N'Performance Testing', 3, N'Focuses on load, stress, endurance testing, and performance bottleneck diagnostics.'),
(N'SP26', N'SEC202', N'Secure Web Development', 3, N'Builds secure web applications with advanced authn/authz and defense strategies.'),
(N'SP26', N'PRF301', N'Algorithm Design and Analysis', 3, N'Covers algorithm paradigms, complexity analysis, and optimization techniques.'),
(N'SP26', N'PRO301', N'Professional Practice in Software', 2, N'Prepares students for enterprise workflows, agile teamwork, and delivery discipline.'),
(N'SP26', N'AI201', N'Introduction to Machine Learning', 3, N'Presents core ML concepts, model training, evaluation metrics, and feature engineering.'),
(N'SP26', N'NET301', N'Cloud Networking Foundations', 3, N'Explains virtual networks, cloud connectivity, load balancing, and traffic security.');

/* SU26 - 10 */
INSERT INTO #CoursePool VALUES
(N'SU26', N'PRN223', N'Enterprise Application Development', 3, N'Develops layered enterprise systems with dependency injection and clean architecture.'),
(N'SU26', N'API301', N'API Integration and Gateway', 3, N'Covers API lifecycle, gateway policies, throttling, observability, and contract governance.'),
(N'SU26', N'DBI206', N'Data Warehouse Fundamentals', 3, N'Introduces ETL, dimensional modeling, OLAP concepts, and analytical reporting pipelines.'),
(N'SU26', N'SWT204', N'Quality Assurance Engineering', 3, N'Builds QA strategy across testing levels, quality metrics, and release readiness checks.'),
(N'SU26', N'SEC301', N'Identity and Access Management', 3, N'Implements identity lifecycle, SSO, MFA, RBAC, and secure session management.'),
(N'SU26', N'OPS301', N'DevOps Practices', 3, N'Applies CI/CD, infrastructure automation, monitoring, and release reliability principles.'),
(N'SU26', N'UX301', N'Human-Centered Product Design', 2, N'Uses research-driven design to build usable digital products with measurable outcomes.'),
(N'SU26', N'BDA201', N'Data Analytics for Business', 2, N'Applies descriptive and diagnostic analytics to business cases and decision support.'),
(N'SU26', N'CLD201', N'Cloud Computing Essentials', 3, N'Introduces cloud services, deployment models, pricing, and operational best practices.'),
(N'SU26', N'PMA301', N'Agile Project Delivery', 2, N'Applies agile planning, backlog management, sprint execution, and retrospective improvement.');

/* FA26 - 10 */
INSERT INTO #CoursePool VALUES
(N'FA26', N'PRN224', N'Microservices Development', 3, N'Builds service-based systems with resilience, observability, and inter-service communication.'),
(N'FA26', N'API302', N'Event-Driven Architectures', 3, N'Designs asynchronous workflows with messaging, event sourcing, and eventual consistency.'),
(N'FA26', N'DBI207', N'Database Performance Engineering', 3, N'Optimizes query plans, indexing, caching, and high-throughput transactional workloads.'),
(N'FA26', N'SWT205', N'Reliability Testing and SRE', 3, N'Applies reliability engineering, failure analysis, and production readiness validation.'),
(N'FA26', N'SEC302', N'Application Threat Modeling', 3, N'Identifies attack surfaces, threat scenarios, and mitigation strategies in software systems.'),
(N'FA26', N'OPS302', N'Containerization and Orchestration', 3, N'Uses containers, orchestration platforms, and deployment automation for scalable systems.'),
(N'FA26', N'AI301', N'Applied Machine Learning', 3, N'Builds applied ML solutions with practical pipelines, model monitoring, and deployment.'),
(N'FA26', N'CLD301', N'Cloud Native Development', 3, N'Implements cloud-native patterns for resilient, scalable, and observable applications.'),
(N'FA26', N'QMS201', N'Quality Management Systems', 2, N'Introduces quality frameworks, process controls, and continuous improvement mechanisms.'),
(N'FA26', N'PRO302', N'Engineering Leadership Basics', 2, N'Develops technical leadership, mentoring, and decision-making for software teams.');

/* SP27 - 10 */
INSERT INTO #CoursePool VALUES
(N'SP27', N'PRN225', N'Distributed Application Engineering', 3, N'Builds distributed systems handling consistency, partitioning, and fault tolerance concerns.'),
(N'SP27', N'API303', N'High-Scale API Systems', 3, N'Designs APIs for high throughput with caching, rate limiting, and resilience mechanisms.'),
(N'SP27', N'DBI208', N'Data Governance and Security', 3, N'Covers data governance, lineage, protection policies, and regulatory compliance controls.'),
(N'SP27', N'SWT206', N'Software Metrics and Quality', 3, N'Uses actionable software metrics to improve maintainability, reliability, and delivery speed.'),
(N'SP27', N'SEC303', N'Advanced Secure Architecture', 3, N'Architects secure systems with layered controls and robust trust boundaries.'),
(N'SP27', N'OPS303', N'Observability Engineering', 3, N'Implements logging, tracing, metrics, and alerting for effective production diagnostics.'),
(N'SP27', N'AI302', N'Intelligent Systems Engineering', 3, N'Combines ML components with software architecture for reliable intelligent applications.'),
(N'SP27', N'CLD302', N'Multi-Cloud Architecture', 3, N'Designs workloads across multiple cloud providers with portability and resilience strategies.'),
(N'SP27', N'PMA302', N'Product Delivery Management', 2, N'Coordinates roadmap, delivery planning, and cross-functional execution for product teams.'),
(N'SP27', N'ENT201', N'Technology Entrepreneurship', 2, N'Explores startup fundamentals, product-market fit, and technology business models.');

/* SU27 - 10 */
INSERT INTO #CoursePool VALUES
(N'SU27', N'PRN226', N'Scalable Software Systems', 3, N'Builds scalable applications with performance optimization and distributed design practices.'),
(N'SU27', N'API304', N'API Security and Compliance', 3, N'Implements secure API ecosystems with policy enforcement and compliance standards.'),
(N'SU27', N'DBI209', N'Real-Time Data Processing', 3, N'Builds streaming pipelines, event processing, and low-latency analytics workloads.'),
(N'SU27', N'SWT207', N'Test Strategy and Governance', 3, N'Defines organization-wide testing strategy, governance, and quality risk management.'),
(N'SU27', N'SEC304', N'DevSecOps Engineering', 3, N'Integrates security controls into CI/CD workflows and operational delivery practices.'),
(N'SU27', N'OPS304', N'Platform Engineering', 3, N'Builds internal developer platforms to accelerate delivery and improve developer experience.'),
(N'SU27', N'AI303', N'LLM Application Development', 3, N'Develops AI assistants with tool-calling, grounding, guardrails, and evaluation.'),
(N'SU27', N'CLD303', N'Cloud Cost Optimization', 2, N'Optimizes cloud architecture and operations for performance-cost efficiency balance.'),
(N'SU27', N'QMS301', N'Operational Excellence', 2, N'Applies continuous improvement and operational discipline to software organizations.'),
(N'SU27', N'CAP401', N'Capstone Preparation', 2, N'Prepares students for capstone planning, architecture proposal, and execution readiness.');

MERGE dbo.Courses AS tgt
USING (
    SELECT DISTINCT CourseCode, CourseName, Credits, Description
    FROM #CoursePool
) AS src
ON tgt.CourseCode = src.CourseCode
WHEN MATCHED THEN
    UPDATE SET
        CourseName = src.CourseName,
        Credits = src.Credits,
        Description = src.Description,
        IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (CourseCode, CourseName, Credits, Description, IsActive)
    VALUES (src.CourseCode, src.CourseName, src.Credits, src.Description, 1);
GO

/* ----------------------------------------------
   3) Seed 3 class sections per course per semester
   only for allowed semesters in #CoursePool
   MaxCapacity always = 30
   ---------------------------------------------- */
IF OBJECT_ID('tempdb..#SecNums') IS NOT NULL DROP TABLE #SecNums;
CREATE TABLE #SecNums (N INT NOT NULL PRIMARY KEY);
INSERT INTO #SecNums(N) VALUES (1),(2),(3);

DECLARE @FallbackTeacherId INT = (SELECT TOP 1 TeacherId FROM dbo.Teachers ORDER BY TeacherId);

;WITH CourseBySemester AS
(
    SELECT
        cp.SemesterCode,
        s.SemesterId,
        c.CourseId,
        cp.CourseCode
    FROM #CoursePool cp
    JOIN dbo.Semesters s ON s.SemesterCode = cp.SemesterCode
    JOIN dbo.Courses c ON c.CourseCode = cp.CourseCode
),
TeacherPick AS
(
    SELECT
        t.TeacherId,
        ROW_NUMBER() OVER (ORDER BY t.TeacherId) AS rn,
        COUNT(*) OVER() AS total
    FROM dbo.Teachers t
),
ClassTarget AS
(
    SELECT
        cs.SemesterId,
        cs.CourseId,
        cs.SemesterCode,
        cs.CourseCode,
        sn.N AS SectionNo,
        -- section code format similar existing style (e.g., SE1801), no semester prefix
        CONCAT(LEFT(cs.CourseCode, 3), RIGHT('00' + CAST(sn.N AS VARCHAR(2)), 2)) AS SectionCode,
        COALESCE(tp.TeacherId, @FallbackTeacherId) AS TeacherId
    FROM CourseBySemester cs
    CROSS JOIN #SecNums sn
    OUTER APPLY
    (
        SELECT TOP 1 tp2.TeacherId
        FROM TeacherPick tp2
        WHERE tp2.rn = ((ABS(CHECKSUM(CONCAT(cs.SemesterCode, '-', cs.CourseCode, '-', sn.N))) % NULLIF(tp2.total,0)) + 1)
    ) tp
)
MERGE dbo.ClassSections AS tgt
USING ClassTarget AS src
ON tgt.SemesterId = src.SemesterId
   AND tgt.CourseId = src.CourseId
   AND tgt.SectionCode = src.SectionCode
WHEN MATCHED THEN
    UPDATE SET
        TeacherId = src.TeacherId,
        IsOpen = 1,
        MaxCapacity = 30,
        Room = CONCAT(N'BE-', RIGHT('000' + CAST((100 + src.SectionNo) AS VARCHAR(3)), 3)),
        Notes = CONCAT(N'Auto-seeded for ', src.SemesterCode, N' / ', src.CourseCode)
WHEN NOT MATCHED THEN
    INSERT
    (
        SemesterId,
        CourseId,
        TeacherId,
        SectionCode,
        IsOpen,
        MaxCapacity,
        CurrentEnrollment,
        Room,
        OnlineUrl,
        Notes
    )
    VALUES
    (
        src.SemesterId,
        src.CourseId,
        src.TeacherId,
        src.SectionCode,
        1,
        30,
        0,
        CONCAT(N'BE-', RIGHT('000' + CAST((100 + src.SectionNo) AS VARCHAR(3)), 3)),
        NULL,
        CONCAT(N'Auto-seeded for ', src.SemesterCode, N' / ', src.CourseCode)
    );
GO

-- =====================================================
-- SEED: Enrollments (Mỗi student học ít nhất 5 lớp)
-- Dùng SELECT dynamic từ Students x ClassSections
-- =====================================================
BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM dbo.Enrollments;
    UPDATE dbo.ClassSections SET CurrentEnrollment = 0;

    IF OBJECT_ID('tempdb..#TargetSections') IS NOT NULL DROP TABLE #TargetSections;
    IF OBJECT_ID('tempdb..#Students') IS NOT NULL DROP TABLE #Students;
    IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;
    IF OBJECT_ID('tempdb..#ClassQueue') IS NOT NULL DROP TABLE #ClassQueue;
    IF OBJECT_ID('tempdb..#NeedStudents') IS NOT NULL DROP TABLE #NeedStudents;

    /* Target classes */
    SELECT
        cs.ClassSectionId,
        cs.SemesterId,
        s.SemesterCode,
        cs.CourseId,
        c.Credits,
        cs.MaxCapacity,
        CASE
            WHEN s.SemesterCode = N'SU26' THEN 5 + ABS(CHECKSUM(CONCAT(N'SU26-', cs.ClassSectionId))) % 6  -- 5..10
            ELSE 20 + ABS(CHECKSUM(CONCAT(N'GEN-', cs.ClassSectionId))) % 11                                  -- 20..30
        END AS TargetCount
    INTO #TargetSections
    FROM dbo.ClassSections cs
    JOIN dbo.Semesters s ON s.SemesterId = cs.SemesterId
    JOIN dbo.Courses c ON c.CourseId = cs.CourseId
    WHERE s.SemesterCode IN (N'SP25', N'SU25', N'FA25', N'SP26', N'SU26');

    UPDATE ts
    SET TargetCount = CASE WHEN TargetCount > MaxCapacity THEN MaxCapacity ELSE TargetCount END
    FROM #TargetSections ts;

    SELECT StudentId INTO #Students FROM dbo.Students;

    CREATE TABLE #Assigned
    (
        StudentId INT NOT NULL,
        SemesterId INT NOT NULL,
        CourseId INT NOT NULL,
        CONSTRAINT PK_Assigned PRIMARY KEY (StudentId, SemesterId, CourseId)
    );

    CREATE TABLE #ClassQueue
    (
        RowNum INT IDENTITY(1,1) PRIMARY KEY,
        ClassSectionId INT NOT NULL,
        SemesterId INT NOT NULL,
        CourseId INT NOT NULL,
        Credits INT NOT NULL,
        TargetCount INT NOT NULL,
        MaxCapacity INT NOT NULL
    );

    INSERT INTO #ClassQueue (ClassSectionId, SemesterId, CourseId, Credits, TargetCount, MaxCapacity)
    SELECT ClassSectionId, SemesterId, CourseId, Credits, TargetCount, MaxCapacity
    FROM #TargetSections
    ORDER BY
        CASE WHEN SemesterCode = N'SU26' THEN 1 ELSE 0 END, -- fill regular semesters first
        SemesterId, CourseId, ClassSectionId;

    DECLARE
        @i INT = 1,
        @n INT = (SELECT COUNT(*) FROM #ClassQueue),
        @ClassSectionId INT,
        @SemesterId INT,
        @CourseId INT,
        @Credits INT,
        @TargetCount INT,
        @MaxCapacity INT;

    /* Phase A: fill each class safely one-by-one */
    WHILE @i <= @n
    BEGIN
        SELECT
            @ClassSectionId = ClassSectionId,
            @SemesterId = SemesterId,
            @CourseId = CourseId,
            @Credits = Credits,
            @TargetCount = TargetCount,
            @MaxCapacity = MaxCapacity
        FROM #ClassQueue
        WHERE RowNum = @i;

        ;WITH Candidate AS
        (
            SELECT
                s.StudentId,
                ROW_NUMBER() OVER (
                    ORDER BY ABS(CHECKSUM(CONCAT(@ClassSectionId, N'-', s.StudentId))), s.StudentId
                ) AS rn
            FROM #Students s
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM #Assigned a
                WHERE a.StudentId = s.StudentId
                  AND a.SemesterId = @SemesterId
                  AND a.CourseId = @CourseId
            )
            AND NOT EXISTS
            (
                SELECT 1
                FROM dbo.Enrollments e
                WHERE e.StudentId = s.StudentId
                  AND e.SemesterId = @SemesterId
                  AND e.CourseId = @CourseId
                  AND e.Status IN (N'ENROLLED', N'WITHDRAWN')
            )
        )
        INSERT INTO dbo.Enrollments
        (
            StudentId, ClassSectionId, SemesterId, CourseId, CreditsSnapshot, Status, EnrolledAt
        )
        SELECT
            c.StudentId, @ClassSectionId, @SemesterId, @CourseId, @Credits, N'ENROLLED', SYSUTCDATETIME()
        FROM Candidate c
        WHERE c.rn <= @TargetCount;

        INSERT INTO #Assigned (StudentId, SemesterId, CourseId)
        SELECT e.StudentId, e.SemesterId, e.CourseId
        FROM dbo.Enrollments e
        WHERE e.ClassSectionId = @ClassSectionId
          AND e.SemesterId = @SemesterId
          AND e.CourseId = @CourseId
          AND NOT EXISTS
          (
              SELECT 1
              FROM #Assigned a
              WHERE a.StudentId = e.StudentId
                AND a.SemesterId = e.SemesterId
                AND a.CourseId = e.CourseId
          );

        SET @i += 1;
    END

    /* Phase B: ensure each student has >= 4 enrollments (best-effort) */
    SELECT
        s.StudentId,
        4 - COUNT(e.EnrollmentId) AS NeedCount
    INTO #NeedStudents
    FROM #Students s
    LEFT JOIN dbo.Enrollments e ON e.StudentId = s.StudentId AND e.Status = N'ENROLLED'
    GROUP BY s.StudentId
    HAVING 4 - COUNT(e.EnrollmentId) > 0;

    DECLARE @NeedStudentId INT, @NeedCount INT, @j INT, @m INT;
    DECLARE curNeed CURSOR LOCAL FAST_FORWARD FOR
        SELECT StudentId, NeedCount FROM #NeedStudents ORDER BY StudentId;
    OPEN curNeed;
    FETCH NEXT FROM curNeed INTO @NeedStudentId, @NeedCount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @j = 1;
        SET @m = @NeedCount;

        WHILE @j <= @m
        BEGIN
            ;WITH Slot AS
            (
                SELECT TOP (1)
                    q.ClassSectionId,
                    q.SemesterId,
                    q.CourseId,
                    q.Credits
                FROM #ClassQueue q
                WHERE (SELECT COUNT(*) FROM dbo.Enrollments e WHERE e.ClassSectionId = q.ClassSectionId) < q.MaxCapacity
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM #Assigned a
                      WHERE a.StudentId = @NeedStudentId
                        AND a.SemesterId = q.SemesterId
                        AND a.CourseId = q.CourseId
                  )
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.Enrollments e2
                      WHERE e2.StudentId = @NeedStudentId
                        AND e2.SemesterId = q.SemesterId
                        AND e2.CourseId = q.CourseId
                        AND e2.Status IN (N'ENROLLED', N'WITHDRAWN')
                  )
                ORDER BY ABS(CHECKSUM(CONCAT(@NeedStudentId, N'-', q.ClassSectionId))), q.ClassSectionId
            )
            INSERT INTO dbo.Enrollments (StudentId, ClassSectionId, SemesterId, CourseId, CreditsSnapshot, Status, EnrolledAt)
            SELECT @NeedStudentId, s.ClassSectionId, s.SemesterId, s.CourseId, s.Credits, N'ENROLLED', SYSUTCDATETIME()
            FROM Slot s;

            IF @@ROWCOUNT = 0 BREAK;

            INSERT INTO #Assigned (StudentId, SemesterId, CourseId)
            SELECT @NeedStudentId, e.SemesterId, e.CourseId
            FROM dbo.Enrollments e
            WHERE e.StudentId = @NeedStudentId
              AND e.EnrollmentId = SCOPE_IDENTITY()
              AND NOT EXISTS
              (
                  SELECT 1 FROM #Assigned a
                  WHERE a.StudentId = @NeedStudentId
                    AND a.SemesterId = e.SemesterId
                    AND a.CourseId = e.CourseId
              );

            SET @j += 1;
        END

        FETCH NEXT FROM curNeed INTO @NeedStudentId, @NeedCount;
    END
    CLOSE curNeed;
    DEALLOCATE curNeed;

    /* Sync CurrentEnrollment */
    ;WITH Agg AS
    (
        SELECT ClassSectionId, COUNT(*) AS Cnt
        FROM dbo.Enrollments
        GROUP BY ClassSectionId
    )
    UPDATE cs
    SET cs.CurrentEnrollment = ISNULL(a.Cnt, 0)
    FROM dbo.ClassSections cs
    LEFT JOIN Agg a ON a.ClassSectionId = cs.ClassSectionId
    WHERE cs.ClassSectionId IN (SELECT ClassSectionId FROM #TargetSections);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ---------- 1) GradeBooks ---------- */
;WITH TargetSections AS
(
    SELECT
        cs.ClassSectionId,
        s.SemesterCode
    FROM dbo.ClassSections cs
    INNER JOIN dbo.Semesters s ON s.SemesterId = cs.SemesterId
    WHERE s.SemesterCode IN (N'SP25', N'SU25', N'FA25', N'SP26')
)
INSERT INTO dbo.GradeBooks
(
    ClassSectionId,
    Status,
    Version,
    PublishedAt,
    LockedAt,
    CreatedAt,
    UpdatedAt
)
SELECT
    ts.ClassSectionId,
    CASE
        WHEN ts.SemesterCode IN (N'SP25', N'SU25', N'FA25') THEN N'PUBLISHED'
        WHEN ts.SemesterCode = N'SP26' THEN N'DRAFT'
    END AS Status,
    1 AS Version,
    CASE
        WHEN ts.SemesterCode IN (N'SP25', N'SU25', N'FA25') THEN SYSUTCDATETIME()
        ELSE NULL
    END AS PublishedAt,
    NULL AS LockedAt,
    SYSUTCDATETIME(),
    NULL
FROM TargetSections ts
LEFT JOIN dbo.GradeBooks gb ON gb.ClassSectionId = ts.ClassSectionId
WHERE gb.GradeBookId IS NULL;
GO

/* ---------- 2) GradeItems (4 default columns) ---------- */
;WITH ItemTemplate AS
(
    SELECT 1 AS SortOrder, N'Điểm danh' AS ItemName, CAST(10.00 AS DECIMAL(5,2)) AS MaxScore, CAST(0.10 AS DECIMAL(6,4)) AS Weight
    UNION ALL
    SELECT 2, N'15 phút',            CAST(10.00 AS DECIMAL(5,2)), CAST(0.20 AS DECIMAL(6,4))
    UNION ALL
    SELECT 3, N'Kiểm tra giữa kỳ',   CAST(10.00 AS DECIMAL(5,2)), CAST(0.30 AS DECIMAL(6,4))
    UNION ALL
    SELECT 4, N'Thi cuối kỳ',        CAST(10.00 AS DECIMAL(5,2)), CAST(0.40 AS DECIMAL(6,4))
)
INSERT INTO dbo.GradeItems
(
    GradeBookId,
    ItemName,
    MaxScore,
    Weight,
    IsRequired,
    SortOrder,
    CreatedAt
)
SELECT
    gb.GradeBookId,
    it.ItemName,
    it.MaxScore,
    it.Weight,
    1 AS IsRequired,
    it.SortOrder,
    SYSUTCDATETIME()
FROM dbo.GradeBooks gb
INNER JOIN dbo.ClassSections cs ON cs.ClassSectionId = gb.ClassSectionId
INNER JOIN dbo.Semesters s ON s.SemesterId = cs.SemesterId
CROSS JOIN ItemTemplate it
LEFT JOIN dbo.GradeItems gi
    ON gi.GradeBookId = gb.GradeBookId
   AND gi.ItemName = it.ItemName
WHERE s.SemesterCode IN (N'SP25', N'SU25', N'FA25', N'SP26')
  AND gi.GradeItemId IS NULL;
GO

/**** ---------- 3) GradeEntries ----------
   Generate scores for all enrollments belonging to classsections that have gradebooks.
   For SP26:
     - only scores for "15 phút" and "Kiểm tra giữa kỳ"
     - others = NULL
****/
;WITH EntrySource AS
(
    SELECT
        gi.GradeItemId,
        gi.ItemName,
        e.EnrollmentId,
        e.StudentId,
        cs.ClassSectionId,
        s.SemesterCode
    FROM dbo.GradeBooks gb
    INNER JOIN dbo.ClassSections cs ON cs.ClassSectionId = gb.ClassSectionId
    INNER JOIN dbo.Semesters s ON s.SemesterId = cs.SemesterId
    INNER JOIN dbo.GradeItems gi ON gi.GradeBookId = gb.GradeBookId
    INNER JOIN dbo.Enrollments e ON e.ClassSectionId = cs.ClassSectionId
    WHERE s.SemesterCode IN (N'SP25', N'SU25', N'FA25', N'SP26')
      AND e.Status IN (N'ENROLLED', N'COMPLETED', N'WITHDRAWN')
),
Calculated AS
(
    SELECT
        es.GradeItemId,
        es.EnrollmentId,
        CASE
            WHEN es.SemesterCode = N'SP26'
                 AND es.ItemName IN (N'Điểm danh', N'Thi cuối kỳ')
                THEN CAST(NULL AS DECIMAL(5,2))
            ELSE
                -- deterministic pseudo-score [5.00 .. 10.00]
                CAST(
                    (
                        5.00
                        + (
                            (ABS(CHECKSUM(CONCAT(es.EnrollmentId, N'-', es.GradeItemId))) % 51) / 10.0
                          )
                    ) AS DECIMAL(5,2)
                )
        END AS Score
    FROM EntrySource es
)
INSERT INTO dbo.GradeEntries
(
    GradeItemId,
    EnrollmentId,
    Score,
    UpdatedBy,
    UpdatedAt
)
SELECT
    c.GradeItemId,
    c.EnrollmentId,
    c.Score,
    NULL AS UpdatedBy,
    SYSUTCDATETIME()
FROM Calculated c
LEFT JOIN dbo.GradeEntries ge
    ON ge.GradeItemId = c.GradeItemId
   AND ge.EnrollmentId = c.EnrollmentId
WHERE ge.GradeEntryId IS NULL;
GO

-- =====================================================
-- SEED: ChatRooms (100 rows)
-- =====================================================
DECLARE @AdminUserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'ADMIN');
INSERT INTO dbo.ChatRooms (RoomType, ClassSectionId, RoomName, Status, CreatedBy)
SELECT TOP 50
    'CLASS',
    ClassSectionId,
    N'Phòng chat - ' + SectionCode,
    'ACTIVE',
    @AdminUserId
FROM dbo.ClassSections
ORDER BY ClassSectionId;

INSERT INTO dbo.ChatRooms (RoomType, CourseId, RoomName, Status, CreatedBy)
SELECT TOP 50
    'COURSE',
    CourseId,
    N'Diễn đàn môn - ' + CourseName,
    'ACTIVE',
    @AdminUserId
FROM dbo.Courses
ORDER BY CourseId;
GO

-- =====================================================
-- SEED: ChatRoomMembers (100 rows)
-- =====================================================
INSERT INTO dbo.ChatRoomMembers (RoomId, UserId, RoleInRoom, MemberStatus)
SELECT TOP 100
    cr.RoomId,
    u.UserId,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY cr.RoomId, u.UserId) % 10 = 0 THEN 'OWNER'
         WHEN ROW_NUMBER() OVER (ORDER BY cr.RoomId, u.UserId) % 10 = 1 THEN 'MODERATOR'
         ELSE 'MEMBER' END,
    'JOINED'
FROM dbo.ChatRooms cr
CROSS JOIN (SELECT TOP 5 UserId FROM dbo.Users ORDER BY UserId) u
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ChatRoomMembers m WHERE m.RoomId = cr.RoomId AND m.UserId = u.UserId
)
ORDER BY cr.RoomId, u.UserId;
GO

-- =====================================================
-- SEED: ChatMessages (100 rows)
-- =====================================================
INSERT INTO dbo.ChatMessages (RoomId, SenderId, MessageType, Content)
SELECT TOP 100
    cr.RoomId,
    u.UserId,
    'TEXT',
    N'Xin chào các bạn, đây là tin nhắn thứ ' + CAST(ROW_NUMBER() OVER (ORDER BY cr.RoomId, u.UserId) AS NVARCHAR(10)) + N' trong phòng chat!'
FROM dbo.ChatRooms cr
CROSS JOIN (SELECT TOP 5 UserId FROM dbo.Users ORDER BY UserId) u
ORDER BY cr.RoomId, u.UserId;
GO

-- =====================================================
-- SEED: StudentWallets (100 rows, 1 per student)
-- =====================================================
INSERT INTO dbo.StudentWallets (StudentId, Balance, WalletStatus)
SELECT StudentId,
       CAST((StudentId * 173 % 10000000) / 100.0 AS DECIMAL(18,2)),
       CASE WHEN StudentId % 10 = 0 THEN 'LOCKED' ELSE 'ACTIVE' END
FROM dbo.Students
WHERE StudentId NOT IN (SELECT StudentId FROM dbo.StudentWallets);
GO

-- =====================================================
-- SEED: AIChatSessions (100 rows)
-- =====================================================
INSERT INTO dbo.AIChatSessions (UserId, Purpose, ModelName, State, PromptVersion)
SELECT TOP 100
    u.UserId,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY u.UserId) % 3 = 0 THEN 'SCORE_SUMMARY'
         WHEN ROW_NUMBER() OVER (ORDER BY u.UserId) % 3 = 1 THEN 'STUDY_PLAN'
         ELSE 'COURSE_SUGGESTION' END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY u.UserId) % 2 = 0 THEN 'gemini-1.5-flash' ELSE 'gemini-1.5-pro' END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY u.UserId) % 3 = 0 THEN 'COMPLETED' ELSE 'ACTIVE' END,
    'v1.0'
FROM dbo.Users u
CROSS JOIN (SELECT 1 AS x UNION ALL SELECT 2) dup
ORDER BY u.UserId;
GO

-- =====================================================
-- SEED: AIChatMessages (100 rows)
-- =====================================================
INSERT INTO dbo.AIChatMessages (ChatSessionId, SenderType, Content)
SELECT TOP 100
    s.ChatSessionId,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.ChatSessionId) % 3 = 0 THEN 'ASSISTANT'
         WHEN ROW_NUMBER() OVER (ORDER BY s.ChatSessionId) % 3 = 1 THEN 'USER'
         ELSE 'SYSTEM' END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.ChatSessionId) % 3 = 1 
         THEN N'Hãy tóm tắt điểm số học kỳ SP26 cho tôi'
         WHEN ROW_NUMBER() OVER (ORDER BY s.ChatSessionId) % 3 = 0 
         THEN N'Điểm của bạn học kỳ SP26: Trung bình 7.5, đã hoàn thành 12 tín chỉ.'
         ELSE N'[Khởi tạo phiên AI]' END
FROM dbo.AIChatSessions s
CROSS JOIN (SELECT 1 AS x UNION ALL SELECT 2) dup
ORDER BY s.ChatSessionId;
GO

-- =====================================================
-- SEED: Recurrences (100 rows) for ScheduleEvents
-- =====================================================
INSERT INTO dbo.Recurrences (RRule, StartDate, EndDate)
SELECT TOP 100
    CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 3 = 0 
         THEN 'FREQ=WEEKLY;BYDAY=MO,WE;INTERVAL=1'
         WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 3 = 1 
         THEN 'FREQ=WEEKLY;BYDAY=TU,TH;INTERVAL=1'
         ELSE 'FREQ=WEEKLY;BYDAY=FR;INTERVAL=1' END,
    '2026-01-05',
    '2026-04-30'
FROM dbo.ClassSections
ORDER BY ClassSectionId;
GO

-- =====================================================
-- SEED: ScheduleEvents (100 rows)
-- =====================================================
INSERT INTO dbo.ScheduleEvents (ClassSectionId, Title, StartAt, EndAt, Location, Status, CreatedBy)
SELECT TOP 100
    cs.ClassSectionId,
    N'Buổi học ' + c.CourseName + N' - Tuần ' + CAST(ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 16 + 1 AS NVARCHAR(5)),
    DATEADD(DAY, (ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 90), '2026-01-05T07:30:00'),
    DATEADD(HOUR, 3, DATEADD(DAY, (ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 90), '2026-01-05T07:30:00')),
    cs.Room,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 4 = 0 THEN 'COMPLETED'
         WHEN ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 4 = 1 THEN 'PUBLISHED'
         WHEN ROW_NUMBER() OVER (ORDER BY cs.ClassSectionId) % 4 = 2 THEN 'DRAFT'
         ELSE 'RESCHEDULED' END,
    (SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'ADMIN')
FROM dbo.ClassSections cs
JOIN dbo.Courses c ON c.CourseId = cs.CourseId
ORDER BY cs.ClassSectionId;
GO

-- =====================================================
-- SEED: GradeBookApprovals (100 rows)
-- =====================================================
INSERT INTO dbo.GradeBookApprovals (GradeBookId, RequestBy, RequestMessage, ResponseBy, ResponseMessage, Outcome, ResponseAt)
SELECT TOP 100
    gb.GradeBookId,
    t.TeacherId,
    N'Đề nghị duyệt sổ điểm cho lớp ' + cs.SectionCode,
    (SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'ADMIN'),
    CASE WHEN ROW_NUMBER() OVER (ORDER BY gb.GradeBookId) % 3 = 0 THEN N'Đã duyệt'
         WHEN ROW_NUMBER() OVER (ORDER BY gb.GradeBookId) % 3 = 1 THEN N'Cần bổ sung thông tin'
         ELSE NULL END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY gb.GradeBookId) % 3 = 0 THEN 'APPROVED'
         WHEN ROW_NUMBER() OVER (ORDER BY gb.GradeBookId) % 3 = 1 THEN 'REJECTED'
         ELSE NULL END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY gb.GradeBookId) % 3 = 2 THEN NULL
         ELSE SYSUTCDATETIME() END
FROM dbo.GradeBooks gb
JOIN dbo.ClassSections cs ON cs.ClassSectionId = gb.ClassSectionId
JOIN dbo.Teachers t ON t.TeacherId = cs.TeacherId
ORDER BY gb.GradeBookId;
GO

-- =====================================================
-- SEED: PaymentTransactions (100 rows)
-- =====================================================
INSERT INTO dbo.PaymentTransactions (StudentId, PaymentMethod, MoMoRequestId, MoMoOrderId, MoMoTransId, Amount, OrderInfo, Status, ErrorCode, PaymentDate)
SELECT TOP 100
    s.StudentId,
    'MOMO',
    'REQ-' + CAST(s.StudentId AS VARCHAR(10)) + '-' + CAST(ROW_NUMBER() OVER (ORDER BY s.StudentId) AS VARCHAR(10)),
    'ORD-' + CAST(s.StudentId * 100 + ROW_NUMBER() OVER (ORDER BY s.StudentId) AS VARCHAR(20)),
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 = 0 THEN NULL
         ELSE CAST(s.StudentId * 1000 + ROW_NUMBER() OVER (ORDER BY s.StudentId) AS BIGINT) END,
    CAST((3 + ROW_NUMBER() OVER (ORDER BY s.StudentId) % 10) * 500000 AS DECIMAL(18,2)),
    N'Nạp tiền học phí kỳ SP26 - SV ' + u.FullName,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 = 0 THEN 'FAILED'
         WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 = 1 THEN 'PENDING'
         ELSE 'SUCCESS' END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 = 0 THEN 9999
         WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 = 1 THEN NULL
         ELSE 0 END,
    CASE WHEN ROW_NUMBER() OVER (ORDER BY s.StudentId) % 4 < 2 THEN NULL
         ELSE SYSUTCDATETIME() END
FROM dbo.Students s
JOIN dbo.Users u ON u.UserId = s.StudentId
CROSS JOIN (SELECT 1 AS x UNION ALL SELECT 2) dup
ORDER BY s.StudentId;
GO

/* =====================================================
   CHATROOM SEED FOR PRN01, PRN02 (FULL BLOCK)
   Replace old block lines 2116 -> 2507
   ===================================================== */
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @CS1Q INT, @CS2Q INT, @TeacherQ INT;

    SELECT TOP 1
        @CS1Q = cs.ClassSectionId,
        @TeacherQ = cs.TeacherId
    FROM dbo.ClassSections cs
    WHERE cs.SectionCode = N'PRN01'
    ORDER BY cs.ClassSectionId;

    SELECT TOP 1
        @CS2Q = cs.ClassSectionId
    FROM dbo.ClassSections cs
    WHERE cs.SectionCode = N'PRN02'
    ORDER BY cs.ClassSectionId;

    IF @CS1Q IS NULL OR @CS2Q IS NULL
    BEGIN
        RAISERROR(N'Không tìm thấy ClassSection PRN01 hoặc PRN02. Hãy seed ClassSections trước.', 16, 1);
    END

    IF @TeacherQ IS NULL
    BEGIN
        SELECT TOP 1 @TeacherQ = t.TeacherId FROM dbo.Teachers t ORDER BY t.TeacherId;
    END

    IF @TeacherQ IS NULL
    BEGIN
        RAISERROR(N'Không tìm thấy Teacher để seed chat.', 16, 1);
    END

    /* =========================
       CHATROOM seed PRN01/PRN02
       ========================= */
    DECLARE @RoomPRN01 INT, @RoomPRN02 INT;

    -- cleanup old rooms/messages/members for these 2 classes (rerun-safe)
    DELETE cm
    FROM dbo.ChatMessages cm
    JOIN dbo.ChatRooms cr ON cr.RoomId = cm.RoomId
    WHERE cr.RoomType = N'CLASS'
      AND cr.ClassSectionId IN (@CS1Q, @CS2Q);

    DELETE crm
    FROM dbo.ChatRoomMembers crm
    JOIN dbo.ChatRooms cr ON cr.RoomId = crm.RoomId
    WHERE cr.RoomType = N'CLASS'
      AND cr.ClassSectionId IN (@CS1Q, @CS2Q);

    DELETE FROM dbo.ChatRooms
    WHERE RoomType = N'CLASS'
      AND ClassSectionId IN (@CS1Q, @CS2Q);

    -- create rooms
    INSERT INTO dbo.ChatRooms (RoomType, CourseId, ClassSectionId, RoomName, Status, CreatedBy)
    SELECT N'CLASS', cs.CourseId, cs.ClassSectionId, N'Class Room - ' + cs.SectionCode, N'ACTIVE', @TeacherQ
    FROM dbo.ClassSections cs
    WHERE cs.ClassSectionId IN (@CS1Q, @CS2Q);

    SELECT @RoomPRN01 = RoomId FROM dbo.ChatRooms WHERE RoomType = N'CLASS' AND ClassSectionId = @CS1Q;
    SELECT @RoomPRN02 = RoomId FROM dbo.ChatRooms WHERE RoomType = N'CLASS' AND ClassSectionId = @CS2Q;

    -- teacher as owner
    INSERT INTO dbo.ChatRoomMembers (RoomId, UserId, RoleInRoom, MemberStatus)
    VALUES
    (@RoomPRN01, @TeacherQ, N'OWNER', N'JOINED'),
    (@RoomPRN02, @TeacherQ, N'OWNER', N'JOINED');

    -- enrolled students as members
    INSERT INTO dbo.ChatRoomMembers (RoomId, UserId, RoleInRoom, MemberStatus)
    SELECT DISTINCT @RoomPRN01, e.StudentId, N'MEMBER', N'JOINED'
    FROM dbo.Enrollments e
    WHERE e.ClassSectionId = @CS1Q AND e.Status = N'ENROLLED';

    INSERT INTO dbo.ChatRoomMembers (RoomId, UserId, RoleInRoom, MemberStatus)
    SELECT DISTINCT @RoomPRN02, e.StudentId, N'MEMBER', N'JOINED'
    FROM dbo.Enrollments e
    WHERE e.ClassSectionId = @CS2Q AND e.Status = N'ENROLLED';

    -- system + welcome messages
    INSERT INTO dbo.ChatMessages (RoomId, SenderId, MessageType, Content, CreatedAt)
    VALUES
    (@RoomPRN01, @TeacherQ, N'SYSTEM', N'Phòng chat đã được tạo cho lớp PRN01', SYSUTCDATETIME()),
    (@RoomPRN01, @TeacherQ, N'TEXT',   N'Chào mừng các bạn đến với lớp PRN01.', SYSUTCDATETIME()),
    (@RoomPRN02, @TeacherQ, N'SYSTEM', N'Phòng chat đ�� được tạo cho lớp PRN02', SYSUTCDATETIME()),
    (@RoomPRN02, @TeacherQ, N'TEXT',   N'Chào mừng các bạn đến với lớp PRN02.', SYSUTCDATETIME());

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* =====================================================
   SEED: SemesterTuitionPolicies
   ===================================================== */
INSERT INTO dbo.SemesterTuitionPolicies
(
    SemesterId,
    AmountPerCredit,
    DefaultSurcharge,
    CurrencyCode,
    IsActive
)
SELECT
    s.SemesterId,
    CAST(500000 AS DECIMAL(18,2)) AS AmountPerCredit,
    CAST(CASE WHEN ROW_NUMBER() OVER (ORDER BY s.SemesterId) % 2 = 0 THEN 300000 ELSE 200000 END AS DECIMAL(18,2)) AS DefaultSurcharge,
    N'VND',
    1
FROM dbo.Semesters s
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.SemesterTuitionPolicies p
    WHERE p.SemesterId = s.SemesterId
      AND p.IsActive = 1
);
GO


/* =====================================================
   SEED: RegistrationOrders
   ===================================================== */
DECLARE @SeedSemesterId INT =
(
    SELECT TOP 1 cs.SemesterId
    FROM dbo.ClassSections cs
    ORDER BY cs.SemesterId DESC, cs.ClassSectionId DESC
);

;WITH CandidateOrders AS
(
    SELECT TOP 12
        st.StudentId,
        ROW_NUMBER() OVER (ORDER BY st.StudentId) AS rn
    FROM dbo.Students st
    JOIN dbo.StudentWallets sw ON sw.StudentId = st.StudentId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RegistrationOrders ro
        WHERE ro.StudentId = st.StudentId
          AND ro.SemesterId = @SeedSemesterId
    )
    ORDER BY st.StudentId
)
INSERT INTO dbo.RegistrationOrders
(
    StudentId,
    SemesterId,
    CartId,
    OrderCode,
    PricingPolicyId,
    OrderStatus,
    SubTotalAmount,
    SurchargeAmount,
    DiscountAmount,
    TotalAmount,
    PaidAmount,
    FailureReason,
    PaidAt,
    CancelledAt,
    CreatedAt
)
SELECT
    co.StudentId,
    @SeedSemesterId,
    NULL,
    N'ORD-FP-' + RIGHT('000000' + CAST(co.rn AS VARCHAR(6)), 6),
    p.PolicyId,
    CASE
        WHEN co.rn IN (1,2,3,4,5,6) THEN N'PAID'
        WHEN co.rn IN (7,8) THEN N'FAILED'
        WHEN co.rn IN (9,10) THEN N'CANCELLED'
        ELSE N'PARTIALLY_REFUNDED'
    END,
    0, 0, 0, 0, 0,
    CASE WHEN co.rn IN (7,8) THEN N'Checkout failed - insufficient wallet balance (seed sample)' ELSE NULL END,
    CASE WHEN co.rn IN (1,2,3,4,5,6,11,12) THEN SYSUTCDATETIME() ELSE NULL END,
    CASE WHEN co.rn IN (9,10) THEN SYSUTCDATETIME() ELSE NULL END,
    SYSUTCDATETIME()
FROM CandidateOrders co
LEFT JOIN dbo.SemesterTuitionPolicies p
    ON p.SemesterId = @SeedSemesterId
   AND p.IsActive = 1;
GO


/* =====================================================
   SEED: RegistrationOrderItems
   ===================================================== */
;WITH OrderBase AS
(
    SELECT
        ro.OrderId,
        ro.StudentId,
        ro.SemesterId,
        ro.OrderStatus,
        ROW_NUMBER() OVER (ORDER BY ro.OrderId) AS order_rn
    FROM dbo.RegistrationOrders ro
    WHERE ro.OrderCode LIKE N'ORD-FP-%'
),
CandidateItems AS
(
    SELECT
        ob.OrderId,
        ob.order_rn,
        cs.ClassSectionId,
        cs.CourseId,
        c.Credits,
        p.AmountPerCredit,
        ROW_NUMBER() OVER
        (
            PARTITION BY ob.OrderId
            ORDER BY ABS(CHECKSUM(CONCAT(ob.OrderId, N'-', cs.ClassSectionId))), cs.ClassSectionId
        ) AS item_rn
    FROM OrderBase ob
    JOIN dbo.ClassSections cs
        ON cs.SemesterId = ob.SemesterId
       AND cs.IsOpen = 1
    JOIN dbo.Courses c
        ON c.CourseId = cs.CourseId
    JOIN dbo.SemesterTuitionPolicies p
        ON p.SemesterId = ob.SemesterId
       AND p.IsActive = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RegistrationOrderItems oi
        WHERE oi.OrderId = ob.OrderId
          AND oi.ClassSectionId = cs.ClassSectionId
    )
)
INSERT INTO dbo.RegistrationOrderItems
(
    OrderId,
    ClassSectionId,
    CourseId,
    EnrollmentId,
    CreditsSnapshot,
    UnitPricePerCredit,
    LineSubTotalAmount,
    LineSurchargeAmount,
    LineTotalAmount,
    RefundAmount,
    ItemStatus,
    CreatedAt
)
SELECT
    ci.OrderId,
    ci.ClassSectionId,
    ci.CourseId,
    NULL,
    ci.Credits,
    ci.AmountPerCredit,
    CAST(ci.Credits * ci.AmountPerCredit AS DECIMAL(18,2)),
    CAST(0 AS DECIMAL(18,2)),
    CAST(ci.Credits * ci.AmountPerCredit AS DECIMAL(18,2)),
    CAST(
        CASE
            WHEN ci.order_rn IN (11,12) AND ci.item_rn = 1
                THEN ci.Credits * ci.AmountPerCredit
            ELSE 0
        END
        AS DECIMAL(18,2)
    ) AS RefundAmount,
    CASE
        WHEN ci.order_rn IN (1,2,3) AND ci.item_rn = 1 THEN N'ENROLLED'
        WHEN ci.order_rn IN (1,2,3,4,5,6) THEN N'PENDING_APPROVAL'
        WHEN ci.order_rn IN (7,8,9,10) THEN N'CANCELLED'
        WHEN ci.order_rn IN (11,12) AND ci.item_rn = 1 THEN N'REFUNDED'
        ELSE N'ENROLLED'
    END,
    SYSUTCDATETIME()
FROM CandidateItems ci
WHERE ci.item_rn <= 2;
GO


/* =====================================================
   UPDATE totals for seeded orders
   ===================================================== */
;WITH OrderTotals AS
(
    SELECT
        ro.OrderId,
        SUM(oi.LineTotalAmount) AS CalcSubTotal,
        MAX(ISNULL(p.DefaultSurcharge, 0)) AS CalcSurcharge
    FROM dbo.RegistrationOrders ro
    JOIN dbo.RegistrationOrderItems oi
        ON oi.OrderId = ro.OrderId
    LEFT JOIN dbo.SemesterTuitionPolicies p
        ON p.PolicyId = ro.PricingPolicyId
    WHERE ro.OrderCode LIKE N'ORD-FP-%'
    GROUP BY ro.OrderId
)
UPDATE ro
SET
    SubTotalAmount  = ot.CalcSubTotal,
    SurchargeAmount = ot.CalcSurcharge,
    DiscountAmount  = 0,
    TotalAmount     = ot.CalcSubTotal + ot.CalcSurcharge,
    PaidAmount      = CASE
                        WHEN ro.OrderStatus IN (N'PAID', N'PARTIALLY_REFUNDED', N'REFUNDED')
                            THEN ot.CalcSubTotal + ot.CalcSurcharge
                        ELSE 0
                      END,
    UpdatedAt       = SYSUTCDATETIME()
FROM dbo.RegistrationOrders ro
JOIN OrderTotals ot
    ON ot.OrderId = ro.OrderId;
GO

/* =====================================================
   SEED: WalletTransactions for seeded orders
   ===================================================== */

/* 5.1 Checkout payment */
INSERT INTO dbo.WalletTransactions
(
    WalletId,
    Amount,
    TransactionType,
    RelatedPaymentId,
    RelatedOrderId,
    BalanceBefore,
    BalanceAfter,
    Description,
    CreatedAt
)
SELECT
    sw.WalletId,
    -ro.TotalAmount,
    N'CHECKOUT_PAYMENT',
    NULL,
    ro.OrderId,
    NULL,
    NULL,
    N'Thanh toán đơn đăng ký ' + ro.OrderCode,
    SYSUTCDATETIME()
FROM dbo.RegistrationOrders ro
JOIN dbo.StudentWallets sw
    ON sw.StudentId = ro.StudentId
WHERE ro.OrderCode LIKE N'ORD-FP-%'
  AND ro.OrderStatus IN (N'PAID', N'PARTIALLY_REFUNDED', N'REFUNDED')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.WalletTransactions wt
      WHERE wt.RelatedOrderId = ro.OrderId
        AND wt.TransactionType = N'CHECKOUT_PAYMENT'
  );
GO

/* 5.2 Refund */
;WITH RefundSummary AS
(
    SELECT
        oi.OrderId,
        SUM(oi.RefundAmount) AS RefundAmount
    FROM dbo.RegistrationOrderItems oi
    GROUP BY oi.OrderId
    HAVING SUM(oi.RefundAmount) > 0
)
INSERT INTO dbo.WalletTransactions
(
    WalletId,
    Amount,
    TransactionType,
    RelatedPaymentId,
    RelatedOrderId,
    BalanceBefore,
    BalanceAfter,
    Description,
    CreatedAt
)
SELECT
    sw.WalletId,
    rs.RefundAmount,
    N'REFUND',
    NULL,
    ro.OrderId,
    NULL,
    NULL,
    N'Hoàn tiền cho đơn đăng ký ' + ro.OrderCode,
    SYSUTCDATETIME()
FROM RefundSummary rs
JOIN dbo.RegistrationOrders ro
    ON ro.OrderId = rs.OrderId
JOIN dbo.StudentWallets sw
    ON sw.StudentId = ro.StudentId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.WalletTransactions wt
    WHERE wt.RelatedOrderId = ro.OrderId
      AND wt.TransactionType = N'REFUND'
);
GO

/* =====================================================
   PREP: ensure some GradeBooks are PUBLISHED for appeal seed
   ===================================================== */
;WITH PublishTargets AS
(
    SELECT TOP (12)
        gb.GradeBookId
    FROM dbo.GradeBooks gb
    JOIN dbo.ClassSections cs
        ON cs.ClassSectionId = gb.ClassSectionId
    JOIN dbo.Enrollments e
        ON e.ClassSectionId = cs.ClassSectionId
       AND e.Status IN (N'ENROLLED', N'COMPLETED', N'WITHDRAWN')
    ORDER BY gb.GradeBookId
)
UPDATE gb
SET
    Status      = N'PUBLISHED',
    PublishedAt = COALESCE(gb.PublishedAt, DATEADD(DAY, -5, SYSUTCDATETIME())),
    UpdatedAt   = SYSUTCDATETIME()
FROM dbo.GradeBooks gb
JOIN PublishTargets pt
    ON pt.GradeBookId = gb.GradeBookId
WHERE gb.Status <> N'LOCKED';
GO


/* =====================================================
   SEED: GradeAppeals
   ===================================================== */
DECLARE @AppealAdminId INT =
(
    SELECT TOP 1 u.UserId
    FROM dbo.Users u
    WHERE u.Role = N'ADMIN'
    ORDER BY u.UserId
);

;WITH CandidateAppeals AS
(
    SELECT TOP 8
        gb.GradeBookId,
        e.EnrollmentId,
        e.StudentId,
        gi.GradeItemId,
        ROW_NUMBER() OVER (ORDER BY gb.GradeBookId, e.EnrollmentId) AS rn
    FROM dbo.GradeBooks gb
    JOIN dbo.ClassSections cs
        ON cs.ClassSectionId = gb.ClassSectionId
    JOIN dbo.Enrollments e
        ON e.ClassSectionId = cs.ClassSectionId
       AND e.Status IN (N'ENROLLED', N'COMPLETED', N'WITHDRAWN')
    OUTER APPLY
    (
        SELECT TOP 1 gi.GradeItemId
        FROM dbo.GradeItems gi
        WHERE gi.GradeBookId = gb.GradeBookId
        ORDER BY gi.SortOrder, gi.GradeItemId
    ) gi
    WHERE gb.Status = N'PUBLISHED'
      AND gi.GradeItemId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.GradeAppeals ga
          WHERE ga.GradeBookId = gb.GradeBookId
            AND ga.StudentId = e.StudentId
      )
    ORDER BY gb.GradeBookId, e.EnrollmentId
)
INSERT INTO dbo.GradeAppeals
(
    GradeBookId,
    EnrollmentId,
    StudentId,
    GradeItemId,
    AppealContent,
    EvidenceNote,
    Status,
    ResponseMessage,
    ResolvedBy,
    SubmittedAt,
    ResolvedAt
)
SELECT
    ca.GradeBookId,
    ca.EnrollmentId,
    ca.StudentId,
    ca.GradeItemId,
    CASE
        WHEN ca.rn % 4 = 1 THEN N'Em đề nghị kiểm tra lại điểm vì em tin rằng bài của em bị thiếu điểm ở phần trình bày.'
        WHEN ca.rn % 4 = 2 THEN N'Em muốn khiếu nại điểm vì em đã nộp đủ bài nhưng điểm hiện tại chưa phản ánh đúng kết quả.'
        WHEN ca.rn % 4 = 3 THEN N'Em đề nghị rà soát lại thành phần điểm giữa kỳ vì có thể đã nhập nhầm dữ liệu.'
        ELSE N'Em xin kiểm tra lại điểm thành phần do em có minh chứng đã hoàn thành bài tập.'
    END,
    N'Minh chứng seed sample',
    CASE
        WHEN ca.rn % 4 = 1 THEN N'SUBMITTED'
        WHEN ca.rn % 4 = 2 THEN N'UNDER_REVIEW'
        WHEN ca.rn % 4 = 3 THEN N'APPROVED'
        ELSE N'REJECTED'
    END,
    CASE
        WHEN ca.rn % 4 = 3 THEN N'Đã đối chiếu bài làm và chấp nhận điều chỉnh điểm.'
        WHEN ca.rn % 4 = 0 THEN N'Đã rà soát nhưng không đủ căn cứ để điều chỉnh điểm.'
        ELSE NULL
    END,
    CASE
        WHEN ca.rn % 4 IN (0,3) THEN @AppealAdminId
        ELSE NULL
    END,
    DATEADD(DAY, -ca.rn, SYSUTCDATETIME()),
    CASE
        WHEN ca.rn % 4 IN (0,3) THEN DATEADD(DAY, -(ca.rn - 1), SYSUTCDATETIME())
        ELSE NULL
    END
FROM CandidateAppeals ca;
GO
/* =============================================================
   SEED: Programs
   ============================================================= */
IF NOT EXISTS (SELECT 1 FROM dbo.Programs WHERE ProgramCode = N'SE')
BEGIN
    INSERT INTO dbo.Programs (ProgramCode, ProgramName, IsActive)
    VALUES (N'SE', N'Software Engineering', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Programs WHERE ProgramCode = N'AI')
BEGIN
    INSERT INTO dbo.Programs (ProgramCode, ProgramName, IsActive)
    VALUES (N'AI', N'Artificial Intelligence', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Programs WHERE ProgramCode = N'BA')
BEGIN
    INSERT INTO dbo.Programs (ProgramCode, ProgramName, IsActive)
    VALUES (N'BA', N'Business Administration', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Programs WHERE ProgramCode = N'GD')
BEGIN
    INSERT INTO dbo.Programs (ProgramCode, ProgramName, IsActive)
    VALUES (N'GD', N'Graphic Design', 1);
END
GO


/* =============================================================
   SEED: CoursePrerequisites
   NOTE:
   - Requires dbo.Courses already seeded.
   - Uses CourseCode mapping -> CourseId to avoid hard-coded IDs.
   - Only inserts when both course codes exist and relation not exists.
   ============================================================= */

-- CS201 requires CS101
INSERT INTO dbo.CoursePrerequisites (CourseId, PrerequisiteCourseId)
SELECT cMain.CourseId, cPre.CourseId
FROM dbo.Courses cMain
JOIN dbo.Courses cPre ON 1 = 1
WHERE cMain.CourseCode = N'CS201'
  AND cPre.CourseCode  = N'CS101'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CoursePrerequisites cp
      WHERE cp.CourseId = cMain.CourseId
        AND cp.PrerequisiteCourseId = cPre.CourseId
  );
GO

-- CS301 requires CS201
INSERT INTO dbo.CoursePrerequisites (CourseId, PrerequisiteCourseId)
SELECT cMain.CourseId, cPre.CourseId
FROM dbo.Courses cMain
JOIN dbo.Courses cPre ON 1 = 1
WHERE cMain.CourseCode = N'CS301'
  AND cPre.CourseCode  = N'CS201'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CoursePrerequisites cp
      WHERE cp.CourseId = cMain.CourseId
        AND cp.PrerequisiteCourseId = cPre.CourseId
  );
GO

-- AI201 requires MATH101
INSERT INTO dbo.CoursePrerequisites (CourseId, PrerequisiteCourseId)
SELECT cMain.CourseId, cPre.CourseId
FROM dbo.Courses cMain
JOIN dbo.Courses cPre ON 1 = 1
WHERE cMain.CourseCode = N'AI201'
  AND cPre.CourseCode  = N'MATH101'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CoursePrerequisites cp
      WHERE cp.CourseId = cMain.CourseId
        AND cp.PrerequisiteCourseId = cPre.CourseId
  );
GO

-- AI301 requires AI201
INSERT INTO dbo.CoursePrerequisites (CourseId, PrerequisiteCourseId)
SELECT cMain.CourseId, cPre.CourseId
FROM dbo.Courses cMain
JOIN dbo.Courses cPre ON 1 = 1
WHERE cMain.CourseCode = N'AI301'
  AND cPre.CourseCode  = N'AI201'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CoursePrerequisites cp
      WHERE cp.CourseId = cMain.CourseId
        AND cp.PrerequisiteCourseId = cPre.CourseId
  );
GO
