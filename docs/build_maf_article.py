"""Build the Blogger article from authored prose and an explicit, nonsecret source allowlist."""
from pathlib import Path
from html import escape
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
parts = []
SOURCE_FILES = [
    'AdventureWorksTool/AdventureWorksTool.csproj',
    'AdventureWorksTool/Models/EntityBase.cs',
    'AdventureWorksTool/Responses/ResponseData.cs',
    'AdventureWorksTool/Queries/AdventureWorksQueryProcessor.cs',
    'AdventureWorksTool/AgentTools/AdventureworksTool.cs',
    'AdventureWorksTool/Program.cs',
    'AdventureWorksClient/AdventureWorksClient.csproj',
    'AdventureWorksClient/Contracts/AgentResponse.cs',
    'AdventureWorksClient/Services/AgentApiClient.cs',
    'AdventureWorksClient/Program.cs',
    'AdventureWorksClient/_Imports.razor',
    'AdventureWorksClient/App.razor',
    'AdventureWorksClient/Components/QueryResultTable.razor',
    'AdventureWorksClient/Components/AgentChat.razor',
    'AdventureWorksTool/Properties/launchSettings.json',
    'AdventureWorksClient/Properties/launchSettings.json',
    'AdventureWorksTool/AdventureWorksTool.http',
]


def add(html):
    parts.append(html.strip())


def code(text, language='csharp', source=None):
    attribute = f' data-source="{escape(source, quote=True)}"' if source else ''
    add(f'<pre style="overflow-x:auto; white-space:pre;"><code class="language-{language}"{attribute}>{escape(text, quote=False)}</code></pre>')


def source(path, language='csharp'):
    if path not in SOURCE_FILES:
        raise ValueError(f'Source is not approved for publication: {path}')
    add(f'<h3>{escape(path)}</h3>')
    code((ROOT / path).read_text(encoding='utf-8-sig'), language, path)


def table(headers, rows):
    add('<table border="1" cellpadding="6"><thead><tr>' + ''.join('<th scope="col">' + escape(h) + '</th>' for h in headers) + '</tr></thead><tbody>')
    for row in rows:
        add('<tr>' + ''.join('<td>' + escape(str(cell)) + '</td>' for cell in row) + '</tr>')
    add('</tbody></table>')


