using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NuitrackSDK;

/*
 * BirdSpawner
 * -----------
 * Controla la sesión de juego: crea un pool de birds a partir de prefabs,
 * gestiona series y repeticiones, activa birds en pantalla y registra resultados.
 *
 * Funciones clave:
 *  - InitializeBirdPool(): instancia los prefabs dentro del contenedor UI y añade BirdInstance/BirdMover.
 *  - SessionLoop(): coroutine principal que recorre series/repeticiones, spawnea y espera fin de apariciones.
 *  - ActivateBird(): calcula posición de spawn.
 *  - Notificaciones desde BirdInstance: hits/misses/fin de aparición y logging de datos.
 *
 * Nota TFG: En esta versión SOLO se añaden datos para exportación (validación en Excel)
 * y se adapta el spawn a la altura segura calculada en UI(px) relativa al torso.
 */
public class BirdSpawner : MonoBehaviour
{
    public enum SpawnSide { RightArm, LeftArm, Bilateral }
    public enum MovementType { Straight, Butterfly }

    [System.Serializable]
    public class BirdTypeInfo
    {
        public string displayName = "Bird";
        public GameObject prefab;
        public float speed = 200f;
        public int maxHealth = 5;
        public MovementType movementType = MovementType.Straight;

        [Header("Oscilación")]
        public float waveAmplitude = 40f;
        public float waveFrequency = 1.0f;

        public AudioClip hitSound;
    }

    [Header("Catálogo")]
    // Lista de tipos de birds disponibles (prefabs + parámetros)
    public List<BirdTypeInfo> birdTypes = new List<BirdTypeInfo>();

    // Índices de birds que se usarán en la sesión (subset del catálogo)
    public List<int> sessionBirdIndices = new List<int>();

    [Header("Canvas")]
    // Contenedor UI donde se instancian y se mueven los birds (RectTransform)
    public RectTransform videoContainerRect;

    [Header("Configuración")]
    // Nº de series y repeticiones por serie
    public int totalSeries = 1;
    public int repsPerSerie = 10;

    // Si está activo, el bird solo se puede golpear una vez por aparición
    public bool singleHitPerAppearance = true;

    // Tiempo de descanso entre series
    public float restTimeBetweenSeries = 10.0f;

    // Retraso entre aparición de birds
    [SerializeField] float delayBetweenBirds = 1.0f;

    [Header("Juego")]
    // Lado de aparición (derecho/izquierdo/bilateral)
    public SpawnSide spawnSide = SpawnSide.RightArm;

    // Máximo de birds simultáneos en pantalla
    public int maxBirdsOnScreen = 1;

    [Header("Referencias Externas (ARRASTRAR AQUÍ)")]
    // Analizador biomecánico (métricas, detección de usuario, etc.)
    public BiomechanicalAnalyzer analyzer;

    // Sistema de detección de golpes por proximidad UI
    public HandTouchUI handTouchUI;

    // Panel de instrucciones/feedback textual
    public InstructionPanelController instructionController;

    // UI
    public GameObject progressPanel;
    public TMP_Text progressText;

    // Runtime: lista de birds activos en pantalla y pool instanciado
    private List<BirdInstance> activeInstances = new List<BirdInstance>();
    private List<BirdInstance> birdPool = new List<BirdInstance>();

    // Contadores de sesión
    private int completedBirds = 0;
    private int currentHits = 0;
    private int poolIndex = 0;
    private int currentSerie = 1;

    // Lista para guardar los datos de la sesión (resumen por repetición)
    private List<RepetitionResult> sessionResultsLog = new List<RepetitionResult>();

    [Header("Spawn gating")]
    [Tooltip("Segundos de esqueleto estable antes de permitir el primer spawn.")]
    public float requiredStableTrackingSeconds = 0.3f;

    // -------------------------------------------------------------------------
    // NUEVO (TFG): registro de alturas reales de spawn (para validar en Excel)
    // -------------------------------------------------------------------------
    private float lastBirdSpawnY_UI_px = 0f;
    private float lastTorsoY_UI_px = 0f;
    private float lastBirdRelativeHeight_UI_px = 0f;

