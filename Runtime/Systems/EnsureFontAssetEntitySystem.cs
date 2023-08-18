using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace E7.ECS.HybridTextMesh
{
    /// <summary>
    /// For any <see cref="HtmFontAsset"/> usage ensure an asset entity representing that
    /// font asset exist. Then we could make a lookup hash map and prefab per character.
    ///
    /// Has 1 frame delay.
    /// </summary>
    [UpdateInGroup(typeof(HybridTextMeshSimulationGroup))]
    internal partial class EnsureFontAssetEntitySystem : SystemBase
    {
        EntityQuery fontAssetQuery;
        EntityQuery potentiallyNewSpriteFontAssetQuery;
        EntityArchetype characterWithPrefabArchetype;
        EntityArchetype fontAssetArchetype;
        
        NativeArray<ComponentType> entitiesGraphicsComponents = new NativeList<ComponentType>(8, Allocator.Persistent)
        {
            // Absolute minimum set of components required by Entities Graphics
            // to be considered for rendering. Entities without these components will
            // not match queries and will never be rendered.
            ComponentType.ReadWrite<WorldRenderBounds>(),
            ComponentType.ReadWrite<RenderFilterSettings>(),
            ComponentType.ReadWrite<MaterialMeshInfo>(),
            ComponentType.ChunkComponent<ChunkWorldRenderBounds>(),
            ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>(),
            // Extra transform related components required to render correctly
            // using many default SRP shaders. Custom shaders could potentially
            // work without it.
            ComponentType.ReadWrite<WorldToLocal_Tag>(),
            // Components required by Entities Graphics package visibility culling.
            ComponentType.ReadWrite<RenderBounds>(),
            ComponentType.ReadWrite<PerInstanceCullingTag>(),
        }.AsArray();

        struct FontAssetEntityExistForThisText : IComponentData
        {
        }

        protected override void OnCreate()
        {
            characterWithPrefabArchetype = EntityManager.CreateArchetype(
                ArchetypeCollection.CharacterTypes.Concat(new[] {ComponentType.ReadOnly<Prefab>()}).ToArray()
            );
            fontAssetQuery = GetEntityQuery(
                ComponentType.ReadOnly<FontAssetEntity>(),
                ComponentType.ReadOnly<FontAssetHolder>()
            );
            fontAssetArchetype = EntityManager.CreateArchetype(
                ArchetypeCollection.FontAssetTypes
            );
            RequireForUpdate(potentiallyNewSpriteFontAssetQuery);
        }

        protected override void OnUpdate()
        {
            var singleton = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = singleton.CreateCommandBuffer(EntityManager.WorldUnmanaged);
            
            var worked = new NativeList<int>(4, Allocator.Temp);
            Entities
                .WithAll<TextContent>()
                .WithNone<FontAssetEntityExistForThisText>()
                .ForEach((Entity e, FontAssetHolder sfah) =>
                {
                    var sfa = sfah.htmFontAsset;
                    ecb.SetComponent(e, sfa.fontMetrics);

                    int sfaInstanceId = sfa.GetInstanceID();
                    // One SPA get only one prepare, so if there are something here don't do it anymore.
                    fontAssetQuery.SetSharedComponentFilterManaged(sfah);
                    if (fontAssetQuery.CalculateChunkCount() == 0 && worked.IndexOf(sfaInstanceId) == -1)
                    {
                        Entity fontAssetEntity = ecb.CreateEntity(fontAssetArchetype);

                        ecb.SetSharedComponentManaged(fontAssetEntity, sfah);
                        var buffer = ecb.SetBuffer<GlyphPrefabBuffer>(fontAssetEntity);
                        // Prepare prefabs for this asset.
                        for (int i = 0; i < sfa.characterInfos.Length; i++)
                        {
                            CharacterInfo c = sfa.characterInfos[i];
                            RegisterCharacter(sfa, c, ecb, buffer);
                        }

                        RegisterCharacter(sfa, new CharacterInfo
                        {
                            character = '\n',
                        }, ecb, buffer, new SpecialCharacter {newLine = true});

                        //Prevents loading the same font in the same frame since ECB target
                        //the next frame.
                        worked.Add(sfaInstanceId);
                    }
                })
                .WithStoreEntityQueryInField(ref potentiallyNewSpriteFontAssetQuery)
                .WithoutBurst().Run();
            ecb.AddComponent<FontAssetEntityExistForThisText>(potentiallyNewSpriteFontAssetQuery, EntityQueryCaptureMode.AtRecord);
            worked.Dispose();
        }

        void RegisterCharacter(HtmFontAsset sfa,
            CharacterInfo c,
            EntityCommandBuffer ecb,
            DynamicBuffer<GlyphPrefabBuffer> buffer,
            SpecialCharacter specialCharacter = default)
        {
            Entity characterPrefab = ecb.CreateEntity(characterWithPrefabArchetype);
            // TODO: Entity characterPrefabWithScale = ecb.CreateEntity(characterWithPrefabArchetype);
            // ecb.AddComponent<NonUniformScale>(characterPrefabWithScale,
            //    new NonUniformScale {Value = new float3(1)});

            ecb.SetSharedComponentManaged(characterPrefab, new RenderMeshArray
            {
                Materials = new [] { sfa.material },
                Meshes = new []{ c.meshForCharacter },
            });
            ecb.AddSharedComponent(characterPrefab, new RenderMeshDescription(ShadowCastingMode.Off).FilterSettings);
            ecb.AddComponent(characterPrefab, MaterialMeshInfo.FromRenderMeshArrayIndices(0 ,0));
            foreach (var entitiesGraphicsComponent in entitiesGraphicsComponents)
            {
                ecb.AddComponent(characterPrefab, entitiesGraphicsComponent);
            }
            
            // ecb.SetSharedComponentManaged(characterPrefabWithScale, new RenderMesh
            // {
            //     material = sfa.material,
            //     mesh = c.meshForCharacter,
            // });

            ecb.SetComponent(characterPrefab, c.glyphMetrics);
            // ecb.SetComponent(characterPrefabWithScale, c.glyphMetrics);

            ecb.SetComponent(characterPrefab, specialCharacter);
            // ecb.SetComponent(characterPrefabWithScale, specialCharacter);

            buffer.Add(new GlyphPrefabBuffer
            {
                character = c.character.ToString(),
                prefab = characterPrefab,
                //prefabWithScale = characterPrefabWithScale
            });
        }
    }
}