IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE TABLE [DataSources] (
        [DataSourceId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [Connection] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_DataSources] PRIMARY KEY ([DataSourceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE TABLE [Folders] (
        [FolderId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [ParentFolderId] nvarchar(128) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Path] nvarchar(400) NOT NULL,
        CONSTRAINT [PK_Folders] PRIMARY KEY ([FolderId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE TABLE [Reports] (
        [ReportId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [FolderId] nvarchar(128) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [LatestRevisionId] nvarchar(128) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([ReportId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE TABLE [Revisions] (
        [RevisionId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [ReportId] nvarchar(128) NOT NULL,
        [RevisionNumber] int NOT NULL,
        [DefinitionJson] nvarchar(max) NOT NULL,
        [CreatedByUserId] nvarchar(128) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Comment] nvarchar(max) NULL,
        [IsPublished] bit NOT NULL,
        CONSTRAINT [PK_Revisions] PRIMARY KEY ([RevisionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DataSources_TenantId_Name] ON [DataSources] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Folders_TenantId_Path] ON [Folders] ([TenantId], [Path]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_Reports_TenantId_FolderId_Name] ON [Reports] ([TenantId], [FolderId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Revisions_TenantId_ReportId_RevisionNumber] ON [Revisions] ([TenantId], [ReportId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717064405_InitialCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717064405_InitialCatalog', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    CREATE TABLE [ApiKeys] (
        [KeyId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [ApplicationId] nvarchar(256) NOT NULL,
        [KeyHash] nvarchar(88) NOT NULL,
        [Permissions] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [RevokedAt] datetimeoffset NULL,
        [RevokedByUserId] nvarchar(256) NULL,
        CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([KeyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] nvarchar(128) NOT NULL,
        [ActorId] nvarchar(256) NOT NULL,
        [Action] int NOT NULL,
        [ResourceKind] int NOT NULL,
        [ResourceId] nvarchar(200) NOT NULL,
        [Outcome] int NOT NULL,
        [Timestamp] datetimeoffset NOT NULL,
        [DetailsJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ApiKeys_KeyHash] ON [ApiKeys] ([KeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_TenantId_ApplicationId] ON [ApiKeys] ([TenantId], [ApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_TenantId_Timestamp] ON [AuditEvents] ([TenantId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717071801_ApiKeysAndAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717071801_ApiKeysAndAudit', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    CREATE TABLE [FolderPermissions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] nvarchar(128) NOT NULL,
        [FolderId] nvarchar(128) NOT NULL,
        [Path] nvarchar(400) NOT NULL,
        [SubjectId] nvarchar(256) NOT NULL,
        [Role] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_FolderPermissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    CREATE TABLE [Users] (
        [Subject] nvarchar(256) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [Email] nvarchar(200) NULL,
        [DisplayName] nvarchar(200) NULL,
        [FirstSeenAt] datetimeoffset NOT NULL,
        [LastSeenAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Subject])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    CREATE INDEX [IX_FolderPermissions_TenantId_FolderId] ON [FolderPermissions] ([TenantId], [FolderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FolderPermissions_TenantId_SubjectId_FolderId] ON [FolderPermissions] ([TenantId], [SubjectId], [FolderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717082119_UsersAndFolderPermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717082119_UsersAndFolderPermissions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    CREATE TABLE [ScheduleRuns] (
        [RunId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [ScheduleId] nvarchar(128) NOT NULL,
        [OccurrenceUtc] datetimeoffset NOT NULL,
        [StartedUtc] datetimeoffset NOT NULL,
        [CompletedUtc] datetimeoffset NULL,
        [Status] nvarchar(32) NOT NULL,
        [Attempt] int NOT NULL,
        [DeliveryKind] nvarchar(16) NOT NULL,
        [DeliveryTarget] nvarchar(1024) NOT NULL,
        [ArtifactFileName] nvarchar(200) NULL,
        [ArtifactContentType] nvarchar(128) NULL,
        [ArtifactByteCount] int NOT NULL,
        [ErrorMessage] nvarchar(1024) NULL,
        CONSTRAINT [PK_ScheduleRuns] PRIMARY KEY ([RunId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    CREATE TABLE [Schedules] (
        [ScheduleId] nvarchar(128) NOT NULL,
        [TenantId] nvarchar(128) NOT NULL,
        [OwnerUserId] nvarchar(256) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ReportId] nvarchar(128) NOT NULL,
        [CronExpression] nvarchar(120) NOT NULL,
        [Format] nvarchar(16) NOT NULL,
        [CultureName] nvarchar(32) NOT NULL,
        [ParametersJson] nvarchar(max) NOT NULL,
        [DeliveryKind] nvarchar(16) NOT NULL,
        [DeliveryTarget] nvarchar(1024) NOT NULL,
        [MissedRunPolicy] nvarchar(16) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [NextRunUtc] datetimeoffset NOT NULL,
        [LastRunUtc] datetimeoffset NULL,
        [LastDeliveredUtc] datetimeoffset NULL,
        [RetryAfterUtc] datetimeoffset NULL,
        [FailureCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [LastStatus] nvarchar(32) NOT NULL,
        [LastStatusMessage] nvarchar(400) NOT NULL,
        [PendingOccurrencesJson] nvarchar(4000) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Schedules] PRIMARY KEY ([ScheduleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    CREATE INDEX [IX_ScheduleRuns_TenantId_ScheduleId_OccurrenceUtc] ON [ScheduleRuns] ([TenantId], [ScheduleId], [OccurrenceUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    CREATE INDEX [IX_Schedules_IsEnabled_NextRunUtc] ON [Schedules] ([IsEnabled], [NextRunUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Schedules_TenantId_ScheduleId] ON [Schedules] ([TenantId], [ScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717120652_Scheduling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717120652_Scheduling', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    DROP INDEX [IX_FolderPermissions_TenantId_SubjectId_FolderId] ON [FolderPermissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    ALTER TABLE [FolderPermissions] ADD [Effect] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    ALTER TABLE [FolderPermissions] ADD [Permissions] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    ALTER TABLE [FolderPermissions] ADD [SubjectKind] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FolderPermissions_TenantId_FolderId_SubjectKind_SubjectId_Effect] ON [FolderPermissions] ([TenantId], [FolderId], [SubjectKind], [SubjectId], [Effect]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719052344_AclSubjectKindAndEffect'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260719052344_AclSubjectKindAndEffect', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    CREATE TABLE [Favorites] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] nvarchar(128) NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [ReportId] nvarchar(128) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Favorites] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    CREATE TABLE [RenderRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] nvarchar(128) NOT NULL,
        [ActorId] nvarchar(256) NOT NULL,
        [ReportId] nvarchar(128) NOT NULL,
        [ParametersJson] nvarchar(max) NOT NULL,
        [Format] nvarchar(16) NOT NULL,
        [Outcome] nvarchar(32) NOT NULL,
        [PageCount] int NULL,
        [ByteSize] bigint NULL,
        [DurationMs] int NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RenderRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Favorites_TenantId_UserId_ReportId] ON [Favorites] ([TenantId], [UserId], [ReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    CREATE INDEX [IX_RenderRuns_TenantId_ActorId_CreatedAt] ON [RenderRuns] ([TenantId], [ActorId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    CREATE INDEX [IX_RenderRuns_TenantId_ReportId] ON [RenderRuns] ([TenantId], [ReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719063908_ReportFavoritesAndRenderRuns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260719063908_ReportFavoritesAndRenderRuns', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719121542_SchedulingLease'
)
BEGIN
    ALTER TABLE [Schedules] ADD [LeaseOwner] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719121542_SchedulingLease'
)
BEGIN
    ALTER TABLE [Schedules] ADD [LeasedUntil] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260719121542_SchedulingLease'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260719121542_SchedulingLease', N'10.0.9');
END;

COMMIT;
GO

