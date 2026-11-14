Shader "Whispers/VHSOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                // Bandas e grão animados, mais visíveis sem capturar a tela.
                float scan = 0.5 + 0.5 * sin(i.uv.y * _ScreenParams.y * 1.5707963);
                float noise = hash(floor(i.uv * float2(460, 260)) + floor(_Time.y * 12.0));
                float trackingBand = exp(-abs(frac(i.uv.y - _Time.y * 0.035) - 0.5) * 110.0);
                float vignette = saturate(length((i.uv - 0.5) * float2(1.25, 1.05)) * 1.5 - 0.55);
                float alpha = 0.025 + scan * 0.095 + (1.0 - noise) * 0.045 + trackingBand * 0.07 + vignette * 0.08;
                return fixed4(0.025, 0.03, 0.045, saturate(alpha));
            }
            ENDCG
        }
    }
}
