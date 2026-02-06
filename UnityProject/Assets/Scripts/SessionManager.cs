using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * SessionManager
 * --------------
 * Gestor alternativo de resultados de sesión:
 *  - Almacena una lista de datos por repetición (hit, ángulo máximo, velocidad máxima, compensaciones).
 *  - Permite exportar la sesión a CSV dentro de Application.persistentDataPath.
 *
 * Nota: En tu proyecto convive con DataManager, pero este script mantiene su propio flujo de registro/exportación.
 *
 * 
 */
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;

    [Header("Referencias")]
    public BirdSpawner spawner;
    public BiomechanicalAnalyzer analyzer;

    // Estructura para cada repetición
    public class RepetitionData
    {
        public int repNumber;
        public bool hitSuccess;
        public float maxAngle;
        public float maxVelocity;
        public int shoulderComp;
        public int trunkLatComp;
        public int trunkFwdComp;
    }

    // Lista interna con todos los registros de repetición
    private List<RepetitionData> sessionStats = new List<RepetitionData>();

    void Awake() { Instance = this; }

    // Llamado por el BirdSpawner cada vez que un pájaro se va o es golpeado
    // Guarda un registro por repetición en sessionStats
    public void RecordRepetition(bool hit, float angle, float velocity, int sComp, int tlComp, int tfComp)
    {
        sessionStats.Add(new RepetitionData
        {
            repNumber = sessionStats.Count + 1,
            hitSuccess = hit,
            maxAngle = angle,
            maxVelocity = velocity,
            shoulderComp = sComp,
            trunkLatComp = tlComp,
            trunkFwdComp = tfComp
        });
    }

    // Exporta a CSV con cabecera, filas por repetición y un resumen final
    public void ExportToCSV()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "Sesion_Rehabilitacion_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".csv");
        StringBuilder sb = new StringBuilder();

        // Cabecera
        sb.AppendLine("Repeticion;Acierto;Angulo Max;Velocidad Max;Comp Hombro;Comp Tronco Lat;Comp Tronco Fwd");

        int totalHits = 0;
        int totalComp = 0;

        // Filas por repetición
        foreach (var data in sessionStats)
        {
            sb.AppendLine($"{data.repNumber};{(data.hitSuccess ? "SI" : "NO")};{data.maxAngle:F2};{data.maxVelocity:F2};{data.shoulderComp};{data.trunkLatComp};{data.trunkFwdComp}");
            if (data.hitSuccess) totalHits++;
            totalComp += (data.shoulderComp + data.trunkLatComp + data.trunkFwdComp);
        }

        // Resumen final
        float successRate = (float)totalHits / sessionStats.Count * 100f;
        sb.AppendLine("");
        sb.AppendLine($"RESUMEN;Tasa Aciertos: {successRate:F2}%;Total Compensaciones: {totalComp};;;;");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        Debug.Log("Archivo Excel guardado en: " + filePath);
    }
}
