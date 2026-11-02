using AdventureWorksTool.Models;
using AdventureWorksTool.Responses;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace AdventureWorksTool.Queries
{
    public sealed class AdventureWorksQueryProcessor
    {
        private readonly AdventureWorks2025Context context;

        // Store the database context used to run the read-only queries.
        public AdventureWorksQueryProcessor(AdventureWorks2025Context context)
        {
            ArgumentNullException.ThrowIfNull(context);
            this.context = context;
        }

        // CONVERT(char(7), d, 126) yields 'yyyy-MM' and avoids FORMAT(), which is markedly
        // slower when it has to run per row in a GROUP BY.
        private const string SalesByMonthYearSql = """
            SELECT
                CONVERT(char(7), soh.OrderDate, 126) AS Period,
                SUM(sod.LineTotal) AS Sales
            FROM Sales.SalesOrderHeader soh
            JOIN Sales.SalesOrderDetail sod ON soh.SalesOrderID = sod.SalesOrderID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
            GROUP BY CONVERT(char(7), soh.OrderDate, 126)
            ORDER BY Period ASC;
            """;

        private const string TopProductsBySalesSql = """
            SELECT TOP (@topN)
                p.Name AS Product,
                SUM(sod.LineTotal) AS Sales
            FROM Production.Product p
            JOIN Sales.SalesOrderDetail sod ON p.ProductID = sod.ProductID
            JOIN Sales.SalesOrderHeader soh ON soh.SalesOrderID = sod.SalesOrderID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
            GROUP BY p.Name
            ORDER BY Sales DESC;
            """;

        private const string SalesByCategorySql = """
            SELECT
                pc.Name AS Category,
                SUM(sod.LineTotal) AS Sales
            FROM Production.ProductCategory pc
            JOIN Production.ProductSubcategory ps ON pc.ProductCategoryID = ps.ProductCategoryID
            JOIN Production.Product p ON p.ProductSubcategoryID = ps.ProductSubcategoryID
            JOIN Sales.SalesOrderDetail sod ON p.ProductID = sod.ProductID
            JOIN Sales.SalesOrderHeader soh ON soh.SalesOrderID = sod.SalesOrderID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
            GROUP BY pc.Name
            ORDER BY Sales DESC;
            """;

        // @region matches a territory name ('Northwest'), a country code ('US') or a
        // territory group ('Europe'), so the model does not have to know which it was given.
        private const string RevenueByRegionSql = """
            SELECT
                st.Name AS Region,
                SUM(sod.LineTotal) AS Revenue
            FROM Sales.SalesOrderHeader soh
            JOIN Sales.SalesOrderDetail sod ON soh.SalesOrderID = sod.SalesOrderID
            JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
              AND (@region IS NULL
                   OR st.Name = @region
                   OR st.CountryRegionCode = @region
                   OR st.[Group] = @region)
            GROUP BY st.Name
            ORDER BY Revenue DESC;
            """;

        // Bind every referenced parameter; use DBNull.Value for optional filters.
        // @topN is a required positive int; callers must enforce a maximum result size.
        // @from and @to are optional SQL date values (inclusive); @to must be before 9999-12-31.
        // Other filters are optional unless marked required. @name is a literal substring.
        // Sales uses SubTotal (before tax/freight); summaries include all statuses unless filtered.

        // Parameters: @topN, @customerId, @territoryId, @name.
        private const string CustomersSql = """
            SELECT TOP (@topN)
                c.CustomerID,
                c.AccountNumber,
                COALESCE(s.Name, NULLIF(LTRIM(RTRIM(CONCAT(p.FirstName, N' ', p.LastName))), N''), c.AccountNumber) AS Customer,
                CASE WHEN c.StoreID IS NULL THEN N'Individual' ELSE N'Store' END AS CustomerType,
                c.TerritoryID,
                st.Name AS Territory
            FROM Sales.Customer c
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
            LEFT JOIN Sales.SalesTerritory st ON st.TerritoryID = c.TerritoryID
            WHERE (@customerId IS NULL OR c.CustomerID = @customerId)
              AND (@territoryId IS NULL OR c.TerritoryID = @territoryId)
              AND (@name IS NULL OR CHARINDEX(@name, s.Name) > 0
                   OR CHARINDEX(@name, CONCAT(p.FirstName, N' ', p.LastName)) > 0)
            ORDER BY Customer ASC, c.CustomerID ASC;
            """;

        // Parameters: @topN, @from, @to, @status (sales order status).
        private const string TopCustomersBySalesSql = """
            SELECT TOP (@topN)
                c.CustomerID,
                COALESCE(s.Name, NULLIF(LTRIM(RTRIM(CONCAT(p.FirstName, N' ', p.LastName))), N''), c.AccountNumber) AS Customer,
                COUNT_BIG(*) AS OrderCount,
                SUM(soh.SubTotal) AS Sales,
                MAX(soh.OrderDate) AS LastOrderDate
            FROM Sales.Customer c
            JOIN Sales.SalesOrderHeader soh ON soh.CustomerID = c.CustomerID
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
              AND (@status IS NULL OR soh.Status = @status)
            GROUP BY c.CustomerID, c.AccountNumber, s.Name, p.FirstName, p.LastName
            ORDER BY Sales DESC, c.CustomerID ASC;
            """;

        // Parameters: @topN, @customerId, @status, @from, @to.
        // Sales status: 1 In process, 2 Approved, 3 Backordered, 4 Rejected, 5 Shipped, 6 Cancelled.
        private const string SalesOrdersSql = """
            SELECT TOP (@topN)
                soh.SalesOrderID,
                soh.SalesOrderNumber,
                soh.CustomerID,
                soh.OrderDate,
                soh.DueDate,
                soh.ShipDate,
                soh.Status,
                sm.Name AS ShipMethod,
                soh.SubTotal,
                soh.TaxAmt,
                soh.Freight,
                soh.TotalDue
            FROM Sales.SalesOrderHeader soh
            JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = soh.ShipMethodID
            WHERE (@customerId IS NULL OR soh.CustomerID = @customerId)
              AND (@status IS NULL OR soh.Status = @status)
              AND (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
            ORDER BY soh.OrderDate DESC, soh.SalesOrderID DESC;
            """;

        // Parameters: @topN, @salesOrderId (required).
        private const string SalesOrderDetailsSql = """
            SELECT TOP (@topN)
                sod.SalesOrderID,
                sod.SalesOrderDetailID,
                sod.ProductID,
                p.Name AS Product,
                p.ProductNumber,
                sod.OrderQty,
                sod.UnitPrice,
                sod.UnitPriceDiscount,
                sod.LineTotal
            FROM Sales.SalesOrderDetail sod
            JOIN Production.Product p ON p.ProductID = sod.ProductID
            WHERE sod.SalesOrderID = @salesOrderId
            ORDER BY sod.SalesOrderDetailID ASC;
            """;

        // Parameters: @topN, @from, @to, @territoryId (order territory), @status.
        private const string SalesBySalesPersonSql = """
            SELECT TOP (@topN)
                sp.BusinessEntityID AS SalesPersonID,
                CONCAT(p.FirstName, N' ', p.LastName) AS SalesPerson,
                COUNT_BIG(*) AS OrderCount,
                SUM(soh.SubTotal) AS Sales
            FROM Sales.SalesPerson sp
            JOIN Person.Person p ON p.BusinessEntityID = sp.BusinessEntityID
            JOIN Sales.SalesOrderHeader soh ON soh.SalesPersonID = sp.BusinessEntityID
            WHERE (@from IS NULL OR soh.OrderDate >= @from)
              AND (@to IS NULL OR soh.OrderDate < DATEADD(day, 1, @to))
              AND (@territoryId IS NULL OR soh.TerritoryID = @territoryId)
              AND (@status IS NULL OR soh.Status = @status)
            GROUP BY sp.BusinessEntityID, p.FirstName, p.LastName
            ORDER BY Sales DESC, sp.BusinessEntityID ASC;
            """;

        // Parameters: @topN, @productId, @categoryId, @name.
        private const string ProductCatalogSql = """
            SELECT TOP (@topN)
                p.ProductID,
                p.ProductNumber,
                p.Name AS Product,
                pc.ProductCategoryID,
                pc.Name AS Category,
                ps.Name AS Subcategory,
                p.Color,
                p.Size,
                p.ListPrice,
                p.StandardCost,
                p.FinishedGoodsFlag,
                p.SellStartDate,
                p.SellEndDate,
                p.DiscontinuedDate
            FROM Production.Product p
            LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
            WHERE (@productId IS NULL OR p.ProductID = @productId)
              AND (@categoryId IS NULL OR pc.ProductCategoryID = @categoryId)
              AND (@name IS NULL OR CHARINDEX(@name, p.Name) > 0)
            ORDER BY p.Name ASC, p.ProductID ASC;
            """;

        // Parameters: @topN, @productId, @locationId. One row per product/location.
        private const string ProductInventorySql = """
            SELECT TOP (@topN)
                p.ProductID,
                p.Name AS Product,
                pi.LocationID,
                l.Name AS Location,
                pi.Shelf,
                pi.Bin,
                pi.Quantity,
                p.SafetyStockLevel,
                p.ReorderPoint
            FROM Production.ProductInventory pi
            JOIN Production.Product p ON p.ProductID = pi.ProductID
            JOIN Production.Location l ON l.LocationID = pi.LocationID
            WHERE (@productId IS NULL OR pi.ProductID = @productId)
              AND (@locationId IS NULL OR pi.LocationID = @locationId)
            ORDER BY p.ProductID ASC, pi.LocationID ASC;
            """;

        // Parameters: @topN, @categoryId. Missing inventory counts as zero across all locations.
        private const string LowStockProductsSql = """
            SELECT TOP (@topN)
                p.ProductID,
                p.Name AS Product,
                pc.Name AS Category,
                COALESCE(SUM(CONVERT(bigint, pi.Quantity)), 0) AS QuantityOnHand,
                p.ReorderPoint,
                p.SafetyStockLevel,
                p.ReorderPoint - COALESCE(SUM(CONVERT(bigint, pi.Quantity)), 0) AS ReorderShortfall
            FROM Production.Product p
            LEFT JOIN Production.ProductInventory pi ON pi.ProductID = p.ProductID
            LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
            WHERE (@categoryId IS NULL OR pc.ProductCategoryID = @categoryId)
            GROUP BY p.ProductID, p.Name, pc.Name, p.ReorderPoint, p.SafetyStockLevel
            HAVING COALESCE(SUM(CONVERT(bigint, pi.Quantity)), 0) < p.ReorderPoint
            ORDER BY ReorderShortfall DESC, p.ProductID ASC;
            """;

        // Parameters: @topN, @vendorId, @activeOnly (bit; NULL/0 includes inactive), @name.
        private const string VendorsSql = """
            SELECT TOP (@topN)
                v.BusinessEntityID AS VendorID,
                v.Name AS Vendor,
                v.CreditRating,
                v.PreferredVendorStatus,
                v.ActiveFlag
            FROM Purchasing.Vendor v
            WHERE (@vendorId IS NULL OR v.BusinessEntityID = @vendorId)
              AND (@activeOnly IS NULL OR @activeOnly = 0 OR v.ActiveFlag = 1)
              AND (@name IS NULL OR CHARINDEX(@name, v.Name) > 0)
            ORDER BY v.Name ASC, v.BusinessEntityID ASC;
            """;

        // Parameters: @topN, @productId, @vendorId, @activeOnly (bit; NULL/0 includes inactive).
        private const string ProductSuppliersSql = """
            SELECT TOP (@topN)
                p.ProductID,
                p.Name AS Product,
                v.BusinessEntityID AS VendorID,
                v.Name AS Vendor,
                v.ActiveFlag,
                pv.StandardPrice,
                pv.AverageLeadTime,
                pv.MinOrderQty,
                pv.MaxOrderQty,
                pv.OnOrderQty,
                pv.UnitMeasureCode
            FROM Purchasing.ProductVendor pv
            JOIN Production.Product p ON p.ProductID = pv.ProductID
            JOIN Purchasing.Vendor v ON v.BusinessEntityID = pv.BusinessEntityID
            WHERE (@productId IS NULL OR pv.ProductID = @productId)
              AND (@vendorId IS NULL OR pv.BusinessEntityID = @vendorId)
              AND (@activeOnly IS NULL OR @activeOnly = 0 OR v.ActiveFlag = 1)
            ORDER BY p.ProductID ASC, pv.StandardPrice ASC, v.BusinessEntityID ASC;
            """;

        // Parameters: @topN, @vendorId, @status, @from, @to.
        // Purchase status: 1 Pending, 2 Approved, 3 Rejected, 4 Complete.
        private const string PurchaseOrdersSql = """
            SELECT TOP (@topN)
                poh.PurchaseOrderID,
                poh.VendorID,
                v.Name AS Vendor,
                poh.EmployeeID,
                poh.OrderDate,
                poh.ShipDate AS ExpectedShipDate,
                poh.Status,
                sm.Name AS ShipMethod,
                poh.SubTotal,
                poh.TaxAmt,
                poh.Freight,
                poh.TotalDue
            FROM Purchasing.PurchaseOrderHeader poh
            JOIN Purchasing.Vendor v ON v.BusinessEntityID = poh.VendorID
            JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = poh.ShipMethodID
            WHERE (@vendorId IS NULL OR poh.VendorID = @vendorId)
              AND (@status IS NULL OR poh.Status = @status)
              AND (@from IS NULL OR poh.OrderDate >= @from)
              AND (@to IS NULL OR poh.OrderDate < DATEADD(day, 1, @to))
            ORDER BY poh.OrderDate DESC, poh.PurchaseOrderID DESC;
            """;

        // Parameters: @topN, @purchaseOrderId (required).
        private const string PurchaseOrderDetailsSql = """
            SELECT TOP (@topN)
                pod.PurchaseOrderID,
                pod.PurchaseOrderDetailID,
                pod.ProductID,
                p.Name AS Product,
                pod.DueDate,
                pod.OrderQty,
                pod.UnitPrice,
                pod.LineTotal,
                pod.ReceivedQty,
                pod.RejectedQty,
                pod.StockedQty
            FROM Purchasing.PurchaseOrderDetail pod
            JOIN Production.Product p ON p.ProductID = pod.ProductID
            WHERE pod.PurchaseOrderID = @purchaseOrderId
            ORDER BY pod.PurchaseOrderDetailID ASC;
            """;

        // Parameters: @topN, @from, @to, @status (purchase order status).
        private const string PurchasingByVendorSql = """
            SELECT TOP (@topN)
                v.BusinessEntityID AS VendorID,
                v.Name AS Vendor,
                COUNT_BIG(*) AS OrderCount,
                SUM(poh.SubTotal) AS PurchaseSubtotal,
                SUM(poh.TotalDue) AS TotalPurchaseAmount
            FROM Purchasing.Vendor v
            JOIN Purchasing.PurchaseOrderHeader poh ON poh.VendorID = v.BusinessEntityID
            WHERE (@from IS NULL OR poh.OrderDate >= @from)
              AND (@to IS NULL OR poh.OrderDate < DATEADD(day, 1, @to))
              AND (@status IS NULL OR poh.Status = @status)
            GROUP BY v.BusinessEntityID, v.Name
            ORDER BY TotalPurchaseAmount DESC, v.BusinessEntityID ASC;
            """;

        // Parameters: @topN, @productId, @openOnly (bit; NULL/0 includes completed), @from, @to (start date).
        private const string WorkOrdersSql = """
            SELECT TOP (@topN)
                wo.WorkOrderID,
                wo.ProductID,
                p.Name AS Product,
                wo.OrderQty,
                wo.StockedQty,
                wo.ScrappedQty,
                wo.StartDate,
                wo.DueDate,
                wo.EndDate,
                sr.Name AS ScrapReason
            FROM Production.WorkOrder wo
            JOIN Production.Product p ON p.ProductID = wo.ProductID
            LEFT JOIN Production.ScrapReason sr ON sr.ScrapReasonID = wo.ScrapReasonID
            WHERE (@productId IS NULL OR wo.ProductID = @productId)
              AND (@openOnly IS NULL OR @openOnly = 0 OR wo.EndDate IS NULL)
              AND (@from IS NULL OR wo.StartDate >= @from)
              AND (@to IS NULL OR wo.StartDate < DATEADD(day, 1, @to))
            ORDER BY wo.StartDate DESC, wo.WorkOrderID DESC;
            """;

        // Parameters: @topN, @productId, @from, @to (work order start date).
        private const string ScrapByReasonSql = """
            SELECT TOP (@topN)
                wo.ScrapReasonID,
                COALESCE(sr.Name, N'Unspecified') AS ScrapReason,
                COUNT_BIG(*) AS AffectedWorkOrders,
                SUM(CONVERT(bigint, wo.ScrappedQty)) AS ScrappedQuantity
            FROM Production.WorkOrder wo
            LEFT JOIN Production.ScrapReason sr ON sr.ScrapReasonID = wo.ScrapReasonID
            WHERE wo.ScrappedQty > 0
              AND (@productId IS NULL OR wo.ProductID = @productId)
              AND (@from IS NULL OR wo.StartDate >= @from)
              AND (@to IS NULL OR wo.StartDate < DATEADD(day, 1, @to))
            GROUP BY wo.ScrapReasonID, sr.Name
            ORDER BY ScrappedQuantity DESC, wo.ScrapReasonID ASC;
            """;

        // Parameters: @topN, @employeeId, @departmentId, @name. Current employees/assignments only.
        private const string EmployeeDirectorySql = """
            SELECT TOP (@topN)
                e.BusinessEntityID AS EmployeeID,
                CONCAT(p.FirstName, N' ', p.LastName) AS Employee,
                e.JobTitle,
                d.DepartmentID,
                d.Name AS Department,
                d.GroupName,
                sh.Name AS Shift,
                edh.StartDate AS DepartmentStartDate
            FROM HumanResources.Employee e
            JOIN Person.Person p ON p.BusinessEntityID = e.BusinessEntityID
            LEFT JOIN HumanResources.EmployeeDepartmentHistory edh
                ON edh.BusinessEntityID = e.BusinessEntityID AND edh.EndDate IS NULL
            LEFT JOIN HumanResources.Department d ON d.DepartmentID = edh.DepartmentID
            LEFT JOIN HumanResources.Shift sh ON sh.ShiftID = edh.ShiftID
            WHERE e.CurrentFlag = 1
              AND (@employeeId IS NULL OR e.BusinessEntityID = @employeeId)
              AND (@departmentId IS NULL OR d.DepartmentID = @departmentId)
              AND (@name IS NULL OR CHARINDEX(@name, CONCAT(p.FirstName, N' ', p.LastName)) > 0)
            ORDER BY p.LastName ASC, p.FirstName ASC, e.BusinessEntityID ASC,
                     d.DepartmentID ASC, edh.ShiftID ASC, edh.StartDate ASC;
            """;

        // Parameters: @topN. Count current employees once per department, including empty departments.
        private const string EmployeeCountByDepartmentSql = """
            SELECT TOP (@topN)
                d.DepartmentID,
                d.Name AS Department,
                d.GroupName,
                COUNT(DISTINCT e.BusinessEntityID) AS EmployeeCount
            FROM HumanResources.Department d
            LEFT JOIN HumanResources.EmployeeDepartmentHistory edh
                ON edh.DepartmentID = d.DepartmentID AND edh.EndDate IS NULL
            LEFT JOIN HumanResources.Employee e
                ON e.BusinessEntityID = edh.BusinessEntityID AND e.CurrentFlag = 1
            GROUP BY d.DepartmentID, d.Name, d.GroupName
            ORDER BY EmployeeCount DESC, d.DepartmentID ASC;
            """;

        // Parameters: @topN, @productId, @asOf (required SQL datetime). Offers valid at that instant.
        private const string ProductSpecialOffersSql = """
            SELECT TOP (@topN)
                p.ProductID,
                p.Name AS Product,
                so.SpecialOfferID,
                so.Description,
                so.DiscountPct,
                so.Type AS OfferType,
                so.Category AS OfferCategory,
                so.StartDate,
                so.EndDate,
                so.MinQty,
                so.MaxQty
            FROM Sales.SpecialOffer so
            JOIN Sales.SpecialOfferProduct sop ON sop.SpecialOfferID = so.SpecialOfferID
            JOIN Production.Product p ON p.ProductID = sop.ProductID
            WHERE (@productId IS NULL OR p.ProductID = @productId)
              AND so.StartDate <= @asOf
              AND so.EndDate >= @asOf
            ORDER BY so.DiscountPct DESC, p.ProductID ASC, so.SpecialOfferID ASC;
            """;

        // Get monthly sales totals, optionally limited to a date range.
        public async Task<ResponseData> GetSalesByMonthYearAsync(
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(SalesByMonthYearSql, cancellationToken,
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Get the highest-selling products within the requested row and date limits.
        public async Task<ResponseData> GetTopProductsBySalesAsync(
            int topN = 100, DateTime? from = null, DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(TopProductsBySalesSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Get sales totals grouped by product category.
        public async Task<ResponseData> GetSalesByCategoryAsync(
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(SalesByCategorySql, cancellationToken,
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Get territory revenue, optionally filtered by territory, country, or group and dates.
        public async Task<ResponseData> GetRevenueByRegionAsync(
            string? region = null, DateTime? from = null, DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(region?.Length ?? 0, 50, nameof(region));
            return await ExecuteQueryAsync(RevenueByRegionSql, cancellationToken,
                new SqlParameter("@region", SqlDbType.NVarChar, 50) { Value = (object?)region ?? DBNull.Value },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Find customers using optional customer ID, territory ID, and name filters.
        public async Task<ResponseData> GetCustomersAsync(
            int topN = 100, int? customerId = null, int? territoryId = null, string? name = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name?.Length ?? 0, 101, nameof(name));
            return await ExecuteQueryAsync(CustomersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@customerId", SqlDbType.Int) { Value = (object?)customerId ?? DBNull.Value },
                new SqlParameter("@territoryId", SqlDbType.Int) { Value = (object?)territoryId ?? DBNull.Value },
                new SqlParameter("@name", SqlDbType.NVarChar, 101) { Value = (object?)name ?? DBNull.Value });
        }

        // Rank customers by sales, with optional date and order-status filters.
        public async Task<ResponseData> GetTopCustomersBySalesAsync(
            int topN = 100, DateTime? from = null, DateTime? to = null, byte? status = null,
            CancellationToken cancellationToken = default)
        {
            if (status is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(status));
            return await ExecuteQueryAsync(TopCustomersBySalesSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value },
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = (object?)status ?? DBNull.Value });
        }

        // Get sales orders matching the requested customer, status, and dates.
        public async Task<ResponseData> GetSalesOrdersAsync(
            int topN = 100, int? customerId = null, byte? status = null,
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            if (status is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(status));
            return await ExecuteQueryAsync(SalesOrdersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@customerId", SqlDbType.Int) { Value = (object?)customerId ?? DBNull.Value },
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = (object?)status ?? DBNull.Value },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Get the product line items for one sales order after checking its ID.
        public async Task<ResponseData> GetSalesOrderDetailsAsync(
            int salesOrderId, int topN = 100, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(salesOrderId);
            return await ExecuteQueryAsync(SalesOrderDetailsSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@salesOrderId", SqlDbType.Int) { Value = salesOrderId });
        }

        // Get sales totals for salespeople using the requested order filters.
        public async Task<ResponseData> GetSalesBySalesPersonAsync(
            int topN = 100, DateTime? from = null, DateTime? to = null, int? territoryId = null,
            byte? status = null, CancellationToken cancellationToken = default)
        {
            if (status is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(status));
            return await ExecuteQueryAsync(SalesBySalesPersonSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value },
                new SqlParameter("@territoryId", SqlDbType.Int) { Value = (object?)territoryId ?? DBNull.Value },
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = (object?)status ?? DBNull.Value });
        }

        // Find catalog products by optional product ID, category, or name.
        public async Task<ResponseData> GetProductCatalogAsync(
            int topN = 100, int? productId = null, int? categoryId = null, string? name = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name?.Length ?? 0, 50, nameof(name));
            return await ExecuteQueryAsync(ProductCatalogSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@categoryId", SqlDbType.Int) { Value = (object?)categoryId ?? DBNull.Value },
                new SqlParameter("@name", SqlDbType.NVarChar, 50) { Value = (object?)name ?? DBNull.Value });
        }

        // Get stock quantities for products at the selected inventory locations.
        public async Task<ResponseData> GetProductInventoryAsync(
            int topN = 100, int? productId = null, short? locationId = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(ProductInventorySql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@locationId", SqlDbType.SmallInt) { Value = (object?)locationId ?? DBNull.Value });
        }

        // Find products whose total stock is below their reorder point.
        public async Task<ResponseData> GetLowStockProductsAsync(
            int topN = 100, int? categoryId = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(LowStockProductsSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@categoryId", SqlDbType.Int) { Value = (object?)categoryId ?? DBNull.Value });
        }

        // Find vendors using optional ID, active-status, and name filters.
        public async Task<ResponseData> GetVendorsAsync(
            int topN = 100, int? vendorId = null, bool? activeOnly = null, string? name = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name?.Length ?? 0, 50, nameof(name));
            return await ExecuteQueryAsync(VendorsSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@vendorId", SqlDbType.Int) { Value = (object?)vendorId ?? DBNull.Value },
                new SqlParameter("@activeOnly", SqlDbType.Bit) { Value = (object?)activeOnly ?? DBNull.Value },
                new SqlParameter("@name", SqlDbType.NVarChar, 50) { Value = (object?)name ?? DBNull.Value });
        }

        // Get supplier information for the selected products and vendors.
        public async Task<ResponseData> GetProductSuppliersAsync(
            int topN = 100, int? productId = null, int? vendorId = null, bool? activeOnly = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(ProductSuppliersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@vendorId", SqlDbType.Int) { Value = (object?)vendorId ?? DBNull.Value },
                new SqlParameter("@activeOnly", SqlDbType.Bit) { Value = (object?)activeOnly ?? DBNull.Value });
        }

        // Get purchase orders matching the requested vendor, status, and dates.
        public async Task<ResponseData> GetPurchaseOrdersAsync(
            int topN = 100, int? vendorId = null, byte? status = null,
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            if (status is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(status));
            return await ExecuteQueryAsync(PurchaseOrdersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@vendorId", SqlDbType.Int) { Value = (object?)vendorId ?? DBNull.Value },
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = (object?)status ?? DBNull.Value },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Get the line items for one purchase order after checking its ID.
        public async Task<ResponseData> GetPurchaseOrderDetailsAsync(
            int purchaseOrderId, int topN = 100, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(purchaseOrderId);
            return await ExecuteQueryAsync(PurchaseOrderDetailsSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@purchaseOrderId", SqlDbType.Int) { Value = purchaseOrderId });
        }

        // Summarize purchase amounts by vendor for the requested dates and status.
        public async Task<ResponseData> GetPurchasingByVendorAsync(
            int topN = 100, DateTime? from = null, DateTime? to = null, byte? status = null,
            CancellationToken cancellationToken = default)
        {
            if (status is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(status));
            return await ExecuteQueryAsync(PurchasingByVendorSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value },
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = (object?)status ?? DBNull.Value });
        }

        // Get manufacturing work orders, optionally limited to open orders or a product.
        public async Task<ResponseData> GetWorkOrdersAsync(
            int topN = 100, int? productId = null, bool? openOnly = null,
            DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(WorkOrdersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@openOnly", SqlDbType.Bit) { Value = (object?)openOnly ?? DBNull.Value },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Summarize manufacturing scrap quantities by reason.
        public async Task<ResponseData> GetScrapByReasonAsync(
            int topN = 100, int? productId = null, DateTime? from = null, DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(ScrapByReasonSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@from", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
                new SqlParameter("@to", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value });
        }

        // Find current employees and their department assignments.
        public async Task<ResponseData> GetEmployeeDirectoryAsync(
            int topN = 100, int? employeeId = null, short? departmentId = null, string? name = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name?.Length ?? 0, 101, nameof(name));
            return await ExecuteQueryAsync(EmployeeDirectorySql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@employeeId", SqlDbType.Int) { Value = (object?)employeeId ?? DBNull.Value },
                new SqlParameter("@departmentId", SqlDbType.SmallInt) { Value = (object?)departmentId ?? DBNull.Value },
                new SqlParameter("@name", SqlDbType.NVarChar, 101) { Value = (object?)name ?? DBNull.Value });
        }

        // Count current employees in each department, including empty departments.
        public async Task<ResponseData> GetEmployeeCountByDepartmentAsync(
            int topN = 100, CancellationToken cancellationToken = default)
        {
            return await ExecuteQueryAsync(EmployeeCountByDepartmentSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN });
        }

        // Get product offers valid at the supplied timestamp after checking its SQL date range.
        public async Task<ResponseData> GetProductSpecialOffersAsync(
            DateTime asOf, int topN = 100, int? productId = null, CancellationToken cancellationToken = default)
        {
            if (asOf < System.Data.SqlTypes.SqlDateTime.MinValue.Value ||
                asOf > System.Data.SqlTypes.SqlDateTime.MaxValue.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(asOf), "The timestamp must be within the SQL datetime range.");
            }

            return await ExecuteQueryAsync(ProductSpecialOffersSql, cancellationToken,
                new SqlParameter("@topN", SqlDbType.Int) { Value = topN },
                new SqlParameter("@productId", SqlDbType.Int) { Value = (object?)productId ?? DBNull.Value },
                new SqlParameter("@asOf", SqlDbType.DateTime) { Value = asOf });
        }

        // Check shared limits, execute parameterized SQL through EF, and return rows keyed by column name.
        private async Task<ResponseData> ExecuteQueryAsync(
            string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameters.FirstOrDefault(p => p.ParameterName == "@topN")?.Value is int topN &&
                (topN < 1 || topN > 1000))
            {
                throw new ArgumentOutOfRangeException(nameof(topN), "The row limit must be between 1 and 1000.");
            }

            var from = parameters.FirstOrDefault(p => p.ParameterName == "@from")?.Value as DateTime?;
            var to = parameters.FirstOrDefault(p => p.ParameterName == "@to")?.Value as DateTime?;
            var minimum = System.Data.SqlTypes.SqlDateTime.MinValue.Value.Date;

            if (from.HasValue && from.Value < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(from), "The start date must be on or after 1753-01-01.");
            }

            if (to.HasValue && (to.Value < minimum || to.Value == DateTime.MaxValue.Date))
            {
                throw new ArgumentOutOfRangeException(nameof(to), "The end date must be on or after 1753-01-01 and before 9999-12-31.");
            }

            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                throw new ArgumentException("The end date must be on or after the start date.", nameof(to));
            }

            using var criticalSection = context.GetService<IConcurrencyDetector>().EnterCriticalSection();

            return await context.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                // EF binds the supplied parameters and manages the context connection and transaction.
                var command = context.GetService<IRawSqlCommandBuilder>().Build(sql, parameters, context.Model);
                var commandParameters = new RelationalCommandParameterObject(
                    context.GetService<IRelationalConnection>(), command.ParameterValues, null, context,
                    context.GetService<IRelationalCommandDiagnosticsLogger>(), CommandSource.FromSqlQuery);

                await using var result = await command.RelationalCommand.ExecuteReaderAsync(commandParameters, token);
                var reader = result.DbDataReader;
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                var data = new List<Dictionary<string, object?>>();

                while (await reader.ReadAsync(token))
                {
                    var row = new Dictionary<string, object?>(columns.Length, StringComparer.OrdinalIgnoreCase);
                    for (var index = 0; index < columns.Length; index++)
                    {
                        row.Add(columns[index], await reader.IsDBNullAsync(index, token) ? null : reader.GetValue(index));
                    }

                    data.Add(row);
                }

                return new ResponseData
                {
                    Data = data,
                    Message = data.Count == 0 ? "No data found." : $"Retrieved {data.Count} record(s)."
                };
            }, cancellationToken);
        }

    }
}
