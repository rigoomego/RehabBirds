using UnityEngine;

/*
 * RotationAnimation
 * -----------------
 * Componente simple para rotar continuamente un GameObject en el eje Z.
 * Se usa típicamente para elementos decorativos o indicadores (p.ej. iconos giratorios).
 *
 * 
 */
public class RotationAnimation : MonoBehaviour
{
    [Tooltip("Velocidad de rotación en grados por segundo.")]
    public float rotationSpeed = 100f;

    [Tooltip("Dirección de la rotación. 1 para horario, -1 para antihorario.")]
    public float rotationDirection = 1f;

    void Update()
    {
        // Rotar el propio GameObject alrededor de su pivote (que debe ser el centro de la órbita)
        transform.Rotate(0, 0, rotationDirection * rotationSpeed * Time.deltaTime);
    }
}
