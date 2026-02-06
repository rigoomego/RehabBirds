using UnityEngine;

/*
 * BirdMover
 * ---------
 * Controla el movimiento del bird en UI (RectTransform.anchoredPosition).
 * Soporta:
 *  - Movimiento rectilíneo horizontal (Straight).
 *  - Movimiento oscilatorio vertical tipo "butterfly" (senoidal).
 *
 * Cuando el bird sale de los límites del contenedor (más un margen), llama a OnScreenExit()
 * del BirdInstance para que se procese la finalización de la aparición.
 *
 * Nota TFG: Solo se han añadido comentarios. No se ha modificado ninguna línea de código.
 */
public class BirdMover : MonoBehaviour
{
    // Dirección de movimiento horizontal (signo)
    float direction;

    // Velocidad horizontal en unidades UI (px/seg aprox según escala del canvas)
    float speed;

    // Contenedor UI donde se mueve (videoContainerRect)
    RectTransform containerRect;

    // Info del tipo de bird (velocidad, tipo de movimiento, amplitud, etc.)
    BirdSpawner.BirdTypeInfo info;

    // Parámetros de oscilación (si movementType == Butterfly)
    float waveAmplitude;
    float waveFrequency;
    float phaseOffset = 0f;
    float baseY;

    // RectTransform del bird
    RectTransform rect;

    // Flag para no mover antes de inicializar
    bool initialized = false;

    // Referencia al BirdInstance asociado (para notificar salida)
    BirdInstance birdInstance;

    [Header("Boundaries")]
    [Tooltip("Margen extra fuera de pantalla para considerar que 'salió'.")]
    public float exitMargin = 120f;

    // Inicializa el movimiento del bird (llamado por BirdSpawner al activar un bird)
    public void Initialize(float dir, float spd, RectTransform container, BirdSpawner.BirdTypeInfo typeInfo)
    {
        // Normaliza dirección a -1 o +1
        direction = Mathf.Sign(dir) == 0f ? 1f : Mathf.Sign(dir);
        speed = spd;

        containerRect = container;
        info = typeInfo;

        // Obtiene el RectTransform del bird (o lo añade si faltara)
        rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();

        // Necesita BirdInstance para notificar salida de pantalla
        birdInstance = GetComponent<BirdInstance>();
        if (birdInstance == null)
        {
            Debug.LogError("BirdMover: No se encontró BirdInstance en el objeto.");
            return;
        }

        // Guarda la altura base de inicio
        baseY = rect.anchoredPosition.y;

        // Configura oscilación si el tipo es Butterfly
        if (info != null && info.movementType == BirdSpawner.MovementType.Butterfly)
        {
            waveAmplitude = info.waveAmplitude;
            waveFrequency = info.waveFrequency;
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
        else
        {
            waveAmplitude = 0f;
            waveFrequency = 0f;
        }

        initialized = true;
    }

    void Update()
    {
        // Seguridad: no ejecutar si faltan referencias
        if (!initialized || rect == null || containerRect == null || birdInstance == null) return;

        // Movimiento horizontal continuo
        Vector2 pos = rect.anchoredPosition;
        pos.x += direction * speed * Time.deltaTime;

        // Oscilación vertical senoidal (si aplica)
        if (waveAmplitude > 0f)
        {
            float t = Time.time;
            pos.y = baseY + Mathf.Sin((t + phaseOffset) * waveFrequency * 2f * Mathf.PI) * waveAmplitude;
        }

        rect.anchoredPosition = pos;

        // Límite horizontal: medio ancho del contenedor + margen extra
        float boundX = (containerRect.rect.width / 2f) + exitMargin;

        // Si sale de pantalla, se considera finalizada la aparición
        if (Mathf.Abs(pos.x) > boundX)
        {
            initialized = false;
            birdInstance.OnScreenExit();
        }
    }
}
