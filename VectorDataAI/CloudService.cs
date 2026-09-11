using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.VectorData;

namespace VectorDataAI;

internal class CloudService
{
    [VectorStoreKey]
    public int Key { get; set;  }

    [VectorStoreData]
    public string Name { get; set; } = string.Empty;

    [VectorStoreData]
    public string Description { get; set; } = string.Empty;

    [VectorStoreVector(dimensions:384,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
