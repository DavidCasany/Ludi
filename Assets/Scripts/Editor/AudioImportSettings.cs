using UnityEditor;
using UnityEngine;

// Configura AUTOMÁTICAMENTE la importación de los audios generados (no hay que tocarlos a mano).
//  - .../Audio/Music/ : música -> Compressed In Memory + Vorbis 70 %, estéreo
//    (los loops suenan sin cortes y la pausa puede sincronizarse al sample)
//  - .../Audio/SFX/   : efectos -> Decompress On Load + PCM, mono (sin retardo al sonar)
// Vale tanto para Assets/Audio como para Assets/Resources/Audio. Se aplica al importar o al hacer Reimport.
// Va en una carpeta "Editor": no entra en la build del juego.
public class AudioImportSettings : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        bool isMusic = path.Contains("/Audio/Music/");
        bool isSfx = path.Contains("/Audio/SFX/");
        if (!isMusic && !isSfx) return;

        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (isMusic)
        {
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.forceToMono = false;
            importer.loadInBackground = true;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.forceToMono = true;
            importer.loadInBackground = false;
        }

        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}
