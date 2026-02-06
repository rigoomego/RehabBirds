using UnityEngine;
using NuitrackSDK;
using System.Collections.Generic;

/*
 * BiomechanicalAnalyzer
 * --------------------
 * Lee el esqueleto detectado por Nuitrack (posiciones 3D de articulaciones) y calcula métricas
 * biomecánicas en tiempo real durante el ejercicio de abducción del hombro.
 *
 * IMPORTANTE (CAMBIO):
 * - Se mantiene currentHandHeight en metros (para timeseries/CSV).
 * - PERO la calibración de altura para gameplay (birds) ahora se hace en UI (píxeles):
 *     currentHandHeight_UI = HandUI.y - TorsoUI.y
 *   y se promedia 10 repeticiones + safeHeightFactor (0.9) para guardar en SessionData.MaxHandHeight.
 *
 */
public class BiomechanicalAnalyzer : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // CONFIGURACIÓN GENERAL (Inspector)
    // -----------------------------------------------------------------------------

    [Header("Configuración General")]
    public bool rightSide = true;

    // -----------------------------------------------------------------------------
    // NUEVO: REFERENCIA UI PARA PROYECCIÓN (metros -> UI via AnchoredPosition)
    // -----------------------------------------------------------------------------

    [Header("UI Projection (para calibración de altura en píxeles)")]
    [Tooltip("Arrastra aquí el mismo RectTransform donde están el vídeo/esqueleto/birds (p.ej. videoContainerRect).")]
    public RectTransform uiReferenceRect;

    [Tooltip("Mantén true si estás usando espejo horizontal (como en HandTouchUI).")]
    public bool mirrorX_UI = true;

    // Altura relativa en UI (píxeles): mano respecto al torso
    public float currentHandHeight_UI = 0f;

    // Snapshot (máximo durante la repetición) en UI
    public float maxHandHeightSnapshot_UI = Mathf.NegativeInfinity;

    // -----------------------------------------------------------------------------
    // ESTABILIZACIÓN INICIAL (grace period)
    // -----------------------------------------------------------------------------

    [Header("Estabilización Inicial")]
    [Tooltip("Segundos al inicio para que el usuario se acomode antes de medir.")]
    public float startUpGracePeriod = 2.0f;
    private float graceTimer = 0f;

    // -----------------------------------------------------------------------------
    // CALIBRACIÓN: umbral indoloro (altura segura para gameplay)
    // -----------------------------------------------------------------------------

    [Header("Calibración: Umbral indoloro (factor de seguridad)")]
    [Tooltip("Factor aplicado a la altura media máxima de la mano antes de guardarla (p.ej., 0.9 = 90%).")]
    [Range(0.1f, 1.0f)]
    public float safeHeightFactor = 0.9f;

    // -----------------------------------------------------------------------------
    // DETECCIÓN DE COMPENSACIÓN DE HOMBRO (Hiking)
    // -----------------------------------------------------------------------------

    [Header("1. Compensación Hombro (Hiking)")]
    public float shoulderLimit_Strict = 0.03f;
    public float angleForStrictThreshold = 45f;
    public float shoulderLimit_Permissive = 0.08f;
    public float angleForPermissiveThreshold = 110f;

    // -----------------------------------------------------------------------------
    // DETECCIÓN DE COMPENSACIONES DE TRONCO
    // -----------------------------------------------------------------------------

    [Header("2. Compensaciones Tronco")]
    public float trunkLateralThreshold_m = 0.05f;
    public float trunkForwardThreshold_m = 0.05f;

    // -----------------------------------------------------------------------------
    // CONTROL DE CAPTURA (Start/Stop)
    // -----------------------------------------------------------------------------

    [Header("Control de captura")]
    public bool isCapturing = false;

    // -----------------------------------------------------------------------------
    // MÉTRICAS EN TIEMPO REAL (solo lectura para UI)
    // -----------------------------------------------------------------------------

    [Header("Datos calculados (Read Only)")]
    public float currentAbductionAngle = 0f;
    public float maxAbductionAngle = 0f;

    // Altura en METROS (se mantiene para CSV)
    public float currentHandHeight = 0f;

    // Snapshot en metros (no se usa para calibración de birds, pero se mantiene para debug/compatibilidad)
    public float maxHandHeightSnapshot = Mathf.NegativeInfinity;

    // Contadores Totales
    public int compensationCount { get; private set; } = 0;
    public int trunkLateralCompensationCount { get; private set; } = 0;
    public int trunkForwardCompensationCount { get; private set; } = 0;
    public int TotalRepCompensationCount => compensationCount + trunkLateralCompensationCount + trunkForwardCompensationCount;

    // ===== Métricas Medias =====
    public int currentRepetition = 0;
    public const int MAX_REPETITIONS = 10;

    public float sumMaxAbductionAngle = 0f;

    // IMPORTANTE (CAMBIO): este sum/avg ahora se usa para UI(px) de la altura relativa mano-torso
    public float sumMaxHandHeight = 0f;
    public int validRepetitionCount = 0;

    public float averageMaxAbductionAngle = 0f;

    // IMPORTANTE (CAMBIO): ahora es promedio en UI(px), no en metros
    public float averageMaxHandHeight = 0f;

    // Valor seguro final (aplicando 0.9) -> UI(px)
    public float averageMaxHandHeight_Safe = 0f;

    // Internos (Baselines)
    private float initialShoulderDiffY = 0f;
    private float baseTorsoX = 0f;
    private float baseNeckWaistDiff_Z = 0f;
    private bool userDetected = false;

    // --- VELOCIDAD EUCLIDIANA CRUDA (RAW) ---
    public float currentHandVelocity_mps { get; private set; } = 0f;
    public float maxHandVelocity_mps { get; private set; } = 0f;
    private Vector3 prevHandPos = Vector3.zero;
    private bool firstFrameVelocity = true;

    // Estados de Compensación Actual (Live)
    public bool isCompensatingShoulder { get; private set; } = false;
    public bool isCompensatingTrunkLateral { get; private set; } = false;
    public bool isCompensatingTrunkForward { get; private set; } = false;
    public bool isCompensating => isCompensatingShoulder || isCompensatingTrunkLateral || isCompensatingTrunkForward;

    // Flags repetición actual
    public bool compensatedThisRep { get; private set; } = false;
    public bool compensatedTrunkLatThisRep { get; private set; } = false;
    public bool compensatedTrunkFwdThisRep { get; private set; } = false;

    // Snapshots para DataManager
    private float lastRepMaxAngle = 0f;
    private float lastRepMaxVelocity = 0f;
    private bool lastRepCompShoulder = false;
    private bool lastRepCompTrunk = false;
    private bool lastRepCompTrunkFwd = false;

    public bool IsUserDetected() => userDetected;

    private Vector3 lastValidWaistPos = Vector3.zero;
    public Vector3 GetWaistPosition() => lastValidWaistPos;

    // -----------------------------------------------------------------------------
    // SERIE TEMPORAL (exportación CSV)
    // -----------------------------------------------------------------------------

    [Header("Timeseries (CSV)")]
    [Tooltip("Periodo de muestreo (seg). 0.033 ? 30 Hz.")]
    public float samplePeriod = 0.033f;

    [Tooltip("Índice de serie actual (BirdSpawner puede asignarlo al cambiar de serie).")]
    public int currentSerieIndex = 1;

    // Lista de muestras instantáneas (se guardan al final)
    public List<MetricSample> samples = new List<MetricSample>();

    private float sampleTimer = 0f;
    private float repTimer = 0f;

    // Control interno: permite detener el muestreo (por ejemplo, al finalizar condiciones)
    private bool stopSampling = false;

    void Update()
    {
        UserData user = NuitrackManager.Users.Current;
        if (user == null || user.Skeleton == null)
        {
            userDetected = false;
            firstFrameVelocity = true;
            return;
        }
        userDetected = true;

        if (isCapturing)
        {
            if (graceTimer > 0f)
            {
                graceTimer -= Time.deltaTime;
                InitializeBaselines(user);
                firstFrameVelocity = true;
                return;
            }

            CalculateMetrics(user);
        }
    }

    public void StartCapture()
    {
        isCapturing = true;
        currentRepetition = 0;
        graceTimer = startUpGracePeriod;

        sumMaxAbductionAngle = 0f;

        // IMPORTANTE: ahora sumMaxHandHeight es UI(px)
        sumMaxHandHeight = 0f;

        validRepetitionCount = 0;
        averageMaxAbductionAngle = 0f;
        averageMaxHandHeight = 0f;
        averageMaxHandHeight_Safe = 0f;

        compensationCount = 0;
        trunkLateralCompensationCount = 0;
        trunkForwardCompensationCount = 0;

        samples.Clear();
        stopSampling = false;

        ResetForNewRepetition();
        currentRepetition = 1;
    }

    public void StopCapture()
    {
        isCapturing = false;
        CalculateAverages();
    }

    public void EndRepetition()
    {
        if (!isCapturing) return;

        lastRepMaxAngle = maxAbductionAngle;
        lastRepMaxVelocity = maxHandVelocity_mps;
        lastRepCompShoulder = compensatedThisRep;
        lastRepCompTrunk = compensatedTrunkLatThisRep;
        lastRepCompTrunkFwd = compensatedTrunkFwdThisRep;

        // IMPORTANTE: el promedio de altura se calcula con la snapshot UI(px)
        if (maxAbductionAngle > 10f && maxHandHeightSnapshot_UI > -999999f)
        {
            sumMaxAbductionAngle += maxAbductionAngle;
            sumMaxHandHeight += maxHandHeightSnapshot_UI;
            validRepetitionCount++;
        }

        currentRepetition++;
        if (currentRepetition <= MAX_REPETITIONS)
        {
            ResetForNewRepetition();
        }
        else
        {
            CalculateAverages();
        }
    }

    public void CalculateAverages()
    {
        if (validRepetitionCount > 0)
        {
            averageMaxAbductionAngle = sumMaxAbductionAngle / validRepetitionCount;

            // IMPORTANTE (CAMBIO): promedio en UI(px)
            averageMaxHandHeight = sumMaxHandHeight / validRepetitionCount;

            // Altura segura con factor 0.9 (en UI px)
            averageMaxHandHeight_Safe = averageMaxHandHeight * safeHeightFactor;
        }
        else
        {
            averageMaxAbductionAngle = 0f;
            averageMaxHandHeight = 0f;
            averageMaxHandHeight_Safe = 0f;
        }

        // Guardar en SessionData el valor ya "seguro" para gameplay
        if (SessionData.Instance != null)
        {
            // SessionData.MaxHandHeight ahora representa "altura relativa segura en UI(px)"
            SessionData.Instance.MaxHandHeight = averageMaxHandHeight_Safe;
        }
    }

    private void ResetForNewRepetition()
    {
        maxAbductionAngle = 0f;
        maxHandVelocity_mps = 0f;

        // Reseteo snapshots
        maxHandHeightSnapshot = Mathf.NegativeInfinity;
        maxHandHeightSnapshot_UI = Mathf.NegativeInfinity;

        compensatedThisRep = false;
        compensatedTrunkLatThisRep = false;
        compensatedTrunkFwdThisRep = false;

        firstFrameVelocity = true;

        repTimer = 0f;
        sampleTimer = 0f;

        UserData user = NuitrackManager.Users.Current;
        if (user != null && user.Skeleton != null && graceTimer <= 0f)
        {
            InitializeBaselines(user);
        }
    }

    public RepetitionResult GetCurrentRepResult(int repIndex, bool hit)
    {
        RepetitionResult r = new RepetitionResult();
        r.repIndex = repIndex;
        r.isHit = hit;
        r.maxAngle = this.lastRepMaxAngle;
        r.maxVelocity = this.lastRepMaxVelocity;
        r.compShoulder = this.lastRepCompShoulder;
        r.compTrunk = this.lastRepCompTrunk;
        r.compTrunkFwd = this.lastRepCompTrunkFwd;
        return r;
    }

    private void InitializeBaselines(UserData user)
    {
        var jTorso = user.Skeleton.GetJoint(nuitrack.JointType.Torso);
        if (jTorso.Confidence > 0.1f) baseTorsoX = jTorso.Position.x;

        var jNeck = user.Skeleton.GetJoint(nuitrack.JointType.Neck);
        var jWaist = user.Skeleton.GetJoint(nuitrack.JointType.Waist);
        if (jNeck.Confidence > 0.1f && jWaist.Confidence > 0.1f)
        {
            baseNeckWaistDiff_Z = jNeck.Position.z - jWaist.Position.z;
        }

        nuitrack.JointType sType = rightSide ? nuitrack.JointType.RightShoulder : nuitrack.JointType.LeftShoulder;
        nuitrack.JointType oppType = rightSide ? nuitrack.JointType.LeftShoulder : nuitrack.JointType.RightShoulder;
        var s = user.Skeleton.GetJoint(sType);
        var opp = user.Skeleton.GetJoint(oppType);
        if (s.Confidence > 0.1f && opp.Confidence > 0.1f)
            initialShoulderDiffY = s.Position.y - opp.Position.y;
    }

    void CalculateMetrics(UserData user)
    {
        nuitrack.JointType shoulderType = rightSide ? nuitrack.JointType.RightShoulder : nuitrack.JointType.LeftShoulder;
        nuitrack.JointType oppositeShoulderType = rightSide ? nuitrack.JointType.LeftShoulder : nuitrack.JointType.RightShoulder;
        nuitrack.JointType elbowType = rightSide ? nuitrack.JointType.RightElbow : nuitrack.JointType.LeftElbow;
        nuitrack.JointType handType = rightSide ? nuitrack.JointType.RightHand : nuitrack.JointType.LeftHand;
        nuitrack.JointType torsoType = nuitrack.JointType.Torso;
        nuitrack.JointType waistType = nuitrack.JointType.Waist;
        nuitrack.JointType neckType = nuitrack.JointType.Neck;

        var js = user.Skeleton.GetJoint(shoulderType);
        var jos = user.Skeleton.GetJoint(oppositeShoulderType);
        var je = user.Skeleton.GetJoint(elbowType);
        var jh = user.Skeleton.GetJoint(handType);
        var jt = user.Skeleton.GetJoint(torsoType);
        var jw = user.Skeleton.GetJoint(waistType);
        var jn = user.Skeleton.GetJoint(neckType);

        if (jw.Confidence > 0.1f) lastValidWaistPos = jw.Position;

        if (js.Confidence < 0.1f || je.Confidence < 0.1f || jh.Confidence < 0.1f || jt.Confidence < 0.1f) return;

        Vector3 shoulder = js.Position;
        Vector3 elbow = je.Position;
        Vector3 rawHandPos = jh.Position;
        Vector3 torso = jt.Position;

        Vector3 waist = (jw.Confidence > 0.1f) ? jw.Position : torso - new Vector3(0, 0.4f, 0);
        Vector3 neck = (jn.Confidence > 0.1f) ? jn.Position : torso + new Vector3(0, 0.3f, 0);

        // 1) Altura relativa (m): mano respecto al torso (se mantiene para CSV)
        currentHandHeight = rawHandPos.y - torso.y;

        // 1B) NUEVO: Altura relativa (UI px): manoUI.y - torsoUI.y
        if (uiReferenceRect != null)
        {
            // Proyectamos ambos joints a coordenadas UI con el método del SDK (igual que skeleton/touch)
            Vector2 handUI = jh.AnchoredPosition(uiReferenceRect.rect, uiReferenceRect);
            Vector2 torsoUI = jt.AnchoredPosition(uiReferenceRect.rect, uiReferenceRect);

            if (mirrorX_UI)
            {
                handUI.x = -handUI.x;
                torsoUI.x = -torsoUI.x;
            }

            currentHandHeight_UI = handUI.y - torsoUI.y;
        }

        // 2) Ángulo (deg): vector brazo vs vertical
        Vector3 arm = elbow - shoulder;
        currentAbductionAngle = Vector3.Angle(arm, Vector3.down);

        if (currentAbductionAngle > maxAbductionAngle)
        {
            maxAbductionAngle = currentAbductionAngle;

            // Mantener snapshot metros (opcional)
            maxHandHeightSnapshot = currentHandHeight;

            // IMPORTANTE: snapshot para calibración en UI(px)
            maxHandHeightSnapshot_UI = currentHandHeight_UI;
        }

        // 3) Velocidad euclidiana (m/s): desplazamiento entre frames / deltaTime
        float dt = Time.deltaTime;
        if (dt > 0)
        {
            if (firstFrameVelocity)
            {
                prevHandPos = rawHandPos;
                currentHandVelocity_mps = 0f;
                firstFrameVelocity = false;
            }
            else
            {
                float distance = (rawHandPos - prevHandPos).magnitude;
                currentHandVelocity_mps = distance / dt;

                if (currentHandVelocity_mps > maxHandVelocity_mps)
                    maxHandVelocity_mps = currentHandVelocity_mps;

                prevHandPos = rawHandPos;
            }
        }

        // --- COMPENSACIONES ---

        // A) Hombro (hiking)
        float currentShoulderDiffY = shoulder.y - jos.Position.y;
        float t = Mathf.InverseLerp(angleForStrictThreshold, angleForPermissiveThreshold, currentAbductionAngle);
        float dynamicThreshold = Mathf.Lerp(shoulderLimit_Strict, shoulderLimit_Permissive, t);

        if (currentShoulderDiffY > initialShoulderDiffY + dynamicThreshold)
        {
            isCompensatingShoulder = true;
            if (!compensatedThisRep)
            {
                compensationCount++;
                compensatedThisRep = true;
            }
        }
        else isCompensatingShoulder = false;

        // B) Tronco lateral
        if (Mathf.Abs(torso.x - baseTorsoX) > trunkLateralThreshold_m)
        {
            isCompensatingTrunkLateral = true;
            if (!compensatedTrunkLatThisRep)
            {
                trunkLateralCompensationCount++;
                compensatedTrunkLatThisRep = true;
            }
        }
        else isCompensatingTrunkLateral = false;

        // C) Tronco adelante/atrás (variación cuello-cintura en Z)
        float currentNeckWaistDiff_Z = neck.z - waist.z;
        float zDeviation = Mathf.Abs(currentNeckWaistDiff_Z - baseNeckWaistDiff_Z);

        if (zDeviation > trunkForwardThreshold_m)
        {
            isCompensatingTrunkForward = true;
            if (!compensatedTrunkFwdThisRep)
            {
                trunkForwardCompensationCount++;
                compensatedTrunkFwdThisRep = true;
            }
        }
        else isCompensatingTrunkForward = false;

        // 4) Timeseries (instantáneos) a 30 Hz (por defecto)
        if (!stopSampling)
        {
            repTimer += Time.deltaTime;
            sampleTimer += Time.deltaTime;

            if (sampleTimer >= samplePeriod)
            {
                sampleTimer = 0f;

                MetricSample s = new MetricSample
                {
                    serieIndex = currentSerieIndex,
                    repIndex = currentRepetition,
                    time_s = repTimer,
                    angle_deg = currentAbductionAngle,
                    velocity_mps = currentHandVelocity_mps,
                    handHeight_m = currentHandHeight,
                    compShoulder = isCompensatingShoulder,
                    compTrunkLat = isCompensatingTrunkLateral,
                    compTrunkFwd = isCompensatingTrunkForward
                };

                samples.Add(s);
            }
        }
    }
}
