// Corrección de distorsión de lente para visores tipo Cardboard.
Shader "Hidden/VRBasketball/BarrelDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _K1 ("Distorsion radial", Float) = 0.10
        _K2 ("Distorsion radial 2", Float) = 0.02
        _StereoSplit ("Separar en dos ojos", Float) = 1
        _Aspect ("Aspecto", Float) = 1.7
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _K1;
            float _K2;
            float _StereoSplit;
            float _Aspect;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 Distort(float2 uv, float aspect)
            {
                float2 c = (uv - 0.5) * 2.0;
                c.x *= aspect;
                float r2 = dot(c, c);
                c *= 1.0 + _K1 * r2 + _K2 * r2 * r2;
                c.x /= aspect;
                return c * 0.5 + 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                if (_StereoSplit > 0.5)
                {
                    float eye = (uv.x < 0.5) ? 0.0 : 0.5;
                    float2 local = float2((uv.x - eye) * 2.0, uv.y);
                    float halfAspect = (_ScreenParams.x * 0.5) / max(_ScreenParams.y, 1.0);
                    local = Distort(local, halfAspect);
                    uv = float2(local.x * 0.5 + eye, local.y);
                }
                else
                {
                    uv = Distort(uv, _Aspect);
                }

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return fixed4(0.0, 0.0, 0.0, 1.0);
                }

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }

    Fallback Off
}
