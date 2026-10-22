using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Header("Rimbalzo (sempre sul posto)")]
    [SerializeField] private float bounceHeight = 0.5f;
    [SerializeField] private float gravity = 4f;

    [Tooltip("Metà lato del cubo più grande (es. 0.9 se Mesh_Outer ha scala 1.8).")]
    [SerializeField] private float halfSize = 0.9f;

    [Header("Rotazione")]
    [SerializeField] private float spinY = 45f;
    [SerializeField] private float impactSpin = 250f;
    [SerializeField] private float randomSpin = 30f;
    [SerializeField] private float airDamping = 0.8f;

    [Header("Squash all'impatto")]
    [SerializeField] private float squashAmount = 0.25f;
    [SerializeField] private float squashFrequency = 3f;
    [SerializeField] private float squashDamping = 8f;

    [Header("Trasformazione in volo")]
    [Tooltip("Quanto si stira il cubo (0 = mai). Il volume viene conservato.")]
    [Range(0f, 1f)][SerializeField] private float morphStrength = 0.35f;

    [Tooltip("Frazione del volo in cui il cubo è già tornato perfettamente cubo.")]
    [Range(0.4f, 0.95f)][SerializeField] private float morphEnd = 0.8f;

    [Tooltip("Sfasamento tra un livello e l'altro.")]
    [SerializeField] private float morphLayerDelay = 0.06f;

    [Header("Angoli smussati")]
    [Tooltip("0 = angoli vivi, 1 = diventa una sfera al picco del volo.")]
    [Range(0f, 1f)][SerializeField] private float roundAmount = 1f;

    [Tooltip("Suddivisioni per lato della mesh. Più alto = curva più liscia (consigliato 12-20).")]
    [Range(4, 32)][SerializeField] private int meshDetail = 16;

    [Header("Colore")]
    [SerializeField] private float hueShiftMin = 0.15f;
    [SerializeField] private float hueShiftMax = 0.35f;
    [SerializeField] private float colorSmoothing = 2.5f;
    [Range(0f, 1f)][SerializeField] private float saturation = 0.8f;
    [Range(0f, 1f)][SerializeField] private float brightness = 1f;
    [SerializeField] private float emissionBase = 0.6f;
    [SerializeField] private float emissionPulse = 2.5f;

    [Header("Pulse Light")]
    [SerializeField] private float lightBaseIntensity = 1f;
    [SerializeField] private float lightPulseIntensity = 8f;
    [SerializeField] private float lightRange = 6f;
    [SerializeField] private float pulseDecay = 4f;

    [Header("Effetto a terra")]
    [SerializeField] private float ringSize = 2.5f;
    [SerializeField] private float ringLifetime = 0.6f;
    [SerializeField] private int sparkCount = 10;
    [SerializeField] private float sparkSpeed = 2.5f;
    [SerializeField] private float groundOffset = 0f;
    [SerializeField] private Material fxMaterial;

    // --- rimbalzo/rotazione ---
    private Vector3 startLocalPosition;
    private Vector3 startLocalScale;
    private float groundY;
    private float z, vz, launchSpeed;
    private float airTime, airTimer;
    private float squashTimer = 999f;
    private Vector3 angularVelocity;
    private Quaternion rotation = Quaternion.identity;
    private Transform[] bodies;
    private Quaternion[] baseRotations;
    private Vector3[] baseScales;
    private Vector3[] morphTargets;

    // --- mesh arrotondabile ---
    private MeshFilter[] filters;
    private Mesh[] roundMeshes;
    private Vector3[][] workVerts;
    private Vector3[][] workNormals;
    private float[] lastRound;
    private Vector3[] gridPos;      // posizioni del cubo unitario (-0.5..0.5)
    private Vector3[] gridFaceN;    // normale della faccia di ogni vertice
    private Vector2[] gridUV;
    private int[] gridTris;
    private Vector3[] meshSize;

    // --- colore/luce ---
    private Renderer[] renderers;
    private Material[][] materials;
    private float[][] originalAlpha;
    private Light pulseLight;
    private float currentHue = 0.5f;
    private float targetHue = 0.5f;
    private float pulse;
    private Color currentColor = Color.cyan;

    // --- effetto a terra ---
    private ParticleSystem fx;
    private Texture2D fxTexture;
    private Material createdFxMaterial;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        startLocalPosition = transform.localPosition;
        startLocalScale = transform.localScale;
        groundY = startLocalPosition.y - halfSize;

        launchSpeed = Mathf.Sqrt(2f * gravity * bounceHeight);
        airTime = 2f * launchSpeed / gravity;
        vz = launchSpeed;
        angularVelocity = new Vector3(0f, spinY, 0f);

        int n = transform.childCount;
        bodies = new Transform[n];
        baseRotations = new Quaternion[n];
        baseScales = new Vector3[n];
        morphTargets = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            bodies[i] = transform.GetChild(i);
            baseRotations[i] = bodies[i].localRotation;
            baseScales[i] = bodies[i].localScale;
            morphTargets[i] = Vector3.one;
        }

        SetupRoundMeshes();
        SetupLight();
        SetupMaterials();
        SetupFx();
        ApplyColor();
    }

    // ---------------------------------------------------------------- MESH ARROTONDABILE

    private void BuildGrid(int div)
    {
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3[] us = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
        Vector3[] vs = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

        int side = div + 1;
        int vertsPerFace = side * side;
        gridPos = new Vector3[6 * vertsPerFace];
        gridFaceN = new Vector3[6 * vertsPerFace];
        gridUV = new Vector2[6 * vertsPerFace];
        gridTris = new int[6 * div * div * 6];

        int t = 0;
        for (int f = 0; f < 6; f++)
        {
            Vector3 nrm = normals[f], u = us[f], v = vs[f];
            bool flip = Vector3.Dot(Vector3.Cross(u, v), nrm) > 0f;
            int baseIndex = f * vertsPerFace;

            for (int j = 0; j <= div; j++)
                for (int i = 0; i <= div; i++)
                {
                    int idx = baseIndex + j * side + i;
                    float fu = (float)i / div, fv = (float)j / div;
                    gridPos[idx] = nrm * 0.5f + u * (fu - 0.5f) + v * (fv - 0.5f);
                    gridFaceN[idx] = nrm;
                    gridUV[idx] = new Vector2(fu, fv);
                }

            for (int j = 0; j < div; j++)
                for (int i = 0; i < div; i++)
                {
                    int a = baseIndex + j * side + i;
                    int b = a + 1;          // +u
                    int c = a + side;       // +v
                    int d = c + 1;

                    if (flip)
                    {
                        gridTris[t++] = a; gridTris[t++] = b; gridTris[t++] = c;
                        gridTris[t++] = b; gridTris[t++] = d; gridTris[t++] = c;
                    }
                    else
                    {
                        gridTris[t++] = a; gridTris[t++] = c; gridTris[t++] = b;
                        gridTris[t++] = b; gridTris[t++] = c; gridTris[t++] = d;
                    }
                }
        }
    }

    private void SetupRoundMeshes()
    {
        BuildGrid(meshDetail);

        int n = bodies.Length;
        filters = new MeshFilter[n];
        roundMeshes = new Mesh[n];
        workVerts = new Vector3[n][];
        workNormals = new Vector3[n][];
        lastRound = new float[n];
        meshSize = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            var mf = bodies[i].GetComponent<MeshFilter>();
            if (mf == null) continue;

            // Dimensioni della mesh originale (un cubo di Unity è 1x1x1)
            meshSize[i] = mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;

            var m = new Mesh { name = "RoundedCube_" + bodies[i].name };
            workVerts[i] = new Vector3[gridPos.Length];
            workNormals[i] = new Vector3[gridPos.Length];
            m.vertices = gridPos;
            m.normals = gridFaceN;
            m.uv = gridUV;
            m.triangles = gridTris;
            m.RecalculateBounds();

            mf.sharedMesh = m;
            filters[i] = mf;
            roundMeshes[i] = m;
            lastRound[i] = -1f;
            SetRoundness(i, 0f);
        }
    }

    // roundness: 0 = cubo, 1 = sfera
    private void SetRoundness(int i, float roundness)
    {
        if (filters[i] == null) return;
        if (Mathf.Abs(roundness - lastRound[i]) < 0.001f && !(roundness == 0f && lastRound[i] != 0f)) return;
        lastRound[i] = roundness;

        float r = Mathf.Clamp01(roundness) * 0.5f;
        float lim = 0.5f - r;
        float blend = Mathf.Clamp01(r / 0.05f); // evita lo scatto delle normali per r molto piccoli

        Vector3[] verts = workVerts[i];
        Vector3[] norms = workNormals[i];
        Vector3 size = meshSize[i];

        for (int k = 0; k < gridPos.Length; k++)
        {
            Vector3 p = gridPos[k];

            if (r <= 0.0001f)
            {
                verts[k] = Vector3.Scale(p, size);
                norms[k] = gridFaceN[k];
                continue;
            }

            Vector3 inner = new Vector3(
                Mathf.Clamp(p.x, -lim, lim),
                Mathf.Clamp(p.y, -lim, lim),
                Mathf.Clamp(p.z, -lim, lim));

            Vector3 d = p - inner;
            float len = d.magnitude;
            Vector3 dir = len > 1e-6f ? d / len : gridFaceN[k];

            verts[k] = Vector3.Scale(inner + dir * r, size);
            norms[k] = Vector3.Normalize(Vector3.Lerp(gridFaceN[k], dir, blend));
        }

        Mesh m = roundMeshes[i];
        m.vertices = verts;
        m.normals = norms;
        m.RecalculateBounds();
    }

    // ---------------------------------------------------------------- SETUP

    private void SetupLight()
    {
        pulseLight = GetComponent<Light>();
        if (pulseLight == null) pulseLight = gameObject.AddComponent<Light>();

        pulseLight.type = LightType.Point;
        pulseLight.range = lightRange;
        pulseLight.shadows = LightShadows.None;
    }

    private void SetupMaterials()
    {
        renderers = GetComponentsInChildren<Renderer>();
        materials = new Material[renderers.Length][];
        originalAlpha = new float[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].materials;
            originalAlpha[i] = new float[materials[i].Length];

            for (int m = 0; m < materials[i].Length; m++)
            {
                Material mat = materials[i][m];
                int colorProp = GetColorProp(mat);
                originalAlpha[i][m] = colorProp != -1 ? mat.GetColor(colorProp).a : 1f;
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    private void SetupFx()
    {
        fx = GetComponent<ParticleSystem>();
        if (fx == null) fx = gameObject.AddComponent<ParticleSystem>();
        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = fx.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.gravityModifier = 0f;
        main.maxParticles = 300;

        var emission = fx.emission;
        emission.enabled = false;

        var shape = fx.shape;
        shape.enabled = false;

        var sheet = fx.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0.25f);
        sheet.cycleCount = 1;

        var size = fx.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(0.3f, 0.7f),
            new Keyframe(1f, 1f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var col = fx.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = fx.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;

        if (fxMaterial != null)
        {
            psr.sharedMaterial = fxMaterial;
        }
        else
        {
            fxTexture = CreateFxTexture();
            Shader sh = Shader.Find("Sprites/Default");
            createdFxMaterial = new Material(sh) { mainTexture = fxTexture };
            psr.sharedMaterial = createdFxMaterial;
        }

        fx.Play();
    }

    private static Texture2D CreateFxTexture()
    {
        const int s = 64;
        var tex = new Texture2D(s * 2, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color[s * 2 * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s * 2; x++)
            {
                int tile = x / s;
                float u = ((x % s) + 0.5f) / s * 2f - 1f;
                float v = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                float a;
                if (tile == 0)
                {
                    float d = (r - 0.78f) / 0.1f;
                    a = Mathf.Exp(-d * d);
                }
                else
                {
                    float f = Mathf.Clamp01(1f - r);
                    a = f * f;
                }
                px[y * s * 2 + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private int GetColorProp(Material mat)
    {
        if (mat.HasProperty(BaseColorId)) return BaseColorId;
        if (mat.HasProperty(ColorId)) return ColorId;
        return -1;
    }

    private void OnDestroy()
    {
        if (materials != null)
            foreach (var arr in materials)
                foreach (var mat in arr)
                    if (mat != null) Destroy(mat);

        if (roundMeshes != null)
            foreach (var m in roundMeshes)
                if (m != null) Destroy(m);

        if (createdFxMaterial != null) Destroy(createdFxMaterial);
        if (fxTexture != null) Destroy(fxTexture);
    }

    // --------------------------------------------------------------- UPDATE

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateSpin(dt);
        UpdateBounce(dt);
        UpdateMorph();
        UpdateColor(dt);
    }

    private void UpdateSpin(float dt)
    {
        angularVelocity.x = Mathf.Lerp(angularVelocity.x, 0f, airDamping * dt);
        angularVelocity.z = Mathf.Lerp(angularVelocity.z, 0f, airDamping * dt);
        angularVelocity.y = Mathf.Lerp(angularVelocity.y, spinY, airDamping * dt);

        float speed = angularVelocity.magnitude;
        if (speed > 0.001f)
        {
            rotation = Quaternion.AngleAxis(speed * dt, angularVelocity / speed) * rotation;
            rotation.Normalize();
        }

        for (int i = 0; i < bodies.Length; i++)
            bodies[i].localRotation = rotation * baseRotations[i];
    }

    private void UpdateBounce(float dt)
    {
        vz -= gravity * dt;
        z += vz * dt;
        airTimer += dt;

        bool impact = false;
        if (z <= 0f && vz < 0f)
        {
            z = 0f;
            vz = launchSpeed;
            impact = true;
        }

        GetContact(out float lowestDepth, out Vector3 contactOffset);

        if (impact)
        {
            Vector3 torque = Vector3.Cross(contactOffset, Vector3.up);
            angularVelocity += torque * impactSpin;
            angularVelocity += new Vector3(
                Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * randomSpin;

            squashTimer = 0f;
            airTimer = 0f;
            OnImpact();
        }
        squashTimer += dt;

        float squash = squashAmount
                       * Mathf.Exp(-squashDamping * squashTimer)
                       * Mathf.Cos(squashTimer * squashFrequency * 2f * Mathf.PI);

        float scaleY = 1f - squash;
        float scaleXZ = 1f + squash * 0.5f;

        transform.localScale = new Vector3(
            startLocalScale.x * scaleXZ,
            startLocalScale.y * scaleY,
            startLocalScale.z * scaleXZ);

        float y = groundY + z + lowestDepth * scaleY;
        transform.localPosition = new Vector3(startLocalPosition.x, y, startLocalPosition.z);
    }

    // Trasformazione in volo: stiramento + smussatura degli angoli.
    // A morphEnd entrambi sono esattamente 0: il cubo è di nuovo un cubo perfetto prima dell'impatto.
    private void UpdateMorph()
    {
        float tNorm = airTime > 0f ? airTimer / airTime : 0f;

        for (int i = 0; i < bodies.Length; i++)
        {
            float start = Mathf.Min(i * morphLayerDelay, morphEnd * 0.5f);
            float t = Mathf.InverseLerp(start, morphEnd, tNorm);

            float e = (t <= 0f || t >= 1f) ? 0f : Mathf.Sin(t * Mathf.PI);
            e = e * e * (3f - 2f * e);

            Vector3 s = Vector3.Lerp(Vector3.one, morphTargets[i], e);
            bodies[i].localScale = Vector3.Scale(baseScales[i], s);

            SetRoundness(i, e * roundAmount);
        }
    }

    private void RollMorphTargets()
    {
        for (int i = 0; i < morphTargets.Length; i++)
        {
            float a = Mathf.Exp(Random.Range(-morphStrength, morphStrength));
            float b = Mathf.Exp(Random.Range(-morphStrength, morphStrength));
            float c = Mathf.Exp(Random.Range(-morphStrength, morphStrength));

            float k = Mathf.Pow(a * b * c, 1f / 3f);
            morphTargets[i] = new Vector3(a / k, b / k, c / k);
        }
    }

    private void OnImpact()
    {
        pulse = 1f;
        targetHue += Random.Range(hueShiftMin, hueShiftMax);
        RollMorphTargets();
        SpawnImpactFx();
    }

    private void SpawnImpactFx()
    {
        Vector3 local = new Vector3(startLocalPosition.x, groundY + groundOffset, startLocalPosition.z);
        Vector3 pos = transform.parent != null ? transform.parent.TransformPoint(local) : local;
        pos.y += 0.02f;

        Color c = currentColor;
        var sheet = fx.textureSheetAnimation;

        sheet.startFrame = new ParticleSystem.MinMaxCurve(0.25f);
        var ring = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = Vector3.zero,
            startSize = ringSize,
            startLifetime = ringLifetime,
            startColor = c
        };
        fx.Emit(ring, 1);

        sheet.startFrame = new ParticleSystem.MinMaxCurve(0.75f);
        for (int i = 0; i < sparkCount; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

            var spark = new ParticleSystem.EmitParams
            {
                position = pos + dir * 0.3f,
                velocity = dir * Random.Range(sparkSpeed * 0.5f, sparkSpeed) + Vector3.up * 0.3f,
                startSize = Random.Range(0.12f, 0.3f),
                startLifetime = Random.Range(0.4f, 0.7f),
                startColor = c
            };
            fx.Emit(spark, 1);
        }
    }

    private void UpdateColor(float dt)
    {
        float k = 1f - Mathf.Exp(-colorSmoothing * dt);
        currentHue = Mathf.Lerp(currentHue, targetHue, k);

        pulse = Mathf.Max(0f, pulse - pulse * pulseDecay * dt);
        if (pulse < 0.001f) pulse = 0f;

        ApplyColor();
    }

    private void ApplyColor()
    {
        Color c = Color.HSVToRGB(Mathf.Repeat(currentHue, 1f), saturation, brightness);
        currentColor = c;

        pulseLight.color = c;
        pulseLight.intensity = lightBaseIntensity + lightPulseIntensity * pulse;

        Color emission = c * (emissionBase + emissionPulse * pulse);
        for (int i = 0; i < materials.Length; i++)
        {
            for (int m = 0; m < materials[i].Length; m++)
            {
                Material mat = materials[i][m];
                int colorProp = GetColorProp(mat);
                if (colorProp != -1)
                {
                    Color tinted = c;
                    tinted.a = originalAlpha[i][m];
                    mat.SetColor(colorProp, tinted);
                }
                if (mat.HasProperty(EmissionId)) mat.SetColor(EmissionId, emission);
            }
        }
    }

    private void GetContact(out float lowestDepth, out Vector3 contactOffset)
    {
        float minY = float.MaxValue;
        Vector3[] verts = new Vector3[8];
        int i = 0;

        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 v = rotation * (new Vector3(sx, sy, sz) * halfSize);
                    verts[i++] = v;
                    if (v.y < minY) minY = v.y;
                }

        float tolerance = halfSize * 0.05f;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var v in verts)
        {
            if (v.y <= minY + tolerance)
            {
                sum += v;
                count++;
            }
        }

        lowestDepth = -minY;
        contactOffset = sum / count;
    }
}