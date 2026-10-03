using System.Collections;
using UnityEngine;

/// <summary>
/// Random lightning: a burst of bright flickers on a directional light and the ambient colour, followed by thunder
/// (from the soundscape) after a delay that grows with distance.
/// </summary>
public class LightningStorm : MonoBehaviour
{
    public Light Sky;
    public ScarySoundscape Soundscape;
    public Camera Camera;
    public Vector2 Interval = new Vector2(7f, 16f);
    public float FlashIntensity = 7f;
    public Color FlashColor = new Color(0.78f, 0.84f, 1f);
    public Color FlashSkyColor = new Color(0.32f, 0.36f, 0.46f);

    float baseIntensity;
    Color baseColor, baseAmbient, baseBackground;

    void Start()
    {
        if (Sky != null) { baseIntensity = Sky.intensity; baseColor = Sky.color; }
        baseAmbient = RenderSettings.ambientLight;
        if (Camera != null) baseBackground = Camera.backgroundColor;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(Random.Range(3f, 6f));
        while (true)
        {
            int flickers = Random.Range(2, 5);
            for (int i = 0; i < flickers; i++)
            {
                float k = Random.Range(0.55f, 1f);
                Set(k);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                Set(Random.Range(0f, 0.2f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.15f));
            }
            Set(0f);
            float distance = Random.Range(0.3f, 2.2f); // seconds until the thunder arrives
            yield return new WaitForSeconds(distance);
            if (Soundscape != null) Soundscape.PlayThunder(Mathf.InverseLerp(2.2f, 0.3f, distance));
            yield return new WaitForSeconds(Random.Range(Interval.x, Interval.y));
        }
    }

    void Set(float k)
    {
        if (Sky != null)
        {
            Sky.intensity = Mathf.Lerp(baseIntensity, FlashIntensity, k);
            Sky.color = Color.Lerp(baseColor, FlashColor, k);
        }
        RenderSettings.ambientLight = Color.Lerp(baseAmbient, FlashSkyColor, k);
        if (Camera != null) Camera.backgroundColor = Color.Lerp(baseBackground, FlashSkyColor * 0.7f, k);
    }
}
