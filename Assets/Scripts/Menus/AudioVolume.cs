using UnityEngine;

// Pon este componente en cada AudioSource del juego y elige si es Música o Efectos.
// El volumen que pongas en el AudioSource es el "volumen base" de ese sonido;
// el final es  base × volumen del canal en Ajustes, y se actualiza al momento si se cambia.
//
// Para sonidos lanzados por código sin AudioSource propio:
//   AudioSource.PlayClipAtPoint(clip, pos, GameSettings.SfxVolume);
[RequireComponent(typeof(AudioSource))]
public class AudioVolume : MonoBehaviour
{
    public enum Channel { Music, Effects }

    [SerializeField] Channel channel = Channel.Effects;

    AudioSource source;
    float baseVolume;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    void Apply()
    {
        float channelVolume = channel == Channel.Music ? GameSettings.MusicVolume : GameSettings.SfxVolume;
        source.volume = baseVolume * channelVolume;
    }
}
