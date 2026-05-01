USE GadgetVaultDB;
BEGIN TRANSACTION;

-- Target IDs for deletion based on user criteria
DECLARE @TargetIds TABLE (Id INT);
INSERT INTO @TargetIds 
SELECT Id FROM BusinessPartners 
WHERE (CompanyName IN ('Global Tech', 'Global Tech Inc', 'Nexus IT', 'Pacific Power') AND Email IN ('john@globaltech.com', 'neo@nexus-it.com', 'pat@pacificpower.com'))
   OR (CompanyName = 'Global Tech Inc' AND Email = 'john@globaltech.com');

-- 1. Users associated with these partners or emails
DELETE FROM Users 
WHERE SupplierId IN (SELECT Id FROM @TargetIds)
   OR Email IN ('john@globaltech.com', 'neo@nexus-it.com', 'pat@pacificpower.com');
DECLARE @UserCount INT = @@ROWCOUNT;

-- 2. Cascade cleanup for transactional data
-- 2.a Inventory Transactions
DELETE FROM InventoryTransactions 
WHERE PurchaseOrderId IN (SELECT Id FROM PurchaseOrders WHERE SupplierId IN (SELECT Id FROM @TargetIds))
   OR ProductId IN (SELECT Id FROM Products WHERE SupplierId IN (SELECT Id FROM @TargetIds));

-- 2.b Purchase Order Items
DELETE FROM PurchaseOrderItems 
WHERE PurchaseOrderId IN (SELECT Id FROM PurchaseOrders WHERE SupplierId IN (SELECT Id FROM @TargetIds));

-- 2.c Purchase Orders
DELETE FROM PurchaseOrders 
WHERE SupplierId IN (SELECT Id FROM @TargetIds);

-- 2.d Stock Levels
DELETE FROM StockLevels 
WHERE ProductId IN (SELECT Id FROM Products WHERE SupplierId IN (SELECT Id FROM @TargetIds));

-- 2.e Products
DELETE FROM Products 
WHERE SupplierId IN (SELECT Id FROM @TargetIds);

-- 3. Final Step: Delete Business Partners
DELETE FROM BusinessPartners 
WHERE Id IN (SELECT Id FROM @TargetIds);
DECLARE @PartnerCount INT = @@ROWCOUNT;

-- Summary Output
SELECT 'Users Deleted' AS [Entity], @UserCount AS [Count]
UNION ALL
SELECT 'Business Partners Deleted', @PartnerCount;

-- Critical Safety Check Verification (Active Partners)
SELECT CompanyName, Email, PartnerType 
FROM BusinessPartners 
WHERE CompanyName IN ('Global Tech Inc.', 'Nexus IT Solutions', 'Pacific Power & Cooling');

COMMIT TRANSACTION;