add('''<article>
<h1>Build an AdventureWorks AI Agent with Microsoft Agent Framework, ASP.NET Core 10, and Blazor WebAssembly</h1>
<p>A useful business assistant must do more than generate a convincing paragraph. It must understand a request, ask for missing details, choose an allowed operation, and return trustworthy data. In this walkthrough, we build that assistant around AdventureWorks using a .NET 10 API, Azure OpenAI, Microsoft Agent Framework, and a Blazor WebAssembly chat interface.</p>
<p>The solution is named <strong>AdventureWorks_Agents</strong>. Its API project is <strong>AdventureWorksTool</strong>, and its browser application is <strong>AdventureWorksClient</strong>. The agent selects from 22 read-only database tools. The browser displays the agent's message and the actual SQL result rows separately.</p>
<h2>What is Microsoft Agent Framework?</h2>
<p>The official product name is <strong>Microsoft Agent Framework</strong>, abbreviated as <strong>MAF</strong> in this article. “Microsoft Agentic Framework” describes the idea, but is not the official name. It is an SDK for building agents and agent workflows. An agent combines model access, instructions, tools, and conversation context so it can decide how to respond or which application capability to invoke.</p>
<p>A basic chatbot generates text from messages. A tool-calling agent can also ask the application to execute a described function, receive its result, and continue the interaction. The language model proposes tool calls; application code executes them. The model does not directly connect to SQL Server.</p>
<p>Our agent is a constrained, single-agent application. It is not a multi-agent workflow, a background autonomous worker, a vector-search/RAG system, or a text-to-SQL generator. All SQL is predefined by the developer. MAF supports broader agent and workflow scenarios, but this example deliberately uses a small subset that is easy to understand.</p>
<h2>Advantages of Microsoft Agent Framework</h2>
<ul>
<li><strong>Agent abstraction:</strong> application code works with an agent rather than manually treating every exchange as an unrelated completion.</li>
<li><strong>Tools from existing methods:</strong> ordinary .NET methods become capabilities the model can select. We reuse an existing query processor instead of asking the model to invent SQL.</li>
<li><strong>Conversation-aware interaction:</strong> the agent can interpret “Northwest” as an answer to a region question when previous messages are supplied.</li>
<li><strong>Composition with Microsoft.Extensions.AI:</strong> the IChatClient abstraction and its middleware let us configure function invocation independently of the Azure client connection.</li>
<li><strong>Controlled execution:</strong> registered tools, application validation, cancellation, and iteration settings constrain the available work. These controls complement, rather than replace, authentication and database permissions.</li>
<li><strong>Room to grow:</strong> additional tools, observability, streaming, or coordinated workflows can be introduced later. They are capabilities of the ecosystem, not features automatically enabled by the code below.</li>
</ul>
<h2>Implementation architecture</h2>
<figure id="architecture-image-placeholder">
<p><strong>[IMAGE PLACEHOLDER: insert the exported MAF architecture image here]</strong></p>
<!-- Upload the image exported from MAF-Architecture.pptx to Blogger and replace this placeholder with Blogger's image markup. -->
<figcaption>Figure 1. Browser prompt and history → API validation → agent and Azure OpenAI → registered read-only tools → EF Core and SQL Server. Status returns to the model; actual rows return directly to the browser.</figcaption>
</figure>
<p>The companion <strong>MAF-Architecture.pptx</strong> contains an editable architecture slide and a follow-up conversation slide. Export the architecture slide as a PNG and insert it at the placeholder. The diagram shows an important split: full business rows are not sent back to the model in this implementation.</p>
<ol>
<li>The browser sends a prompt and previous successful user/assistant text messages to POST /api/agent.</li>
<li>The API validates them and creates a request-specific ChatClientAgent with the available tools.</li>
<li>The configured Azure OpenAI deployment either produces a clarification question or requests a tool call.</li>
<li>The function-invocation middleware calls AdventureworksTool, which calls AdventureWorksQueryProcessor.</li>
<li>EF executes parameterized SELECT statements. ResponseData contains a status message and named-column rows.</li>
<li>The tool stores ResponseData in its request-scoped collection and returns only the status string to the model.</li>
<li>The API sends the agent's final message plus the stored results to the browser. Blazor renders the real rows as tables.</li>
</ol>
<h2>Prerequisites and scope</h2>
<ul>
<li>Visual Studio with .NET 10 and ASP.NET/Blazor development support, or the .NET 10 SDK and a suitable editor.</li>
<li>SQL Server with an AdventureWorks database containing the Sales, Production, Purchasing, HumanResources, and Person schemas used below. This workspace calls its context AdventureWorks2025Context; a database name does not guarantee its sales date range.</li>
<li>An Azure OpenAI resource and a deployed model supporting chat completions and function calling. Use your actual deployment name, not a hard-coded model name from an unrelated example.</li>
<li>An account permitted to query the required tables. Use a read-only database identity for deployment.</li>
</ul>
<p>The package versions shown are the versions used by this workspace, not a claim that they are the latest releases. SDK APIs can change between versions. The full custom implementation is included below; generated EF entity/mapping files and standard template boilerplate are intentionally omitted. No CSS source is included. The UI remains functional without the optional visual styling.</p>
<h2>Step 1: Create a new Blank Solution named AdventureWorks_Agents</h2>
<p>In Visual Studio, select <strong>Create a new project → Blank Solution</strong>, enter <strong>AdventureWorks_Agents</strong>, and choose a working directory. Keep both projects under this solution. Command-line readers can use the following equivalent command from that directory:</p>''')
code('dotnet new sln --name AdventureWorks_Agents --format slnx', 'powershell')
add('''<h2>Step 2: Add an ASP.NET Core 10 API project named AdventureWorksTool</h2>
<p>Add an <strong>ASP.NET Core Web API</strong> project. Select .NET 10, HTTPS, OpenAPI support, no template authentication for this local tutorial, and minimal APIs rather than controllers. Remove the sample WeatherForecast endpoint and its sample types. We replace Program.cs completely in a later step.</p>''')
code('dotnet new webapi --name AdventureWorksTool --framework net10.0 --use-controllers false\ndotnet sln AdventureWorks_Agents.slnx add AdventureWorksTool/AdventureWorksTool.csproj', 'powershell')
add('''<p>Create folders named <code>Models</code>, <code>Queries</code>, <code>Responses</code>, and <code>AgentTools</code>. These separate database scaffolding, query execution, the result envelope, and agent-callable functions.</p>
<h2>Step 3: Install the packages and understand their roles</h2>
<p>A NuGet package, a namespace, and a type are different things. A package can supply types in several namespaces, and multiple packages can contribute APIs to the same namespace. The following table explains the requested package versions and the namespaces relevant to this application.</p>''')
table(['Package and version', 'Relevant namespace/API', 'Purpose in this solution'], [
    ('Azure.AI.OpenAI 2.1.0', 'Azure.AI.OpenAI; AzureOpenAIClient', 'Connects to Azure OpenAI. GetChatClient(deploymentName) returns the deployment-specific OpenAI chat client.'),
    ('Microsoft.Agents.AI 1.27.0', 'Microsoft.Agents.AI', 'Provides the agent layer used here: AIAgent, ChatClientAgent, and ChatClientAgentOptions, including types supplied through dependencies.'),
    ('Microsoft.AspNetCore.OpenApi 10.0.14', 'ASP.NET Core OpenAPI extensions; Microsoft.Extensions.DependencyInjection and Microsoft.AspNetCore.Builder at call sites', 'AddOpenApi registers document generation; MapOpenApi exposes the document. This package is not the Swagger UI itself.'),
    ('Microsoft.Extensions.AI 10.11.0', 'Microsoft.Extensions.AI', 'Chat abstractions and middleware: IChatClient, ChatOptions, ChatMessage, ChatRole, AITool, AIFunctionFactory and function invocation. Some abstractions are supplied by its abstractions dependency.'),
    ('Microsoft.Extensions.AI.OpenAI 10.11.0', 'Microsoft.Extensions.AI extension methods', 'Adapts the OpenAI SDK ChatClient to IChatClient through AsIChatClient(). The package name is not an extra using statement required by this code.'),
    ('System.ClientModel (dependency)', 'System.ClientModel; ApiKeyCredential', 'Represents the Azure OpenAI API-key credential. It is not defined in Azure.AI.OpenAI.'),
    ('System.ComponentModel (framework API)', 'System.ComponentModel; DescriptionAttribute', 'Supplies human-readable descriptions of tool methods and parameters.'),
    ('EF Core packages 10.0.14', 'Microsoft.EntityFrameworkCore and its Storage/Infrastructure APIs', 'SQL Server configuration, database scaffolding, execution strategies, relational commands, and data reading.'),
    ('Swashbuckle.AspNetCore.SwaggerUI 10.2.3', 'ASP.NET Core application-builder extensions', 'Displays the built-in OpenAPI document in a browser. We do not add a separate SwaggerGen pipeline.'),
])
add('<p>The complete API project file below fixes the versions used in this implementation. Restore packages after updating it. Azure.Identity is present in the workspace but the demonstrated connection uses ApiKeyCredential, not token authentication. The explicit System.Security.Cryptography.Pkcs reference is an existing dependency pin, not an agent API.</p>')
source('AdventureWorksTool/AdventureWorksTool.csproj', 'xml')
code('dotnet restore AdventureWorksTool/AdventureWorksTool.csproj', 'powershell')
add('''<h2>Step 4: Understand the important classes, interfaces, properties, and methods</h2>
<p>Before wiring the API, understand how the SDK pieces fit together. The following reference focuses on the members used by the implementation and calls out a few related operations for context.</p>''')
table(['Type', 'Members and usage', 'Meaning'], [
    ('IChatClient — Microsoft.Extensions.AI', 'GetResponseAsync; GetStreamingResponseAsync; GetService; Dispose', 'Provider-neutral chat interface. The agent calls the client; this application does not call these completion methods directly. It supports nonstreaming and streaming responses, but our endpoint uses nonstreaming execution.'),
    ('IChatClient extension pipeline', 'AsBuilder().UseFunctionInvocation(...).Build()', 'Extension methods build a decorated client that executes requested functions and returns their results to the model. They are not instance members declared by IChatClient itself.'),
    ('AzureOpenAIClient — Azure.AI.OpenAI', 'new AzureOpenAIClient(endpointUri, credential); GetChatClient(deploymentName)', 'The endpoint identifies the Azure resource. GetChatClient selects the model deployment. The model deployment is supplied through configuration.'),
    ('ApiKeyCredential — System.ClientModel', 'new ApiKeyCredential(apiKey); Update(newKey)', 'Wraps a secret API key. The constructor is used here. Update exists for changing the stored key but is not called by this implementation. No key is sent to Blazor.'),
    ('AIAgent — Microsoft.Agents.AI', 'RunAsync(messages, cancellationToken: ...)', 'Agent abstraction used for basicAgent. The call accepts the conversation and returns an agent response; response.Text is checked and returned to the caller. AIAgent is not the Azure resource client.'),
    ('ChatClientAgent — Microsoft.Agents.AI', 'new ChatClientAgent(chatClient, options); inherited RunAsync', 'An agent backed by IChatClient. This instance is created per HTTP request using scoped tool methods and the shared chat client.'),
    ('ChatClientAgentOptions', 'Name; UseProvidedChatClientAsIs; ChatOptions', 'Name labels the agent. UseProvidedChatClientAsIs=true avoids adding the default client decorators because our client already has function-invocation middleware. ChatOptions supplies instructions and tools.'),
    ('ChatOptions — Microsoft.Extensions.AI', 'Instructions; Tools; AllowMultipleToolCalls', 'Instructions guide behavior; Tools holds the advertised AITool list. AllowMultipleToolCalls=false requests at most one tool call per model response; it does not prevent later sequential tool calls in the same agent run.'),
    ('FunctionInvokingChatClient settings', 'AllowConcurrentInvocation=false; MaximumIterationsPerRequest=8; IncludeDetailedErrors=false', 'Keep shared DbContext operations sequential, bound function-calling iterations, and avoid sending detailed exception information to the model. The iteration limit is not a SQL row limit or a timeout.'),
    ('AIFunctionFactory — Microsoft.Extensions.AI', 'Create(methodDelegate, toolName)', 'Turns a method into an AIFunction (an AITool), exposing its parameters and description for model tool selection. The registered name such as revenue_by_region is separate from the C# method name.'),
    ('DescriptionAttribute — System.ComponentModel', '[Description("...")]; Description property', 'C# permits the short attribute spelling Description. Method descriptions explain capability; parameter descriptions explain meanings, formats, limits, and defaults. Descriptions help selection but do not validate or authorize execution.'),
    ('ChatMessage and ChatRole', 'new ChatMessage(ChatRole.User, text); ChatRole.Assistant', 'Represent prior user/assistant turns as distinct messages. The API accepts only these two roles from clients, never a client-supplied system or tool role.'),
    ('ResponseData — application type', 'Message; Data; Count', 'Message is the query status. Data holds actual dictionary rows. Count is derived from Data.Count. This is not a framework response type.'),
])
add('''<p>The connection chain is therefore <code>AzureOpenAIClient → GetChatClient → AsIChatClient → function-invocation middleware → ChatClientAgent</code>. Configuration of Azure transport, chat behavior, and application tool execution stays separate. Dependency injection supplies the objects with appropriate lifetimes.</p>
<h2>Step 5: Configure the database and scaffold the EF Core model</h2>
<p>Restore a compatible AdventureWorks database first. Configure the API's <code>ConnectionStrings:DefaultConnection</code> locally using user secrets or an environment variable. Do not copy a real database password into an article, source repository, or browser configuration.</p>''')
code('dotnet user-secrets init --project AdventureWorksTool\ndotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR-LOCAL-CONNECTION-STRING>" --project AdventureWorksTool\ndotnet tool install --global dotnet-ef --version 10.0.14\ndotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer --project AdventureWorksTool --output-dir Models --context AdventureWorks2025Context --context-dir Models --no-onconfiguring', 'powershell')
add('''<p>If dotnet-ef is already installed, update it to a compatible 10.0.x version instead of installing it again. The <code>Name=...</code> syntax resolves configuration while scaffolding. Avoid <code>--force</code> unless you intend to overwrite existing generated files.</p>
<p><strong>Important:</strong> use <code>--no-onconfiguring</code>. The existing scaffolded workspace context contains an unconditional OnConfiguring connection setting; copying that setting would expose environment information and could override the connection configured in Program.cs. For this tutorial, generate without that method, or remove the generated inline connection override from an existing context. Do not replace it with a second hard-coded connection string. The generated options constructor must remain so AddDbContext can supply configuration.</p>
<p>Generated entity classes and the large DbContext mapping are standard EF scaffolding, so they are not reproduced here. To match this solution's entity convention, add EntityBase below and make each generated table/view entity inherit it, for example change <code>public partial class Customer</code> to <code>public partial class Customer : EntityBase</code>. Exclude EntityBase itself and AdventureWorks2025Context. This empty base adds no persistence fields; do not register it as a separate entity or introduce an inheritance discriminator.</p>''')
source('AdventureWorksTool/Models/EntityBase.cs')
add('''<h2>Step 6: Add the result envelope</h2>
<p>Create <code>Responses/ResponseData.cs</code>. A dictionary result avoids writing a separate DTO for each SELECT shape. The SQL column aliases become dictionary keys; native values remain numbers, strings, dates, and so on. The reader converts database DBNull values to null before JSON serialization.</p>''')
source('AdventureWorksTool/Responses/ResponseData.cs')
add('''<p>Count is calculated from Data instead of being maintained separately. “Retrieved 38 record(s).” is an execution status, not the business data. Keeping these concepts separate prevents the model's prose from becoming the authoritative source of sales totals.</p>
<h2>Step 7: Add AdventureWorksQueryProcessor with all 22 queries</h2>
<p>Create <code>Queries/AdventureWorksQueryProcessor.cs</code>. This class owns the approved SELECT statements. Its constructor receives AdventureWorks2025Context. Every public method builds typed SqlParameter values and delegates to ExecuteQueryAsync; the model cannot submit a replacement SQL string through these methods.</p>
<p>The following catalog connects every SQL constant to its public execution method and agent tool. All execution methods accept a CancellationToken. Optional parameters default to null unless stated otherwise. Methods with topN default to 100 and validate the range 1–1,000. The original monthly, category, and territory summaries do not accept topN, so their group counts are not capped by that limit.</p>''')

