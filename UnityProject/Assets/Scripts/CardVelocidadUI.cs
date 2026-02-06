using UnityEngine;
using TMPro;

/*
 * CardVelocidadUI
 * ---------------
 * Tarjeta/Panel UI específico para mostrar velocidad actual y máxima
 * calculadas por BiomechanicalAnalyzer durante la captura.
 *
 * Si no hay usuario detectado o no se está capturando, muestra "---".
 *
 * Nota TFG: Solo se han añadido comentarios. No se ha modificado ninguna línea de código.
 */
public class CardVelocidadUI : MonoBehaviour
{
    [Header("Referencia al Analyzer")]
    // Origen de datos (velocidad actual y máxima)
    public BiomechanicalAnalyzer analyzer;

    [Header("Textos UI")]
    public TextMeshProUGUI velocidadActualText;
    public TextMeshProUGUI velocidadMaximaText;

    [Header("Formato")]
    [Tooltip("Número de decimales a mostrar")]
    public int decimals = 2;

    void Awake()
    {
        // Autodetección por seguridad
        if (analyzer == null)
            analyzer = FindObjectOfType<BiomechanicalAnalyzer>();
    }

    void Update()
    {
        if (analyzer == null)
            return;

        // Si no hay usuario detectado o no se está capturando
        if (!analyzer.IsUserDetected() || !analyzer.isCapturing)
        {
            if (velocidadActualText != null)
                velocidadActualText.text = "---";

            if (velocidadMaximaText != null)
                velocidadMaximaText.text = "---";

            return;
        }

        // Formato numérico (F2, F3, etc.)
        string format = "F" + decimals;

        if (velocidadActualText != null)
            velocidadActualText.text = analyzer.currentHandVelocity_mps.ToString(format);

        if (velocidadMaximaText != null)
            velocidadMaximaText.text = analyzer.maxHandVelocity_mps.ToString(format);
    }
}
