using UnityEngine;

// Pasa los choques físicos de la bola y de los jugadores a MatchAudio.
// Lo añade MatchAudio automáticamente: no hay que ponerlo a mano.
public class MatchAudioRelay : MonoBehaviour
{
    public enum Kind { Ball, Skater }

    MatchAudio owner;
    Kind kind;
    Skater skater;

    public void Setup(MatchAudio audio, Kind k)
    {
        owner = audio;
        kind = k;
        skater = GetComponent<Skater>();
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (owner == null) return;
        if (kind == Kind.Ball) owner.OnBallCollision(c);
        else if (skater != null) owner.OnSkaterCollision(skater, c);
    }
}
