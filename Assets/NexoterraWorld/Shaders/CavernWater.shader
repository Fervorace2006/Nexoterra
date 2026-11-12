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
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor;
                float _FlowSpeed;
            CBUFFER_END
            struct A {float4 p:POSITION; float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half fog:TEXCOORD2;};
            V Vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.p.xyz);
                float fade=1;
                o.world.y+=(sin(o.world.x*.7+_Time.y*_FlowSpeed)+cos(o.world.z*.6+_Time.y*_FlowSpeed))*.025*fade;
                o.p=TransformWorldToHClip(o.world);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float t=_Time.y*_FlowSpeed;
                float a=sin(i.world.x*.65+i.world.z*.45+t+sin(i.world.z*.25-t));
                float b=sin(i.world.z*.8-i.world.x*.28-t*.8+sin(i.world.x*.4));
                float caustic=pow(saturate(1-abs(a+b)*2.8),10)*.16;

                float2 screenUV=GetNormalizedScreenSpaceUV(i.p);
                float floorDepth=LinearEyeDepth(SampleSceneDepth(screenUV),_ZBufferParams);
                float surfaceDepth=-TransformWorldToView(i.world).z;
                float thickness=max(0,floorDepth-surfaceDepth);
                float shore=1-smoothstep(.1,3,thickness);
                float foam=(1-smoothstep(.03,.28,thickness))*smoothstep(0,.025,thickness);
                float ripples=pow(saturate(sin(i.world.x*.34+i.world.z*.8+t*.4)),24)*.06;
                half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,.35+shore*.65);
                color+=_FoamColor.rgb*(caustic+ripples);
                color=lerp(color,_FoamColor.rgb,foam*.75);
                float3 n=normalize(float3(-.045*cos(i.world.x*.7+t),1,.04*sin(i.world.z*.6+t)));
                float3 v=GetWorldSpaceNormalizeViewDir(i.world);
                float fresnel=pow(1-abs(dot(n,v)),4);
                Light sun=GetMainLight();
                color+=sun.color*pow(saturate(dot(n,normalize(v+sun.direction))),80)*.3;

                // Submerged rock softens into teal as the water column deepens.
                float transmission=exp(-thickness*.36);
                color=lerp(color*.65+_DeepColor.rgb*.35,color,transmission);
                float contact=(1-smoothstep(.04,.55,thickness))*smoothstep(.0,.035,thickness);
                float crest=.5+.5*sin(dot(i.world.xz,float2(2.4,1.8))+t+sin(i.world.z*1.3-t));
                color=lerp(color,_FoamColor.rgb,contact*smoothstep(.30,.85,crest)*.52);
                half3 reflected=SampleSH(reflect(-v,n));
                color=lerp(color,reflected,.48*fresnel);
                float opacity=lerp(.16,.52,1-exp(-thickness*.18));
                opacity=lerp(opacity,.68,fresnel);
                return half4(MixFog(color,i.fog),saturate(opacity+foam*.12));
            }
            ENDHLSL
        }
    }
}
