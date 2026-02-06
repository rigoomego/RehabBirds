using UnityEngine;
using NuitrackSDK;
using System.Collections.Generic;
using System.Linq;

/*
 * GameAnalyzer
 * ------------
 * Analizador de métricas durante el juego basado en el esqueleto de Nuitrack.
 * Calcula y acumula por repetición/serie:
 *  - Compensación de hombro (diferencia Y entre hombros).
 *  - Compensación de tronco lateral (desviación en X del torso).
 *  - Compensación de arqueo/espalda (variación en Z torso-cintura).
 *  - Velocidad máxima del movimiento (peak velocity) durante una elevación.
 *
 * 
 */
public class GameAnalyzer : MonoBehaviour
{
    [Header("Configuración de Análisis")]
    [Tooltip("True = hombro derecho, False = hombro izquierdo")]
    public bool rightSide = true;

    [Header("Compensación de Hombro (Y)")]
    public float shoulderCompensationThreshold_m = 0.04f;

    [Header("Compensación de Tronco (Lateral X)")]
    public float trunkCompensationThreshold_m = 0.05f;

    [Header("Compensación de Espalda (Arqueo Z)")]
    public float trunkArchThreshold_m = 0.07f; // 7cm

    // --- Propiedades Públicas (Resultados) ---
    // Contadores acumulados de compensaciones detectadas
    public int CurrentShoulderCompensations { get; private set; }
    public int CurrentTrunkCompensations { get; private set; }
    public int CurrentTrunkArchCompensations { get; private set; }

    // Velocidad media de picos (promedio de peaks por cada movimiento detectado)
    public float AvgPeakVelocity { get; private set; }

    // --- Variables Internas ---
    // Baselines iniciales para comparar compensaciones
    private float initialShoulderDiffY = 0f;
    private float baseTorsoX = 0f;
    private float baseTorsoWaistDiff_Z = 0f; // <-- MODIFICADO (Punto 2)

    // Control de detección de movimiento (cuando la mano está por encima de la cadera)
    private bool isTrackingMovement = false;

    // Flags por repetición para no contar varias veces la misma compensación
    private bool hasCompensatedShoulderThisRep = false;
    private bool hasCompensatedTrunkThisRep = false;
    private bool hasCompensatedArchThisRep = false;

    // Velocidad: cálculo frame a frame + pico de cada movimiento
    private Vector3 previousHandPos = Vector3.zero;
    private float currentPeakVelocityThisMovement = 0f;
    private List<float> peakVelocitiesList = new List<float>();

    void Start()
    {
        // Inicializa baselines si ya hay usuario detectado al inicio
        UserData user = NuitrackManager.Users.Current;
        if (user != null && user.Skeleton != null)
        {
            InitializeBaseValues(user);
        }

        // Reinicia contadores al comenzar
        ResetForNewSerie();
    }