    void Start()
    {
        // Autoreferencia por seguridad si no se asignó en Inspector
        if (analyzer == null) analyzer = FindObjectOfType<BiomechanicalAnalyzer>();
        if (instructionController == null) instructionController = FindObjectOfType<InstructionPanelController>();

        // HandTouchUI necesita el mismo contenedor que los birds para convertir coordenadas correctamente
        if (handTouchUI != null && videoContainerRect != null)
            handTouchUI.parentRect = videoContainerRect;

        // (Opcional recomendado) Si tu Analyzer usa proyección UI, asegúrate de que conoce el contenedor
        if (analyzer != null && analyzer.uiReferenceRect == null && videoContainerRect != null)
            analyzer.uiReferenceRect = videoContainerRect;

        // Pre-instancia birds en pool
        InitializeBirdPool();

        if (progressPanel != null) progressPanel.SetActive(true);

        // Limpieza de datos previos
        sessionResultsLog.Clear();

        // Inicia el bucle principal de sesión
        StartCoroutine(SessionLoop());
    }

    bool InitializeBirdPool()
    {
        // Pooling: instanciamos una vez y luego solo activamos/desactivamos
        birdPool.Clear();
        if (birdTypes.Count == 0 || sessionBirdIndices.Count == 0) return false;

        // Nº de apariciones objetivo por tipo, en función de repsPerSerie
        int appearances = Mathf.Max(1, repsPerSerie / sessionBirdIndices.Count);

        foreach (int typeIndex in sessionBirdIndices)
        {
            if (typeIndex < 0 || typeIndex >= birdTypes.Count) continue;
            var info = birdTypes[typeIndex];

            // Instancia el prefab dentro del contenedor UI (así hereda correctamente la jerarquía UI)
            GameObject go = Instantiate(info.prefab, videoContainerRect);

            // Añade BirdInstance dinámicamente (gestiona vida/estado/feedback)
            BirdInstance bi = go.AddComponent<BirdInstance>();

            // Inicializa BirdInstance con configuración de este tipo
            bi.InitializeFromPool(this, info, typeIndex, appearances);

            // Añade BirdMover (movimiento recto u oscilatorio)
            go.AddComponent<BirdMover>();

            // Inactivo hasta que se use
            go.SetActive(false);
            birdPool.Add(bi);
        }
        return true;
    }

    // Espera a que el esqueleto esté detectado de forma estable un tiempo mínimo
    IEnumerator WaitForStableSkeleton()
    {
        if (analyzer == null) yield break;

        float stableTimer = 0f;
        while (stableTimer < requiredStableTrackingSeconds)
        {
            // Si no hay tracking, reiniciamos contador
            if (!analyzer.IsUserDetected())
            {
                stableTimer = 0f;
                yield return null;
                continue;
            }

            stableTimer += Time.deltaTime;
            yield return null;
        }
    }

    // Coroutine principal: recorre series, spawnea birds y guarda resultados
    IEnumerator SessionLoop()
    {
        poolIndex = 0;
        yield return new WaitForSeconds(0.5f);

        if (instructionController != null) instructionController.OnSerieStart();

        for (int s = 1; s <= totalSeries; s++)
        {
            currentSerie = s;

            // Informa al analyzer qué serie está activa (para exportación timeseries)
            if (analyzer != null) analyzer.currentSerieIndex = s;

            // Reinicia contadores por serie
            currentHits = 0;
            completedBirds = 0;
            UpdateProgress();

            // 1) Esperar tracking estable ANTES de empezar a spawnear
            yield return StartCoroutine(WaitForStableSkeleton());

            // 2) Iniciar captura (ya con tracking)
            if (analyzer != null) analyzer.StartCapture();

            // Reinicia estado de birds en pool
            foreach (var b in birdPool) b.ResetForNewSerie();

            // 3) Bucle de SPAWN
            while (completedBirds < repsPerSerie && !AreAllBirdsComplete())
            {
                CheckRealtimeCompensations();

                // Control de número de birds simultáneos
                if (activeInstances.Count >= maxBirdsOnScreen)
                {
                    yield return null;
                    continue;
                }

                // Si se pierde el tracking, pausamos spawns hasta recuperarlo
                if (analyzer != null && !analyzer.IsUserDetected())
                {
                    yield return null;
                    continue;
                }

                BirdInstance bird = birdPool[poolIndex];
                poolIndex = (poolIndex + 1) % birdPool.Count;

                // Activar un bird si está inactivo y aún no completó su objetivo de apariciones
                if (!bird.gameObject.activeInHierarchy && !bird.isSerieComplete)
                {
                    ActivateBird(bird);
                    yield return new WaitForSeconds(delayBetweenBirds);
                }
                else yield return null;
            }

            // Esperar a que terminen de caer los últimos
            while (activeInstances.Count > 0)
            {
                yield return null;
            }

            // Finaliza captura al terminar la serie
            if (analyzer != null) analyzer.StopCapture();

            // Descanso entre series
            if (currentSerie < totalSeries)
                yield return new WaitForSeconds(restTimeBetweenSeries);
        }

        // Fin de sesión: notifica UI
        if (instructionController != null) instructionController.OnSerieEnd();

        // Guardado de datos (resumen + timeseries)
        if (DataManager.Instance != null)
        {
            Debug.Log("BirdSpawner: Guardando datos de sesión en DataManager.");
            DataManager.Instance.SaveSessionData(sessionResultsLog);

            // Guardar también serie temporal (instantáneos)
            if (analyzer != null)
            {
                DataManager.Instance.SaveTimeSeriesData(analyzer.samples);
            }
        }
        else
        {
            Debug.LogWarning("BirdSpawner: No se encontró DataManager.Instance. Asegúrate de tener el objeto DataManager en la escena.");
        }
    }

