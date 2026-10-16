using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EraCollectionArtAuthoring
{
    [MenuItem("Tools/Kingdom/UI/Apply Generated Era Card Art")]
    public static void Author()
    {
        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            string artPath = "Assets/Resources/Art/UI/EraCards/" + era + ".png";
            AssetDatabase.ImportAsset(artPath);
            var importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing generated era art: " + artPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(180, 80, 180, 80);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            foreach (string kind in new[] { KingdomUIPrefabLibrary.ResourceCard, KingdomUIPrefabLibrary.BuildingCard })
            {
                string path = "Assets/Resources/UI/Kingdom/" + KingdomUIRoot.GetEraCollectionPrefabName(kind, era) + ".prefab";
                GameObject card = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Image surface = card.GetComponent<Image>();
                    surface.sprite = art;
                    surface.type = Image.Type.Sliced;
                    surface.pixelsPerUnitMultiplier = 4;
                    surface.color = Color.white;
                    // Generated artwork owns the visible frame; retain serialized decoration IDs.
                    card.transform.Find("EraDesign").gameObject.SetActive(false);
                    RectTransform title = card.transform.Find("Label").GetComponent<TMP_Text>().rectTransform;
                    title.offsetMax = new Vector2(title.offsetMax.x, -20);
                    RectTransform metadata = card.transform.Find("Metadata").GetComponent<TMP_Text>().rectTransform;
                    metadata.offsetMin = new Vector2(metadata.offsetMin.x, 18);
                    PrefabUtility.SaveAsPrefabAsset(card, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(card); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EraCardArt] Seven restrained era-specific ImageGen textures applied to fourteen authored cards.");
    }
}
