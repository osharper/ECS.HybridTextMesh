using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace E7.ECS.HybridTextMesh
{
    [RequireComponent(typeof(RectTransform))]
    public class HybridTextMeshAuthoring : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<RectTransform>().sizeDelta = new Vector2(10, 10);
            textTransform = new TextTransform
            {
                modifyLeading = 1
            };
        }
#pragma warning disable 0649
        [Multiline] [SerializeField] public string text;
        [SerializeField] internal HtmFontAsset htmFontAsset;
        [Space] [SerializeField] public int persistentCharacterEntities;
        [SerializeField] public TextStructure textStructure;
        [SerializeField] public TextTransform textTransform;
#pragma warning restore 0649
    }

    public class HybridTextMeshAuthoringBaker : Baker<HybridTextMeshAuthoring>
    {
        public override void Bake(HybridTextMeshAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var rt = authoring.GetComponent<RectTransform>();
            authoring.textTransform.rect = rt.rect;
            AddComponent(entity, authoring.textTransform);

            //Account for pivot, so pivot could be anywhere and not influencing starting position of glyphs.
            var shiftBack = rt.pivot;
            shiftBack.y = 1 - shiftBack.y;
            shiftBack *= rt.sizeDelta;
            var translation = new LocalTransform();
            translation.Position.x -= shiftBack.x;
            translation.Position.y += shiftBack.y;
            AddComponent(entity, translation);

            AddComponent<FontMetrics>(entity);

            var ea = new NativeArray<GlyphEntityGroup>(authoring.persistentCharacterEntities, Allocator.Temp);
            for (var i = 0; i < authoring.persistentCharacterEntities; i++)
            {
                Entity persistentCharacter = CreateAdditionalEntity(TransformUsageFlags.Dynamic, false, $"{authoring.name}_Char{i}");
                
                foreach (var type in ArchetypeCollection.CharacterTypes)
                    AddComponent(persistentCharacter, type);

                ea[i] = new GlyphEntityGroup { character = persistentCharacter };

                //buffer.Add(new CharacterEntityGroup {character = persistentCharacter});
                AddComponent(persistentCharacter, new Parent { Value = entity });
                AddComponent<Prefab>(persistentCharacter);
            }

            var buffer = AddBuffer<GlyphEntityGroup>(entity);
            buffer.AddRange(ea);

            AddComponent(entity, new TextContent { text = authoring.text });

            authoring.textStructure.persistentCharacterEntityMode = authoring.persistentCharacterEntities > 0;
            AddComponent(entity, authoring.textStructure);


            AddSharedComponentManaged(entity, new FontAssetHolder { htmFontAsset = authoring.htmFontAsset });
        }
    }
}