using AdventureWorksTool.Queries;
using AdventureWorksTool.Responses;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AdventureWorksTool.AgentTools
{
    public sealed class AdventureworksTool
    {
        private readonly AdventureWorksQueryProcessor _queries;
        private readonly List<(string Tool, ResponseData Response)> _results = [];

        public IReadOnlyList<(string Tool, ResponseData Response)> Results => _results.AsReadOnly();

        // Store the query processor used by all tools in this request.
        public AdventureworksTool(AdventureWorksQueryProcessor queries)
        {
            ArgumentNullException.ThrowIfNull(queries);
            _queries = queries;
        }

        // Keep the actual rows for the API response and give the agent only the query status.
        private string CaptureResult(string tool, ResponseData response)
        {
            _results.Add((tool, response));
            return response.Message;
        }

        // Register the methods and their names so the agent can choose which tool to call.
        public IList<AITool> AsTools() =>
        [
            AIFunctionFactory.Create(SalesByMonthYearAsync, "sales_by_month_year"),
            AIFunctionFactory.Create(TopProductsBySalesAsync, "top_products_by_sales"),
            AIFunctionFactory.Create(SalesByCategoryAsync, "sales_by_category"),
            AIFunctionFactory.Create(RevenueByTerritoryAsync, "revenue_by_region"),
            AIFunctionFactory.Create(CustomersAsync, "customers"),
            AIFunctionFactory.Create(TopCustomersBySalesAsync, "top_customers_by_sales"),
            AIFunctionFactory.Create(SalesOrdersAsync, "sales_orders"),
            AIFunctionFactory.Create(SalesOrderDetailsAsync, "sales_order_details"),
            AIFunctionFactory.Create(SalesBySalesPersonAsync, "sales_by_sales_person"),
            AIFunctionFactory.Create(ProductCatalogAsync, "product_catalog"),
            AIFunctionFactory.Create(ProductInventoryAsync, "product_inventory"),
            AIFunctionFactory.Create(LowStockProductsAsync, "low_stock_products"),
            AIFunctionFactory.Create(VendorsAsync, "vendors"),
            AIFunctionFactory.Create(ProductSuppliersAsync, "product_suppliers"),
            AIFunctionFactory.Create(PurchaseOrdersAsync, "purchase_orders"),
            AIFunctionFactory.Create(PurchaseOrderDetailsAsync, "purchase_order_details"),
            AIFunctionFactory.Create(PurchasingByVendorAsync, "purchasing_by_vendor"),
            AIFunctionFactory.Create(WorkOrdersAsync, "work_orders"),
            AIFunctionFactory.Create(ScrapByReasonAsync, "scrap_by_reason"),
            AIFunctionFactory.Create(EmployeeDirectoryAsync, "employee_directory"),
            AIFunctionFactory.Create(EmployeeCountByDepartmentAsync, "employee_count_by_department"),
            AIFunctionFactory.Create(ProductSpecialOffersAsync, "product_special_offers")
        ];

        [Description("Run the AdventureWorks sales query grouped by calendar month (yyyy-MM) for monthly trends. " +
                     "Sales exclude tax and freight. Returns a status message; actual rows are included separately in the API results.")]
        // Run the monthly sales query and save its result for the API response.
        public async Task<string> SalesByMonthYearAsync(
            [Description("Inclusive start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("sales_by_month_year", await _queries.GetSalesByMonthYearAsync(from, to, cancellationToken));

        [Description("Run the AdventureWorks best-selling product query ranked by sales value before tax and freight. " +
                     "Products are specific variants, such as a bike's color and size. Returns a status message; actual rows are included separately in the API results.")]
        // Run the top-selling products query and save its result.
        public async Task<string> TopProductsBySalesAsync(
            [Description("Maximum products to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Inclusive start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("top_products_by_sales", await _queries.GetTopProductsBySalesAsync(topN, from, to, cancellationToken));

        [Description("Run the AdventureWorks sales query grouped by top-level product category, such as Bikes, Components, " +
                     "Clothing, or Accessories. Sales exclude tax and freight. Returns a status message; actual rows are included separately in the API results.")]
        // Run the category sales query and save its result.
        public async Task<string> SalesByCategoryAsync(
            [Description("Inclusive start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("sales_by_category", await _queries.GetSalesByCategoryAsync(from, to, cancellationToken));

        [Description("Run the AdventureWorks revenue query grouped by sales territory, optionally restricted to a territory, " +
                     "country, or territory group. Revenue excludes tax and freight. Returns a status message; actual rows are included separately in the API results.")]
        // Pass the region and dates to the revenue query and save its result.
        public async Task<string> RevenueByTerritoryAsync(
            [Description("Exact territory name (Northwest), country code (US), or group (Europe). Maximum 50 characters. Null includes all territories.")] string? region = null,
            [Description("Inclusive start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("revenue_by_region", await _queries.GetRevenueByRegionAsync(region, from, to, cancellationToken));

        [Description("Run the AdventureWorks customer lookup for individual or store customers, with optional ID, territory, " +
                     "and name filters. Returns a status message; actual rows are included separately in the API results.")]
        // Run the customer lookup with the supplied filters and save its result.
        public async Task<string> CustomersAsync(
            [Description("Maximum customers to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Customer ID to match. Null includes all customers.")] int? customerId = null,
            [Description("Customer territory ID to match. Null includes all territories.")] int? territoryId = null,
            [Description("Literal substring of the store or person's full name, up to 101 characters. Null disables the name filter.")] string? name = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("customers", await _queries.GetCustomersAsync(topN, customerId, territoryId, name, cancellationToken));

        [Description("Run the AdventureWorks customer ranking by sales subtotal, including order counts and last order dates. " +
                     "Sales exclude tax and freight. Returns a status message; actual rows are included separately in the API results.")]
        // Run the customer sales ranking query and save its result.
        public async Task<string> TopCustomersBySalesAsync(
            [Description("Maximum customers to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Inclusive order start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive order end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            [Description("Sales order status: 1 In process, 2 Approved, 3 Backordered, 4 Rejected, 5 Shipped, 6 Cancelled. Null includes all statuses.")] byte? status = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("top_customers_by_sales", await _queries.GetTopCustomersBySalesAsync(topN, from, to, status, cancellationToken));

        [Description("Run the AdventureWorks sales order lookup, newest orders first, including shipping dates, order status, " +
                     "subtotal, tax, freight, and total due. Returns a status message; actual rows are included separately in the API results.")]
        // Run the sales order lookup and save its result.
        public async Task<string> SalesOrdersAsync(
            [Description("Maximum orders to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Customer ID whose orders to query. Null includes all customers.")] int? customerId = null,
            [Description("Sales order status: 1 In process, 2 Approved, 3 Backordered, 4 Rejected, 5 Shipped, 6 Cancelled. Null includes all statuses.")] byte? status = null,
            [Description("Inclusive order start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive order end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("sales_orders", await _queries.GetSalesOrdersAsync(topN, customerId, status, from, to, cancellationToken));

        [Description("Run the line-item query for one AdventureWorks sales order, including products, quantities, unit prices, " +
                     "discounts, and line totals. Returns a status message; actual rows are included separately in the API results.")]
        // Get the line items for the requested sales order and save its result.
        public async Task<string> SalesOrderDetailsAsync(
            [Description("Required positive sales order ID, not the sales order number.")] int salesOrderId,
            [Description("Maximum order lines to return, from 1 to 1000. Defaults to 100; larger orders may be truncated.")] int topN = 100,
            CancellationToken cancellationToken = default)
            => CaptureResult("sales_order_details", await _queries.GetSalesOrderDetailsAsync(salesOrderId, topN, cancellationToken));

        [Description("Run the AdventureWorks salesperson ranking by order sales subtotal, optionally filtered by order territory " +
                     "and status. Excludes orders without a salesperson. Returns a status message; actual rows are included separately in the API results.")]
        // Run the salesperson sales query and save its result.
        public async Task<string> SalesBySalesPersonAsync(
            [Description("Maximum salespeople to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Inclusive order start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive order end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            [Description("Territory ID on the sales orders, not the salesperson's current territory. Null includes all territories.")] int? territoryId = null,
            [Description("Sales order status: 1 In process, 2 Approved, 3 Backordered, 4 Rejected, 5 Shipped, 6 Cancelled. Null includes all statuses.")] byte? status = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("sales_by_sales_person", await _queries.GetSalesBySalesPersonAsync(topN, from, to, territoryId, status, cancellationToken));

        [Description("Run the AdventureWorks product catalog lookup with category, subcategory, variant details, prices, costs, " +
                     "and selling dates. Includes discontinued products. Returns a status message; actual rows are included separately in the API results.")]
        // Run the product catalog lookup and save its result.
        public async Task<string> ProductCatalogAsync(
            [Description("Maximum products to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Product ID to match. Null includes all products.")] int? productId = null,
            [Description("Top-level product category ID to match. Null includes all categories and uncategorized products.")] int? categoryId = null,
            [Description("Literal substring of the product name, up to 50 characters. Null disables the name filter.")] string? name = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("product_catalog", await _queries.GetProductCatalogAsync(topN, productId, categoryId, name, cancellationToken));

        [Description("Run the AdventureWorks inventory lookup with one row per product and location, including shelf, bin, " +
                     "quantity, safety stock, and reorder point. Returns a status message; actual rows are included separately in the API results.")]
        // Run the inventory query for the requested products and locations and save its result.
        public async Task<string> ProductInventoryAsync(
            [Description("Maximum product-location rows to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Product ID to match. Null includes all stocked products.")] int? productId = null,
            [Description("Inventory location ID to match. Null includes all locations.")] short? locationId = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("product_inventory", await _queries.GetProductInventoryAsync(topN, productId, locationId, cancellationToken));

        [Description("Run the AdventureWorks low-stock query for products below their reorder point, summing stock across all " +
                     "locations and treating missing stock as zero. Largest shortfalls come first. Returns a status message; actual rows are included separately in the API results.")]
        // Run the low-stock product query and save its result.
        public async Task<string> LowStockProductsAsync(
            [Description("Maximum products to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Top-level product category ID to match. Null includes all categories.")] int? categoryId = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("low_stock_products", await _queries.GetLowStockProductsAsync(topN, categoryId, cancellationToken));

        [Description("Run the AdventureWorks vendor lookup including credit rating, preferred-vendor status, and active flag. " +
                     "Returns a status message; actual rows are included separately in the API results.")]
        // Run the vendor lookup and save its result.
        public async Task<string> VendorsAsync(
            [Description("Maximum vendors to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Vendor business entity ID to match. Null includes all vendors.")] int? vendorId = null,
            [Description("True restricts results to active vendors. False or null also includes inactive vendors.")] bool? activeOnly = null,
            [Description("Literal substring of the vendor name, up to 50 characters. Null disables the name filter.")] string? name = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("vendors", await _queries.GetVendorsAsync(topN, vendorId, activeOnly, name, cancellationToken));

        [Description("Run the AdventureWorks product-supplier lookup including standard prices, lead times, order quantity " +
                     "limits, quantities on order, and units of measure. Returns a status message; actual rows are included separately in the API results.")]
        // Run the product supplier lookup and save its result.
        public async Task<string> ProductSuppliersAsync(
            [Description("Maximum product-vendor rows to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Product ID whose suppliers to query. Null includes all supplied products.")] int? productId = null,
            [Description("Vendor business entity ID to match. Null includes all vendors.")] int? vendorId = null,
            [Description("True restricts results to active vendors. False or null also includes inactive vendors.")] bool? activeOnly = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("product_suppliers", await _queries.GetProductSuppliersAsync(topN, productId, vendorId, activeOnly, cancellationToken));

        [Description("Run the AdventureWorks purchase order lookup, newest orders first, including vendor, expected shipment " +
                     "date, status, subtotal, tax, freight, and total due. Returns a status message; actual rows are included separately in the API results.")]
        // Run the purchase order lookup and save its result.
        public async Task<string> PurchaseOrdersAsync(
            [Description("Maximum purchase orders to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Vendor business entity ID whose purchase orders to query. Null includes all vendors.")] int? vendorId = null,
            [Description("Purchase order status: 1 Pending, 2 Approved, 3 Rejected, 4 Complete. Null includes all statuses.")] byte? status = null,
            [Description("Inclusive order start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive order end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("purchase_orders", await _queries.GetPurchaseOrdersAsync(topN, vendorId, status, from, to, cancellationToken));

        [Description("Run the line-item query for one AdventureWorks purchase order, including products, due dates, ordered " +
                     "quantities, prices, and received, rejected, and stocked quantities. Returns a status message; actual rows are included separately in the API results.")]
        // Get the line items for the requested purchase order and save its result.
        public async Task<string> PurchaseOrderDetailsAsync(
            [Description("Required positive purchase order ID.")] int purchaseOrderId,
            [Description("Maximum order lines to return, from 1 to 1000. Defaults to 100; larger orders may be truncated.")] int topN = 100,
            CancellationToken cancellationToken = default)
            => CaptureResult("purchase_order_details", await _queries.GetPurchaseOrderDetailsAsync(purchaseOrderId, topN, cancellationToken));

        [Description("Run the AdventureWorks vendor ranking by total purchase amount, including order counts and subtotals. " +
                     "Total purchase amount includes tax and freight. Returns a status message; actual rows are included separately in the API results.")]
        // Run the vendor purchasing summary and save its result.
        public async Task<string> PurchasingByVendorAsync(
            [Description("Maximum vendors to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Inclusive order start date (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive order end date (yyyy-MM-dd), before 9999-12-31 and not before the start date. Null for no upper bound.")] DateTime? to = null,
            [Description("Purchase order status: 1 Pending, 2 Approved, 3 Rejected, 4 Complete. Null includes all statuses.")] byte? status = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("purchasing_by_vendor", await _queries.GetPurchasingByVendorAsync(topN, from, to, status, cancellationToken));

        [Description("Run the AdventureWorks manufacturing work order lookup, newest starts first, including quantities, " +
                     "start/due/end dates, and scrap reasons. Returns a status message; actual rows are included separately in the API results.")]
        // Run the manufacturing work order lookup and save its result.
        public async Task<string> WorkOrdersAsync(
            [Description("Maximum work orders to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Manufactured product ID to match. Null includes all products.")] int? productId = null,
            [Description("True includes only work orders with no end date. False or null also includes completed orders.")] bool? openOnly = null,
            [Description("Inclusive work-order start-date lower bound (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive work-order start-date upper bound (yyyy-MM-dd), before 9999-12-31 and not before the lower bound. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("work_orders", await _queries.GetWorkOrdersAsync(topN, productId, openOnly, from, to, cancellationToken));

        [Description("Run the AdventureWorks manufacturing scrap summary grouped by reason, including affected work orders " +
                     "and scrapped quantities. Includes unspecified reasons. Returns a status message; actual rows are included separately in the API results.")]
        // Run the scrap summary query and save its result.
        public async Task<string> ScrapByReasonAsync(
            [Description("Maximum scrap-reason groups to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Manufactured product ID to match. Null includes all products.")] int? productId = null,
            [Description("Inclusive work-order start-date lower bound (yyyy-MM-dd), on or after 1753-01-01. Null for no lower bound.")] DateTime? from = null,
            [Description("Inclusive work-order start-date upper bound (yyyy-MM-dd), before 9999-12-31 and not before the lower bound. Null for no upper bound.")] DateTime? to = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("scrap_by_reason", await _queries.GetScrapByReasonAsync(topN, productId, from, to, cancellationToken));

        [Description("Run the AdventureWorks current employee directory lookup with job titles and current department/shift " +
                     "assignments. Does not query salary or national ID numbers. Returns a status message; actual rows are included separately in the API results.")]
        // Run the employee directory lookup and save its result.
        public async Task<string> EmployeeDirectoryAsync(
            [Description("Maximum employee-assignment rows to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Employee business entity ID to match. Null includes all current employees.")] int? employeeId = null,
            [Description("Current department ID to match. Null includes all departments and unassigned employees.")] short? departmentId = null,
            [Description("Literal substring of the employee's full name, up to 101 characters. Null disables the name filter.")] string? name = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("employee_directory", await _queries.GetEmployeeDirectoryAsync(topN, employeeId, departmentId, name, cancellationToken));

        [Description("Run the AdventureWorks current employee headcount query grouped by department. Counts each employee " +
                     "once per department and includes empty departments. Returns a status message; actual rows are included separately in the API results.")]
        // Run the department headcount query and save its result.
        public async Task<string> EmployeeCountByDepartmentAsync(
            [Description("Maximum departments to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            CancellationToken cancellationToken = default)
            => CaptureResult("employee_count_by_department", await _queries.GetEmployeeCountByDepartmentAsync(topN, cancellationToken));

        [Description("Run the AdventureWorks product special-offer query for offers valid at a specified instant, including " +
                     "discounts, eligible quantity ranges, and validity dates. Returns a status message; actual rows are included separately in the API results.")]
        // Run the offers query for the supplied timestamp and save its result.
        public async Task<string> ProductSpecialOffersAsync(
            [Description("Required timestamp in the database's time convention, formatted as yyyy-MM-ddTHH:mm:ss. Must be within SQL datetime range (1753-01-01 through 9999-12-31 23:59:59.997).")] DateTime asOf,
            [Description("Maximum product-offer rows to return, from 1 to 1000. Defaults to 100.")] int topN = 100,
            [Description("Product ID whose offers to query. Null includes all products with matching offers.")] int? productId = null,
            CancellationToken cancellationToken = default)
            => CaptureResult("product_special_offers", await _queries.GetProductSpecialOffersAsync(asOf, topN, productId, cancellationToken));
    }
}
