Shader "Nexoterra/Cavern Water"
{
    Properties
    {
        _ShallowColor("Shallows", Color) = (.12,.7,.8,1)
        _DeepColor("Depth", Color) = (.025,.19,.28,1)
        _FoamColor("Foam", Color) = (.6,.95,.9,1)
        _FlowSpeed("Flow", Float) = .35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor;
                float _FlowSpeed;
            CBUFFER_END
            struct A {float4 p:POSITION; float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half fog:TEXCOORD2;};
            V Vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.p.xyz);
                float fade=saturate((1-i.uv.x)*8);
                o.world.y+=(sin(o.world.x*.7+_Time.y*_FlowSpeed)+cos(o.world.z*.6+_Time.y*_FlowSpeed))*.025*fade;
                o.p=TransformWorldToHClip(o.world);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float t=_Time.y*_FlowSpeed;
                float a=sin(i.world.x*.65+i.world.z*.45+t+sin(i.world.z*.25-t));
                float b=sin(i.world.z*.8-i.world.x*.28-t*.8+sin(i.world.x*.4));
                float caustic=pow(saturate(1-abs(a+b)*2.8),10)*.16;
                float shore=smoothstep(.62,1,i.uv.x);
                float foam=smoothstep(.955,.985,i.uv.x+sin(i.uv.y*48+t)*.008);
                float ripples=pow(saturate(sin(i.world.x*.34+i.world.z*.8+t*.4)),24)*.06;
                half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,.35+shore*.65);
                color+=_FoamColor.rgb*(caustic+ripples);
                color=lerp(color,_FoamColor.rgb,foam*.75);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
