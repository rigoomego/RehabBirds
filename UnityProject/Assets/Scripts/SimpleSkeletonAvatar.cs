using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NuitrackSDK;

/*
 * SimpleSkeletonAvatar
 * --------------------
 * Visualizador 2D del esqueleto detectado por Nuitrack dentro de un Canvas de Unity.
 * Dibuja:
 *  - Joints (puntos) usando un prefab (jointPrefab).
 *  - Conexiones/huesos usando otro prefab (connectionPrefab).
 *
 * Permite configurar desde Inspector:
 *  - Qué joints son visibles (JointVisibility).
 *  - Escala de joints y grosor de huesos.
 *  - Colores de joints y conexiones.
 *  - Reflejo horizontal (mirrorX) para efecto espejo.
 *
 * 
 */
public class SimpleSkeletonAvatar : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // CONFIGURACIÓN GENERAL DE VISUALIZACIÓN
    // -------------------------------------------------------------------------
    [Header("General")]
    public bool showSkeleton = true;

    [Tooltip("Reflejo horizontal para visualización tipo espejo.")]
    public bool mirrorX = true;

    // -------------------------------------------------------------------------
    // AJUSTES VISUALES (ESCALA Y COLOR)
    // -------------------------------------------------------------------------
    [Header("Visual")]
    [Tooltip("Escala uniforme aplicada a cada JointUI (punto).")]
    [Range(0.1f, 5f)]
    public float jointScale = 1f;

    [Tooltip("Grosor (escala Y) aplicado a cada ConnectionUI (hueso).")]
    [Range(0.1f, 10f)]
    public float boneThickness = 1f;

    [Tooltip("Color aplicado a los elementos visuales de los joints (JointUI).")]
    public Color jointColor = Color.white;

    [Tooltip("Color aplicado a los elementos visuales de las conexiones (ConnectionUI).")]
    public Color connectionColor = Color.white;

    // -------------------------------------------------------------------------
    // PREFABS UTILIZADOS PARA LA REPRESENTACIÓN VISUAL
    // -------------------------------------------------------------------------
    [Header("Prefabs")]
    [SerializeField] private GameObject jointPrefab = null;
    [SerializeField] private GameObject connectionPrefab = null;

    // -------------------------------------------------------------------------
    // SELECCIÓN DE ARTICULACIONES VISIBLES DESDE EL INSPECTOR
    // -------------------------------------------------------------------------
    [Header("Visible Joints")]
    public JointVisibility visibleJoints = new JointVisibility();

    // -------------------------------------------------------------------------
    // PARÁMETROS INTERNOS
    // -------------------------------------------------------------------------
    private const float CONFIDENCE_THRESHOLD = 0.01f;
    private RectTransform parentRect;

    // -------------------------------------------------------------------------
    // DEFINICIÓN DE ARTICULACIONES Y TOPOLOGÍA DEL ESQUELETO (ESTILO "ANTIGUO")
    // -------------------------------------------------------------------------
    private nuitrack.JointType[] jointsInfo = new nuitrack.JointType[]
    {
        nuitrack.JointType.Head,
        nuitrack.JointType.Neck,
        nuitrack.JointType.LeftCollar,
        nuitrack.JointType.Torso,
        nuitrack.JointType.Waist,

        nuitrack.JointType.LeftShoulder,
        nuitrack.JointType.RightShoulder,

        nuitrack.JointType.LeftElbow,
        nuitrack.JointType.RightElbow,

        nuitrack.JointType.LeftWrist,
        nuitrack.JointType.RightWrist,

        nuitrack.JointType.LeftHand,
        nuitrack.JointType.RightHand,

        nuitrack.JointType.LeftHip,
        nuitrack.JointType.RightHip,

        nuitrack.JointType.LeftKnee,
        nuitrack.JointType.RightKnee,

        nuitrack.JointType.LeftAnkle,
        nuitrack.JointType.RightAnkle
    };

    private nuitrack.JointType[,] connectionsInfo = new nuitrack.JointType[,]
    {
        { nuitrack.JointType.Neck,          nuitrack.JointType.Head },
        { nuitrack.JointType.LeftCollar,    nuitrack.JointType.Neck },
        { nuitrack.JointType.LeftCollar,    nuitrack.JointType.LeftShoulder },
        { nuitrack.JointType.LeftCollar,    nuitrack.JointType.RightShoulder },
        { nuitrack.JointType.LeftCollar,    nuitrack.JointType.Torso },

        { nuitrack.JointType.Waist,         nuitrack.JointType.Torso },
        { nuitrack.JointType.Waist,         nuitrack.JointType.LeftHip },
        { nuitrack.JointType.Waist,         nuitrack.JointType.RightHip },

        { nuitrack.JointType.LeftShoulder,  nuitrack.JointType.LeftElbow },
        { nuitrack.JointType.LeftElbow,     nuitrack.JointType.LeftWrist },
        { nuitrack.JointType.LeftWrist,     nuitrack.JointType.LeftHand },

        { nuitrack.JointType.RightShoulder, nuitrack.JointType.RightElbow },
        { nuitrack.JointType.RightElbow,    nuitrack.JointType.RightWrist },
        { nuitrack.JointType.RightWrist,    nuitrack.JointType.RightHand },

        { nuitrack.JointType.LeftHip,       nuitrack.JointType.LeftKnee },
        { nuitrack.JointType.LeftKnee,      nuitrack.JointType.LeftAnkle },

        { nuitrack.JointType.RightHip,      nuitrack.JointType.RightKnee },
        { nuitrack.JointType.RightKnee,     nuitrack.JointType.RightAnkle }
    };

    private Dictionary<nuitrack.JointType, RectTransform> joints;
    private List<RectTransform> connections;

    // -------------------------------------------------------------------------
    // CLASE AUXILIAR PARA CONTROLAR LA VISIBILIDAD DE CADA JOINT
    // -------------------------------------------------------------------------
    [System.Serializable]
    public class JointVisibility
    {
        public bool Head = true;
        public bool Neck = true;
        public bool LeftCollar = true;
        public bool Torso = true;
        public bool Waist = true;

        public bool LeftShoulder = true;
        public bool RightShoulder = true;
        public bool LeftElbow = true;
        public bool RightElbow = true;
        public bool LeftWrist = true;
        public bool RightWrist = true;
        public bool LeftHand = true;
        public bool RightHand = true;

        public bool LeftHip = true;
        public bool RightHip = true;
        public bool LeftKnee = true;
        public bool RightKnee = true;
        public bool LeftAnkle = true;
        public bool RightAnkle = true;

        public bool IsVisible(nuitrack.JointType jt)
        {
            switch (jt)
            {
                case nuitrack.JointType.Head: return Head;
                case nuitrack.JointType.Neck: return Neck;
                case nuitrack.JointType.LeftCollar: return LeftCollar;
                case nuitrack.JointType.Torso: return Torso;
                case nuitrack.JointType.Waist: return Waist;

                case nuitrack.JointType.LeftShoulder: return LeftShoulder;
                case nuitrack.JointType.RightShoulder: return RightShoulder;
                case nuitrack.JointType.LeftElbow: return LeftElbow;
                case nuitrack.JointType.RightElbow: return RightElbow;
                case nuitrack.JointType.LeftWrist: return LeftWrist;
                case nuitrack.JointType.RightWrist: return RightWrist;
                case nuitrack.JointType.LeftHand: return LeftHand;
                case nuitrack.JointType.RightHand: return RightHand;

                case nuitrack.JointType.LeftHip: return LeftHip;
                case nuitrack.JointType.RightHip: return RightHip;
                case nuitrack.JointType.LeftKnee: return LeftKnee;
                case nuitrack.JointType.RightKnee: return RightKnee;
                case nuitrack.JointType.LeftAnkle: return LeftAnkle;
                case nuitrack.JointType.RightAnkle: return RightAnkle;

                default: return true;
            }
        }
    }

    // -------------------------------------------------------------------------
    // INICIALIZACIÓN DEL SISTEMA DE VISUALIZACIÓN
    // -------------------------------------------------------------------------
    void Start()
    {
        parentRect = GetComponent<RectTransform>();
        CreateSkeletonParts();
    }

    // -------------------------------------------------------------------------
    // ACTUALIZACIÓN EN TIEMPO REAL DEL ESQUELETO
    // -------------------------------------------------------------------------
    void Update()
    {
        if (!showSkeleton)
        {
            SetAllActive(false);
            return;
        }

        ProcessSkeleton(NuitrackManager.Users.Current);
    }

    // -------------------------------------------------------------------------
    // CREACIÓN DE LOS ELEMENTOS VISUALES (JOINTS Y CONEXIONES)
    // -------------------------------------------------------------------------
    void CreateSkeletonParts()
    {
        joints = new Dictionary<nuitrack.JointType, RectTransform>();
        connections = new List<RectTransform>();

        // Instanciación de joints
        for (int i = 0; i < jointsInfo.Length; i++)
        {
            if (jointPrefab == null) continue;

            GameObject go = Instantiate(jointPrefab, transform);
            go.SetActive(false);

            ApplyColorToAllImages(go, jointColor);

            RectTransform rt = go.GetComponent<RectTransform>();
            joints.Add(jointsInfo[i], rt);
        }

        // Instanciación de conexiones (huesos)
        for (int i = 0; i < connectionsInfo.GetLength(0); i++)
        {
            if (connectionPrefab == null) continue;

            GameObject go = Instantiate(connectionPrefab, transform);
            go.SetActive(false);

            ApplyColorToAllImages(go, connectionColor);

            RectTransform rt = go.GetComponent<RectTransform>();
            connections.Add(rt);
        }
    }

    // -------------------------------------------------------------------------
    // ACTIVACIÓN / DESACTIVACIÓN GLOBAL DEL ESQUELETO
    // -------------------------------------------------------------------------
    void SetAllActive(bool active)
    {
        if (joints != null)
        {
            foreach (var j in joints.Values)
                if (j != null) j.gameObject.SetActive(active);
        }

        if (connections != null)
        {
            for (int i = 0; i < connections.Count; i++)
                if (connections[i] != null) connections[i].gameObject.SetActive(active);
        }
    }

    // -------------------------------------------------------------------------
    // PROCESAMIENTO Y DIBUJADO DEL ESQUELETO A PARTIR DE NUITRACK
    // -------------------------------------------------------------------------
    public void ProcessSkeleton(UserData user)
    {
        if (user == null || user.Skeleton == null || parentRect == null)
        {
            SetAllActive(false);
            return;
        }

        // -----------------------------
        // ACTUALIZACIÓN DE JOINTS
        // -----------------------------
        for (int i = 0; i < jointsInfo.Length; i++)
        {
            nuitrack.JointType jt = jointsInfo[i];
            UserData.SkeletonData.Joint joint = user.Skeleton.GetJoint(jt);
            RectTransform rt = joints[jt];

            bool shouldShow =
                joint.Confidence > CONFIDENCE_THRESHOLD &&
                visibleJoints.IsVisible(jt);

            rt.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                Vector2 pos = joint.AnchoredPosition(parentRect.rect, rt);
                if (mirrorX) pos.x = -pos.x;

                rt.anchoredPosition = pos;
                rt.localScale = Vector3.one * jointScale;
            }
        }

        // -----------------------------
        // ACTUALIZACIÓN DE CONEXIONES
        // -----------------------------
        for (int i = 0; i < connectionsInfo.GetLength(0); i++)
        {
            RectTransform a = joints[connectionsInfo[i, 0]];
            RectTransform b = joints[connectionsInfo[i, 1]];

            bool active = a.gameObject.activeSelf && b.gameObject.activeSelf;
            connections[i].gameObject.SetActive(active);

            if (active)
            {
                connections[i].anchoredPosition = a.anchoredPosition;

                // Vector dirección (v = p2 - p1) usado para orientar el “hueso”
                connections[i].transform.right = b.position - a.position;

                // Longitud del segmento en coordenadas UI
                float d = Vector2.Distance(a.anchoredPosition, b.anchoredPosition);

                // Longitud = d, grosor = boneThickness
                connections[i].transform.localScale = new Vector3(d, boneThickness, 1f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helper: aplica un color a todos los componentes Image del prefab (incluye hijos)
    // -------------------------------------------------------------------------
    private static void ApplyColorToAllImages(GameObject root, Color color)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].color = color;
    }
}
