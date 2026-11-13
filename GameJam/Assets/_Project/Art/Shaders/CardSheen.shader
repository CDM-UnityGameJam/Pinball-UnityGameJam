// Foil sheen over a card face: a soft diagonal light band, masked by the card sprite's alpha, that slides
// across the card as it tilts (UV1.xy, from CardTilt) and drifts slowly on its own. A faint rainbow tint
// rides along the band. Strength per card comes from UV1.w (CardMotion.Sheen). Additive.
Shader "Pinball/UI/CardSheen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Face", 2D) = "white" {}
        _Width ("Band Width", Float) = 0.16
        _Strength ("Strength", Float) = 0.55
        _TiltShift ("Band Shift Per Tilt", Float) = 0.9
        _Drift ("Idle Drift Speed", Float) = 0.12
        _Rainbow ("Rainbow Tint", Range(0, 1)) = 0.45

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 drive : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 drive : TEXCOORD1;
                float4 world : TEXCOORD2;
            };

            sampler2D _MainTex;
            float _Width, _Strength, _TiltShift, _Drift, _Rainbow;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.drive = v.drive;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a;
                // Position along the diagonal; the band moves with the tilt and drifts slowly.
                float along = dot(i.uv - 0.5, normalize(float2(1.0, 0.7)));
                float centre = (i.drive.y - i.drive.x) * _TiltShift + sin(_Time.y * _Drift * 6.2831853) * 0.35;
                float band = saturate(1.0 - abs(along - centre) / _Width);
                band = band * band * (3.0 - 2.0 * band);
                float3 rainbow = 0.5 + 0.5 * cos(6.2831853 * (along * 1.5 + float3(0.0, 0.33, 0.67)));
                float3 rgb = lerp(1.0, rainbow, _Rainbow) * i.color.rgb;
                fixed4 c = fixed4(rgb, band * mask * _Strength * i.drive.w * i.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
