using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/*
 * InstructionPanelController
 * --------------------------
 * Controla el panel de instrucciones (Panel A) mostrando mensajes al usuario.
 * Implementa:
 *  - Mensaje base persistente (msgBase).
 *  - Mensajes de evento (hit, miss, compensaciones, fin de serie).
 *  - Selección aleatoria de mensajes positivos en aciertos.
 *  - Temporización: evita spam de mensajes (minTimeBetweenMessages) y vuelve al mensaje base
 *    tras un tiempo (eventMessageDuration).
 *  - Bloqueo al final de serie para mantener mensaje final (isLocked).
 *
 * 
 */
public class InstructionPanelController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI instructionText;

    [Header("Mensajes")]
    [TextArea] public string msgBase = "Levanta el brazo y alcanza el pájaro";

    [Tooltip("Mensajes positivos (se elige uno al azar). Mete 3 aquí.")]
    public List<string> hitMessages = new List<string>()
    {
        "Muy bien, sigue así.",
        "¡Buen trabajo!",
        "¡Genial, continúa!"
    };

    [TextArea] public string msgMiss = "No pasa nada, inténtalo otra vez.";
    [TextArea] public string msgManyComp = "Hazlo lento y con calma.";
    [TextArea] public string msgSerieEnd = "¡Serie completada! Descansa un minuto.";

    [Header("Timing")]
    public float minTimeBetweenMessages = 1.0f;
    public float eventMessageDuration = 2.0f;

    // Control de frecuencia de mensajes y coroutine para revertir al mensaje base
    private float lastMsgTime = -999f;
    private Coroutine revertRoutine;

    // Bloqueo al final de serie
    private bool isLocked = false;

    void Start()
    {
        // Inicializa el panel con el mensaje base
        SetText(msgBase);
    }

    // Llamado al empezar la serie: desbloquea y vuelve al mensaje base
    public void OnSerieStart()
    {
        isLocked = false;

        if (revertRoutine != null)
        {
            StopCoroutine(revertRoutine);
            revertRoutine = null;
        }

        SetText(msgBase);
        lastMsgTime = Time.time;
    }

    // Evento: acierto (muestra mensaje positivo aleatorio)
    public void OnHit()
    {
        if (isLocked) return;
        string msg = Pick(hitMessages, fallback: "Muy bien, sigue así.");
        ShowMessage(msg, eventMessageDuration);
    }

    // Evento: fallo
    public void OnMiss()
    {
        if (isLocked) return;
        ShowMessage(msgMiss, eventMessageDuration);
    }

    // Evento: muchas compensaciones detectadas
    public void OnManyCompensations()
    {
        if (isLocked) return;
        ShowMessage(msgManyComp, eventMessageDuration);
    }

    // Evento: fin de serie (bloquea mensaje)
    public void OnSerieEnd()
    {
        isLocked = true;

        if (revertRoutine != null)
        {
            StopCoroutine(revertRoutine);
            revertRoutine = null;
        }

        SetText(msgSerieEnd);
        lastMsgTime = Time.time;
    }

    // Muestra un mensaje temporal y programa la vuelta al mensaje base
    private void ShowMessage(string msg, float revertAfterSeconds)
    {
        // Evita mensajes demasiado seguidos
        if (Time.time - lastMsgTime < minTimeBetweenMessages)
            return;

        lastMsgTime = Time.time;

        // Si ya había una coroutine de revert, se reemplaza
        if (revertRoutine != null)
            StopCoroutine(revertRoutine);

        SetText(msg);

        if (revertAfterSeconds > 0f)
            revertRoutine = StartCoroutine(RevertToBaseAfter(revertAfterSeconds));
    }

    // Coroutine: espera X segundos y vuelve al mensaje base si no está bloqueado
    private IEnumerator RevertToBaseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (!isLocked)
            SetText(msgBase);

        revertRoutine = null;
    }

    // Aplica texto al UI
    private void SetText(string msg)
    {
        if (instructionText != null)
            instructionText.text = msg;
    }

    // Selección aleatoria con fallback si la lista está vacía
    private string Pick(List<string> list, string fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        return list[Random.Range(0, list.Count)];
    }
}
