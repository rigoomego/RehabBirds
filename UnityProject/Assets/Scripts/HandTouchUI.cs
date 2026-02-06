using UnityEngine;
using NuitrackSDK;
using System.Collections.Generic;

/*
 * HandTouchUI
 * -----------
 * Detecta impactos mano–bird en un entorno 2D (UI) usando joints de Nuitrack.
 * Cada frame:
 *  - Lee varias articulaciones (mano y muñeca, izquierda/derecha).
 *  - Convierte la posición del joint a coordenadas UI del contenedor (parentRect).
 *  - Calcula distancia al RectTransform del target (bird).
 *  - Si la distancia es menor que touchThreshold, aplica daño llamando a BirdInstance.OnHit().
 *
 * Nota: BirdSpawner añade/quita targets usando AddTarget/RemoveTarget cuando activa/desactiva birds.
 *
 * 
 */
public class HandTouchUI : MonoBehaviour
{
    [Header("Detección")]
    // Distancia umbral en UI (píxeles aprox) para considerar "toque/impacto"
    [SerializeField] float touchThreshold = 50f;

    // Daño aplicado al bird en cada impacto detectado
    [SerializeField] int touchDamage = 1;

    [Header("Tracking")]
    // Confianza mínima del joint para considerarlo válido
    [Range(0.1f, 0.9f)]
    public float minJointConfidence = 0.35f;

    [Header("Targets tocables")]
    // Lista de RectTransforms de targets activos (birds visibles)
    public List<RectTransform> targetObjects = new List<RectTransform>();

    [Header("Rect de referencia (debe ser el mismo contenedor de los birds)")]
    // Contenedor base para convertir coordenadas del joint a UI
    public RectTransform parentRect;

    void Start()
    {
        // Si no se asigna desde inspector, usa el RectTransform del propio objeto
        if (parentRect == null)
            parentRect = GetComponent<RectTransform>();
    }

    void Update()
    {
        var user = NuitrackManager.Users.Current;
        if (user == null || user.Skeleton == null || targetObjects.Count == 0 || parentRect == null)
            return;

        // Limpia nulos (por seguridad si algún target fue destruido/desactivado)
        for (int i = targetObjects.Count - 1; i >= 0; i--)
            if (targetObjects[i] == null) targetObjects.RemoveAt(i);

        // Joints a comprobar (mano y muñeca de ambos lados)
        nuitrack.JointType[] jointsToCheck =
        {
            nuitrack.JointType.LeftHand,
            nuitrack.JointType.RightHand,
            nuitrack.JointType.LeftWrist,
            nuitrack.JointType.RightWrist
        };

        // Recorre joints válidos y targets para detectar proximidad
        foreach (var jt in jointsToCheck)
        {
            var joint = user.Skeleton.GetJoint(jt);
            if (joint.Confidence < minJointConfidence) continue;

            for (int i = targetObjects.Count - 1; i >= 0; i--)
            {
                RectTransform target = targetObjects[i];
                if (target == null) { targetObjects.RemoveAt(i); continue; }

                // Convertir joint a coordenadas UI del target
                Vector2 jointUIPos = joint.AnchoredPosition(parentRect.rect, target);
                jointUIPos.x = -jointUIPos.x; // tu convención de espejo

                // Distancia entre joint y posición UI del target
                float dist = Vector2.Distance(jointUIPos, target.anchoredPosition);
                if (dist < touchThreshold)
                {
                    // Localiza el BirdInstance asociado al target
                    BirdInstance bi = target.GetComponent<BirdInstance>();
                    if (bi == null) bi = target.GetComponentInParent<BirdInstance>();

                    // Si existe, aplica daño mediante OnHit()
                    if (bi != null)
                    {
                        bi.OnHit(touchDamage);
                    }
                }
            }
        }
    }

    // Añade un target (llamado por BirdSpawner al activar bird)
    public void AddTarget(RectTransform target)
    {
        if (target != null && !targetObjects.Contains(target))
            targetObjects.Add(target);
    }

    // Elimina un target (llamado por BirdSpawner al desactivar bird)
    public void RemoveTarget(RectTransform target)
    {
        if (targetObjects.Contains(target))
            targetObjects.Remove(target);
    }

    // Limpia todos los targets (utilidad)
    public void ClearTargets() => targetObjects.Clear();
}
