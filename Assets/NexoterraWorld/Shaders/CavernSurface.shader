Shader "Nexoterra/Cavern Surface"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BaseMap("Base map", 2D) = "white" {}
        _Cutoff("Cutoff", Float) = 0.5
        _Strata("Rock strata", Range(0,1)) = 0.3
        _Emission("Luminescence", Range(0,4)) = 0
        _Wind("Foliage motion", Range(0,1)) = 0
        _Detail("Organic detail", Range(0,1)) = 0.45
        _Spots("Mushroom spots", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "CavernForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Cutoff, _Strata, _Emission, _Wind, _Detail, _Spots;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.p.xyz);
                world.xz += float2(sin(_Time.y*1.2+world.x*.6+world.z*.3),cos(_Time.y+world.z*.4)) * _Wind * v.uv.y * .13;
                o.world=world; o.p=TransformWorldToHClip(world); o.normal=TransformObjectToWorldNormal(v.n);
                o.color=v.c*_BaseColor; o.fog=ComputeFogFactor(o.p.z); return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.normal);
                float grain = sin(i.world.x*.71+sin(i.world.z*.53))*sin(i.world.z*.67+i.world.y*.8);
                float frequencyFade=1-saturate(length(fwidth(i.world))*2);
                float strata = sin(i.world.y*4.2+sin(i.world.x*.18)*1.1+sin(i.world.z*.22));
                half3 baseColor=i.color.rgb*(.94+grain*.055+strata*_Strata*.11*frequencyFade);
                float patches=Noise(i.world.xz*.65)*.6+Noise(i.world.xz*3.8)*.4;
                baseColor*=lerp(1,.69+patches*.65,_Detail);
                float grains=step(.87,Noise(i.world.xz*14))*frequencyFade;
                baseColor+=i.color.rgb*grains*_Detail*.18;
                float2 cells=i.world.xz*2.7;
                float spots=(1-smoothstep(.11,.22,length(frac(cells)-.5)))*step(.35,Hash(floor(cells)))*saturate(n.y*2)*_Spots;
                baseColor=lerp(baseColor,half3(.42,.92,1),spots*.8);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half nl=saturate(dot(n,light.direction)*.65+.35);
                half3 ambient=half3(.34,.42,.43)+saturate(n.y)*half3(.10,.10,.06);
                half3 color=baseColor*(ambient+light.color*nl*lerp(.58,1,light.shadowAttenuation)*.62);
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.world;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light extra=GetAdditionalLight(lightIndex,i.world);
                    color+=baseColor*extra.color*saturate(dot(n,extra.direction)) * extra.distanceAttenuation;
                LIGHT_LOOP_END
                #endif
                color+=baseColor*_Emission+half3(.12,.35,.42)*spots;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
