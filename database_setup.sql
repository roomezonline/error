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
CREATE TABLE [SliderItems] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Subtitle] nvarchar(400) NULL,
    [ImageUrl] nvarchar(500) NULL,
    [LinkUrl] nvarchar(500) NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SliderItems] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_SliderItems_IsActive_SortOrder] ON [SliderItems] ([IsActive], [SortOrder]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260207123602_InitSliders', N'9.0.0');

CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Icon] nvarchar(50) NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [Price] decimal(18,2) NOT NULL,
    [DiscountPrice] decimal(18,2) NULL,
    [MainImageUrl] nvarchar(500) NULL,
    [CategoryId] int NOT NULL,
    [IsAvailable] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);

CREATE INDEX [IX_Products_CreatedAt] ON [Products] ([CreatedAt]);

CREATE INDEX [IX_Products_IsAvailable] ON [Products] ([IsAvailable]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260207132905_AddStoreTables', N'9.0.0');

ALTER TABLE [Products] ADD [DiscountExpiryDate] datetimeoffset NULL;

ALTER TABLE [Products] ADD [ImageUrl2] nvarchar(500) NULL;

ALTER TABLE [Products] ADD [ImageUrl3] nvarchar(500) NULL;

ALTER TABLE [Products] ADD [ImageUrl4] nvarchar(500) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260208060543_UpdateProductModelV2', N'9.0.0');

COMMIT;
GO

