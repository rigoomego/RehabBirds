using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("Configuración")]
    public string patientID = "Paciente_Prueba";

    [Header("Archivos CSV")]
    public string csvFilenameSummary = "Resultados_Sesion_Summary.csv";
    public string csvFilenameTimeseries = "Resultados_Sesion_Timeseries.csv";

    private string filePathSummary;
    private string filePathTimeseries;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string folderPath = Path.Combine(desktopPath, "Datos_Rehabilitacion");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        filePathSummary = Path.Combine(folderPath, csvFilenameSummary);
        filePathTimeseries = Path.Combine(folderPath, csvFilenameTimeseries);
    }

    // =========================================================
    // RESUMEN POR REPETICIÓN + RESUMEN GLOBAL + TABLA ADAPTACIÓN
    // =========================================================
    public void SaveSessionData(List<RepetitionResult> results)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("Repeticion;Acierto_01;AnguloMax_deg;VelocidadMax_mps;CompHombro_01;CompTroncoLat_01;CompTroncoFwd_01;MaxHandHeight_UI_px;SafeHeight_UI_px;BirdSpawnY_UI_px;TorsoSpawnY_UI_px;BirdRelativeHeight_UI_px");

        int totalHits = 0;
        int totalShoulder = 0;
        int totalTrunk = 0;
        int totalFwd = 0;

        float sumMaxAngle = 0f;
        float sumMaxVelocity = 0f;

        // Para stats de alturas máximas del JUEGO (para comparar)
        float sumSessionMaxHandHeights = 0f;
        int countSessionHeights = 0;
        float minSessionHeight = float.PositiveInfinity;
        float maxSessionHeight = float.NegativeInfinity;

        float sumBirdRelative = 0f;
        int countBirdRelative = 0;

        int n = (results != null) ? results.Count : 0;

        if (results != null)
        {
            foreach (var r in results)
            {
                int hit01 = r.isHit ? 1 : 0;
                int cShoulder01 = r.compShoulder ? 1 : 0;
                int cTrunk01 = r.compTrunk ? 1 : 0;
                int cFwd01 = r.compTrunkFwd ? 1 : 0;

                sb.AppendLine($"{r.repIndex};{hit01};{r.maxAngle:F2};{r.maxVelocity:F2};{cShoulder01};{cTrunk01};{cFwd01};{r.maxHandHeight_UI_px:F2};{r.safeHeight_UI_px:F2};{r.birdSpawnY_UI_px:F2};{r.torsoSpawnY_UI_px:F2};{r.birdRelativeHeight_UI_px:F2}");

                if (r.isHit) totalHits++;
                if (r.compShoulder) totalShoulder++;
                if (r.compTrunk) totalTrunk++;
                if (r.compTrunkFwd) totalFwd++;

                sumMaxAngle += r.maxAngle;
                sumMaxVelocity += r.maxVelocity;

                // Stats alturas de mano en el juego (solo valores útiles > 0)
                if (r.maxHandHeight_UI_px > 0f)
                {
                    sumSessionMaxHandHeights += r.maxHandHeight_UI_px;
                    countSessionHeights++;
                    if (r.maxHandHeight_UI_px < minSessionHeight) minSessionHeight = r.maxHandHeight_UI_px;
                    if (r.maxHandHeight_UI_px > maxSessionHeight) maxSessionHeight = r.maxHandHeight_UI_px;
                }

                // Media de altura real de salida del bird (relativa al torso)
                sumBirdRelative += r.birdRelativeHeight_UI_px;
                countBirdRelative++;
            }
        }

        float avgMaxAngle = (n > 0) ? (sumMaxAngle / n) : 0f;
        float avgMaxVelocity = (n > 0) ? (sumMaxVelocity / n) : 0f;
        float successRate = (n > 0) ? ((float)totalHits / n) * 100f : 0f;

        float sessionMeanHandHeight = (countSessionHeights > 0) ? (sumSessionMaxHandHeights / countSessionHeights) : 0f;
        float birdMeanRelativeHeight = (countBirdRelative > 0) ? (sumBirdRelative / countBirdRelative) : 0f;

        // Resumen general
        sb.AppendLine("");
        sb.AppendLine("--- RESUMEN DE SESION ---");
        sb.AppendLine($"TotalIntentos;{n}");
        sb.AppendLine($"TotalAciertos;{totalHits}");
        sb.AppendLine($"TotalFallos;{n - totalHits}");
        sb.AppendLine($"TasaExito_pct;{successRate:F2}");
        sb.AppendLine($"MediaAnguloMax_deg;{avgMaxAngle:F2}");
        sb.AppendLine($"MediaVelocidadMax_mps;{avgMaxVelocity:F2}");

        sb.AppendLine("");
        sb.AppendLine("--- DETALLE COMPENSACIONES ---");
        sb.AppendLine($"TotalCompHombro;{totalShoulder}");
        sb.AppendLine($"TotalCompTroncoLat;{totalTrunk}");
        sb.AppendLine($"TotalCompTroncoFwd;{totalFwd}");

        // =========================================================
        // NUEVO (TFG): TABLA ADAPTACIÓN DE ALTURA (Calibración vs Juego)
        // =========================================================
        float calMean = (SessionData.Instance != null) ? SessionData.Instance.CalibrationMeanHandHeight_UI_px : 0f;
        float calSafe = (SessionData.Instance != null) ? SessionData.Instance.CalibrationSafeHandHeight_UI_px : 0f;

        sb.AppendLine("");
        sb.AppendLine("--- ADAPTACION DE ALTURA (CALIBRACION vs JUEGO) ---");
        sb.AppendLine("Variable;Valor_UI_px;Descripcion");

        sb.AppendLine($"Calibracion_MediaMaxHandHeight_UI_px;{calMean:F2};Media de las alturas máximas mano-torso en calibración (reps válidas)");
        sb.AppendLine($"Calibracion_SafeHeight_UI_px;{calSafe:F2};Altura segura = Media × 0.9 (zona indolora)");
        sb.AppendLine($"Bird_AlturaMediaReal_UI_px;{birdMeanRelativeHeight:F2};Media de BirdRelativeHeight_UI_px en la sesión (debería ? SafeHeight)");
        sb.AppendLine($"Juego_MediaMaxHandHeight_UI_px;{sessionMeanHandHeight:F2};Media de alturas máximas mano-torso durante el juego (solo valores > 0)");
        sb.AppendLine($"Juego_MinMaxHandHeight_UI_px;{(countSessionHeights > 0 ? minSessionHeight : 0f):F2};Mínimo de alturas máximas en el juego (valores > 0)");
        sb.AppendLine($"Juego_MaxMaxHandHeight_UI_px;{(countSessionHeights > 0 ? maxSessionHeight : 0f):F2};Máximo de alturas máximas en el juego (valores > 0)");

        sb.AppendLine("");
        sb.AppendLine("Interpretacion; ;");
        sb.AppendLine("Comparacion_1; ;Bird_AlturaMediaReal_UI_px debería ser muy similar a Calibracion_SafeHeight_UI_px");
        sb.AppendLine("Comparacion_2; ;Juego_MediaMaxHandHeight_UI_px puede ser menor o mayor que SafeHeight dependiendo del rendimiento del usuario");

        // Metadatos
        sb.AppendLine("");
        sb.AppendLine($"Fecha;{DateTime.Now}");
        sb.AppendLine($"ID_Paciente;{patientID}");

        try
        {
            File.WriteAllText(filePathSummary, sb.ToString(), Encoding.UTF8);
            Debug.Log($"<color=green>CSV resumen guardado en: {filePathSummary}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error guardando CSV resumen: {e.Message}");
        }
    }

    // =========================================================
    // TIMESERIES
    // =========================================================
    public void SaveTimeSeriesData(List<MetricSample> samples)
    {
        if (samples == null || samples.Count == 0)
        {
            Debug.LogWarning("SaveTimeSeriesData: No hay muestras para guardar.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serie;Repeticion;TiempoRep_s;TiempoSesion_s;Angulo_deg;Velocidad_mps;AlturaMano_m;CompHombro_01;CompTroncoLat_01;CompTroncoFwd_01");

        foreach (var s in samples)
        {
            float tRep = (s.time_rep_s != 0f) ? s.time_rep_s : s.time_s;
            float tSess = s.time_session_s;

            int cS = s.compShoulder ? 1 : 0;
            int cTL = s.compTrunkLat ? 1 : 0;
            int cTF = s.compTrunkFwd ? 1 : 0;

            sb.AppendLine($"{s.serieIndex};{s.repIndex};{tRep:F3};{tSess:F3};{s.angle_deg:F2};{s.velocity_mps:F2};{s.handHeight_m:F3};{cS};{cTL};{cTF}");
        }

        sb.AppendLine("");
        sb.AppendLine($"Fecha;{DateTime.Now}");
        sb.AppendLine($"ID_Paciente;{patientID}");

        try
        {
            File.WriteAllText(filePathTimeseries, sb.ToString(), Encoding.UTF8);
            Debug.Log($"<color=green>CSV timeseries guardado en: {filePathTimeseries}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error guardando CSV timeseries: {e.Message}");
        }
    }
}
