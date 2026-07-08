using System;
using UnityEngine;

/// <summary>
/// Anexe este script no MESMO GameObject que tem o ParticleSystem (shotParticles).
/// Ele repassa o evento de colisão pra quem quiser ouvir.
/// Ative o Collision Module do ParticleSystem e marque "Send Collision Messages" = true.
/// </summary>
public class ParticleCollisionForwarder : MonoBehaviour
{
    public event Action<GameObject> OnCollision;

    private void OnParticleCollision(GameObject other)
    {
        OnCollision?.Invoke(other);
    }
}
