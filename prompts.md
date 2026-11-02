# AdventureWorks Agent Prompts

Use these prompts with `POST /api/agent` through Swagger UI at `/swagger` or an HTTP client.

## How to use

In Swagger, expand `POST /api/agent`, select **Try it out**, and submit a JSON body such as:

```json
{
  "prompt": "Run the top 10 products by sales query for all available dates."
}
```

The API returns the agent's text in `message` and the actual database results in a `results` array. Each successful tool call adds an entry with:

- `tool`: the registered tool name, such as `sales_by_month_year`.
- `message`: the query status, such as `Retrieved 38 record(s).`.
- `count`: the number of rows returned by that query.
- `data`: an array of row objects, keyed by the SQL column names, containing the actual database values.

For monthly sales, read `results[0].data` for the monthly totals rather than parsing the agent's message. Rows come directly from the query result, not from model-generated text, and the query is not executed again to populate the response. Numbers remain JSON numbers and database nulls become JSON nulls.

Each successful invocation has its own entry, including repeated calls to the same tool. A query with no matching rows returns `count: 0`, `data: []`, and `message: "No data found."`. If no query succeeds (for example, the agent asks for clarification or all calls fail), `results` is empty. Failed calls do not add successful result entries; check the agent message for reported errors, including partial failures.

The model receives query status messages, not the full rows, keeping large result sets out of its context. Its message summarizes execution; the API's structured `data` contains the business values.

## Before testing

The agent now asks for missing report, territory, or date choices before querying. In the Blazor chat, try “Show revenue by region”, followed by “Northwest” and “all available dates” when prompted. It remembers your earlier answers in that chat. A complete prompt such as “Show revenue for Northwest using all available dates” should run directly.

For HTTP or Swagger follow-ups, include an optional `history` array of previous `{ "role": "user" or "assistant", "text": "..." }` messages alongside the new `prompt`. Use actual previous messages, not result rows. History permits up to 20 prior messages, 8,000 characters each, and 40,000 total characters. Omit history for a new conversation. See `AdventureWorksTool/AdventureWorksTool.http` for an example.

