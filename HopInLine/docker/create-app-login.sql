-- Creates the least-privilege login the web app connects as. Safe to run repeatedly.
-- Run by the compose "db-setup" service after migrations have created the database.
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'hopinline_app')
    CREATE LOGIN hopinline_app WITH PASSWORD = '$(APP_DB_PASSWORD)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN hopinline_app WITH PASSWORD = '$(APP_DB_PASSWORD)';
GO

USE hopinline;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'hopinline_app')
    CREATE USER hopinline_app FOR LOGIN hopinline_app;
ALTER ROLE db_datareader ADD MEMBER hopinline_app;
ALTER ROLE db_datawriter ADD MEMBER hopinline_app;
GO
