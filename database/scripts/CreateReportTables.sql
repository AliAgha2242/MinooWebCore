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
            CHECK (Type IN (N'Text', N'Number', N'Date', N'PersianDate', N'Checkbox', N'RadioButton', N'ListBox', N'ComboBox'))
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

SET IDENTITY_INSERT dbo.SoftwareGroups ON;
INSERT INTO dbo.SoftwareGroups (Id, Name, IsActive)
SELECT seed.Id, seed.Name, 1
FROM (VALUES
    (1, N'مالی'),
    (2, N'انبار و برنامه‌ریزی'),
    (3, N'فروش و توزیع و پخش')
) AS seed(Id, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SoftwareGroups existing WHERE existing.Id = seed.Id);
SET IDENTITY_INSERT dbo.SoftwareGroups OFF;
GO

SET IDENTITY_INSERT dbo.ReportLevels ON;
INSERT INTO dbo.ReportLevels (Id, Name, IsActive)
SELECT seed.Id, seed.Name, 1
FROM (VALUES
    (1, N'هلدینگ مینو'),
    (2, N'شرکت قاسم ایران')
) AS seed(Id, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ReportLevels existing WHERE existing.Id = seed.Id);
SET IDENTITY_INSERT dbo.ReportLevels OFF;
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


IF OBJECT_ID(N'dbo.ReportParameters', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ReportParameters_Type' AND parent_object_id = OBJECT_ID(N'dbo.ReportParameters'))
        ALTER TABLE dbo.ReportParameters DROP CONSTRAINT CK_ReportParameters_Type;
    ALTER TABLE dbo.ReportParameters ADD CONSTRAINT CK_ReportParameters_Type
        CHECK (Type IN (N'Text', N'Number', N'Date', N'PersianDate', N'Checkbox', N'RadioButton', N'ListBox', N'ComboBox'));
END;
GO

/* Seeded parameter types */
IF OBJECT_ID(N'dbo.ParameterTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParameterTypes
    (
        Id INT NOT NULL CONSTRAINT PK_ParameterTypes PRIMARY KEY,
        Code NVARCHAR(30) NOT NULL,
        Title NVARCHAR(100) NOT NULL,
        RequiresDataSource BIT NOT NULL CONSTRAINT DF_ParameterTypes_RequiresDataSource DEFAULT (0),
        RequiresOptions BIT NOT NULL CONSTRAINT DF_ParameterTypes_RequiresOptions DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_ParameterTypes_IsActive DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_ParameterTypes_Code ON dbo.ParameterTypes(Code);
END;
GO
INSERT INTO dbo.ParameterTypes (Id, Code, Title, RequiresDataSource, RequiresOptions, IsActive)
SELECT seed.Id, seed.Code, seed.Title, seed.RequiresDataSource, seed.RequiresOptions, 1
FROM (VALUES
    (1, N'Text', N'متن', 0, 0),
    (2, N'Number', N'عدد', 0, 0),
    (3, N'Date', N'تاریخ', 0, 0),
    (4, N'PersianDate', N'تاریخ شمسی (۱۴۰۵۰۱۰۱)', 0, 0),
    (5, N'ListBox', N'لیست‌باکس', 1, 0),
    (6, N'RadioButton', N'رادیوباتن', 0, 1),
    (7, N'ComboBox', N'کمبوباکس تک‌مقداره', 1, 0),
    (8, N'Checkbox', N'چک‌باکس', 0, 0)
) AS seed(Id, Code, Title, RequiresDataSource, RequiresOptions)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ParameterTypes existing WHERE existing.Id = seed.Id);
GO
IF COL_LENGTH(N'dbo.ReportParameters', N'ParameterTypeId') IS NULL
    ALTER TABLE dbo.ReportParameters ADD ParameterTypeId INT NULL;
GO
UPDATE p SET ParameterTypeId = t.Id
FROM dbo.ReportParameters p
JOIN dbo.ParameterTypes t ON t.Code = p.Type
WHERE p.ParameterTypeId IS NULL;
GO
UPDATE dbo.ReportParameters SET ParameterTypeId = 1 WHERE ParameterTypeId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ReportParameters') AND name = N'ParameterTypeId' AND is_nullable = 1)
    ALTER TABLE dbo.ReportParameters ALTER COLUMN ParameterTypeId INT NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReportParameters_ParameterTypes')
    ALTER TABLE dbo.ReportParameters ADD CONSTRAINT FK_ReportParameters_ParameterTypes FOREIGN KEY (ParameterTypeId) REFERENCES dbo.ParameterTypes(Id);
GO
