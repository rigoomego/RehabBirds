using nuitrack;
using NuitrackSDK.Frame;
using UnityEngine;
using UnityEngine.UI;
using NuitrackSDK;

/*
 * DrawColorFrame
 * --------------
 * Muestra el frame de color (RGB) proporcionado por Nuitrack en un RawImage de Unity.
 *
 * Funcionamiento:
 *  - Se suscribe al evento NuitrackManager.onColorUpdate en Start().
 *  - Cada vez que llega un ColorFrame, lo convierte a Texture2D y lo asigna al RawImage.
 *  - Al destruir el objeto, se desuscribe del evento para evitar fugas de memoria.
 *
 * Nota TFG: Solo se han añadido comentarios. No se ha modificado ninguna línea de código.
 */
public class DrawColorFrame : MonoBehaviour
{
    // RawImage del Canvas donde se renderiza el frame de cámara
    [SerializeField] RawImage background;

    void Start()
    {
        // Suscribir evento de actualización de frame
        NuitrackManager.onColorUpdate += DrawColor;
    }

    // Callback llamado por Nuitrack cuando hay un nuevo frame de color
    void DrawColor(ColorFrame frame)
    {
        // Seguridad: comprobar referencias antes de asignar textura
        if (background != null && frame != null)
        {
            // Convierte el frame a Texture2D (función del SDK) y lo muestra en UI
            background.texture = frame.ToTexture2D();
        }
    }

    void OnDestroy()
    {
        // Cancelar suscripción para evitar memory leaks
        NuitrackManager.onColorUpdate -= DrawColor;
    }
}
