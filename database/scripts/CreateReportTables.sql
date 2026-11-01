/*
    Report Builder schema
    Run this script once against the database configured as DefaultConnection.
*/

IF OBJECT_ID(N'dbo.ReportDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportDefinitions
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportDefinitions PRIMARY KEY,
        Name              NVARCHAR(200) NOT NULL,
        SourceType        NVARCHAR(20) NOT NULL,
        SqlQuery          NVARCHAR(MAX) NULL,
        ExcelFileName     NVARCHAR(260) NULL,
        ExcelContent      VARBINARY(MAX) NULL,
        NeedsPagination   BIT NOT NULL CONSTRAINT DF_ReportDefinitions_NeedsPagination DEFAULT (0),
        PageSize          INT NULL,
        IsDeleted         BIT NOT NULL CONSTRAINT DF_ReportDefinitions_IsDeleted DEFAULT (0),
        CreatedAtUtc      DATETIME2(0) NOT NULL CONSTRAINT DF_ReportDefinitions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc      DATETIME2(0) NULL,
        DeletedAtUtc      DATETIME2(0) NULL,

        CONSTRAINT CK_ReportDefinitions_SourceType CHECK (SourceType IN (N'Excel', N'Sql')),
        CONSTRAINT CK_ReportDefinitions_PageSize CHECK
            ((NeedsPagination = 0 AND PageSize IS NULL) OR (NeedsPagination = 1 AND PageSize > 0)),
        CONSTRAINT CK_ReportDefinitions_SourcePayload CHECK
            ((SourceType = N'Sql' AND SqlQuery IS NOT NULL AND ExcelContent IS NULL)
             OR (SourceType = N'Excel' AND ExcelContent IS NOT NULL AND SqlQuery IS NULL))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ReportDefinitions_ActiveName' AND object_id = OBJECT_ID(N'dbo.ReportDefinitions'))
BEGIN
    CREATE UNIQUE INDEX UX_ReportDefinitions_ActiveName
        ON dbo.ReportDefinitions(Name)
        WHERE IsDeleted = 0;
END;
GO

IF OBJECT_ID(N'dbo.ReportParameters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportParameters
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportParameters PRIMARY KEY,
        ReportDefinitionId  INT NOT NULL,
        Name                NVARCHAR(100) NOT NULL,
        DisplayName         NVARCHAR(200) NOT NULL,
        Type                NVARCHAR(30) NOT NULL,
        DataSource          NVARCHAR(MAX) NULL,
        DisplayNameField    NVARCHAR(128) NULL,
        ValueField          NVARCHAR(128) NULL,
        Options             NVARCHAR(MAX) NULL,
        SortOrder           INT NOT NULL CONSTRAINT DF_ReportParameters_SortOrder DEFAULT (0),

        CONSTRAINT FK_ReportParameters_ReportDefinitions
            FOREIGN KEY (ReportDefinitionId) REFERENCES dbo.ReportDefinitions(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ReportParameters_Type
            CHECK (Type IN (N'Text', N'Number', N'Date', N'Checkbox', N'RadioButton', N'ListBox'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportParameters_ReportDefinitionId' AND object_id = OBJECT_ID(N'dbo.ReportParameters'))
BEGIN
    CREATE INDEX IX_ReportParameters_ReportDefinitionId
        ON dbo.ReportParameters(ReportDefinitionId, SortOrder);
END;
GO

/* Report classification lookup tables and initial data */
IF OBJECT_ID(N'dbo.SoftwareGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SoftwareGroups
    (
        Id       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SoftwareGroups PRIMARY KEY,
        Name     NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_SoftwareGroups_IsActive DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_SoftwareGroups_Name ON dbo.SoftwareGroups(Name);
END;
GO

IF OBJECT_ID(N'dbo.ReportLevels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportLevels
    (
        Id       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportLevels PRIMARY KEY,
        Name     NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ReportLevels_IsActive DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_ReportLevels_Name ON dbo.ReportLevels(Name);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SoftwareGroups WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.SoftwareGroups ON;
    INSERT INTO dbo.SoftwareGroups (Id, Name, IsActive) VALUES
        (1, N'مالی', 1),
        (2, N'انبار و برنامه‌ریزی', 1),
        (3, N'فروش', 1),
        (4, N'توزیع و پخش', 1);
    SET IDENTITY_INSERT dbo.SoftwareGroups OFF;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ReportLevels WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.ReportLevels ON;
    INSERT INTO dbo.ReportLevels (Id, Name, IsActive) VALUES
        (1, N'هلدینگ مینو', 1),
        (2, N'شرکت قاسم ایران', 1);
    SET IDENTITY_INSERT dbo.ReportLevels OFF;
END;
GO

/* Add metadata to existing report definitions; legacy reports default to Finance / Holding Minoo. */
IF COL_LENGTH(N'dbo.ReportDefinitions', N'SoftwareGroupId') IS NULL
    ALTER TABLE dbo.ReportDefinitions ADD SoftwareGroupId INT NULL;
GO
IF COL_LENGTH(N'dbo.ReportDefinitions', N'ReportLevelId') IS NULL
    ALTER TABLE dbo.ReportDefinitions ADD ReportLevelId INT NULL;
GO
IF COL_LENGTH(N'dbo.ReportDefinitions', N'SpecificServerVahedeTejariSN') IS NULL
    ALTER TABLE dbo.ReportDefinitions ADD SpecificServerVahedeTejariSN DECIMAL(18,3) NULL;
GO

UPDATE dbo.ReportDefinitions
SET SoftwareGroupId = 1
WHERE SoftwareGroupId IS NULL;
UPDATE dbo.ReportDefinitions
SET ReportLevelId = 1
WHERE ReportLevelId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ReportDefinitions') AND name = N'SoftwareGroupId' AND is_nullable = 1)
    ALTER TABLE dbo.ReportDefinitions ALTER COLUMN SoftwareGroupId INT NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ReportDefinitions') AND name = N'ReportLevelId' AND is_nullable = 1)
    ALTER TABLE dbo.ReportDefinitions ALTER COLUMN ReportLevelId INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReportDefinitions_SoftwareGroups')
    ALTER TABLE dbo.ReportDefinitions ADD CONSTRAINT FK_ReportDefinitions_SoftwareGroups
        FOREIGN KEY (SoftwareGroupId) REFERENCES dbo.SoftwareGroups(Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReportDefinitions_ReportLevels')
    ALTER TABLE dbo.ReportDefinitions ADD CONSTRAINT FK_ReportDefinitions_ReportLevels
        FOREIGN KEY (ReportLevelId) REFERENCES dbo.ReportLevels(Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportDefinitions_SoftwareGroupId_ReportLevelId' AND object_id = OBJECT_ID(N'dbo.ReportDefinitions'))
    CREATE INDEX IX_ReportDefinitions_SoftwareGroupId_ReportLevelId
        ON dbo.ReportDefinitions(SoftwareGroupId, ReportLevelId);
GO