- Configure `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, and `AzureOpenAI:DeploymentName`, plus the database connection string.
- Use an Azure OpenAI deployment that supports chat completions and function calling.
- Start with prompts covering **all available dates** if you do not know the database's date range.
- Dates below are examples; the database name does not guarantee which years contain data.
- Customer, product, order, vendor, employee, department, and location IDs below are examples. Replace them with valid IDs from your database, available in the structured `data` returned by lookup queries.
- Date ranges are inclusive calendar dates. Use `yyyy-MM-dd`, with the start date on or after `1753-01-01` and the end date before `9999-12-31`.
- Bounded queries accept between **1 and 1,000** rows and default to **100**. Limits apply to returned rows, not necessarily distinct products or people.

## Sales summaries

### Monthly sales — `sales_by_month_year`

- Run the monthly sales totals query for all available dates.
- Run the monthly sales query from 2014-01-01 through 2014-12-31.

### Best-selling products — `top_products_by_sales`

- Run the top 10 products by sales query for all available dates.
- Run the top 5 products by sales query from 2014-01-01 through 2014-06-30.

### Sales by category — `sales_by_category`

- Run the sales totals query grouped by product category for all available dates.
- Run sales by category from 2014-01-01 through 2014-12-31.

### Revenue by region — `revenue_by_region`

- Run the revenue query grouped by sales territory for all available dates.
- Run the revenue-by-region query for the country code US, using all available dates.
- Run the revenue-by-region query for the Europe territory group from 2014-01-01 through 2014-12-31.
- Run the revenue-by-region query for the Northwest territory, using all available dates.

Sales and revenue summaries exclude tax and freight. The original monthly, product, category, and region summary tools do not accept an order-status filter.

## Customers and sales orders

### Customer lookup — `customers`

- Run the customer lookup for customer ID 11000.
- Run the customer lookup for territory ID 1, returning at most 20 customers.
- Run the customer lookup for names containing the literal text 'Bike', returning at most 10 customers.

### Highest-value customers — `top_customers_by_sales`

- Run the top 10 customers by sales subtotal query for all dates and all order statuses.
- Run the top 5 customers by sales query for shipped orders from 2014-01-01 through 2014-12-31.

### Sales order lookup — `sales_orders`

- Run the latest 10 sales orders query for customer ID 11000, using all available dates.
- Run the latest 20 shipped sales orders query from 2014-01-01 through 2014-12-31.
- Run the latest 10 backordered sales orders query for all customers and all available dates.

### Sales order line items — `sales_order_details`

- Run the sales order details query for sales order ID 43659, returning up to 100 lines.

A positive numeric sales order ID is required, not a formatted sales order number. The row limit can truncate large orders.

### Salesperson performance — `sales_by_sales_person`

- Run the top 10 salespeople by sales subtotal query for all dates and all order statuses.
- Run the top 5 salespeople by shipped sales query for order territory ID 1 from 2014-01-01 through 2014-12-31.

Sales order statuses: **1** In process, **2** Approved, **3** Backordered, **4** Rejected, **5** Shipped, **6** Cancelled. Omitted status filters include all statuses. Salesperson territory filtering uses the territory on the order, not the salesperson's current assignment.

## Products and inventory

### Product catalog — `product_catalog`

- Run the product catalog lookup for product ID 771.
- Run the product catalog query for names containing the literal text 'Mountain', returning at most 20 products.
- Run the product catalog query for category ID 1, returning at most 10 products.

The catalog includes discontinued products; there is no active-products-only filter.

### Stock by location — `product_inventory`

- Run the inventory query for product ID 771 across all locations, returning at most 100 rows.
- Run the inventory query for location ID 1, returning at most 20 product-location rows.

### Products below reorder point — `low_stock_products`

- Run the low-stock query for the 10 products with the largest reorder shortfalls across all categories.
- Run the low-stock query for category ID 1, returning at most 20 products.

Low stock is determined by summing inventory across all locations and comparing it with each product's reorder point. Missing inventory counts as zero.

## Vendors and purchasing

### Vendor lookup — `vendors`

- Run the vendor lookup for the first 20 active vendors.
- Run the vendor lookup for names containing the literal text 'Bike', including inactive vendors, with a limit of 10.

### Product suppliers — `product_suppliers`

- Run the supplier query for product ID 771, including only active vendors and returning at most 20 rows.
- Run the product-supplier query for vendor ID 1492, including inactive vendors and returning at most 20 rows.

### Purchase order lookup — `purchase_orders`

- Run the latest 10 purchase orders query for all vendors, dates, and statuses.
- Run the latest 20 approved purchase orders query for vendor ID 1492 from 2014-01-01 through 2014-12-31.

### Purchase order line items — `purchase_order_details`

- Run the purchase order details query for purchase order ID 1, returning up to 100 lines.

A positive purchase order ID is required. The row limit can truncate large orders.

### Purchasing totals by vendor — `purchasing_by_vendor`

- Run the top 10 vendors by total purchase amount query for all dates and all purchase order statuses.
- Run the top 5 vendors by total purchase amount query for completed purchase orders from 2014-01-01 through 2014-12-31.

Purchase order statuses: **1** Pending, **2** Approved, **3** Rejected, **4** Complete. These differ from sales order statuses. Total purchase amount includes tax and freight; purchase subtotal does not.

## Production

### Manufacturing work orders — `work_orders`

- Run the latest 20 open manufacturing work orders query for all products and all start dates.
- Run the manufacturing work orders query for product ID 771 with start dates from 2014-01-01 through 2014-12-31, including completed orders and returning at most 20 rows.

Open work orders have no end date. Date filters apply to the work order's start date, not its due or completion date.

### Scrap by reason — `scrap_by_reason`

- Run the scrap summary query grouped by reason for all products and all work order start dates, returning at most 10 reasons.
- Run the scrap-by-reason query for product ID 771 with work order start dates from 2014-01-01 through 2014-12-31, returning at most 10 reasons.

## Employees

### Current employee directory — `employee_directory`

- Run the current employee directory query for employee ID 1.
- Run the current employee directory query for department ID 1, returning at most 20 assignment rows.
- Run the current employee directory query for names containing the literal text 'David', returning at most 10 rows.

The directory queries current employees and current department assignments. It does not query salaries or national ID numbers.

### Department headcounts — `employee_count_by_department`

- Run the current employee headcount query grouped by department, returning at most 100 departments.

This counts each current employee once per department and includes departments with no current employees.

## Product special offers

### Offers valid at an instant — `product_special_offers`

- Run the product special offers query as of 2014-06-01T12:00:00 for all products, returning at most 20 product-offer rows.
- Run the special offers query for product ID 771 as of 2014-06-01T12:00:00, returning at most 10 rows.

An explicit timestamp is required. Use the database's time convention; the tool does not perform timezone conversion.

## Clarification and unsupported-request examples

- Run the details query for a sales order. — The agent should ask for the required order ID.
- Run the product offers query. — The agent should ask for the required timestamp.
- Delete customer ID 11000. — Unsupported: the tools are read-only.
- Update the price of product ID 771. — Unsupported: no update tool exists.
- Give me the actual product names and sales totals from the last query. — Not available through the current Message-only tool responses; the agent should explain this rather than invent values.

## API validation examples

These are request-body checks, not natural-language prompts:

- `{ "prompt": "" }` — returns HTTP 400 validation details.
- `{ "prompt": "   " }` — returns HTTP 400 validation details.
- A prompt longer than 8,000 characters after trimming — returns HTTP 400 validation details.

Configure the Azure OpenAI client before testing: its dependency is resolved before the endpoint handler validates the prompt.
