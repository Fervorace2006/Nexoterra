Shader "Nexoterra/Slug Glow"
{
 Properties { _Ribbon("Ribbon",Float)=0 _DstBlend("Destination blend",Float)=1 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Blend SrcAlpha [_DstBlend]
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Ribbon;
   CBUFFER_END
   struct A {float4 position:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   V Vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 Frag(V i):SV_Target
   {
    float radius=lerp(length(i.uv*2-1),abs(i.uv.y*2-1),_Ribbon);
    float softness=pow(saturate(1-radius),1.6);
    return half4(i.color.rgb*1.7,i.color.a*softness);
   }
   ENDHLSL
  }
 }
}
