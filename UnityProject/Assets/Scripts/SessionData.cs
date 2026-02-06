using UnityEngine;

/*
 * SessionData
 * -----------
 * Singleton persistente para almacenar datos globales entre escenas (calibración -> juego).
 * Guarda métricas de referencia (p.ej. altura máxima de mano segura, ángulo máximo) y contadores.
 *
 * Se marca como DontDestroyOnLoad para que sobreviva cambios de escena.
 */
public class SessionData : MonoBehaviour
{
    public static SessionData Instance { get; private set; }

    // Resultados globales de calibración/sesión
    public float MaxAbductionAngle { get; set; }
    public float MaxHandHeight { get; set; } // En tu sistema actual: ALTURA SEGURA (UI px relativos al torso)

    // NUEVO (TFG): para demostrar en el Excel cómo se calcula el umbral
    // Media de máximos en calibración (UI px relativos al torso)
    public float CalibrationMeanHandHeight_UI_px { get; set; }

    // Altura segura en calibración (media * 0.9), UI px relativos al torso
    public float CalibrationSafeHandHeight_UI_px { get; set; }

    // Compensaciones acumuladas
    public int TotalCompensations { get; set; } // Hombro
    public int TotalTrunkCompensations { get; set; } // Tronco (Lateral X)
    public int TotalTrunkArchCompensations { get; set; } // Espalda (Arqueo Z) <-- NUEVO si lo usabas

    private void Awake()
    {
        // Patrón singleton: asegura una única instancia
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Persiste entre escenas
        DontDestroyOnLoad(gameObject);
    }
}
