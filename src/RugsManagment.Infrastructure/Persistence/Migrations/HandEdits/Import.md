# M6 Import

Generated after M5 Documents; creates only ImportJobs and ImportMappingPresets.
Removed generated ImportJobs.xmin DDL because PostgreSQL already supplies its system concurrency column.
The EF model and snapshot retain the xmin row-version mapping. Down drops only these two new tables.
