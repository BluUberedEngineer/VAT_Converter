Shader "Unlit/VATReader2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD1;
                uint id : SV_VertexID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float3 pos : TEXCOORD4;
                uint id : TEXCOORD3;
                float3 color : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            int _FrameWidth;
            int _AmountFramesSqrt;
            int _TextureWidth;
            int _CurrentFrame;

            v2f vert (appdata v)
            {
                float2 vertCoords = float2(v.id % (uint)_FrameWidth, v.id / (uint)_FrameWidth);
                float2 startPos = float2(_CurrentFrame % _AmountFramesSqrt, _CurrentFrame / _AmountFramesSqrt) * _FrameWidth;
                vertCoords += startPos;
                vertCoords += float2(0.5, 0.5);
                vertCoords /= _TextureWidth;
                float4 texCoords = float4(vertCoords, 0, 0);
                float3 position = tex2Dlod(_MainTex, texCoords);
              
                v2f o;
                o.vertex = UnityObjectToClipPos(position);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.id = v.id;
                o.pos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = v.vertex;
                o.color = tex2Dlod(_MainTex, texCoords);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(i.color, 1);
                return float4(i.pos, 1);
                // if(i.id < 7794 / 2)
                // {
                //     return fixed4(1, 0, 0, 1);
                // }
                // else
                // {
                //     return fixed4(0, 1, 0, 1);
                // }
                float vertCoords = i.uv.x / 7794;
                float animCoords = 0;
                float2 texCoords = float2(vertCoords, animCoords);
                float3 position = tex2D(_MainTex, texCoords);
                return fixed4(i.uv.x, 0, 0, 1);
            }
            ENDCG
        }
    }
}
