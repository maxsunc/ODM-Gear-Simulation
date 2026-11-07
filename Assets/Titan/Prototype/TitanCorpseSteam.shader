Shader "ODM/Titan/CorpseSteam"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 frag(v2f i):SV_Target
            {
                float2 q=(i.uv-.5)*2;
                float cloud=.65+.35*noise(i.uv*5)+.12*noise(i.uv*11);
                float soft=1-smoothstep(.04,1,dot(q,q));
                return half4(i.color.rgb,i.color.a*soft*cloud);
            }
            ENDHLSL
        }
    }
}
