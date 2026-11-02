using System;
using System.Collections.Generic;

namespace AdventureWorksTool.Models;

public partial class WorkOrder : EntityBase
{
    public int WorkOrderId { get; set; }

    public int ProductId { get; set; }

    public int OrderQty { get; set; }

    public int StockedQty { get; set; }

    public short ScrappedQty { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime DueDate { get; set; }

    public short? ScrapReasonId { get; set; }

    public DateTime ModifiedDate { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ScrapReason? ScrapReason { get; set; }

    public virtual ICollection<WorkOrderRouting> WorkOrderRoutings { get; set; } = new List<WorkOrderRouting>();
}
