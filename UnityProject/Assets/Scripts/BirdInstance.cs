using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * BirdInstance
 * ------------
 * Representa una instancia activa de un "bird" en pantalla.
 * Gestiona:
 *  - Salud (maxHealth/currentHealth) y muerte.
 *  - Feedback visual del golpe (flash + shake).
 *  - Efectos de daño acumulativo (overlays: ojo morado, venda, tiritas, estrellas).
 *  - Feedback auditivo (AudioSource + clip por tipo de bird).
 *  - Comunicación con BirdSpawner (hits, misses, fin de aparición, desactivación).
 */
[RequireComponent(typeof(AudioSource))]
public class BirdInstance : MonoBehaviour
{
    public int typeIndex;
    BirdSpawner spawner;

    // Referencias UI/componentes del prefab
    Image image;
    RectTransform rt;
    Image fillImage;

    // Daño visual: overlays activables de forma acumulativa
    private List<GameObject> orderedDamageOverlays;

    // Audio: componente y clip asociado al tipo de bird
    private AudioSource audioSource;
    private AudioClip hitSoundClip;

    // Salud del bird
    public int maxHealth;
    public int currentHealth;

    // Estado general
    public bool isPermanentlyDead = false;

    // Control de apariciones por serie
    public int appearanceGoalPerSerie = 0;
    public int appearancesThisSerie = 0;
    public bool isSerieComplete = false;

    // Flag: si ha sido golpeado en la aparición actual (evita multi-hit si singleHitPerAppearance está activo)
    public bool hasBeenHitThisAppearance = false;

    // Estado interno para evitar registrar golpes durante el feedback (invulnerabilidad temporal)
    bool isInvulnerable = false;

    [Header("Efectos de Muerte")]
    public float fallSpeed = 600f;
    public float tumbleSpeed = 720f;

    // Feedback hit (parpadeo y vibración)
    [Header("Feedback Hit")]
    [SerializeField] private float hitFlashSeconds = 0.12f;
    [SerializeField] private float hitShakePixels = 10f;

    // Estado original para restaurar feedback
    private Color originalColor = Color.white;
    private Vector2 originalAnchoredPos;

    public void InitializeFromPool(BirdSpawner spawner, BirdSpawner.BirdTypeInfo info, int catalogIndex, int appearanceGoal)
    {
        this.typeIndex = catalogIndex;
        this.spawner = spawner;

        // Cache UI refs
        this.image = GetComponent<Image>();
        this.rt = GetComponent<RectTransform>();

        if (image != null) originalColor = image.color;
        if (rt != null) originalAnchoredPos = rt.anchoredPosition;

        // Salud base
        this.maxHealth = info.maxHealth;
        this.currentHealth = info.maxHealth;
        this.isPermanentlyDead = false;

        // Objetivo de apariciones
        this.appearanceGoalPerSerie = appearanceGoal;

        // Health bar
        Transform fill = transform.Find("HealthBar/Fill");
        if (fill != null)
            fillImage = fill.GetComponent<Image>();

        // Overlays de daño (si existen en el prefab)
        orderedDamageOverlays = new List<GameObject>();
        FindAndAddOverlay("Ojo_Morado");
        FindAndAddOverlay("Venda_Cabeza");
        FindAndAddOverlay("Tiritas");
        FindAndAddOverlay("Estrellas");

        // Audio
        this.audioSource = GetComponent<AudioSource>();
        this.audioSource.playOnAwake = false;
        this.hitSoundClip = info.hitSound;

        ResetForNewSerie();
    }

    void FindAndAddOverlay(string name)
    {
        Transform overlayT = transform.Find(name);
        if (overlayT != null)
        {
            overlayT.gameObject.SetActive(false);
            orderedDamageOverlays.Add(overlayT.gameObject);
        }
    }

    public void ResetForNewSerie()
    {
        appearancesThisSerie = 0;
        isSerieComplete = false;
        hasBeenHitThisAppearance = false;
        isInvulnerable = false;

        currentHealth = maxHealth;
        isPermanentlyDead = false;

        // UI reset
        if (fillImage != null) fillImage.fillAmount = 1f;
        if (image != null) image.color = originalColor;

        // Reset overlays
        if (orderedDamageOverlays != null)
        {
            foreach (var o in orderedDamageOverlays)
                if (o != null) o.SetActive(false);
        }

        // Reset transform (por si venía de una caída anterior)
        if (rt == null) rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
    }

