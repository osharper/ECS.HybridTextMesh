using Unity.Entities;
using Unity.Transforms;

namespace E7.ECS.HybridTextMesh
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HybridTextMeshSimulationGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class HybridTextMeshToTransformGroup : ComponentSystemGroup
    {
    }
}