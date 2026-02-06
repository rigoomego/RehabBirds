using UnityEngine;
using UnityEngine.UI;

public class Bird5Black : MonoBehaviour   // ?? importante: hereda de MonoBehaviour
{
    public Sprite[] frames;
    public float frameRate = 10f;

    private Image image;
    private int currentFrame;
    private float timer;

    void Start()
    {
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (frames.Length == 0) return;

        timer += Time.deltaTime;
        if (timer >= 1f / frameRate)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            image.sprite = frames[currentFrame];
        }
    }
}
