# M7 DropRugPurchaseCost

The obsolete Rugs.PurchaseCost property and column are removed after M6. Added `[RequiresBackup("Rugs")]` so startup on populated installations requires an explicit backup confirmation for this migration.

Up also removes the M2 dimension backup table. Down recreates the legacy column from the sum of purchase-line amounts and recreates the dimension-backup schema using current effective sizes, allowing the earlier migration chain to roll back. Original pre-M2 fractional precision cannot be reconstructed after M7; recover those values from the mandatory external backup instead.

No applied M0–M3 migration was modified. No xmin DDL was generated.
