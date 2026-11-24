// Procedural cave rock for flat level walls and ceilings.
// Keeps the material's base color and adds flowstone curtains, vertical drip streaks,
// layered ledges and small bumps through world-space triplanar-style mapping (no UVs needed),
// lit by URP (shadows, fog, additional lights).
Shader "Nexoterra/Cavern Ground"
{
    Properties
    {
        _BaseColor("Grass Color", Color) = (.29,.53,.12,1)
        _PathColor("Path Color", Color) = (.5,.33,.19,1)
        _Scale("Pattern Scale", Float) = 1
        _Paths("Path Amount", Range(0,1)) = .6
        _Flowers("Flowers", Range(0,1)) = .5
        _Relief("Relief", Range(0,3)) = 1
        _Smoothness("Smoothness", Range(0,1)) = .12
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor, _PathColor;
        float _Scale, _Paths, _Flowers, _Relief, _Smoothness;
    CBUFFER_END

    float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
    float Noise(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        float2 u = f * f * (3 - 2 * f);
        return lerp(lerp(Hash(i), Hash(i + float2(1,0)), u.x), lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x), u.y);
    }
    float Fbm(float2 p)
    {
        float v = 0, a = .5;
        [unroll] for (int i = 0; i < 4; i++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= .5; }
        return v;
    }
    float Hash2(float2 p){ return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
    // Winding path mask (1 on the path, 0 on grass) and pebble/flower speckles; p in meters.
    float PathMask(float2 p)
    {
        float2 q = p * .011 * _Scale; q += float2(Fbm(q * 2.3), Fbm(q * 2.3 + 7.1)) * .9;
        float line1 = abs(Fbm(q) - .5), line2 = abs(Fbm(q * 1.7 + 3.3) - .5);
        float m = 1 - smoothstep(.011, .026, min(line1, line2 * 1.3));
        return m * step(.0001, _Paths) * saturate(_Paths * 1.6);
    }
    float Speckle(float2 p, float density, float size)
    {
        float2 cell = floor(p), f = frac(p);
        float h = Hash2(cell);
        float2 c = float2(Hash2(cell + 3.1), Hash2(cell + 7.7)) * .6 + .2;
        return step(1 - density, h) * (1 - smoothstep(size * .6, size, length(f - c)));
    }
    float GroundHeight(float2 p)
    {
        float path = PathMask(p);
        float tufts = Fbm(p * 2.2) * .6 + Fbm(p * 7.0) * .4;
        return lerp(tufts * .45, .05 + Fbm(p * 3.5) * .15, path);
    }
    float3 GroundNormal(float3 pos, float3 n, out float h)
    {
        const float e = .06; float2 p = pos.xz;
        h = GroundHeight(p);
        float du = (GroundHeight(p + float2(e,0)) - h) / e, dv = (GroundHeight(p + float2(0,e)) - h) / e;
        return normalize(n - _Relief * .25 * (du * float3(1,0,0) + dv * float3(0,0,1)) * saturate(n.y));
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float fog:TEXCOORD2; };

            Varyings Vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n0 = normalize(i.normalWS);
                float h; float3 n = GroundNormal(i.positionWS, n0, h);
                float2 p = i.positionWS.xz;
                float3 grass = _BaseColor.rgb * lerp(.6, 1.3, Fbm(p * .06)) * lerp(.78, 1.15, Fbm(p * 2.4)) * lerp(.85, 1.08, Fbm(p * 9.0));
                grass = lerp(grass, grass * float3(.55, .7, .5), Speckle(p * 5.0, .35, .3) * .5);   // darker blades
                grass = lerp(grass, grass * float3(1.12, 1.08, .78), saturate(Fbm(p * .03 + 4) * 1.4 - .4));   // sun-dried patches
                float path = PathMask(p);
                float3 dirt = _PathColor.rgb * lerp(.8, 1.15, Fbm(p * 1.3));
                float edge = path * (1 - path) * 4;                                                          // darker worn border
                float3 albedo = lerp(grass, dirt, path) * (1 - edge * .25);
                float pebble = Speckle(p * 3.4, lerp(.03, .14, path), .16);
                albedo = lerp(albedo, albedo * .55 + float3(.18,.17,.15), pebble * .7);
                float flower = Speckle(p * 1.6 + 11, .06 * _Flowers, .14) * (1 - path);
                float3 petal = lerp(float3(1, .95, .7), float3(1, .55, .75), Hash2(floor(p * 1.6 + 11)));
                albedo = lerp(albedo, petal, flower);
                float occlusion = lerp(.75, 1, h) * (1 - edge * .15);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fog;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1,1,1,1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = _Smoothness;
                surface.occlusion = occlusion;
                surface.alpha = 1;
                surface.normalTS = float3(0,0,1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, i.fog);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            float4 Vert(Attributes i) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(i.positionOS.xyz), n = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 l = normalize(_LightPosition - p);
                #else
                    float3 l = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, l));
                #if UNITY_REVERSED_Z
                    c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(float4 positionOS:POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.positionCS = TransformWorldToHClip(o.positionWS); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float h; float3 n = GroundNormal(i.positionWS, normalize(i.normalWS), h);
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
