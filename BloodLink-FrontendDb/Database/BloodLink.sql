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
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Halls] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Governorate] nvarchar(60) NOT NULL,
        [EntityType] nvarchar(60) NULL,
        [Location] nvarchar(200) NULL,
        [Capacity] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [Rating] decimal(10,2) NOT NULL,
        [Tag] nvarchar(60) NULL,
        [ImageUrl] nvarchar(500) NULL,
        [Description] nvarchar(1000) NULL,
        [SupportedEvents] nvarchar(300) NULL,
        [Features] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Halls] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [City] nvarchar(60) NOT NULL,
        [Address] nvarchar(200) NULL,
        [LicenseNumber] nvarchar(50) NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(30) NULL,
        [ContactPerson] nvarchar(100) NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [SlotCapacity] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [RegisteredAt] datetime2 NOT NULL,
        [DecidedAt] datetime2 NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Advertisements] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Department] nvarchar(60) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [Badge] nvarchar(30) NULL,
        [Description] nvarchar(1000) NULL,
        [EventDate] date NOT NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [Location] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [BookingRequired] bit NOT NULL,
        [Plan] nvarchar(20) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Fee] decimal(10,2) NOT NULL,
        [Vat] decimal(10,2) NOT NULL,
        [TotalPaid] decimal(10,2) NOT NULL,
        [Views] int NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Advertisements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Advertisements_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [BloodInventory] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [BloodType] nvarchar(3) NOT NULL,
        [Units] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BloodInventory] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BloodInventory_Units] CHECK ([Units] >= 0),
        CONSTRAINT [FK_BloodInventory_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [OrganizationDocuments] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [FilePath] nvarchar(260) NOT NULL,
        [FileSizeBytes] bigint NULL,
        [IsChecked] bit NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrganizationDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationDocuments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(80) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Phone] nvarchar(30) NULL,
        [Role] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [BloodType] nvarchar(3) NULL,
        [City] nvarchar(60) NULL,
        [Area] nvarchar(120) NULL,
        [Latitude] float NULL,
        [Longitude] float NULL,
        [ShareLocation] bit NOT NULL,
        [IsAvailable] bit NOT NULL,
        [PreferredContactMethod] nvarchar(20) NULL,
        [LastDonationDate] date NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastActiveAt] datetime2 NULL,
        [OrganizationId] int NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [AdminActivityLogs] (
        [Id] int NOT NULL IDENTITY,
        [AdminId] int NULL,
        [Text] nvarchar(300) NOT NULL,
        [Icon] nvarchar(20) NULL,
        [Color] nvarchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminActivityLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminActivityLogs_Users_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [BloodRequests] (
        [Id] int NOT NULL IDENTITY,
        [RecipientId] int NOT NULL,
        [DonorId] int NOT NULL,
        [HospitalId] int NULL,
        [BloodType] nvarchar(3) NOT NULL,
        [Units] int NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Location] nvarchar(200) NULL,
        [Message] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RespondedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_BloodRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BloodRequests_Units] CHECK ([Units] > 0),
        CONSTRAINT [FK_BloodRequests_Organizations_HospitalId] FOREIGN KEY ([HospitalId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BloodRequests_Users_DonorId] FOREIGN KEY ([DonorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BloodRequests_Users_RecipientId] FOREIGN KEY ([RecipientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Certificates] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [DonationCount] int NOT NULL,
        [SentToEmail] nvarchar(256) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Certificates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [DonationAppointments] (
        [Id] int NOT NULL IDENTITY,
        [DonorId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [DonorName] nvarchar(80) NOT NULL,
        [DonorPhone] nvarchar(30) NOT NULL,
        [DonorEmail] nvarchar(256) NULL,
        [BloodType] nvarchar(3) NOT NULL,
        [RequestedDate] date NOT NULL,
        [RequestedTime] time NOT NULL,
        [Notes] nvarchar(500) NULL,
        [Age] int NOT NULL,
        [WeightKg] decimal(5,1) NOT NULL,
        [LastDonationDate] date NULL,
        [FeelingWell] bit NOT NULL,
        [TakesMedication] bit NOT NULL,
        [MedicationDetail] nvarchar(200) NULL,
        [TravelledAbroad] bit NOT NULL,
        [TravelDetail] nvarchar(200) NULL,
        [RecentProcedure] bit NOT NULL,
        [EligibilityState] nvarchar(20) NOT NULL,
        [EligibilityFlags] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ConfirmedDate] date NULL,
        [ConfirmedTime] time NULL,
        [HospitalNote] nvarchar(500) NULL,
        [DeclineReason] nvarchar(500) NULL,
        [DonorNotified] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DonationAppointments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DonationAppointments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DonationAppointments_Users_DonorId] FOREIGN KEY ([DonorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [HallBookings] (
        [Id] int NOT NULL IDENTITY,
        [HallId] int NOT NULL,
        [RequestedById] int NULL,
        [OrganizationName] nvarchar(150) NOT NULL,
        [ContactName] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [EventType] nvarchar(60) NOT NULL,
        [EventDate] date NOT NULL,
        [EventTime] nvarchar(40) NULL,
        [Attendees] int NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Price] decimal(10,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AdminNote] nvarchar(500) NULL,
        [PaymentMethod] nvarchar(40) NULL,
        [PaidAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_HallBookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HallBookings_Halls_HallId] FOREIGN KEY ([HallId]) REFERENCES [Halls] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HallBookings_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [Reports] (
        [Id] int NOT NULL IDENTITY,
        [ReporterId] int NULL,
        [FromName] nvarchar(100) NOT NULL,
        [ReporterRole] nvarchar(40) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [Related] nvarchar(150) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [IsRead] bit NOT NULL,
        [Response] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reports_Users_ReporterId] FOREIGN KEY ([ReporterId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] int NOT NULL IDENTITY,
        [BloodRequestId] int NOT NULL,
        [SenderId] int NULL,
        [SenderType] nvarchar(20) NOT NULL,
        [Text] nvarchar(500) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_BloodRequests_BloodRequestId] FOREIGN KEY ([BloodRequestId]) REFERENCES [BloodRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChatMessages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [ChatReadStates] (
        [Id] int NOT NULL IDENTITY,
        [BloodRequestId] int NOT NULL,
        [UserId] int NOT NULL,
        [LastReadAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatReadStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatReadStates_BloodRequests_BloodRequestId] FOREIGN KEY ([BloodRequestId]) REFERENCES [BloodRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChatReadStates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [DonationRecords] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [DonorId] int NULL,
        [DonorName] nvarchar(80) NOT NULL,
        [RecipientName] nvarchar(80) NOT NULL,
        [BloodType] nvarchar(3) NOT NULL,
        [Units] int NOT NULL,
        [DonationDate] date NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Notes] nvarchar(300) NULL,
        [AppointmentId] int NULL,
        [BloodRequestId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DonationRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DonationRecords_Units] CHECK ([Units] > 0),
        CONSTRAINT [FK_DonationRecords_BloodRequests_BloodRequestId] FOREIGN KEY ([BloodRequestId]) REFERENCES [BloodRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DonationRecords_DonationAppointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [DonationAppointments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DonationRecords_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DonationRecords_Users_DonorId] FOREIGN KEY ([DonorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [HallBookingDocuments] (
        [Id] int NOT NULL IDENTITY,
        [HallBookingId] int NOT NULL,
        [Label] nvarchar(120) NOT NULL,
        [FilePath] nvarchar(260) NOT NULL,
        CONSTRAINT [PK_HallBookingDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HallBookingDocuments_HallBookings_HallBookingId] FOREIGN KEY ([HallBookingId]) REFERENCES [HallBookings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE TABLE [ReportAttachments] (
        [Id] int NOT NULL IDENTITY,
        [ReportId] int NOT NULL,
        [FilePath] nvarchar(260) NOT NULL,
        CONSTRAINT [PK_ReportAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportAttachments_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Capacity', N'Description', N'EntityType', N'Features', N'Governorate', N'ImageUrl', N'IsActive', N'Location', N'Name', N'Price', N'Rating', N'SupportedEvents', N'Tag') AND [object_id] = OBJECT_ID(N'[Halls]'))
        SET IDENTITY_INSERT [Halls] ON;
    EXEC(N'INSERT INTO [Halls] ([Id], [Capacity], [Description], [EntityType], [Features], [Governorate], [ImageUrl], [IsActive], [Location], [Name], [Price], [Rating], [SupportedEvents], [Tag])
    VALUES (1, 250, N''A modern venue equipped for conferences, seminars, and academic events.'', N''University'', N''Presentation screen,Sound system,Parking'', N''Amman'', N''https://images.unsplash.com/photo-1517457373958-b7bdd4587205?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''Jubeiha, Amman'', N''University of Jordan Conference Hall'', 180.0, 4.9, N''Conference,Workshop,Training'', N''Popular choice''),
    (2, 120, N''A flexible space for community activities, training, and workshops.'', N''Community center'', N''Wi-Fi,Equipment,Parking'', N''Amman'', N''https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''Khalda, Amman'', N''Zaha Cultural Center'', 95.0, 4.8, N''Initiative,Workshop,Training'', N''Great for initiatives''),
    (3, 400, N''A spacious hall with lighting and facilities for large events.'', N''Private institution'', N''Stage,Professional audio,Parking'', N''Aqaba'', N''https://images.unsplash.com/photo-1505373877841-8d25f7d46678?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''Aqaba city center'', N''Aqaba Grand Hall'', 260.0, 4.7, N''Conference,Initiative'', N''Large capacity''),
    (4, 90, N''A practical venue for youth initiatives and small community gatherings.'', N''Government institution'', N''Wi-Fi,Whiteboard,Parking'', N''Irbid'', N''https://images.unsplash.com/photo-1517502884422-41eaead166d4?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''University Street, Irbid'', N''Irbid Innovation Center'', 70.0, 4.8, N''Initiative,Workshop,Training'', N''Budget friendly''),
    (5, 180, N''A multi-purpose hall for public meetings and local events.'', N''Government institution'', N''Presentation screen,Sound system,Seating'', N''Irbid'', N''https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''Downtown, Irbid'', N''Greater Irbid Municipality Hall'', 130.0, 4.6, N''Conference,Initiative'', N''Available to book''),
    (6, 70, N''A comfortable venue for short courses, workshops, and volunteer activities.'', N''Community center'', N''Wi-Fi,Presentation setup,Seating'', N''Zarqa'', N''https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?auto=format&fit=crop&w=900&q=80'', CAST(1 AS bit), N''New Zarqa, Zarqa'', N''King Abdullah II Community Center'', 55.0, 4.5, N''Workshop,Training,Initiative'', N''Affordable'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Capacity', N'Description', N'EntityType', N'Features', N'Governorate', N'ImageUrl', N'IsActive', N'Location', N'Name', N'Price', N'Rating', N'SupportedEvents', N'Tag') AND [object_id] = OBJECT_ID(N'[Halls]'))
        SET IDENTITY_INSERT [Halls] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'City', N'CloseTime', N'ContactPerson', N'DecidedAt', N'Email', N'LicenseNumber', N'Name', N'OpenTime', N'Phone', N'RegisteredAt', N'RejectionReason', N'SlotCapacity', N'Status', N'Type') AND [object_id] = OBJECT_ID(N'[Organizations]'))
        SET IDENTITY_INSERT [Organizations] ON;
    EXEC(N'INSERT INTO [Organizations] ([Id], [Address], [City], [CloseTime], [ContactPerson], [DecidedAt], [Email], [LicenseNumber], [Name], [OpenTime], [Phone], [RegisteredAt], [RejectionReason], [SlotCapacity], [Status], [Type])
    VALUES (1, N''University Street, Irbid'', N''Irbid'', ''16:00:00'', N''Hospital Admin'', ''2026-01-01T00:00:00.0000000Z'', N''bloodbank@alhayat.example'', N''MOH-IRB-40017'', N''Al Hayat Hospital'', ''08:00:00'', N''+962 2 700 0000'', ''2026-01-01T00:00:00.0000000Z'', NULL, 3, N''Active'', N''Hospital''),
    (2, N''Amman–Jerash Road, Jerash'', N''Jerash'', ''15:00:00'', N''Dr. Hiba Khatib'', ''2026-01-01T00:00:00.0000000Z'', N''info@jerashfamily.jo'', N''MOH-JRS-20981'', N''Jerash Family Hospital'', ''08:00:00'', N''+962 2 635 5500'', ''2026-01-01T00:00:00.0000000Z'', NULL, 3, N''Active'', N''Hospital''),
    (3, N''Aqaba city center'', N''Aqaba'', ''15:00:00'', N''Fadi Amro'', ''2026-01-01T00:00:00.0000000Z'', N''aqaba@redcrescent.jo'', N''MOH-AQB-77310'', N''Aqaba Red Crescent Center'', ''09:00:00'', N''+962 3 201 7788'', ''2026-01-01T00:00:00.0000000Z'', NULL, 3, N''Active'', N''BloodDonationCenter'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'City', N'CloseTime', N'ContactPerson', N'DecidedAt', N'Email', N'LicenseNumber', N'Name', N'OpenTime', N'Phone', N'RegisteredAt', N'RejectionReason', N'SlotCapacity', N'Status', N'Type') AND [object_id] = OBJECT_ID(N'[Organizations]'))
        SET IDENTITY_INSERT [Organizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BloodType', N'OrganizationId', N'Units', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[BloodInventory]'))
        SET IDENTITY_INSERT [BloodInventory] ON;
    EXEC(N'INSERT INTO [BloodInventory] ([Id], [BloodType], [OrganizationId], [Units], [UpdatedAt])
    VALUES (1, N''O+'', 1, 52, ''2026-01-01T00:00:00.0000000Z''),
    (2, N''O-'', 1, 12, ''2026-01-01T00:00:00.0000000Z''),
    (3, N''A+'', 1, 44, ''2026-01-01T00:00:00.0000000Z''),
    (4, N''A-'', 1, 8, ''2026-01-01T00:00:00.0000000Z''),
    (5, N''B+'', 1, 36, ''2026-01-01T00:00:00.0000000Z''),
    (6, N''B-'', 1, 6, ''2026-01-01T00:00:00.0000000Z''),
    (7, N''AB+'', 1, 61, ''2026-01-01T00:00:00.0000000Z''),
    (8, N''AB-'', 1, 29, ''2026-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BloodType', N'OrganizationId', N'Units', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[BloodInventory]'))
        SET IDENTITY_INSERT [BloodInventory] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_AdminActivityLogs_AdminId] ON [AdminActivityLogs] ([AdminId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Advertisements_OrganizationId] ON [Advertisements] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Advertisements_StartDate_EndDate] ON [Advertisements] ([StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BloodInventory_OrganizationId_BloodType] ON [BloodInventory] ([OrganizationId], [BloodType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_BloodRequests_DonorId_Status] ON [BloodRequests] ([DonorId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_BloodRequests_HospitalId] ON [BloodRequests] ([HospitalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_BloodRequests_RecipientId_Status] ON [BloodRequests] ([RecipientId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificates_Code] ON [Certificates] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Certificates_UserId] ON [Certificates] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_BloodRequestId_SentAt] ON [ChatMessages] ([BloodRequestId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderId] ON [ChatMessages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChatReadStates_BloodRequestId_UserId] ON [ChatReadStates] ([BloodRequestId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_ChatReadStates_UserId] ON [ChatReadStates] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationAppointments_DonorId] ON [DonationAppointments] ([DonorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationAppointments_OrganizationId_Status_RequestedDate] ON [DonationAppointments] ([OrganizationId], [Status], [RequestedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationRecords_AppointmentId] ON [DonationRecords] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationRecords_BloodRequestId] ON [DonationRecords] ([BloodRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationRecords_DonorId] ON [DonationRecords] ([DonorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_DonationRecords_OrganizationId_DonationDate] ON [DonationRecords] ([OrganizationId], [DonationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_HallBookingDocuments_HallBookingId] ON [HallBookingDocuments] ([HallBookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_HallBookings_HallId] ON [HallBookings] ([HallId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_HallBookings_RequestedById] ON [HallBookings] ([RequestedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_HallBookings_Status] ON [HallBookings] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_OrganizationDocuments_OrganizationId] ON [OrganizationDocuments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Organizations_Name] ON [Organizations] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Organizations_Status] ON [Organizations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_ReportAttachments_ReportId] ON [ReportAttachments] ([ReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Reports_ReporterId] ON [Reports] ([ReporterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Reports_Status_CreatedAt] ON [Reports] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Users_OrganizationId] ON [Users] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    CREATE INDEX [IX_Users_Role_BloodType_City] ON [Users] ([Role], [BloodType], [City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118193217_Initial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118193217_Initial', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    ALTER TABLE [Users] ADD [SuspendReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    ALTER TABLE [Users] ADD [SuspendedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DonationRecords]') AND [c].[name] = N'OrganizationId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [DonationRecords] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [DonationRecords] ALTER COLUMN [OrganizationId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    ALTER TABLE [DonationAppointments] ADD [CancelledBy] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Certificates]') AND [c].[name] = N'SentToEmail');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Certificates] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Certificates] ALTER COLUMN [SentToEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Certificates]') AND [c].[name] = N'SentAt');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Certificates] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Certificates] ALTER COLUMN [SentAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    ALTER TABLE [Certificates] ADD [IssuedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120125920_BackendSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120125920_BackendSupport', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141335_DonorRestPeriod'
)
BEGIN
    ALTER TABLE [Users] ADD [AvailableAgainOn] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141335_DonorRestPeriod'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120141335_DonorRestPeriod', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    ALTER TABLE [Organizations] ADD [SubmittedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    ALTER TABLE [OrganizationDocuments] ADD [FileName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    EXEC(N'UPDATE [Organizations] SET [SubmittedAt] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    EXEC(N'UPDATE [Organizations] SET [SubmittedAt] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    EXEC(N'UPDATE [Organizations] SET [SubmittedAt] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120153546_HospitalVerification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120153546_HospitalVerification', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120175523_StripePayments'
)
BEGIN
    ALTER TABLE [HallBookings] ADD [PaymentIntentId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120175523_StripePayments'
)
BEGIN
    ALTER TABLE [Advertisements] ADD [AwaitingPayment] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120175523_StripePayments'
)
BEGIN
    ALTER TABLE [Advertisements] ADD [PaymentIntentId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120175523_StripePayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120175523_StripePayments', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120185708_DonationRecordEmails'
)
BEGIN
    ALTER TABLE [DonationRecords] ADD [DonorEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120185708_DonationRecordEmails'
)
BEGIN
    ALTER TABLE [DonationRecords] ADD [RecipientEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120185708_DonationRecordEmails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120185708_DonationRecordEmails', N'10.0.15');
END;

COMMIT;
GO

