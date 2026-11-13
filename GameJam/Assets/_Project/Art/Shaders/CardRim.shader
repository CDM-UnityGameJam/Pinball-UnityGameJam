// Card outline for the hand: a crisp rim on the card's rounded edge, a soft outer glow, an inner fill
// (for the shadow variant), and bright sweeps that run around the perimeter with a little energy ripple.
// Colour comes from the vertex colour (Image.color); strength per card comes from UV1.z (CardTilt writes
// CardMotion.Glow there), so every card shares one material.
// The quad is the card plus _Pad on each side; _CardSize and _Radius are in canvas units.
Shader "Pinball/UI/CardRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CardSize ("Card Size (w, h)", Vector) = (118, 187, 0, 0)
        _Pad ("Padding around the card", Float) = 18
        _Radius ("Corner Radius", Float) = 8
        _LineWidth ("Rim Width", Float) = 2.5
        _LineStrength ("Rim Strength", Float) = 1
        _GlowWidth ("Glow Falloff", Float) = 7
        _GlowStrength ("Glow Strength", Float) = 0.8
        _FillStrength ("Inner Fill", Float) = 0
        _SweepStrength ("Sweep Strength", Float) = 1.2
        _SweepSpeed ("Sweep Speed", Float) = 0.35
        _Ripple ("Energy Ripple", Float) = 0.35
        _MinDrive ("Strength With No Drive", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1

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
        Blend [_SrcBlend] [_DstBlend]
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

            float4 _CardSize;
            float _Pad, _Radius, _LineWidth, _LineStrength, _GlowWidth, _GlowStrength, _FillStrength;
            float _SweepStrength, _SweepSpeed, _Ripple, _MinDrive;
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

            float RoundedBox(float2 p, float2 half, float r)
            {
                float2 q = abs(p) - half + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _CardSize.xy + 2.0 * _Pad;
                float2 p = (i.uv - 0.5) * size;
                float d = RoundedBox(p, _CardSize.xy * 0.5, _Radius);

                float strength = max(i.drive.z, _MinDrive);
                float rim = saturate(1.0 - abs(d) / _LineWidth);
                float glow = exp(-max(d, 0.0) / _GlowWidth) * step(-1.0, d);
                float fill = step(d, 0.0) * _FillStrength;

                // Bright sweeps chasing each other around the edge, plus a fine ripple along it.
                float angle = atan2(p.y * _CardSize.x / _CardSize.y, p.x) / 6.2831853 + 0.5;
                float t = _Time.y * _SweepSpeed;
                float a = frac(angle - t), b = frac(angle + 0.5 - t * 0.7);
                float sweep = pow(saturate(1.0 - abs(a - 0.5) * 2.0), 10.0) + 0.6 * pow(saturate(1.0 - abs(b - 0.5) * 2.0), 14.0);
                float ripple = 1.0 + _Ripple * sin(angle * 6.2831853 * 9.0 + _Time.y * 5.0);

                float alpha = (rim * _LineStrength * ripple + glow * _GlowStrength + fill) * strength;
                alpha += sweep * (rim + glow * 0.6) * _SweepStrength * strength;
                float3 rgb = lerp(i.color.rgb, 1.0, saturate(sweep * rim * 0.8));
                fixed4 c = fixed4(rgb, saturate(alpha) * i.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
