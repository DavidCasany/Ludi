using UnityEditor;
using UnityEngine;

// Configura sola la importació dels sprites del jugador i dels patrons de samarreta.
// Qualsevol PNG que es posi a Assets/Sprites/Player/ o Assets/Sprites/Patterns/ queda com a Sprite
// amb el mateix pivot (centre) i els mateixos Pixels Per Unit, perquè totes les capes encaixin.
//
// Només s'aplica la PRIMERA vegada que s'importa un fitxer: si després en canvies alguna opció a mà,
// es respecta. Per tornar-la a aplicar: esborra el .meta del PNG (o clic dret > Reimport després d'esborrar-lo).
public class PlayerSpriteImporter : AssetPostprocessor
{
    // Llenç de 1024 px amb el tors de 396 px d'ample -> el tors fa 1 unitat, igual que el collider del Skater (1x1)
    public const float PixelsPerUnit = 396f;

    static bool Applies(string path) => Normalized(path);

    static bool Normalized(string path)
    {
        path = path.Replace('\\', '/');
        return path.StartsWith("Assets/Sprites/Player/") || path.StartsWith("Assets/Sprites/Patterns/");
    }

    void OnPreprocessTexture()
    {
        if (!Applies(assetPath) || !assetImporter.importSettingsMissing) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;   // grisos exactes: el tint surt net

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spriteAlignment = (int)SpriteAlignment.Center;   // totes les capes giren sobre el mateix punt
        s.spritePixelsPerUnit = PixelsPerUnit;
        s.spriteMeshType = SpriteMeshType.Tight;
        ti.SetTextureSettings(s);
    }
}