    // Si hay compensación en tiempo real, avisa al panel de instrucciones
    void CheckRealtimeCompensations()
    {
        if (analyzer != null && instructionController != null)
        {
            if (analyzer.isCompensating)
            {
                instructionController.OnManyCompensations();
            }
        }
    }

    // Comprueba si todos los birds ya completaron su cuota de apariciones
    bool AreAllBirdsComplete()
    {
        foreach (var b in birdPool) if (!b.isSerieComplete) return false;
        return true;
    }

    // -------------------------------------------------------------------------
    // NUEVO: Torso UI Y actual (px) usando la misma proyección 2D (AnchoredPosition)
    // -------------------------------------------------------------------------
    float GetTorsoY_UI_px()
    {
        if (videoContainerRect == null) return 0f;

        var user = NuitrackManager.Users.Current;
        if (user == null || user.Skeleton == null) return 0f;

        var jt = user.Skeleton.GetJoint(nuitrack.JointType.Torso);
        if (jt.Confidence < 0.1f) return 0f;

        Vector2 torsoUI = jt.AnchoredPosition(videoContainerRect.rect, videoContainerRect);
        torsoUI.x = -torsoUI.x; // espejo (misma convención que HandTouchUI)
        return torsoUI.y;
    }

    // Activa un bird: calcula posición, inicializa mover y lo añade como target tocable
    void ActivateBird(BirdInstance bird)
    {
        // ---------------------------------------------------------------------
        // NUEVO MÉTODO (TFG):
        //  - SessionData.MaxHandHeight ya contiene la ALTURA SEGURA en UI(px) RELATIVA AL TORSO
        //  - Por tanto, la posición final de spawn es:
        //        spawnY_UI = torsoY_UI + safeRelativeHeight_UI
        // ---------------------------------------------------------------------

        // 1) Altura segura calibrada (UI px relativos al torso)
        float safeRelativeHeight_UI_px = 0f;
        if (SessionData.Instance != null)
            safeRelativeHeight_UI_px = SessionData.Instance.MaxHandHeight;

        // 2) Torso UI Y actual (px)
        float torsoY_UI = GetTorsoY_UI_px();

        // 3) Spawn Y (px)
        float spawnY_UI = torsoY_UI + safeRelativeHeight_UI_px;

        // 4) Clamp a límites del contenedor (evita salir demasiado arriba/abajo)
        float sceneMaxY = videoContainerRect.rect.height / 2f;
        float safeY = sceneMaxY - 50f;
        spawnY_UI = Mathf.Clamp(spawnY_UI, -safeY, safeY);

        // 5) Posición X según spawnSide (izquierda o derecha)
        float dir = (spawnSide == SpawnSide.RightArm) ? -1f : 1f;
        float w = videoContainerRect.rect.width / 2f;
        float startX = (dir < 0) ? (w + 100f) : (-w - 100f);

        // 6) Aplicar posición y escala (flip horizontal)
        RectTransform rt = bird.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(startX, spawnY_UI);

        Vector3 sc = bird.transform.localScale;
        sc.x = Mathf.Abs(sc.x) * (dir < 0 ? -1f : 1f);
        bird.transform.localScale = sc;

        // 7) Inicializar movimiento (BirdMover)
        BirdMover mover = bird.GetComponent<BirdMover>();
        var info = birdTypes[bird.typeIndex];
        mover.Initialize(dir, info.speed, videoContainerRect, info);

        // 8) Activar en escena
        bird.gameObject.SetActive(true);
        activeInstances.Add(bird);

        // 9) Registrar como target tocable por HandTouchUI
        if (handTouchUI != null) handTouchUI.AddTarget(rt);

        // ---------------------------------------------------------------------
        // NUEVO (TFG): guardar datos de spawn para Excel
        // ---------------------------------------------------------------------
        lastBirdSpawnY_UI_px = spawnY_UI;
        lastTorsoY_UI_px = torsoY_UI;
        lastBirdRelativeHeight_UI_px = lastBirdSpawnY_UI_px - lastTorsoY_UI_px;
    }