    // Llamado por BirdMover al salir de pantalla
    public void OnScreenExit()
    {
        if (isPermanentlyDead || isSerieComplete) return;

        appearancesThisSerie++;

        // Cuenta como aparición completada (esto incrementa reps en BirdSpawner)
        if (spawner != null) spawner.NotifyAppearanceCompleted();

        // Si no fue golpeado, cuenta como miss
        if (!hasBeenHitThisAppearance && spawner != null)
            spawner.NotifyMissed();

        // Si alcanzó el objetivo de apariciones para esta serie, se marca completo
        if (appearancesThisSerie >= appearanceGoalPerSerie)
            isSerieComplete = true;

        gameObject.SetActive(false);

        // Notifica al spawner para logging y cleanup
        if (spawner != null) spawner.NotifyInstanceDeactivated(this);

        // Reset de la aparición actual
        hasBeenHitThisAppearance = false;
    }

    // Recibe un golpe (desde HandTouchUI / BirdHealthBar etc.)
    public bool OnHit(int damage)
    {
        if (isInvulnerable || isPermanentlyDead || isSerieComplete) return false;
        if (spawner == null) return false;

        // Si solo se permite un hit por aparición y ya fue golpeado, ignorar
        if (spawner.singleHitPerAppearance && hasBeenHitThisAppearance)
            return false;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        UpdateHealthBar();
        StartCoroutine(HitFeedback());
        UpdateDamageVisuals();

        hasBeenHitThisAppearance = true;

        // Notificar hit al spawner (contador de hits)
        spawner.NotifyHitLanded();

        // Audio feedback
        if (audioSource != null && hitSoundClip != null)
            audioSource.PlayOneShot(hitSoundClip);

        // Si muere
        if (currentHealth <= 0)
        {
            Die();
            return true;
        }

        return false;
    }

    void UpdateDamageVisuals()
    {
        if (orderedDamageOverlays == null || orderedDamageOverlays.Count == 0) return;

        int hitsTaken = maxHealth - currentHealth;
        for (int i = 0; i < hitsTaken; i++)
        {
            if (i < orderedDamageOverlays.Count && orderedDamageOverlays[i] != null)
                orderedDamageOverlays[i].SetActive(true);
        }
    }

    void UpdateHealthBar()
    {
        if (fillImage == null) return;
        float ratio = (maxHealth > 0) ? (float)currentHealth / maxHealth : 0f;
        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    // Feedback visual al golpear: flash + shake rápido
    IEnumerator HitFeedback()
    {
        if (image == null || rt == null) yield break;

        isInvulnerable = true;

        Color c0 = originalColor;
        Vector2 p0 = rt.anchoredPosition;

        image.color = Color.red;

        float t = 0f;
        while (t < hitFlashSeconds)
        {
            t += Time.deltaTime;
            float dx = Random.Range(-hitShakePixels, hitShakePixels);
            float dy = Random.Range(-hitShakePixels, hitShakePixels);
            rt.anchoredPosition = p0 + new Vector2(dx, dy);
            yield return null;
        }

        rt.anchoredPosition = p0;
        image.color = c0;
        isInvulnerable = false;
    }

    // Muerte por golpes: inicia animación de caída + giro, y luego desactiva (pooling)
    void Die()
    {
        if (isPermanentlyDead) return;

        isPermanentlyDead = true;

        // Cuenta como aparición completada (INCREMENTA reps)
        if (spawner != null) spawner.NotifyAppearanceCompleted();

        // Detiene el movimiento normal del bird (para que no siga avanzando)
        BirdMover mover = GetComponent<BirdMover>();
        if (mover != null) mover.enabled = false;

        // Notifica muerte por golpes (si tu gameplay lo usa)
        if (spawner != null) spawner.NotifyInstanceKilledByHits(this);

        // IMPORTANTE: parar cualquier coroutine (p.ej. shake) y lanzar caída
        StopAllCoroutines();
        StartCoroutine(FallAndDeactivate());
    }

    // Animación de caída fuera de pantalla y desactivación (pooling)
    IEnumerator FallAndDeactivate()
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        // Para que la caída quede por encima visualmente (si lo usabas)
        transform.SetAsLastSibling();

        float dropDistance = 1000f;
        float startY = rt.anchoredPosition.y;
        float targetY = startY - dropDistance;

        while (rt.anchoredPosition.y > targetY)
        {
            rt.anchoredPosition -= new Vector2(0, fallSpeed * Time.deltaTime);
            rt.Rotate(0, 0, -tumbleSpeed * Time.deltaTime);
            yield return null;
        }

        // Marcar como completado y desactivar (pooling)
        isSerieComplete = true;
        gameObject.SetActive(false);

        // CLAVE: notificar al spawner para que quite targets, loguee y permita avanzar sesión
        if (spawner != null) spawner.NotifyInstanceDeactivated(this);
    }
}
