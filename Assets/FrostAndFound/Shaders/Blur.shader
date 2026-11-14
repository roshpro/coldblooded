// Graphics.Blit motion smear for photos of a dodging Tony ("any photo comes out as a useless blur").
Shader "FrostAndFound/Blur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Direction ("Direction", Vector) = (1,0.2,0,0)
        _Amount ("Amount", Float) = 0.06
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Direction;
            float _Amount;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 dir = normalize(_Direction.xy) * _Amount;
                float jitter = hash(i.uv * 300.0) - 0.5;
                fixed4 acc = 0;
                const int TAPS = 14;
                for (int k = 0; k < TAPS; k++)
                {
                    float t = (k + jitter) / (TAPS - 1.0) - 0.5;
                    float2 wobble = float2(0, sin(t * 9.0) * _Amount * 0.15);
                    acc += tex2D(_MainTex, i.uv + dir * t + wobble);
                }
                acc /= TAPS;
                // washed out, slightly over-exposed — a classic ruined instant photo
                acc.rgb = lerp(acc.rgb, dot(acc.rgb, float3(0.3, 0.59, 0.11)).xxx, 0.35) * 1.08 + 0.04;
                acc.a = 1;
                return acc;
            }
            ENDCG
        }
    }
}
