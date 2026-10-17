# Documents (M5)

The generated `xmin = table.Column<uint>(type: "xid", rowVersion: true, ...)` declaration was removed from `DocumentTemplates`: PostgreSQL owns this system column. It remains in EF's model and snapshot for optimistic concurrency.

This migration follows M4 CostLedger and creates only DocumentTemplates, DocumentTemplateRevisions, ExportRuns plus the nullable SET NULL declaration-template preference foreign key. ImportConfigurations was excluded while scaffolding so M6 remains separate. No real database was touched.
