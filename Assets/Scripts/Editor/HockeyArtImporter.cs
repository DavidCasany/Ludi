#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Configura sola la importación de las imágenes de Assets/Resources/Art/ (las que usa UIArt):
//  - Sprite, sin mipmaps, con transparencia
//  - ButtonFrame.png con bordes de 9-slice, para que el marco se pueda estirar sin deformar los tornillos
// Si cambiáis el dibujo del botón y la decoración de la derecha cambia de tamaño, ajustad ButtonBorder.
public class HockeyArtImporter : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Art/";

    // izquierda, abajo, derecha, arriba (en píxeles de ButtonFrame.png, 1216 × 478)
    public static readonly Vector4 ButtonBorder = new Vector4(110f, 110f, 370f, 110f);

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.spritePixelsPerUnit = 100f;
        ti.wrapMode = TextureWrapMode.Clamp;

        // Panells i barres de la interfície (Tools/gen_ui_sprites.py): 9-slice
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        float border = file == "ui_steel" || file == "ui_paper" ? 64f
                     : file == "ui_steel_s" || file == "ui_paper_s" ? 28f
                     : file == "ui_track" || file == "ui_fill" ? 9f : 0f;
        if (border > 0f)
        {
            TextureImporterSettings st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
            ti.spriteBorder = new Vector4(border, border, border, border);
            ti.filterMode = FilterMode.Bilinear;
        }

        if (assetPath.EndsWith("ButtonFrame.png"))
        {
            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;   // necesario para el 9-slice
            ti.SetTextureSettings(s);
            ti.spriteBorder = ButtonBorder;
        }
    }
}
#endif
