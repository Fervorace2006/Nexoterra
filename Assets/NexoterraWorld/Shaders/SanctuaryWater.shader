Shader "Nexoterra/Sanctuary Water"
{
 Properties {
 _ShallowColor("Shore",Color)=(.08,.56,.60,1)
 _DeepColor("Depth",Color)=(.012,.13,.22,1)
 _FoamColor("Foam",Color)=(.45,.83,.84,1)
 _Center("Center and radii",Vector)=(133,-18,19,15)
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _ShallowColor,_DeepColor,_FoamColor,_Center;
 CBUFFER_END
 struct A {float4 p:POSITION;};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;half fog:TEXCOORD1;};
 V Vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 Frag(V i):SV_Target{
 float2 p=i.world.xz;float t=_Time.y*.6;
 float radial=length((p-_Center.xy)/_Center.zw);
 float shore=smoothstep(.68,1,radial);
 float wave=sin(p.x*1.7+p.y*.9+t)*.5+sin(p.y*2.5-p.x*.7-t*.8)*.25;
 float3 n=normalize(float3(-.08*cos(p.x*1.7+p.y*.9+t),1,-.065*cos(p.y*2.5-p.x*.7-t*.8)));
 float3 v=GetWorldSpaceNormalizeViewDir(i.world);float fresnel=pow(1-saturate(dot(n,v)),4);
 Light light=GetMainLight();float spec=pow(saturate(dot(n,normalize(v+light.direction))),100)*.24;
 float foam=smoothstep(.975,1,radial+wave*.006)*(.5+.25*sin(p.x*8+p.y*6+t));
 float ripple=pow(saturate(wave*.7+.3),8)*.045;
 half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,shore*.8+.15);
 color=lerp(color,half3(.19,.40,.48),fresnel*.45);
 color+=_FoamColor.rgb*(foam*.45+ripple)+light.color*spec;
 return half4(MixFog(color,i.fog),1);
 }
 ENDHLSL
 }
 }
}
