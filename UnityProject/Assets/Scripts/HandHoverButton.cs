using UnityEngine;
using UnityEngine.UI;
using NuitrackSDK;

/// <summary>
/// Presiona un botn UI manteniendo la mano/mueca sobre l durante X segundos.
/// NOTA IMPORTANTE: Para forzar el "mantener pulsado", debes DESMARCAR la casilla
/// 'Interactable' del componente Button en el Inspector de Unity.
/// </summary>
/*
 * HandHoverButton
 * ---------------
 * Implementa un "hover click" (pulsación por permanencia) usando joints de Nuitrack.
 * Cada frame:
 *  - Lee la articulación seleccionada (mano o muñeca, izquierda o derecha).
 *  - Convierte su posición a coordenadas UI del canvas padre.
 *  - Si la posición cae dentro del rectángulo del botón, acumula tiempo.
 *  - Si el tiempo supera activationTime, ejecuta uiButton.onClick.Invoke().
 *
 * Incluye una imagen opcional progressImage (Fill) para mostrar progreso visual.
 *
 * 
 */
[RequireComponent(typeof(Button))]
public class HandHoverButton : MonoBehaviour
{
    [Header("Configuracin")]
    [Tooltip("Tiempo (s) que la mano debe permanecer sobre el botn para activarlo.")]
    public float activationTime = 2f;

    [Tooltip("Si es verdadero, usa la mueca; si falso, la mano.")]
    public bool useWrist = false;

    [Tooltip("Si es verdadero, se usa la mano derecha; si falso, la izquierda.")]
    public bool rightHand = true;

    [Header("Visual")]
    [Tooltip("Imagen opcional que muestra progreso (tipo Filled).")]
    public Image progressImage;

    // referencias internas
    UnityEngine.UI.Button uiButton;
    RectTransform rectTransform;
    RectTransform parentRect;

    float hoverTimer = 0f;

    // Bandera para evitar múltiples activaciones mientras la mano sigue encima
    private bool buttonActivatedThisCycle = false; // Bandera para evitar llamadas mltiples

    void Start()
    {
        // Cachea referencias UI
        uiButton = GetComponent<UnityEngine.UI.Button>();
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;

        // Inicializa progreso visual
        if (progressImage != null)
            progressImage.fillAmount = 0f;
    }

    void Update()
    {
        // Obtener usuario
        UserData user = NuitrackManager.Users.Current;
        if (user == null || user.Skeleton == null || !uiButton.gameObject.activeInHierarchy)
        {
            ResetProgress();
            return;
        }

        // Elegir articulacin (mano o muñeca, izquierda o derecha)
        nuitrack.JointType jointType;
        if (useWrist)
            jointType = rightHand ? nuitrack.JointType.RightWrist : nuitrack.JointType.LeftWrist;
        else
            jointType = rightHand ? nuitrack.JointType.RightHand : nuitrack.JointType.LeftHand;

        var joint = user.Skeleton.GetJoint(jointType);
        if (joint.Confidence < 0.3f)
        {
            ResetProgress();
            return;
        }

        // Obtener posicin anclada del joint respecto al rect del canvas padre.
        Vector2 jointUIPos = joint.AnchoredPosition(parentRect.rect, rectTransform);

        // Reflejo horizontal si usas la misma convencin
        jointUIPos.x = -jointUIPos.x;

        // Comprobar si el punto está dentro del rect del botón
        if (IsPointInsideRectTransform(jointUIPos, rectTransform))
        {
            // Solo si no ha sido activado ya
            if (!buttonActivatedThisCycle)
            {
                // Acumula tiempo de hover
                hoverTimer += Time.deltaTime;
                if (progressImage != null)
                    progressImage.fillAmount = Mathf.Clamp01(hoverTimer / activationTime);

                // Si supera el tiempo requerido, dispara el evento del botón
                if (hoverTimer >= activationTime)
                {
                    // Ejecuta el evento asociado al botón
                    uiButton.onClick.Invoke();
                    buttonActivatedThisCycle = true; // Activar bandera para este ciclo
                    ResetProgress(); // Reiniciar visualmente, pero la lgica de ButtonController maneja el flujo
                }
            }
        }
        else
        {
            // Si la mano sale del botón, se reinicia el progreso
            ResetProgress();
        }
    }

    void ResetProgress()
    {
        // Reinicia contador y UI de progreso
        hoverTimer = 0f;
        if (progressImage != null)
            progressImage.fillAmount = 0f;

        // La bandera se resetea cuando la mano sale del botn
        if (buttonActivatedThisCycle) buttonActivatedThisCycle = false;
    }

    /// <summary>
    /// Comprueba si un punto en coordenadas anchored coincide dentro del rect del RectTransform.
    /// </summary>
    bool IsPointInsideRectTransform(Vector2 anchoredPoint, RectTransform rt)
    {
        // Cálculo de límites del rectángulo del botón (en coordenadas anchored)
        Vector2 center = rt.anchoredPosition;
        Vector2 size = rt.rect.size;
        float left = center.x - size.x / 2f;
        float right = center.x + size.x / 2f;
        float bottom = center.y - size.y / 2f;
        float top = center.y + size.y / 2f;

        return (anchoredPoint.x >= left && anchoredPoint.x <= right &&
                anchoredPoint.y >= bottom && anchoredPoint.y <= top);
    }
}