    // Notificación: se ha registrado un golpe válido
    public void NotifyHitLanded()
    {
        currentHits++;
        UpdateProgress();
        if (instructionController != null) instructionController.OnHit();
    }

    // Notificación: el bird salió sin ser golpeado
    public void NotifyMissed()
    {
        if (instructionController != null) instructionController.OnMiss();
    }

    // Notificación: finalización de una aparición (por salir o morir)
    public void NotifyAppearanceCompleted()
    {
        completedBirds++;
        UpdateProgress();

        // Cierra repetición en el analyzer (máximos, contadores, etc.)
        if (analyzer != null) analyzer.EndRepetition();
    }

    // Notificación: el bird se ha desactivado (pooling), aquí se registra resultado por repetición
    public void NotifyInstanceDeactivated(BirdInstance bird)
    {
        activeInstances.Remove(bird);
        if (handTouchUI) handTouchUI.RemoveTarget(bird.GetComponent<RectTransform>());

        // --- REGISTRO DE DATOS (resumen por repetición) ---
        if (analyzer != null)
        {
            bool wasHit = bird.isPermanentlyDead || bird.hasBeenHitThisAppearance;

            RepetitionResult result = analyzer.GetCurrentRepResult(completedBirds, wasHit);

            // -----------------------------------------------------------------
            // NUEVO (TFG): campos extra para validar en Excel
            //  - maxHandHeight_UI_px: máximo alcanzado por la mano en esa repetición (calibración/medición)
            //  - safeHeight_UI_px: umbral seguro usado en gameplay (media*0.9)
            //  - alturas reales del spawn del bird (UI px) y su altura relativa (bird - torso)
            // -----------------------------------------------------------------
            result.maxHandHeight_UI_px = analyzer.LastRepMaxHandHeight_UI_px;
            result.safeHeight_UI_px = (SessionData.Instance != null) ? SessionData.Instance.MaxHandHeight : 0f;

            result.birdSpawnY_UI_px = lastBirdSpawnY_UI_px;
            result.torsoSpawnY_UI_px = lastTorsoY_UI_px;
            result.birdRelativeHeight_UI_px = lastBirdRelativeHeight_UI_px;

            sessionResultsLog.Add(result);

            Debug.Log($"Rep {completedBirds}: {(wasHit ? "HIT" : "MISS")} | Angulo: {result.maxAngle:F1} | BirdRelY(px): {result.birdRelativeHeight_UI_px:F1} | Safe(px): {result.safeHeight_UI_px:F1}");
        }
    }

    public void NotifyInstanceKilledByHits(BirdInstance i) { }

    // Actualiza UI simple (contador de hits vs objetivo)
    void UpdateProgress()
    {
        if (progressText != null)
            progressText.text = $"{currentHits}/{repsPerSerie}";
    }

    // Devuelve el nº de repeticiones completadas (para BiomechanicalUI)
    public int GetCompletedReps()
    {
        return completedBirds;
    }
}
