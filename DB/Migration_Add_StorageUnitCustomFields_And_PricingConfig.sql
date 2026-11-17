/* ============================================================================
   Adds the storage unit custom dimension / pricing / floor-map columns and the
   dynamic pricing tables introduced with the "Cum 1" refactor.
   Idempotent: safe to re-run against an existing SelfStoragePRN222 database.
   ============================================================================ */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[core].[storage_units]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[core].[storage_units]', N'custom_width_m') IS NULL
        ALTER TABLE [core].[storage_units] ADD [custom_width_m] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'custom_length_m') IS NULL
        ALTER TABLE [core].[storage_units] ADD [custom_length_m] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'custom_height_m') IS NULL
        ALTER TABLE [core].[storage_units] ADD [custom_height_m] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'custom_area_m2') IS NULL
        ALTER TABLE [core].[storage_units] ADD [custom_area_m2] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'storage_category') IS NULL
        ALTER TABLE [core].[storage_units] ADD [storage_category] nvarchar(100) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'custom_monthly_rate') IS NULL
        ALTER TABLE [core].[storage_units] ADD [custom_monthly_rate] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'base_price_per_sqm') IS NULL
        ALTER TABLE [core].[storage_units] ADD [base_price_per_sqm] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'category_surcharge') IS NULL
        ALTER TABLE [core].[storage_units] ADD [category_surcharge] decimal(18, 2) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'image_url') IS NULL
        ALTER TABLE [core].[storage_units] ADD [image_url] nvarchar(1000) NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'grid_row') IS NULL
        ALTER TABLE [core].[storage_units] ADD [grid_row] int NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'grid_col') IS NULL
        ALTER TABLE [core].[storage_units] ADD [grid_col] int NULL;
    IF COL_LENGTH(N'[core].[storage_units]', N'grid_row_span') IS NULL
        ALTER TABLE [core].[storage_units]
            ADD [grid_row_span] int NOT NULL CONSTRAINT [DF_storage_units_grid_row_span] DEFAULT (1);
    IF COL_LENGTH(N'[core].[storage_units]', N'grid_col_span') IS NULL
        ALTER TABLE [core].[storage_units]
            ADD [grid_col_span] int NOT NULL CONSTRAINT [DF_storage_units_grid_col_span] DEFAULT (1);
END;
GO

IF OBJECT_ID(N'[core].[pricing_configs]', N'U') IS NULL
BEGIN
    CREATE TABLE [core].[pricing_configs] (
        [id] bigint IDENTITY(1, 1) NOT NULL CONSTRAINT [PK_pricing_configs] PRIMARY KEY,
        [price_per_square_meter] decimal(18, 2) NOT NULL CONSTRAINT [DF_pricing_configs_price] DEFAULT (100),
        [updated_at] datetimeoffset(7) NOT NULL CONSTRAINT [DF_pricing_configs_updated_at] DEFAULT (SYSDATETIMEOFFSET())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM [core].[pricing_configs])
    INSERT INTO [core].[pricing_configs] ([price_per_square_meter]) VALUES (100);
GO

IF OBJECT_ID(N'[core].[storage_categories]', N'U') IS NULL
BEGIN
    CREATE TABLE [core].[storage_categories] (
        [id] bigint IDENTITY(1, 1) NOT NULL CONSTRAINT [PK_storage_categories] PRIMARY KEY,
        [name] nvarchar(100) NOT NULL,
        [surcharge_amount] decimal(18, 2) NOT NULL CONSTRAINT [DF_storage_categories_surcharge] DEFAULT (0),
        [is_active] bit NOT NULL CONSTRAINT [DF_storage_categories_is_active] DEFAULT (1),
        [created_at] datetimeoffset(7) NOT NULL CONSTRAINT [DF_storage_categories_created_at] DEFAULT (SYSDATETIMEOFFSET())
    );
END;
GO

PRINT N'Migration_Add_StorageUnitCustomFields_And_PricingConfig completed successfully.';
GO
