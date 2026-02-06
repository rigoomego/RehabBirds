using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * ModernDashboardUI
 * -----------------
 * Controla la interfaz principal del videojuego (paneles A/B/C) mostrando:
 *  - Overlays sobre el vídeo (ángulo actual, objetivo completado, estado de tracking).
 *  - Card de ángulo (valor actual, máximo y gauge/slider).
 *  - Card de progreso (repeticiones, porcentaje, sliders).
 *  - Card de compensaciones (warning panel con cambio de color + contadores).
 *
 * Se actualiza cada frame leyendo valores del BiomechanicalAnalyzer y BirdSpawner.
 *
 * 
 */
public class ModernDashboardUI : MonoBehaviour
{
    [Header("CONEXIONES PRINCIPALES")]
    public BiomechanicalAnalyzer analyzer;
    public BirdSpawner spawner;

    [Header("--- SECCIÓN 1: VIDEO OVERLAYS ---")]
    public TextMeshProUGUI overlayAngleText;
    public TextMeshProUGUI overlayObjectiveText;
    public Image trackingStatusDot;
    public TextMeshProUGUI trackingStatusText;

    [Header("--- SECCIÓN 2: CARD ÁNGULO ---")]
    public TextMeshProUGUI currentAngleText;
    public TextMeshProUGUI maxAngleText;
    public Slider angleGaugeSlider;
    public Image angleFillImage;

    [Header("--- SECCIÓN 3: CARD PROGRESO ---")]
    public TextMeshProUGUI repsBigNumber;
    public Slider repsSlider;
    public TextMeshProUGUI progressPercentText;

    [Header("--- SECCIÓN 4: CARD COMPENSACIONES ---")]
    [Tooltip("Arrastra aquí: Card_Compensaciones > WarningBox")]
    public GameObject compensationsWarningPanel;

    [Tooltip("Arrastra aquí: Card_Compensaciones > Titulo (Total General)")]
    public TextMeshProUGUI compTotalText;

    [Tooltip("Arrastra aquí: comp_hombro")]
    public TextMeshProUGUI compShoulderText;

    [Tooltip("Arrastra aquí: comp_tronco")]
    public TextMeshProUGUI compTrunkText;

    [Header("--- OTRAS SECCIONES (opcionales) ---")]
    public TextMeshProUGUI patientNameText;
    public TextMeshProUGUI sessionIDText;
    public TextMeshProUGUI mainInstructionText;
    public Slider globalProgressSlider;

    [Header("Paleta de Colores")]
    public Color colorBlue = new Color32(96, 165, 250, 255);

    // Colores aviso
    private Color colorBaseAmber = new Color32(245, 158, 11, 50);
    private Color colorWarningRed = new Color32(239, 68, 68, 50);

    private Image warningPanelImage;

    void Start()
    {
        // Configuración inicial de sliders y colores
        if (angleGaugeSlider != null)
        {
            angleGaugeSlider.minValue = 0f;
            angleGaugeSlider.maxValue = 180f;
        }

        if (repsSlider != null)
        {
            repsSlider.minValue = 0f;
            repsSlider.maxValue = 1f;
            repsSlider.value = 0f;
        }

        if (angleFillImage != null) angleFillImage.color = colorBlue;

        // Panel de warning de compensaciones (activo por defecto)
        if (compensationsWarningPanel != null)
        {
            compensationsWarningPanel.SetActive(true);
            warningPanelImage = compensationsWarningPanel.GetComponent<Image>();
            if (warningPanelImage != null) warningPanelImage.color = colorBaseAmber;
        }

        // Auto-conexión si no se asignó en inspector
        if (analyzer == null) analyzer = FindObjectOfType<BiomechanicalAnalyzer>();
        if (spawner == null) spawner = FindObjectOfType<BirdSpawner>();
    }

    void Update()
    {
        // Sin analyzer no hay métricas que pintar
        if (analyzer == null) return;

        // Estado de tracking del usuario
        bool userDetected = analyzer.IsUserDetected();

        // 1) ÁNGULOS (valores instantáneo y máximo)
        float currentAngle = analyzer.currentAbductionAngle;
        float maxAngle = analyzer.maxAbductionAngle;

        if (overlayAngleText != null)
            overlayAngleText.text = userDetected ? $"{currentAngle:F1}°" : "--.-°";

        // 2) PROGRESO JUEGO (reps completadas vs total)
        int completedReps = (spawner != null) ? spawner.GetCompletedReps() : 0;
        int totalReps = (spawner != null) ? spawner.repsPerSerie : 10;

        if (overlayObjectiveText != null)
            overlayObjectiveText.text = $"{completedReps}/{totalReps}";

        // 3) CARD ÁNGULO
        if (currentAngleText != null)
            currentAngleText.text = userDetected ? $"{currentAngle:F1}°" : "--.-°";

        if (maxAngleText != null)
            maxAngleText.text = userDetected ? $"{maxAngle:F1}°" : "--.-°";

        if (angleGaugeSlider != null)
            angleGaugeSlider.value = userDetected ? currentAngle : 0f;

        // 4) CARD PROGRESO
        float progressFactor = (totalReps > 0) ? Mathf.Clamp01((float)completedReps / totalReps) : 0f;

        if (repsBigNumber != null)
            repsBigNumber.text = $"{completedReps}/{totalReps}";

        if (repsSlider != null)
            repsSlider.value = progressFactor;

        if (progressPercentText != null)
            progressPercentText.text = $"{(progressFactor * 100f):F0}% completado";

        if (globalProgressSlider != null)
            globalProgressSlider.value = progressFactor;

        // 5) CARD COMPENSACIONES (sin arqueo)
        if (warningPanelImage != null)
        {
            // Cambia color del panel según si hay compensación activa en vivo
            Color targetColor = analyzer.isCompensating ? colorWarningRed : colorBaseAmber;
            warningPanelImage.color = targetColor;
        }

        // Total de compensaciones (según lógica del analyzer)
        if (compTotalText != null)
            compTotalText.text = analyzer.TotalRepCompensationCount.ToString();

        if (compShoulderText != null)
            compShoulderText.text = $"Hombro: {analyzer.compensationCount}";

        // ? Tronco solo lateral X (ya no existe arqueo)
        if (compTrunkText != null)
            compTrunkText.text = $"Tronco: {analyzer.trunkLateralCompensationCount}";
    }
}
