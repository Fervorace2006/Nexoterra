// Procedural cave rock for flat level walls and ceilings.
// Keeps the material's base color and adds flowstone curtains, vertical drip streaks,
// layered ledges and small bumps through world-space triplanar-style mapping (no UVs needed),
// lit by URP (shadows, fog, additional lights).
Shader "Nexoterra/Cavern Rock"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (.78,.59,.4,1)
        _Scale("Pattern Scale", Float) = 1
        _Relief("Relief", Range(0,3)) = 1.2
        _Contrast("Contrast", Range(0,1.5)) = .8
        _Smoothness("Smoothness", Range(0,1)) = .18
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Scale, _Relief, _Contrast, _Smoothness;
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
    // Height field in meters-ish (0..1). wall: u = along the wall, v = world height.
    float RockHeight(float2 p, float wall)
    {
        p *= _Scale;
        if (wall > .5)
        {
            float warp = Fbm(p * float2(.12, .035)) * 7;
            float curtains = pow(1 - abs(sin(p.x * 1.35 + warp)), 2.4);          // draped flowstone folds
            float streaks = Fbm(float2(p.x * 2.1, p.y * .11));                    // long vertical drips
            float ledges = smoothstep(.55, .9, Fbm(float2(p.x * .22, p.y * .85))); // layered shelves
            float bumps = Fbm(p * 4.5);                                            // rough porous rock
            float drips = Fbm(float2(p.x * 7.5 + warp, p.y * .45));                 // fine dripping texture
            float knobs = pow(Fbm(p * float2(1.4, .7)), 2.5);                        // rounded lumps of calcite
            return saturate(curtains * .28 + streaks * .2 + drips * .22 + ledges * .2 + bumps * .2 + knobs * .4);
        }
        float blobs = Fbm(p * .55);
        float tips = pow(Fbm(p * 2.4), 2.2);                                       // stalactite clusters
        return saturate(blobs * .6 + tips * .55);
    }
    // Walls use (along-wall, height) coordinates, ceilings use xz; curved corner pieces blend both
    // by how much the surface faces up/down, so there is no seam on the rounded caves.
    float3 RockNormal(float3 p, float3 n, out float h)
    {
        const float e = .05;
        float w = smoothstep(.45, .8, abs(n.y));
        float3 nw = n, nc = n; float hw = 0, hc = 0;
        if (w < .999)
        {
            float3 flat = float3(-n.z, 0, n.x); float len = length(flat);
            float3 t = len > 1e-3 ? flat / len : float3(1,0,0);
            float2 uv = float2(dot(p, t), p.y);
            hw = RockHeight(uv, 1);
            float du = (RockHeight(uv + float2(e,0), 1) - hw) / e, dv = (RockHeight(uv + float2(0,e), 1) - hw) / e;
            nw = n - _Relief * .35 * (du * t + dv * float3(0,1,0));
        }
        if (w > .001)
        {
            float2 uv = p.xz;
            hc = RockHeight(uv, 0);
            float du = (RockHeight(uv + float2(e,0), 0) - hc) / e, dv = (RockHeight(uv + float2(0,e), 0) - hc) / e;
            nc = n - _Relief * .35 * (du * float3(1,0,0) + dv * float3(0,0,1));
        }
        h = lerp(hw, hc, w);
        return normalize(lerp(nw, nc, w));
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
                float h;
                float3 n = RockNormal(i.positionWS, n0, h);
                float tint = Fbm((i.positionWS.xz + i.positionWS.y * .7) * .07 * _Scale);

                float shade = lerp(1, .55 + .75 * h, _Contrast);
                float3 albedo = _BaseColor.rgb * shade;
                albedo = lerp(albedo, albedo * float3(1.08, 1.0, .86), tint * .6);   // mineral variation, same hue family

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
                surface.smoothness = _Smoothness * (.6 + .4 * h);
                surface.occlusion = lerp(.4, 1, h);
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
                float h; float3 n = RockNormal(i.positionWS, normalize(i.normalWS), h);
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
