Shader "ODM/Titan/StompDebris"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) _Dust("Soft dust",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            float4 _Color;float _Dust;
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;
                float shade=.45+.55*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(.4,.8,.2))));
                o.color.rgb*=lerp(shade,1,saturate(_Dust));return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float soft=1-smoothstep(.05,1,length((i.uv-.5)*2));
                return half4(i.color.rgb,i.color.a*lerp(1,soft,saturate(_Dust)));
            }
            ENDHLSL
        }
    }
}
