Shader "ODM/Giant Forest/Vertex Painted Lit"
{
 Properties
 {
  _BaseMap("Albedo / leaf alpha",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal]_BumpMap("Normal",2D)="bump"{}
  _RoughnessMap("Roughness",2D)="white"{}
  _NormalStrength("Normal strength",Range(0,2))=.65
  _AlphaClip("Leaf alpha test",Float)=0
  _Cutoff("Alpha cutoff",Range(0,1))=.35
  [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
  TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
  TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
  TEXTURE2D(_RoughnessMap);SAMPLER(sampler_RoughnessMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;half _NormalStrength,_AlphaClip,_Cutoff,_Cull;
  CBUFFER_END
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
  void LeafClip(float2 uv){half a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a*_BaseColor.a;clip(lerp(1,a-_Cutoff,_AlphaClip));}
  ENDHLSL
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Cull[_Cull]
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half4 color:COLOR;half4 fogLight:TEXCOORD4;float4 shadow:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes v)
   {
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
    o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
    o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color;o.shadow=GetShadowCoord(p);o.fogLight=half4(ComputeFogFactor(p.positionCS.z),VertexLighting(p.positionWS,n.normalWS));return o;
   }
   half4 Frag(Varyings i,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);LeafClip(i.uv);
    half3 baseNormal=normalize(i.normalWS);half3 tangent=normalize(i.tangentWS.xyz);half3 bitangent=cross(baseNormal,tangent)*i.tangentWS.w;
    half3 nm=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_NormalStrength);
    half3 normal=normalize(mul(nm,half3x3(tangent,bitangent,baseNormal)));normal*=IS_FRONT_VFACE(facing,1,-1);
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=i.shadow;input.fogCoord=i.fogLight.x;input.vertexLighting=i.fogLight.yzw;input.bakedGI=SampleSH(normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*i.color.rgb;surface.alpha=1;surface.specular=half3(.04,.04,.04);surface.smoothness=1-SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,i.uv).r;surface.occlusion=1;
    half4 result=UniversalFragmentPBR(input,surface);result.rgb=MixFog(result.rgb,input.fogCoord);return result;
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull[_Cull]
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection;float3 _LightPosition;
   struct ShadowOut {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   ShadowOut ShadowVert(Attributes v)
   {
    ShadowOut o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);float3 pos=TransformObjectToWorld(v.positionOS.xyz);float3 normal=TransformObjectToWorldNormal(v.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
     float3 direction=normalize(_LightPosition-pos);
    #else
     float3 direction=_LightDirection;
    #endif
    o.positionCS=TransformWorldToHClip(ApplyShadowBias(pos,normal,direction));
    #if UNITY_REVERSED_Z
     o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #else
     o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;
   }
   half4 ShadowFrag(ShadowOut i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);LeafClip(i.uv);return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask 0 Cull[_Cull]
   HLSLPROGRAM
   #pragma vertex DepthVert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   struct DepthOut{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   DepthOut DepthVert(Attributes v){DepthOut o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
   half4 DepthFrag(DepthOut i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);LeafClip(i.uv);return 0;}
   ENDHLSL
  }
 }
 FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
