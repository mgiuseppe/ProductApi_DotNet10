-- Database users of the API and the migrator, one per managed identity, each with only the rights it needs (ADR-0018).
-- Run by the deploy workflow (azure/sql-action) as the server's Entra administrator, after Bicep and before the migrator.
-- Idempotent: an existing user is left alone, adding a role a user already has changes nothing.
--
-- sqlcmd variables, passed by the workflow from the stack outputs: ApiName, ApiClientId, MigratorName, MigratorClientId.
--
-- WITH SID ... TYPE = E instead of FROM EXTERNAL PROVIDER: the SID of a managed identity is its client id, so SQL needs
-- no lookup in Entra ID and the server needs no Directory Readers role. TYPE = E is an Entra application.
-- CREATE USER accepts the SID only as a literal, hence the dynamic SQL.

SET NOCOUNT ON;
DECLARE @sid nvarchar(36);

-- API: reads and writes data
-- A user with the same name but another SID belongs to an identity deleted and recreated: drop it
IF EXISTS (SELECT 1 FROM sys.database_principals
           WHERE name = N'$(ApiName)' AND sid <> CONVERT(varbinary(16), CONVERT(uniqueidentifier, '$(ApiClientId)')))
    DROP USER [$(ApiName)];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiName)')
BEGIN
    SET @sid = CONVERT(nvarchar(36), CONVERT(varbinary(16), CONVERT(uniqueidentifier, '$(ApiClientId)')), 1);
    EXEC (N'CREATE USER [$(ApiName)] WITH SID = ' + @sid + N', TYPE = E;');
END
ALTER ROLE db_datareader ADD MEMBER [$(ApiName)];
ALTER ROLE db_datawriter ADD MEMBER [$(ApiName)];

-- Migrator: reads and writes data, and changes the schema
-- A user with the same name but another SID belongs to an identity deleted and recreated: drop it
IF EXISTS (SELECT 1 FROM sys.database_principals
           WHERE name = N'$(MigratorName)' AND sid <> CONVERT(varbinary(16), CONVERT(uniqueidentifier, '$(MigratorClientId)')))
    DROP USER [$(MigratorName)];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(MigratorName)')
BEGIN
    SET @sid = CONVERT(nvarchar(36), CONVERT(varbinary(16), CONVERT(uniqueidentifier, '$(MigratorClientId)')), 1);
    EXEC (N'CREATE USER [$(MigratorName)] WITH SID = ' + @sid + N', TYPE = E;');
END
ALTER ROLE db_datareader ADD MEMBER [$(MigratorName)];
ALTER ROLE db_datawriter ADD MEMBER [$(MigratorName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(MigratorName)];
