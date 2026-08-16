Shader "MicroJam/Soft Sprite Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (0,0,0,0.3)
        _BlurSize ("Blur Size", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * _BlurSize;
                fixed a = tex2D(_MainTex, input.uv).a * 0.2;
                a += tex2D(_MainTex, input.uv + float2(o.x, 0)).a * 0.1;
                a += tex2D(_MainTex, input.uv - float2(o.x, 0)).a * 0.1;
                a += tex2D(_MainTex, input.uv + float2(0, o.y)).a * 0.1;
                a += tex2D(_MainTex, input.uv - float2(0, o.y)).a * 0.1;
                a += tex2D(_MainTex, input.uv + o).a * 0.1;
                a += tex2D(_MainTex, input.uv - o).a * 0.1;
                a += tex2D(_MainTex, input.uv + float2(o.x, -o.y)).a * 0.1;
                a += tex2D(_MainTex, input.uv + float2(-o.x, o.y)).a * 0.1;
                return fixed4(0, 0, 0, a * input.color.a);
            }
            ENDCG
        }
    }
}
