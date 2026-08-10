using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorCodeImageUrlAndWorkshopBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ErrorCodes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedBankAccountId",
                table: "CustomerReceiptBillings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [sys].[objects] WHERE [name] = 'BankAccounts' AND [type] = 'SN')
    DROP SYNONYM [foodir].[BankAccounts];

IF NOT EXISTS (SELECT 1 FROM [sys].[objects] WHERE [name] = 'BankAccounts' AND [type] = 'U')
BEGIN
    CREATE TABLE [foodir].[BankAccounts] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [BankName] nvarchar(200) NULL,
        [OwnerName] nvarchar(200) NULL,
        [CardNumber] nvarchar(32) NULL,
        [AccountNumber] nvarchar(64) NULL,
        [Iban] nvarchar(64) NULL,
        [IconUrl] nvarchar(500) NULL,
        [ShowInGateway] bit NOT NULL DEFAULT 1,
        [IsActive] bit NOT NULL DEFAULT 1,
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [UpdatedAt] datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([Id])
    );
END

IF COL_LENGTH('[foodir].[BankAccounts]', 'WorkshopId') IS NULL
    ALTER TABLE [foodir].[BankAccounts] ADD [WorkshopId] int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM [sys].[indexes] WHERE [name] = 'IX_BankAccounts_IsActive_ShowInGateway_SortOrder' AND [object_id] = OBJECT_ID('[foodir].[BankAccounts]'))
    CREATE INDEX [IX_BankAccounts_IsActive_ShowInGateway_SortOrder] ON [foodir].[BankAccounts] ([IsActive], [ShowInGateway], [SortOrder]);

IF NOT EXISTS (SELECT 1 FROM [sys].[indexes] WHERE [name] = 'IX_BankAccounts_WorkshopId_IsActive' AND [object_id] = OBJECT_ID('[foodir].[BankAccounts]'))
    CREATE INDEX [IX_BankAccounts_WorkshopId_IsActive] ON [foodir].[BankAccounts] ([WorkshopId], [IsActive]);

IF NOT EXISTS (SELECT 1 FROM [sys].[foreign_keys] WHERE [name] = 'FK_BankAccounts_Workshops_WorkshopId')
    ALTER TABLE [foodir].[BankAccounts] ADD CONSTRAINT [FK_BankAccounts_Workshops_WorkshopId] FOREIGN KEY ([WorkshopId]) REFERENCES [foodir].[Workshops] ([Id]) ON DELETE NO ACTION;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ErrorCodes");

            migrationBuilder.DropColumn(
                name: "SelectedBankAccountId",
                table: "CustomerReceiptBillings");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [sys].[foreign_keys] WHERE [name] = 'FK_BankAccounts_Workshops_WorkshopId')
    ALTER TABLE [foodir].[BankAccounts] DROP CONSTRAINT [FK_BankAccounts_Workshops_WorkshopId];
IF EXISTS (SELECT 1 FROM [sys].[indexes] WHERE [name] = 'IX_BankAccounts_WorkshopId_IsActive' AND [object_id] = OBJECT_ID('[foodir].[BankAccounts]'))
    DROP INDEX [IX_BankAccounts_WorkshopId_IsActive] ON [foodir].[BankAccounts];
IF COL_LENGTH('[foodir].[BankAccounts]', 'WorkshopId') IS NOT NULL
    ALTER TABLE [foodir].[BankAccounts] DROP COLUMN [WorkshopId];
");
        }
    }
}
