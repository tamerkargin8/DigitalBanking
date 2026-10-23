-- Optional repair for an already-created SQL Server schema whose
-- ReconciliationExceptions.RowId foreign key is still ON DELETE SET NULL.
--
-- Fresh installs use the corrected migration and are already NO ACTION; this
-- script is idempotent and does not touch rows or customer data.
--
-- Run manually, with a backup, only on a synthetic/operator-managed database.
-- Do not run against UAT unless the UAT owner explicitly approves.

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ReconciliationExceptions_ReconciliationRows_RowId'
      AND delete_referential_action_desc = N'SET_NULL'
)
BEGIN
    ALTER TABLE [ReconciliationExceptions]
        DROP CONSTRAINT [FK_ReconciliationExceptions_ReconciliationRows_RowId];

    ALTER TABLE [ReconciliationExceptions]
        ADD CONSTRAINT [FK_ReconciliationExceptions_ReconciliationRows_RowId]
        FOREIGN KEY ([RowId]) REFERENCES [ReconciliationRows] ([Id])
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;
END;
