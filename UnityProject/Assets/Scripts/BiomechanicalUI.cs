using UnityEngine;
using TMPro;

public class BiomechanicalUI : MonoBehaviour
{
    [Header("Referencias")]
    public BiomechanicalAnalyzer analyzer;

    [Header("Configuración Visual")]
    // ARRASTRA AQUÍ TU FUENTE (Inter_18pt-Light SDF)
    public TMP_FontAsset fuentePersonalizada;

    [Header("Elementos de texto")]
    public TextMeshProUGUI angleText;
    public TextMeshProUGUI repetitionsText;
    public TextMeshProUGUI compensationsText;
    public TextMeshProUGUI statusText;

    [Header("Mensajes")]
    private const string MSG_ESPERANDO = "Esperando usuario...";
    private const string MSG_LISTO_PARA_START = "Colócate frente a la cámara.\n\nMantén la mano sobre START para comenzar.";
    private const string MSG_CALIBRANDO_OK = "Levanta el brazo al máximo (sin compensar).\n\nMantén sobre REPETIR para guardar.";

    // Mensajes de error/compensación
    private const string MSG_COMP_HOMBRO = "⚠ ¡COMPENSACIÓN!\nBaja el hombro y relájate.";
    private const string MSG_COMP_TRONCO_LAT = "⚠ ¡COMPENSACIÓN!\nEndereza el tronco (Lateral).";
    private const string MSG_COMP_TRONCO_FWD = "⚠ ¡CUIDADO!\nNo te inclines hacia adelante.";

    // Variables internas para los colores
    private Color colWarning; // F59E0B (Naranja/Ámbar)
    private Color colSuccess; // 34D399 (Verde suave)
    private Color colInfo;    // 60A5FA (Azul suave)

    void Start()
    {
        // 1. CONFIGURACIÓN DE COLORES (Desde HEX)
        ColorUtility.TryParseHtmlString("#F59E0B", out colWarning); // Para compensaciones (antes rojo)
        ColorUtility.TryParseHtmlString("#34D399", out colSuccess); // Para mensajes de éxito (antes verde)
        ColorUtility.TryParseHtmlString("#60A5FA", out colInfo);    // Para info/fin (antes azul/cyan)

        // 2. CONFIGURACIÓN DE TEXTOS INICIALES
        if (angleText != null) angleText.text = "Ángulo: ---";
        if (repetitionsText != null) repetitionsText.text = $"Repetición: 0/{BiomechanicalAnalyzer.MAX_REPETITIONS}";
        if (compensationsText != null) compensationsText.text = "Compensaciones: 0";

        // 3. APLICAR FUENTE AUTOMÁTICAMENTE
        AplicarFuente();
    }

    // Función para aplicar tu fuente a todos los textos referenciados
    void AplicarFuente()
    {
        if (fuentePersonalizada != null)
        {
            if (angleText != null) angleText.font = fuentePersonalizada;
            if (repetitionsText != null) repetitionsText.font = fuentePersonalizada;
            if (compensationsText != null) compensationsText.font = fuentePersonalizada;
            if (statusText != null) statusText.font = fuentePersonalizada;
        }
    }

    void Update()
    {
        if (analyzer == null) return;

        bool userDetected = analyzer.IsUserDetected();

        if (userDetected)
        {
            if (repetitionsText != null)
            {
                int repToShow = Mathf.Min(analyzer.currentRepetition, BiomechanicalAnalyzer.MAX_REPETITIONS);
                repetitionsText.text = $"Repetición: {repToShow}/{BiomechanicalAnalyzer.MAX_REPETITIONS}";
            }

            if (compensationsText != null)
                compensationsText.text = $"Compensaciones: {analyzer.TotalRepCompensationCount}";

            if (angleText != null)
                angleText.text = $"Ángulo: {analyzer.currentAbductionAngle:F1}° (Max: {analyzer.maxAbductionAngle:F1}°)";
        }
        else
        {
            if (angleText != null) angleText.text = "Ángulo: ---";
        }

        // Gestión de Mensajes y Colores
        if (statusText == null) return;

        if (!userDetected)
        {
            statusText.text = MSG_ESPERANDO;
            statusText.color = colWarning; // Usamos el color ámbar para espera también
            return;
        }

        if (analyzer.isCapturing)
        {
            // --- ALERTAS DE COMPENSACIÓN (Color F59E0B) ---
            if (analyzer.isCompensatingTrunkForward)
            {
                statusText.text = MSG_COMP_TRONCO_FWD;
                statusText.color = colWarning;
            }
            else if (analyzer.isCompensatingTrunkLateral)
            {
                statusText.text = MSG_COMP_TRONCO_LAT;
                statusText.color = colWarning;
            }
            else if (analyzer.isCompensatingShoulder)
            {
                statusText.text = MSG_COMP_HOMBRO;
                statusText.color = colWarning;
            }
            // --- TODO OK (Color 34D399) ---
            else
            {
                statusText.text = MSG_CALIBRANDO_OK;
                statusText.color = colSuccess;
            }
        }
        else
        {
            // --- FIN O ESPERA (Color 60A5FA o Blanco) ---
            if (analyzer.currentRepetition >= BiomechanicalAnalyzer.MAX_REPETITIONS)
            {
                statusText.text = "¡Calibración Finalizada!";
                statusText.color = colInfo; // Azul
            }
            else
            {
                statusText.text = MSG_LISTO_PARA_START;
                statusText.color = Color.white; // Blanco neutro para instrucciones base
            }
        }
    }
}