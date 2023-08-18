using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace E7.ECS.HybridTextMesh
{
    [UpdateInGroup(typeof(HybridTextMeshSimulationGroup))]
    internal partial class CharacterPrefabLookupPreparationSystem : SystemBase
    {
        List<NativeHashMap<char, Entity>> forDispose;
        EntityQuery noLookupFontAssetQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            forDispose = new List<NativeHashMap<char, Entity>>(4);

            RequireForUpdate(noLookupFontAssetQuery);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var fd in forDispose)
            {
                fd.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            var singleton = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = singleton.CreateCommandBuffer(EntityManager.WorldUnmanaged);
            
            Entities.WithAll<FontAssetEntity>().ForEach(
                    (Entity e, in FontAssetHolder holder, in DynamicBuffer<GlyphPrefabBuffer> buffer) =>
                    {
                        var nativeHashMap = new NativeHashMap<char, Entity>(buffer.Length, Allocator.Persistent);
                        var nativeHashMapWithScale =
                            new NativeHashMap<char, Entity>(buffer.Length, Allocator.Persistent);
                        forDispose.Add(nativeHashMap);
                        forDispose.Add(nativeHashMapWithScale);

                        for (int i = 0; i < buffer.Length; i++)
                        {
                            nativeHashMap.Add(buffer[i].character.ToString()[0], buffer[i].prefab);
                            nativeHashMapWithScale.Add(buffer[i].character.ToString()[0], buffer[i].prefabWithScale);
                        }

                        EntityManager.AddSharedComponent(e,
                            new GlyphPrefabLookup
                            {
                                characterToPrefabEntity = nativeHashMap,
                                characterToPrefabEntityWithScale = nativeHashMapWithScale
                            });
                    })
                .WithStoreEntityQueryInField(ref noLookupFontAssetQuery)
                .WithStructuralChanges()
                .Run();
            
            ecb.RemoveComponent<GlyphPrefabBuffer>(noLookupFontAssetQuery, EntityQueryCaptureMode.AtRecord);
        }
    }
}