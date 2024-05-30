﻿using System;
using System.Collections.Generic;

namespace SampleProject.Models.ProductInventory;

public partial class Category
{
    public string CategoryId { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
