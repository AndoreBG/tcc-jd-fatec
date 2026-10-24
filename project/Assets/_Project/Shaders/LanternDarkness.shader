Shader "Whispers/UI/LanternDarkness"
{
    // Camada de escurecimento da lanterna da Noite (controlada pelo LanternEffect).
    //
    // Image de tela cheia no CanvasUI (Screen Space - Overlay): cobre com cor/alpha
    // de escuridão todo o conteúdo abaixo dela, exceto o SPOT ILUMINADO — que é
    // recortado pelo MESMO sprite do halo da lanterna (_LanternTex). O shader mapeia
    // o retângulo exato que o halo ocupa na tela (mesmo centro = cursor, mesmo
    // diâmetro = raio efetivo × 2) sobre o espaço local do Canvas e amostra o alpha
    // da textura do sprite: onde o sprite é opaco a tela permanece iluminada; onde é
    // transparente, a escuridão chega por completo; as bordas seguem exatamente o
    // falloff desenhado na arte.
    //
    // Uniforms atualizados pelo LanternEffect:
    //   _LanternDarknessCenter = centro do halo (cursor, no espaço local do Canvas)
    //   _LanternDarknessSize   = diâmetro do retângulo do halo (raio efetivo × 2)
    //   _LanternLightIntensity = carga da bateria (0–1): modula a máscara de luz;
    //                            com 0 a máscara vira 1 e a cena escurece por completo
    //   _LanternTex / _LanternTexUV = textura do sprite do halo e sua sub-região UV
    //
    // Os vértices gerados pelo batcher de UI chegam ao shader no espaço local do
    // Canvas (worldPosition), o mesmo espaço de
    // RectTransformUtility.ScreenPointToLocalPointInRectangle e do sizeDelta/raio do halo.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _LanternTex ("Lantern Sprite Mask", 2D) = "white" {}
        _LanternTexUV ("Lantern Sprite UV (offset.xy, scale.zw)", Vector) = (0,0,1,1)
        _LanternDarknessCenter ("Lantern Center (Canvas)", Vector) = (960,540,0,0)
        _LanternDarknessSize ("Lit Diameter (Canvas)", Float) = 800
        _LanternLightIntensity ("Light Intensity", Range(0,1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _LanternTex;
            float4 _LanternTexUV; // offset.xy, scale.zw
            float4 _LanternDarknessCenter;
            float  _LanternDarknessSize;
            float  _LanternLightIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Mesmo retângulo do halo: centro = cursor, diâmetro = raio efetivo × 2.
                // haloUV (0..1) percorre o sprite da lanterna exatamente como o halo o desenha.
                float2 haloUV = (IN.worldPosition.xy - _LanternDarknessCenter.xy)
                              / max(_LanternDarknessSize, 0.0001) + 0.5;

                // Fora do retângulo do sprite não há luz (máscara total de escuridão).
                float2 insideEdge = step(0.0, haloUV) * step(haloUV, 1.0);
                float inside = insideEdge.x * insideEdge.y;

                float2 lightUV = _LanternTexUV.xy + saturate(haloUV) * _LanternTexUV.zw;
                float lightAlpha = tex2D(_LanternTex, lightUV).a * inside;

                // Luz = alpha do sprite × carga da bateria; escuridão é o complemento.
                float darknessMask = saturate(1.0 - lightAlpha * _LanternLightIntensity);

                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                color.a *= darknessMask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
