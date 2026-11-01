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
