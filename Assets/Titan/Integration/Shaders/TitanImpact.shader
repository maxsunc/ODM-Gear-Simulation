Shader "ODM/Titan/Impact"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) }
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
            struct appdata { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            float4 _Color;
            v2f vert(appdata v) { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o; }
            half4 frag(v2f i):SV_Target
            {
                half alpha=1-smoothstep(.25,1,length((i.uv-.5)*2));
                return half4(i.color.rgb,i.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