    void Update()
    {
        // Obtiene usuario actual de Nuitrack
        UserData user = NuitrackManager.Users.Current;
        if (user == null || user.Skeleton == null) return;

        // Si todavía no se han inicializado baselines y torso tiene confianza suficiente, se inicializan
        if (baseTorsoX == 0f && user.Skeleton.GetJoint(nuitrack.JointType.Torso).Confidence > 0.1f)
        {
            InitializeBaseValues(user);
        }
        if (baseTorsoX == 0f) return;

        // Selección de joints según lado
        nuitrack.JointType shoulderType = rightSide ? nuitrack.JointType.RightShoulder : nuitrack.JointType.LeftShoulder;
        nuitrack.JointType oppositeShoulderType = rightSide ? nuitrack.JointType.LeftShoulder : nuitrack.JointType.RightShoulder;
        nuitrack.JointType handType = rightSide ? nuitrack.JointType.RightHand : nuitrack.JointType.LeftHand;
        nuitrack.JointType torsoType = nuitrack.JointType.Torso;
        nuitrack.JointType hipType = rightSide ? nuitrack.JointType.RightHip : nuitrack.JointType.LeftHip;
        nuitrack.JointType waistType = nuitrack.JointType.Waist; // <-- AÑADIDO

        // Lectura de joints
        var js = user.Skeleton.GetJoint(shoulderType);
        var jos = user.Skeleton.GetJoint(oppositeShoulderType);
        var jh = user.Skeleton.GetJoint(handType);
        var jt = user.Skeleton.GetJoint(torsoType);
        var jHip = user.Skeleton.GetJoint(hipType);
        var jWaist = user.Skeleton.GetJoint(waistType); // <-- AÑADIDO

        // Si falta confianza en alguna articulación relevante, se salta el frame
        if (js.Confidence < 0.1f || jos.Confidence < 0.1f || jh.Confidence < 0.1f || jt.Confidence < 0.1f || jHip.Confidence < 0.1f || jWaist.Confidence < 0.1f) return;

        // Posiciones 3D (metros aprox) en espacio de Nuitrack
        Vector3 handPos = jh.Position;
        Vector3 shoulderPos = js.Position;
        Vector3 oppositeShoulderPos = jos.Position;
        Vector3 torsoPos = jt.Position;
        Vector3 hipPos = jHip.Position;
        Vector3 waistPos = jWaist.Position; // <-- AÑADIDO

        // --- 1. Detección de Compensación de Hombro (Y) ---
        // Se compara la diferencia vertical entre hombro activo y contralateral contra el baseline + umbral
        float currentShoulderDiffY = shoulderPos.y - oppositeShoulderPos.y;
        if (currentShoulderDiffY > initialShoulderDiffY + shoulderCompensationThreshold_m)
        {
            if (!hasCompensatedShoulderThisRep)
            {
                CurrentShoulderCompensations++;
                hasCompensatedShoulderThisRep = true;
            }
        }

        // --- 2. Detección de Compensación de Tronco (Lateral X) ---
        // Se evalúa desviación lateral del torso respecto al baseline
        float currentTorsoX = torsoPos.x;
        if (Mathf.Abs(currentTorsoX - baseTorsoX) > trunkCompensationThreshold_m)
        {
            if (!hasCompensatedTrunkThisRep)
            {
                CurrentTrunkCompensations++;
                hasCompensatedTrunkThisRep = true;
            }
        }

        // --- 3. LÓGICA DE ARQUEO CORREGIDA (Punto 2) ---
        // Se evalúa variación en Z entre torso y cintura respecto al baseline
        float currentTorsoWaistDiff_Z = torsoPos.z - waistPos.z;
        if (Mathf.Abs(currentTorsoWaistDiff_Z - baseTorsoWaistDiff_Z) > trunkArchThreshold_m)
        {
            if (!hasCompensatedArchThisRep)
            {
                CurrentTrunkArchCompensations++;
                hasCompensatedArchThisRep = true;
            }
        }

        // --- 4. Cálculo de Velocidad Máxima (Peak Velocity) ---
        // Calcula velocidad euclidiana de la mano entre frames y guarda el máximo durante el movimiento
        float handVelocity = 0f;
        if (previousHandPos != Vector3.zero && Time.deltaTime > 0f)
        {
            handVelocity = (handPos - previousHandPos).magnitude / Time.deltaTime;
        }
        previousHandPos = handPos;

        // Se considera que hay movimiento cuando la mano está por encima de la cadera + margen
        if (handPos.y > (hipPos.y + 0.05f))
        {
            isTrackingMovement = true;
            if (handVelocity > currentPeakVelocityThisMovement)
            {
                currentPeakVelocityThisMovement = handVelocity;
            }
        }
        else if (isTrackingMovement)
        {
            // Al terminar el movimiento, se registra el pico y se reinician flags por repetición
            isTrackingMovement = false;
            if (currentPeakVelocityThisMovement > 0.1f)
            {
                peakVelocitiesList.Add(currentPeakVelocityThisMovement);
                UpdateAverageVelocity();
            }
            currentPeakVelocityThisMovement = 0f;
            hasCompensatedShoulderThisRep = false;
            hasCompensatedTrunkThisRep = false;
            hasCompensatedArchThisRep = false;
        }
    }

    // Calcula promedio de picos registrados en la lista
    void UpdateAverageVelocity()
    {
        if (peakVelocitiesList.Count > 0)
        {
            AvgPeakVelocity = peakVelocitiesList.Average();
        }
        else
        {
            AvgPeakVelocity = 0f;
        }
    }

    // Reinicia contadores, lista de picos y flags para una nueva serie
    public void ResetForNewSerie()
    {
        CurrentShoulderCompensations = 0;
        CurrentTrunkCompensations = 0;
        CurrentTrunkArchCompensations = 0;
        AvgPeakVelocity = 0f;

        peakVelocitiesList.Clear();
        currentPeakVelocityThisMovement = 0f;
        isTrackingMovement = false;
        hasCompensatedShoulderThisRep = false;
        hasCompensatedTrunkThisRep = false;
        hasCompensatedArchThisRep = false;

        // Recalcula baselines si hay usuario disponible
        UserData user = NuitrackManager.Users.Current;
        if (user != null && user.Skeleton != null)
        {
            InitializeBaseValues(user);
        }
    }

    // Inicializa valores base (baseline) para comparar compensaciones
    private void InitializeBaseValues(UserData user)
    {
        nuitrack.JointType shoulderType = rightSide ? nuitrack.JointType.RightShoulder : nuitrack.JointType.LeftShoulder;
        nuitrack.JointType oppositeShoulderType = rightSide ? nuitrack.JointType.LeftShoulder : nuitrack.JointType.RightShoulder;
        var s_active = user.Skeleton.GetJoint(shoulderType);
        var s_opposite = user.Skeleton.GetJoint(oppositeShoulderType);
        var jt = user.Skeleton.GetJoint(nuitrack.JointType.Torso);
        var jWaist = user.Skeleton.GetJoint(nuitrack.JointType.Waist); // <-- AÑADIDO

        // Hombro (Y)
        if (s_active.Confidence > 0.1f && s_opposite.Confidence > 0.1f)
        {
            initialShoulderDiffY = s_active.Position.y - s_opposite.Position.y;
        }

        // Tronco (X)
        if (jt.Confidence > 0.1f)
        {
            baseTorsoX = jt.Position.x;
        }

        // --- LÓGICA DE ARQUEO CORREGIDA (Punto 2) ---
        if (jt.Confidence > 0.1f && jWaist.Confidence > 0.1f)
        {
            baseTorsoWaistDiff_Z = jt.Position.z - jWaist.Position.z;
        }
    }
}
