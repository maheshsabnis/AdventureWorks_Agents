using System;
using System.Collections.Generic;

namespace AdventureWorksTool.Models;

public partial class VProductAndDescription : EntityBase
{
    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string ProductModel { get; set; } = null!;

    public string CultureId { get; set; } = null!;

    public string Description { get; set; } = null!;
}
