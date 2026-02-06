using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/*
 * CalibrationController
 * ---------------------
 * Controla la escena de calibración:
 *  - Gestiona botones (start/stop/repeat/continue).
 *  - Controla el inicio/fin de captura en BiomechanicalAnalyzer.
 *  - Permite registrar repeticiones de calibración mediante clicks en "repeat".
 *  - Al finalizar, guarda resultados en SessionData y cambia a la escena de juego.
 *
 * Nota TFG: Se añade exportación de CSV de calibración (alturas UI por repetición + media + 0.9).
 */
public class CalibrationController : MonoBehaviour
{
    [Header("Referencias")]
    // Analizador biomecánico que realiza la captura/calibración
    public BiomechanicalAnalyzer analyzer;

    [Header("Botones")]
    public Button startButton;
    public Button stopButton;
    public Button continueButton;
    public Button repeatButton;

    [Header("Texto de estado")]
    public TMP_Text statusText;

    // Control interno para saber si está en modo calibración
    private bool isCalibrating = false;

    // ---------------------------------------------------------
    // NUEVO (TFG): registro de máximos por repetición en calibración
    // ---------------------------------------------------------
    private List<float> repMaxHeights_UI_px = new List<float>();
    private List<float> repMaxAngles_deg = new List<float>();

    void Start()
    {
        // Asigna listeners a botones (Unity UI)
        if (startButton != null) startButton.onClick.AddListener(StartCalibration);
        if (stopButton != null) stopButton.onClick.AddListener(StopCalibration);
        if (continueButton != null) continueButton.onClick.AddListener(ContinueToNextScene);
        if (repeatButton != null) repeatButton.onClick.AddListener(EndRepetitionAndPrepareNext);

        // Estado inicial: solo start activo
        SetButtonsState(true, false, false, false);
    }

    // Helper para activar/desactivar botones según fase
    void SetButtonsState(bool start, bool stop, bool cont, bool repeat)
    {
        if (startButton != null) startButton.gameObject.SetActive(start);
        if (stopButton != null) stopButton.gameObject.SetActive(stop);
        if (continueButton != null) continueButton.gameObject.SetActive(cont);
        if (repeatButton != null) repeatButton.gameObject.SetActive(repeat);
    }

    // Inicia calibración: arranca captura del analyzer
    void StartCalibration()
    {
        if (analyzer == null) return;

        // --- CORRECCIÓN CLAVE ---
        // Forzamos que empiece en 0. Así: 0 + 10 clicks = 10.
        analyzer.currentRepetition = 0;
        // ------------------------

        repMaxHeights_UI_px.Clear();
        repMaxAngles_deg.Clear();

        analyzer.StartCapture();
        isCalibrating = true;

        // Durante calibración: se usa botón repeat para registrar repeticiones
        SetButtonsState(false, false, false, true);

        // Asegúrate de que STOP esté visible si quieres poder parar
        if (stopButton != null) stopButton.gameObject.SetActive(true);
    }

    // Registra una repetición de calibración (EndRepetition) y decide si finalizar
    void EndRepetitionAndPrepareNext()
    {
        if (!isCalibrating || analyzer == null) return;

        // Sumamos 1 al contador
        analyzer.EndRepetition();

        // NUEVO (TFG): guardamos máximos de ESA repetición para Excel
        repMaxHeights_UI_px.Add(analyzer.LastRepMaxHandHeight_UI_px);
        repMaxAngles_deg.Add(analyzer.LastRepMaxAngle_deg);

        // --- CORRECCIÓN LÓGICA ---
        // Ahora checkeamos con >=. 
        // Si acabamos de hacer la 10, currentRepetition será 10.
        // 10 >= 10 es TRUE -> Se detiene.
        if (analyzer.currentRepetition >= BiomechanicalAnalyzer.MAX_REPETITIONS)
        {
            StopCalibration();
        }
    }

    // Detiene calibración y muestra UI final (continuar)
    void StopCalibration()
    {
        if (!isCalibrating) return;
        analyzer.StopCapture();
        isCalibrating = false;

        // NUEVO (TFG): exportar CSV de calibración
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveCalibrationData(
                repMaxHeights_UI_px,
                repMaxAngles_deg,
                analyzer.averageMaxHandHeight,
                analyzer.averageMaxHandHeight_Safe,
                analyzer.safeHeightFactor
            );
        }

        if (statusText != null)
        {
            statusText.text = $"✅ ¡Has finalizado!\n\nMantén sobre CONTINUAR.";
            statusText.color = Color.green;
        }

        // Al final: mostrar botón continuar
        SetButtonsState(false, false, true, false);
    }

    // Pasa a la escena siguiente, guardando datos en SessionData
    void ContinueToNextScene()
    {
        // Tu lógica de guardado y cambio de escena.
        if (analyzer == null) return;
        if (statusText != null) statusText.text = "Cargando siguiente escena.";

        // Persistencia de resultados de calibración (usados luego por BirdSpawner)
        if (SessionData.Instance != null)
        {
            // IMPORTANTE: ya guardamos el valor seguro (media * 0.9) en el analyzer
            SessionData.Instance.MaxHandHeight = analyzer.averageMaxHandHeight_Safe;

            SessionData.Instance.MaxAbductionAngle = analyzer.averageMaxAbductionAngle;
            SessionData.Instance.TotalCompensations = analyzer.compensationCount;
            SessionData.Instance.TotalTrunkCompensations =
                analyzer.trunkLateralCompensationCount + analyzer.trunkForwardCompensationCount;
        }

        // Carga escena de juego
        SceneManager.LoadScene("TFEscene");
    }
}
