using UnityEngine;
using UnityEngine.UI;

/*
 * BirdHealthBar
 * -------------
 * Controla la barra de vida UI de cada bird (Image.fillAmount) a partir de la salud del BirdInstance.
 * Se mantiene separado para simplificar el prefab y reutilizar el mismo componente en distintos birds.
 *
 * Nota TFG: Solo se han añadido comentarios. No se ha modificado ninguna línea de código.
 */
public class BirdHealthBar : MonoBehaviour
{
    // Referencia al BirdInstance asociado (de donde se lee currentHealth y maxHealth)
    public BirdInstance bird;

    // Imagen de UI que actúa como barra de relleno (fillAmount)
    public Image fillImage;

    // Cacheo de referencias (BirdInstance y la imagen de relleno).
    void Awake()
    {
        // Si no se asigna desde inspector, intenta encontrar el BirdInstance en el parent
        if (bird == null) bird = GetComponentInParent<BirdInstance>();

        // Si no se asigna desde inspector, usa la Image del propio objeto
        if (fillImage == null) fillImage = GetComponent<Image>();
    }

    // Actualiza el fillAmount según el ratio currentHealth/maxHealth.
    void Update()
    {
        // Si faltan referencias, no se actualiza la UI
        if (bird == null || fillImage == null) return;

        // Ratio de vida restante: (salud actual / salud máxima)
        float ratio = (bird.maxHealth > 0) ? (float)bird.currentHealth / bird.maxHealth : 0f;

        // fillAmount va de 0 a 1
        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }
}
