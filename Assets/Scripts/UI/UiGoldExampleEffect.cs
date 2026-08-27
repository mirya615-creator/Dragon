using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UiGoldExampleEffect : MonoBehaviour
{
    [Header("Gold UI particle example")]
    [SerializeField, Min(1)] private int maxParticles = 120;
    [SerializeField, Min(0f)] private float emissionRate = 12f;
    [SerializeField] private Color darkGold = new Color(1f, 0.48f, 0.03f, 1f);
    [SerializeField] private Color lightGold = new Color(1f, 0.95f, 0.45f, 1f);

    private RectTransform targetRect;
    private ParticleSystem particleSystemInstance;
    private Vector2 lastTargetSize;

    private void Awake()
    {
        BuildEffect();
    }

    private void LateUpdate()
    {
        UpdateEmissionArea();
    }

    private void BuildEffect()
    {
        targetRect = transform as RectTransform;
        if (targetRect == null || transform.Find("GoldParticleExample") != null)
        {
            return;
        }

        var root = new GameObject(
            "GoldParticleExample",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(ParticleSystem),
            typeof(UiParticleGraphic));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        Stretch(rootRect);
        rootRect.SetAsLastSibling();

        particleSystemInstance = root.GetComponent<ParticleSystem>();
        var particleGraphic = root.GetComponent<UiParticleGraphic>();
        particleGraphic.raycastTarget = false;
        particleGraphic.Configure(particleSystemInstance, UiGoldSparkleAsset.Sprite);

        var particleRenderer = root.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            // Overlay Canvas cannot sort a normal ParticleSystemRenderer above UI.
            // UiParticleGraphic renders this ParticleSystem through CanvasRenderer instead.
            particleRenderer.enabled = false;
        }

        particleSystemInstance.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleSystem();
        UpdateEmissionArea(true);
        particleSystemInstance.Play(true);
    }

    private void ConfigureParticleSystem()
    {
        var main = particleSystemInstance.main;
        main.duration = 2f;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(darkGold, lightGold);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = true;
        main.maxParticles = maxParticles;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = particleSystemInstance.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 12, 18, 1, 0.01f)
        });

        var shape = particleSystemInstance.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.randomDirectionAmount = 0f;

        var velocity = particleSystemInstance.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        velocity.y = new ParticleSystem.MinMaxCurve(12f, 30f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var color = particleSystemInstance.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(darkGold, 0f),
                new GradientColorKey(lightGold, 0.45f),
                new GradientColorKey(darkGold, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.85f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var size = particleSystemInstance.sizeOverLifetime;
        size.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.35f, 1f),
            new Keyframe(0.72f, 0.72f),
            new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rotation = particleSystemInstance.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

        var noise = particleSystemInstance.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    private void UpdateEmissionArea(bool force = false)
    {
        if (particleSystemInstance == null || targetRect == null)
        {
            return;
        }

        var size = targetRect.rect.size;
        if (size.x <= 1f || size.y <= 1f)
        {
            size = new Vector2(220f, 110f);
        }

        if (!force && Vector2.SqrMagnitude(size - lastTargetSize) < 0.01f)
        {
            return;
        }

        lastTargetSize = size;
        var shape = particleSystemInstance.shape;
        shape.scale = new Vector3(size.x * 0.88f, size.y * 0.72f, 1f);
        shape.position = new Vector3(0f, -size.y * 0.08f, 0f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

[RequireComponent(typeof(CanvasRenderer), typeof(ParticleSystem))]
public sealed class UiParticleGraphic : MaskableGraphic
{
    private ParticleSystem source;
    private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();
    private Sprite particleSprite;

    public override Texture mainTexture => particleSprite != null
        ? particleSprite.texture
        : Texture2D.whiteTexture;

    public void Configure(ParticleSystem particleSystemSource, Sprite sprite)
    {
        source = particleSystemSource;
        particleSprite = sprite;
        color = Color.white;
        raycastTarget = false;
        SetAllDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (source == null)
        {
            source = GetComponent<ParticleSystem>();
        }
    }

    private void LateUpdate()
    {
        if (source != null && source.isPlaying)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (source == null)
        {
            return;
        }

        var requiredCapacity = Mathf.Max(1, source.main.maxParticles);
        if (particles.Length < requiredCapacity)
        {
            particles = new ParticleSystem.Particle[requiredCapacity];
        }

        var count = source.GetParticles(particles);
        var uv = particleSprite != null
            ? UnityEngine.Sprites.DataUtility.GetOuterUV(particleSprite)
            : new Vector4(0f, 0f, 1f, 1f);
        for (var index = 0; index < count; index++)
        {
            AppendParticleQuad(vertexHelper, particles[index], uv);
        }
    }

    private void AppendParticleQuad(VertexHelper vertexHelper, ParticleSystem.Particle particle, Vector4 uv)
    {
        var size = particle.GetCurrentSize(source);
        var halfSize = size * 0.5f;
        var radians = particle.rotation * Mathf.Deg2Rad;
        var right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * halfSize;
        var up = new Vector2(-right.y, right.x);
        var center = (Vector2)particle.position;
        var particleColor = particle.GetCurrentColor(source) * color;

        var start = vertexHelper.currentVertCount;
        vertexHelper.AddVert(center - right - up, particleColor, new Vector2(uv.x, uv.y));
        vertexHelper.AddVert(center - right + up, particleColor, new Vector2(uv.x, uv.w));
        vertexHelper.AddVert(center + right + up, particleColor, new Vector2(uv.z, uv.w));
        vertexHelper.AddVert(center + right - up, particleColor, new Vector2(uv.z, uv.y));
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start + 2, start + 3, start);
    }
}

internal static class UiGoldSparkleAsset
{
    private static Sprite sprite;
    public static Sprite Sprite => sprite != null ? sprite : sprite = CreateSprite();

    private static Sprite CreateSprite()
    {
        const int textureSize = 32;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "UiGoldParticleTexture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[textureSize * textureSize];
        var center = (textureSize - 1) * 0.5f;
        for (var y = 0; y < textureSize; y++)
        {
            for (var x = 0; x < textureSize; x++)
            {
                var normalizedX = Mathf.Abs(x - center) / center;
                var normalizedY = Mathf.Abs(y - center) / center;
                var radial = Mathf.Clamp01(1f - Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY)));
                var cross = Mathf.Clamp01(1f - Mathf.Min(normalizedX, normalizedY) * 1.35f);
                var alpha = Mathf.Max(Mathf.Pow(radial, 2.1f), Mathf.Pow(cross, 8f) * 0.55f);
                pixels[(y * textureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var result = UnityEngine.Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f);
        result.name = "UiGoldParticleSprite";
        result.hideFlags = HideFlags.HideAndDontSave;
        return result;
    }
}

public static class UiGoldExampleEffectInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InstallInScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInScene(scene);
    }

    private static void InstallInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var canvases = roots[rootIndex].GetComponentsInChildren<Canvas>(true);
            for (var canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
            {
                if (!string.Equals(canvases[canvasIndex].name, "Canvas", StringComparison.Ordinal))
                {
                    continue;
                }

                var imageA = canvases[canvasIndex].transform.Find("ImageA");
                if (imageA == null || imageA.GetComponent<Image>() == null)
                {
                    continue;
                }

                if (imageA.GetComponent<UiGoldExampleEffect>() == null)
                {
                    imageA.gameObject.AddComponent<UiGoldExampleEffect>();
                }

                return;
            }
        }
    }
}
