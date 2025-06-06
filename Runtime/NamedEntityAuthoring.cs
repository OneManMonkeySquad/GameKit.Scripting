using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct NamedEntity : IComponentData
{
    public FixedString32Bytes Name;
}

public class NamedEntityAuthoring : MonoBehaviour
{
    public class Baker : Baker<NamedEntityAuthoring>
    {
        public override void Bake(NamedEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new NamedEntity { Name = authoring.gameObject.name });
        }
    }
}
