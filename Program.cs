using System.Numerics;
using UnityEngine;

public class SoilVFXController : MonoBehaviour
{
    [Header("Эффект земли")]
    public ParticleSystem mudParticles; // Ссылка на систему частиц

    // Этот метод Человек 2 (или тот, кто пишет логику втыкания) 
    // вызовет в момент, когда лопата коснулась земли И зажат триггер
    public void PlayDiggingEffect(Vector3 contactPoint)
    {
        if (mudParticles != null)
        {
            // Перемещаем частицы ровно в точку втыкания лопаты
            mudParticles.transform.position = contactPoint;

            // Выбрасываем порцию земли
            mudParticles.Play();
        }
    }

    // Метод для остановки эффекта, когда лопату достали из земли
    public void StopDiggingEffect()
    {
        if (mudParticles != null && mudParticles.isPlaying)
        {
            mudParticles.Stop();
        }
    }
}