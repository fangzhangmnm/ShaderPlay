Shader "Hidden/FullscreenVolumeTemplate"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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
                float3 viewVector : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }


            float3 BoundsMin, BoundsMax;

            float2 rayBoxDist(float3 minBound, float3 maxBound, float3 rayOrigin, float3 rayDir){
                float3 t0=(minBound-rayOrigin)/rayDir;
                float3 t1=(maxBound-rayOrigin)/rayDir;
                float3 tmin=min(t0,t1);
                float3 tmax=max(t0,t1);
                float dstA=max(max(tmin.x,tmin.y),tmin.z);
                float dstB=min(min(tmax.x,tmax.y),tmax.z);
                float dstToBox=max(0,dstA);
                float dstInsideBox=max(0,dstB-dstToBox);
                return float2(dstToBox,dstInsideBox);
            }

            sampler2D _MainTex,_CameraDepthTexture;

            fixed4 frag (v2f input) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, input.uv);
                float nonLinearDepth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,input.uv);
                float depth=LinearEyeDepth(nonLinearDepth)*length(input.viewVector);
                float3 rayOrigin=_WorldSpaceCameraPos;
                float3 rayDir=normalize(input.viewVector);
                float2 rayBox=rayBoxDist(BoundsMin,BoundsMax,rayOrigin,rayDir);
                if(rayBox.y>0 && rayBox.x<depth){
                    col=0;
                }
                return col;
            }
            ENDCG
        }
    }
}