# Constant stem, wrapper name, registered name, parameters, business meaning.
QUERIES = [
('SalesByMonthYear','SalesByMonthYearAsync','sales_by_month_year','from, to','SalesOrderHeader + SalesOrderDetail: group by yyyy-MM and sum LineTotal. Returns monthly sales before tax/freight, ordered by period; includes all statuses.'),
('TopProductsBySales','TopProductsBySalesAsync','top_products_by_sales','topN, from, to','Sales headers/details + Product: sum LineTotal by product name and rank by sales descending. Product variants have their own names. Includes all statuses.'),
('SalesByCategory','SalesByCategoryAsync','sales_by_category','from, to','Sales details + Product, ProductSubcategory, ProductCategory: sum LineTotal by category. Inner category joins exclude products with no matching category; all statuses are included.'),
('RevenueByRegion','RevenueByTerritoryAsync','revenue_by_region','region, from, to','Sales headers/details + SalesTerritory: sum LineTotal by territory. region matches territory name, country code, or group (maximum 50 characters). Null includes all matching territories; includes all statuses.'),
('Customers','CustomersAsync','customers','topN, customerId, territoryId, name','Customer with person/store and territory information: return individual or store customers. CHARINDEX performs a literal name substring search, up to 101 characters; order by customer label and ID.'),
('TopCustomersBySales','TopCustomersBySalesAsync','top_customers_by_sales','topN, from, to, status','Customer sales ranking from order headers: SUM(SubTotal), order count, and last order date. Status, when supplied, must be 1–6. Header totals avoid multiplying totals through detail joins.'),
('SalesOrders','SalesOrdersAsync','sales_orders','topN, customerId, status, from, to','Sales order headers with customer/territory information, dates, status, SubTotal, TaxAmt, Freight, and TotalDue. Newest orders first; sales status must be 1–6.'),
('SalesOrderDetails','SalesOrderDetailsAsync','sales_order_details','salesOrderId (required), topN','SalesOrderDetail + Product: quantities, prices, discounts, and LineTotal for one positive numeric sales order ID. Ordered by detail ID. Large orders can be truncated by topN.'),
('SalesBySalesPerson','SalesBySalesPersonAsync','sales_by_sales_person','topN, from, to, territoryId, status','Sales order headers joined to salesperson/person: SUM(SubTotal) by salesperson. Territory filter applies to the order, not the salesperson current territory. Status 1–6; excludes orders without a salesperson.'),
('ProductCatalog','ProductCatalogAsync','product_catalog','topN, productId, categoryId, name','Product LEFT JOIN subcategory/category: product variants, prices, costs, and selling dates. Includes discontinued and uncategorized products when filters allow; literal name search up to 50 characters.'),
('ProductInventory','ProductInventoryAsync','product_inventory','topN, productId, locationId','ProductInventory + Product + Location: one row per product/location, including shelf, bin, quantity, safety stock, and reorder point. locationId uses SQL SmallInt.'),
('LowStockProducts','LowStockProductsAsync','low_stock_products','topN, categoryId','Product LEFT JOIN inventory: sum quantities as bigint across locations; missing inventory counts as zero. HAVING stock below ReorderPoint, ordered by largest shortfall.'),
('Vendors','VendorsAsync','vendors','topN, vendorId, activeOnly, name','Purchasing.Vendor: credit rating, preferred status, and active flag. activeOnly=true excludes inactive vendors; false/null includes them. Literal name search up to 50 characters.'),
('ProductSuppliers','ProductSuppliersAsync','product_suppliers','topN, productId, vendorId, activeOnly','ProductVendor joined to Product/Vendor: supplier prices, lead times, order quantities, and units. One product can produce multiple supplier rows; ordered by product and standard price.'),
('PurchaseOrders','PurchaseOrdersAsync','purchase_orders','topN, vendorId, status, from, to','PurchaseOrderHeader + Vendor: dates, status, subtotal, tax, freight, and total due. Newest first. Purchase status must be 1–4, unlike sales status.'),
('PurchaseOrderDetails','PurchaseOrderDetailsAsync','purchase_order_details','purchaseOrderId (required), topN','PurchaseOrderDetail + Product: line items, due dates, prices, and ordered/received/rejected/stocked quantities for a positive purchase order ID.'),
('PurchasingByVendor','PurchasingByVendorAsync','purchasing_by_vendor','topN, from, to, status','PurchaseOrderHeader + Vendor: counts, SUM(SubTotal), and SUM(TotalDue). TotalPurchaseAmount includes tax/freight; rank by it. Status 1–4 when supplied.'),
('WorkOrders','WorkOrdersAsync','work_orders','topN, productId, openOnly, from, to','WorkOrder + Product and optional ScrapReason: quantities and start/due/end dates. openOnly=true means EndDate IS NULL; false/null includes completed work. Dates filter StartDate, newest first.'),
('ScrapByReason','ScrapByReasonAsync','scrap_by_reason','topN, productId, from, to','WorkOrder with ScrapReason: only ScrappedQty > 0, group by reason, count affected orders and sum scrap as bigint. Missing reason becomes Unspecified; dates filter StartDate.'),
('EmployeeDirectory','EmployeeDirectoryAsync','employee_directory','topN, employeeId, departmentId, name','Current employees + Person with current department/shift assignments (EndDate IS NULL). Literal full-name search up to 101 characters. No salary or national ID output; multiple assignments can yield multiple rows.'),
('EmployeeCountByDepartment','EmployeeCountByDepartmentAsync','employee_count_by_department','topN','Department LEFT JOIN current assignments/current employees: COUNT(DISTINCT employee) per department, including zero-count departments; largest headcounts first.'),
('ProductSpecialOffers','ProductSpecialOffersAsync','product_special_offers','asOf (required), topN, productId','SpecialOffer + SpecialOfferProduct + Product: offers valid at the exact timestamp, inclusive start/end. Includes discount and quantity rules. Validate SQL datetime range; order by discount descending.'),
]
table(['SQL constant → execution method', 'Tool method → registered name', 'Parameters (plus CancellationToken)', 'Query behavior'], [
    (stem+'Sql → Get'+stem+'Async', wrapper+' → '+tool, parameters, meaning)
    for stem, wrapper, tool, parameters, meaning in QUERIES
])
add('''<h3>Shared parameter and SQL rules</h3>
<ul>
<li>Date ranges are inclusive calendar dates: SQL compares the lower bound directly and uses less than DATEADD(day, 1, @to) for the upper bound. The C# methods use Date parameters and discard time-of-day for these filters.</li>
<li>Dates must be compatible with SQL datetime data: from is on/after 1753-01-01; to is before 9999-12-31 so adding one day is safe; from cannot exceed to. A null date bound leaves that side unrestricted.</li>
<li>Special-offer asOf is an instant, not a date-only filter. It must be within 1753-01-01 through 9999-12-31 23:59:59.997 in the database's time convention.</li>
<li>Sales status values are 1 In process, 2 Approved, 3 Backordered, 4 Rejected, 5 Shipped, 6 Cancelled. Purchase status values are 1 Pending, 2 Approved, 3 Rejected, 4 Complete. The original four sales summaries have no status argument.</li>
<li>Integer IDs and topN use SqlDbType.Int; status uses TinyInt; department/location use SmallInt; flags use Bit; strings use sized NVarChar. Optional null values are bound as DBNull.Value, not interpolated into SQL.</li>
<li>Most optional lookup IDs are filters rather than existence checks. An unknown ID can produce zero rows. The required order IDs are checked for positivity before execution.</li>
</ul>
<p>Below is the <strong>complete class</strong>, including all SQL statements and execution methods. The inline comments describe the statements, parameters, and method responsibilities.</p>''')
source('AdventureWorksTool/Queries/AdventureWorksQueryProcessor.cs')
add('''<h3>How ExecuteQueryAsync works</h3>
<ol>
<li>It observes cancellation and validates shared topN/date limits before issuing a command.</li>
<li>It enters EF's concurrency detector. A DbContext is not safe for parallel operations; the agent middleware also disables concurrent tool invocation.</li>
<li>It uses the context's execution strategy. The actual retry behavior depends on provider configuration; merely calling CreateExecutionStrategy does not promise automatic retries.</li>
<li>IRawSqlCommandBuilder builds a relational command using supplied SqlParameter objects and context metadata. RelationalCommandParameterObject connects the command to the EF-managed connection, transaction context, and diagnostics.</li>
<li>ExecuteReaderAsync opens the reader. Each row becomes a case-insensitive Dictionary&lt;string, object?&gt; keyed by the SQL column names. IsDBNullAsync converts database nulls to C# null.</li>
<li>The reader is disposed with await using. The returned ResponseData contains the rows and either “No data found.” or a retrieved-row count. Exceptions are not converted into fake success results.</li>
</ol>
<p>This reader uses lower-level EF relational services to support arbitrary result shapes. It is intentionally centralized in one method. These APIs are more version-sensitive than LINQ queries, so re-check them when changing EF versions. SELECT statements are read-only by design; database permissions should enforce that boundary independently.</p>
<h2>Step 8: Add AdventureworksTool and register every function</h2>
<p>Create <code>AgentTools/AdventureworksTool.cs</code>. Preserve the class spelling <code>AdventureworksTool</code> used by the application. This class bridges model-selected functions and database query methods; it contains no SQL of its own.</p>
<ul>
<li><strong>Constructor:</strong> receives the scoped query processor and rejects null.</li>
<li><strong>AsTools:</strong> registers all 22 method delegates using AIFunctionFactory.Create. The catalog above lists every wrapper and registered name.</li>
<li><strong>Each public async wrapper:</strong> forwards its filters, defaults, and cancellation token to its matching Get...Async query method. RevenueByTerritoryAsync deliberately maps to GetRevenueByRegionAsync.</li>
<li><strong>CaptureResult:</strong> appends the successful ResponseData with its tool name and returns only response.Message to the model.</li>
<li><strong>Results:</strong> exposes a read-only view of the captured per-request results. Repeated successful calls remain separate entries; a failed query does not reach CaptureResult.</li>
</ul>
<p>For example, revenue_by_region advertises territory/country/group matching and date bounds. The model selects it after resolving the user's region and date choices. The method calls GetRevenueByRegionAsync, records the rows, and returns a status string. The same wrapper pattern applies to sales, customers, inventory, vendors, purchasing, manufacturing, employees, and special offers.</p>
<p>The Description attributes are part of the tool metadata. They are not comments for the compiler to enforce: a method description can guide tool selection, but typed parameters and query validation still protect execution. The complete class below includes every description, parameter, registration, and method.</p>''')
source('AdventureWorksTool/AgentTools/AdventureworksTool.cs')
add('''<h2>Step 9: Add configuration without publishing secrets</h2>
<p>The following is an intentionally sanitized <code>AdventureWorksTool/appsettings.json</code> example. Every resource-specific endpoint, deployment identifier, key, and database connection string is a placeholder. Replace placeholders locally through configuration; these example values cannot connect to a real service.</p>''')
code(json.dumps({
    'ConnectionStrings': {'DefaultConnection': '<REDACTED-CONNECTION-STRING>'},
    'AzureOpenAI': {'Endpoint': 'https://YOUR-RESOURCE.openai.azure.com/', 'ApiKey': '<REDACTED-API-KEY>', 'DeploymentName': '<YOUR-DEPLOYMENT-NAME>'},
    'Logging': {'LogLevel': {'Default': 'Information', 'Microsoft.AspNetCore': 'Warning'}},
    'AllowedHosts': '*'
}, indent=2), 'json')
add('<p>Keep the local development CORS origins in <code>AdventureWorksTool/appsettings.Development.json</code>. Localhost ports are public development addresses, not resource secrets.</p>')
code(json.dumps({'Cors': {'AllowedOrigins': ['https://localhost:7248','http://localhost:5248']},'Logging': {'LogLevel': {'Default':'Information','Microsoft.AspNetCore':'Warning'}}}, indent=2), 'json')
add('''<p>Set server values with user secrets in Development or with environment variables such as <code>ConnectionStrings__DefaultConnection</code>, <code>AzureOpenAI__Endpoint</code>, <code>AzureOpenAI__ApiKey</code>, and <code>AzureOpenAI__DeploymentName</code>. Production CORS origins can use <code>Cors__AllowedOrigins__0</code>. Origins must match scheme, host, and port exactly, without a trailing slash. User secrets keep values out of the repository but are not an encrypted production secret store.</p>
<h2>Step 10: Replace the API Program.cs with the complete agent endpoint</h2>
<p>The following is the complete current <code>AdventureWorksTool/Program.cs</code>, including request records. No endpoint code is omitted.</p>''')
source('AdventureWorksTool/Program.cs')
add('''<h3>Program.cs explained in detail</h3>
<ol>
<li><strong>Application startup:</strong> WebApplication.CreateBuilder loads configuration and creates the service collection. AddDbContext uses DefaultConnection. The context, query processor, and tools are scoped to one HTTP request; the tool result list therefore does not leak rows between users.</li>
<li><strong>Azure client factory:</strong> the singleton IChatClient factory reads endpoint, key, and deployment. It rejects a non-HTTPS/invalid URI and missing key/deployment. The factory runs when the service is resolved, not necessarily at application startup.</li>
<li><strong>Client adaptation:</strong> AzureOpenAIClient creates the deployment chat client; AsIChatClient adapts it to Microsoft.Extensions.AI. The builder installs function invocation with logging. Parallel execution is disabled, iteration count is bounded at eight, and detailed errors are not sent to the model.</li>
<li><strong>Error infrastructure:</strong> AddProblemDetails and UseExceptionHandler provide a consistent error-response path for unhandled failures. Tool-call failures can also be handled by the invocation middleware and reported through agent text; do not assume every tool error necessarily becomes HTTP 500.</li>
<li><strong>CORS:</strong> the named BlazorClient policy permits only configured origins, POST, and Content-Type. With no origins configured, cross-origin browser access is denied. RequireCors applies the policy to the agent endpoint; routing precedes CORS middleware. CORS is not authentication.</li>
<li><strong>OpenAPI and Swagger:</strong> AddOpenApi registers document generation. In Development, MapOpenApi exposes /openapi/v1.json and UseSwaggerUI displays it at /swagger. The UI uses a relative URL to that document. It is not enabled by this code in Production.</li>
<li><strong>Request validation:</strong> the prompt is trimmed, required, and limited to 8,000 characters. Optional history has at most 20 messages, only user/assistant roles, at most 8,000 characters per nonempty message, and at most 40,000 history characters. Invalid values return a 400 validation problem.</li>
<li><strong>Conversation reconstruction:</strong> prior text becomes ChatMessage instances with ChatRole.User or ChatRole.Assistant; the current prompt is appended once. There is no server-side conversation database or session ID. History is untrusted context, not authorization or proof that a query previously succeeded.</li>
<li><strong>Agent creation:</strong> a new ChatClientAgent receives the shared client, its name, and ChatOptions. UseProvidedChatClientAsIs preserves the middleware already installed. Tools come from the request-scoped AdventureworksTool instance, not a singleton buffer.</li>
<li><strong>Instructions:</strong> vague sales requests are clarified into a report type. Region reports ask for territory/country/group/all territories when missing. Date-filtered reports ask for dates or all available dates. The agent asks one question at a time, remembers answers for the current request, and must not invent required IDs. Complete requests can run immediately.</li>
<li><strong>Tool-call control:</strong> AllowMultipleToolCalls=false requests a single tool call in a model response. It still permits subsequent calls over sequential invocation iterations. The model chooses among tools; application parameter checks remain independent of prompt wording.</li>
<li><strong>Run and response:</strong> RunAsync receives the reconstructed messages and request cancellation token. An empty agent text response produces a 502 problem. Otherwise Results.Ok returns message and a results array directly projected from captured ResponseData objects, with tool, message, count, and data.</li>
<li><strong>Route metadata:</strong> WithName, WithSummary, and Produces calls document the route/status codes. Produces(200) alone does not provide a strongly typed success-body schema. The actual response shape is explained in this article and represented by the client contracts.</li>
</ol>
<p><strong>Clarification is model-driven:</strong> instructions encourage the expected dialogue but are not a deterministic approval workflow. Verify them against your deployment. For sensitive operations, implement explicit application-state checks rather than relying only on model instructions. Here the operations are predefined read-only queries.</p>
<h2>Step 11: Add a Blazor WebAssembly project named AdventureWorksClient</h2>
<p>Add a standalone <strong>Blazor WebAssembly App</strong> targeting .NET 10 with no template authentication. Do not add a server-project reference: the client talks to the API over HTTP. Remove the sample Counter/Weather pages and template navigation from the root rendering path; the App.razor below renders the chat directly.</p>''')
code('dotnet new blazorwasm --name AdventureWorksClient --framework net10.0\ndotnet sln AdventureWorks_Agents.slnx add AdventureWorksClient/AdventureWorksClient.csproj', 'powershell')
source('AdventureWorksClient/AdventureWorksClient.csproj', 'xml')
add('''<p>Use folders named <code>Contracts</code>, <code>Services</code>, and <code>Components</code>. The client needs no Azure OpenAI package or API key. It uses only the public HTTP contract. The following listings include all handwritten client logic and Razor components, not the standard HTML bootstrap or template CSS.</p>
<h2>Step 12: Add client contracts and the API service</h2>
<p>AgentHistoryMessage is a simple role/text class. AgentResponse holds the final message and result list. AgentQueryResult holds a tool name, query status, row count, and dictionaries of JsonElement values. JsonElement allows one generic table component to display different SQL result shapes without per-query classes.</p>''')
source('AdventureWorksClient/Contracts/AgentResponse.cs')
add('''<p>AgentApiClient.AskAsync posts prompt and history to api/agent. It disposes the HTTP response, surfaces validation messages from problem JSON, handles malformed problem bodies without hiding the HTTP status, and rejects an empty success body. Cancellation is forwarded to both HTTP submission and response reading.</p>''')
source('AdventureWorksClient/Services/AgentApiClient.cs')
add('''<h2>Step 13: Configure the client host and root component</h2>
<p>Add <code>wwwroot/appsettings.json</code> in the client. This file is downloaded by the browser and must never contain a key, database connection string, or server secret. The sample below uses a public localhost address; for deployment replace it with your public HTTPS API base address.</p>''')
code(json.dumps({'ApiBaseUrl':'https://localhost:7113/'}, indent=2), 'json')
add('''<p>The client Program.cs validates the HTTP/HTTPS address, ensures a trailing slash for relative requests, sets a three-minute timeout, and registers AgentApiClient. It mounts App at #app and HeadOutlet for PageTitle support. These are client-side services; a Blazor scoped service normally lives for the browser application's lifetime, not a server request.</p>''')
source('AdventureWorksClient/Program.cs')
add('<p>Use these shared Razor imports so the components can refer to contracts, services, JSON values, and Blazor event APIs without repeating using directives.</p>')
source('AdventureWorksClient/_Imports.razor', 'razor')
add('<p>The root component displays the application title and the reusable chat component.</p>')
source('AdventureWorksClient/App.razor', 'razor')
add('''<p>Keep the template's standard wwwroot/index.html document with a base href, the #app element, and the _framework/blazor.webassembly.js bootstrap script. No custom browser script is required. The optional styling in this workspace is intentionally not reproduced; without it the same controls and tables still work. Do not add Azure SDK code to the HTML bootstrap.</p>
<h2>Step 14: Add the reusable query-result table component</h2>
<p>Create Components/QueryResultTable.razor. Result is an editor-required component parameter. OnParametersSet rebuilds column names from the union of dictionary keys when a new result object arrives and resets paging. Previous and Next keep the page within bounds. Format displays JSON nulls explicitly, uses unquoted string values, and preserves raw numeric/boolean JSON text.</p>
<p>The page size is 25 <em>already returned rows</em>; changing pages does not query SQL Server again. Count is the API's returned count, not a count of all database matches before TOP. Blazor's normal text rendering escapes cell content; the component does not use MarkupString or execute database-provided HTML.</p>''')
source('AdventureWorksClient/Components/QueryResultTable.razor', 'razor')
add('''<h2>Step 15: Add the AgentChat component with follow-up conversation support</h2>
<p>Create Components/AgentChat.razor. There are two lists for different purposes: _messages displays user/agent entries and attached result tables, while _history contains only successful user/assistant text pairs to send with the next request. This prevents large tables from being sent to the model.</p>
<ul>
<li><strong>SendAsync:</strong> rejects invalid input/history, prevents duplicate in-flight submissions, displays the user prompt, and calls the API. Only a completed response adds both sides of the exchange to _history. The returned rows stay attached to the display entry.</li>
<li><strong>Cancellation and errors:</strong> a CancellationTokenSource lets the Cancel button interrupt the request. HTTP, timeout/cancellation, and JSON errors produce readable messages. The input remains available for retry; unsuccessful requests are not added to model history, although their submitted user bubbles remain in the visible transcript.</li>
<li><strong>Clear:</strong> removes display entries, remembered text, the prompt, and status/error messages. Reloading also resets history because no browser storage or server persistence is used.</li>
<li><strong>Dispose:</strong> cancels pending work and prevents a late reply from updating a removed component.</li>
<li><strong>History limits:</strong> the client checks the same count/character limits before sending. If the stored history exceeds a limit after a completed exchange, the next request asks the user to clear chat instead of silently forgetting earlier answers.</li>
</ul>
<p>The examples include both vague and complete requests. A clarification is an ordinary agent text reply with no query results; the UI invites the user to answer rather than treating an empty results array as an error.</p>''')
source('AdventureWorksClient/Components/AgentChat.razor', 'razor')
add('''<h2>Step 16: Configure startup ports and run both projects</h2>
<p>Use the following launch profiles so the API base address and allowed origins agree. These local development URLs are deliberately public; they contain no Azure resource endpoint or credential. The configuration is included because mismatched ports are a common cause of CORS errors.</p>''')
source('AdventureWorksTool/Properties/launchSettings.json', 'json')
source('AdventureWorksClient/Properties/launchSettings.json', 'json')
add('''<p>In Visual Studio, select multiple startup projects and set both AdventureWorksTool and AdventureWorksClient to Start. Choose each project's https profile. Trust the ASP.NET Core development certificate if prompted. Open <strong>https://localhost:7248</strong> for the chat and <strong>https://localhost:7113/swagger</strong> for Swagger in Development.</p>''')
code('dotnet dev-certs https --trust\ndotnet build AdventureWorks_Agents.slnx\n# Run each of the following in a separate terminal:\ndotnet run --project AdventureWorksTool --launch-profile https\ndotnet run --project AdventureWorksClient --launch-profile https', 'powershell')
add('''<p>Always call the API's HTTPS address directly from the browser. Redirecting an HTTP preflight request to HTTPS can cause a CORS failure. If you change ports, update both the client's ApiBaseUrl and the API's allowed browser origins. Outside Development, explicitly configure the production origins because the development file is not loaded.</p>
<h2>Step 17: Test a complete request and a follow-up conversation</h2>
<p>A useful first prompt is “Show revenue for Northwest using all available dates.” It already specifies the report, region, and date scope. The model should select revenue_by_region with region=Northwest and null date bounds. “All available dates” means no lower or upper date filter; it does not mean that every SQL statement in the application is run.</p>
<p>Next clear the chat and try this illustrative conversation. The exact wording can vary with the deployment:</p>
<ol>
<li><strong>User:</strong> Show revenue by region.</li>
<li><strong>Agent:</strong> Which territory, country, or group, or all territories?</li>
<li><strong>User:</strong> Northwest.</li>
<li><strong>Agent:</strong> Which date range, or all available dates?</li>
<li><strong>User:</strong> All available dates.</li>
<li><strong>Agent:</strong> Reports the query status. The API returns real matching rows for the UI table.</li>
</ol>
<p>The clarification turns should not invoke a query until the choices are resolved. Also test “Show sales details for all available dates”: the agent should ask which sales report is wanted and remember the already supplied date scope.</p>
<p>The complete HTTP request examples below show both a direct prompt and the optional history contract. The sample history is illustrative; real clients must send their actual previous messages. For the HTTPS launch profile change the sample host from http://localhost:5225 to https://localhost:7113 before calling it directly.</p>''')
source('AdventureWorksTool/AdventureWorksTool.http', 'http')
add('''<p>A successful response has a top-level message and a results array. Every entry has tool, message, count, and data. For a monthly report, the data objects use the SQL aliases Period and Sales. Do not invent example business totals to make a demonstration look successful: inspect the real rows returned from your database.</p>
<ul>
<li><strong>Clarification/unsupported request:</strong> the agent can return text with results=[]; that alone does not indicate a server failure.</li>
<li><strong>Successful empty query:</strong> one result entry has count=0, data=[], and “No data found.”</li>
<li><strong>Multiple successful invocations:</strong> each appears as its own entry, even if the same tool runs more than once.</li>
<li><strong>Tool failure:</strong> failed calls do not add successful entries. Check the message for errors; a response can include successful results from other calls.</li>
<li><strong>Validation failure:</strong> empty/oversized prompts, unsupported history roles, or history over the limits return HTTP 400.</li>
<li><strong>Empty final agent text:</strong> the API returns HTTP 502 rather than a misleading successful reply.</li>
</ul>
<h3>Verification checklist</h3>
<ol>
<li>Build both projects and verify package restore, EF context generation, and matching local URLs.</li>
<li>Verify a complete Northwest request runs directly and the multi-turn version remembers the short answers.</li>
<li>Clear the chat and verify an isolated “Northwest” does not inherit the previous report.</li>
<li>Request more than 25 rows and verify table pagination without additional SQL requests.</li>
<li>Test zero rows, invalid IDs/status values, canceled requests, API unavailability, and malformed request JSON.</li>
<li>Verify an allowed browser origin receives a CORS allow-origin response and an unlisted origin does not.</li>
<li>Check that user text, model output, and database markup are rendered as text rather than executed as HTML.</li>
</ol>
<p>A successful build verifies compilation, not a live SQL connection or model behavior. The configured database and Azure deployment must be exercised separately. No automated test project is included in this workspace.</p>
<h2>Step 18: Understand the boundaries before deployment</h2>
<p>This is an agentic API because the model selects tools and asks clarifying questions rather than simply executing a hard-coded route chosen by the user. However, the model receives only query status strings. It cannot analyze unseen sales rows or use a returned product ID to plan another query unless that value is supplied in subsequent conversation. The user can read actual rows in the UI, but the agent's message must not claim to have analyzed data it never received.</p>
<p>Conversation support is bounded browser-supplied text, not durable memory. The API reconstructs each run and does not retain previous tool-result messages. Untrusted history cannot be used to authorize access. Keep API authentication, authorization, rate limits, least-privilege database access, logging hygiene, and model usage budgets as explicit deployment work. CORS only controls browser cross-origin access; it does not stop nonbrowser callers.</p>
<p>Do not expose detailed SQL exceptions or configuration secrets in the browser. Keep the singleton model client free of request-specific rows, preserve sequential DbContext use, and verify any production identity change independently. This article uses API-key configuration to match the supplied implementation; it does not silently switch providers, models, or authentication schemes.</p>
<h2>References</h2>
<ul>
<li><a href="https://learn.microsoft.com/en-us/agent-framework/overview/">Microsoft Agent Framework overview</a></li>
<li><a href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient">IChatClient API reference</a></li>
<li><a href="https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.azureopenaiclient">AzureOpenAIClient API reference</a></li>
<li><a href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.chatclientagentoptions">ChatClientAgentOptions API reference</a></li>
</ul>
<p>Reference pages may describe newer or different package builds. The project files and complete source listings above identify the versions and API usage for this walkthrough.</p>
<h2>Conclusion</h2>
<p>Microsoft Agent Framework lets us place a natural-language interface over existing .NET capabilities without giving a model unrestricted database access. In this solution, AzureOpenAIClient supplies model connectivity, IChatClient supplies the chat abstraction and invocation pipeline, ChatClientAgent handles the interaction, and AIFunctionFactory exposes 22 described query methods as tools.</p>
<p>AdventureWorksQueryProcessor remains responsible for predefined parameterized SQL. AdventureworksTool keeps real query rows separate from status text. The ASP.NET Core API validates requests and returns both the agent message and authoritative data. Blazor provides the conversation experience, remembers clarification answers, and renders the actual result rows safely. Together these pieces demonstrate a practical, understandable starting point for a read-only business-data agent.</p>
</article>''')

# Fail rather than quietly publishing incomplete source listings.
query_source = (ROOT / 'AdventureWorksTool/Queries/AdventureWorksQueryProcessor.cs').read_text(encoding='utf-8-sig')
tool_source = (ROOT / 'AdventureWorksTool/AgentTools/AdventureworksTool.cs').read_text(encoding='utf-8-sig')
for stem, wrapper, tool, _, _ in QUERIES:
    assert f'private const string {stem}Sql' in query_source
    assert f'Task<ResponseData> Get{stem}Async(' in query_source
    assert f'AIFunctionFactory.Create({wrapper}, "{tool}")' in tool_source
assert len(re.findall(r'private const string \w+Sql', query_source)) == len(QUERIES) == 22
article = '\n\n'.join(parts) + '\n'
assert '<html' not in article.lower() and '<style' not in article.lower()
for path in SOURCE_FILES:
    assert f'data-source="{path}"' in article
(ROOT / 'MAF-Article.html').write_text(article, encoding='utf-8')
print(f'Created MAF-Article.html: {len(article):,} characters; {len(SOURCE_FILES)} full source listings; 22 query/tool explanations.')
