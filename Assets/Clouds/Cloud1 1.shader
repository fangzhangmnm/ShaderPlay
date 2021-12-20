Shader "Hidden/Cloud1"
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

            float3 CloudScale, CloudOffset;
            float DetailStrength,DetailScale;
            float3 DetailOffset;
            float DensityOffset;
            float CloudSize;
            float3 absorption,inscattering;
            float3 mieCoeff;
            float powderCoeff;
            float3 lightColor;
            float3 ambientColorUpper;
            float3 ambientColorLower;

            sampler2D _MainTex,_CameraDepthTexture;

            float stepSize,lightStepSize,stepSizeDistCoeff,stepSizeDensityCoeff,minStepSizeCoeff;
            int maxInScatteringPoints;
            int minInScatteringPoints;
            int numStepsLight;

            Texture3D<float4> NoiseTexture,DetailNoiseTexture;
            SamplerState samplerNoiseTexture,samplerDetailNoiseTexture;


            float2 rayBox(float3 minBound, float3 maxBound, float3 rayOrigin, float3 rayDir){
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

            float getDensity(float3 position){
                float3 uvw=(position-CloudOffset).xzy/CloudScale;
                float2 noise=NoiseTexture.SampleLevel(samplerNoiseTexture,uvw,0)-.5;
                float height01=(position.y-BoundsMin.y)/(BoundsMax.y-BoundsMin.y);
                float heightCoeff=4*height01*(1-height01)-.75+(height01-.5);
                return lerp(noise.r,noise.g,CloudSize)+DensityOffset+heightCoeff;
            }
            float getDensityDetail(float3 position){
                float3 uvw=(position-DetailOffset).xzy/CloudScale*DetailScale;
                return -DetailStrength*(NoiseTexture.SampleLevel(samplerNoiseTexture,uvw,0).r);
            }

            float3 getAmbient(float3 position){
                float height01=(position.y-BoundsMin.y)/(BoundsMax.y-BoundsMin.y);
                return lerp(ambientColorLower,ambientColorUpper,height01);
            }

            float getMiePhase(float cosTh){
                return mieCoeff.x*pow(mieCoeff.y-mieCoeff.z*cosTh,-1.5);
            }
            /*
            float2 getDensity2(float3 position){
                float3 uvw=(position-CloudOffset).xzy/CloudScale;
                float4 noise=NoiseTexture.SampleLevel(samplerNoiseTexture,uvw,0)-.5;
                float height01=(position.y-BoundsMin.y)/(BoundsMax.y-BoundsMin.y);
                float heightCoeff=4*height01*(1-height01);
                float value=noise.r+DensityOffset+heightCoeff-.75;
                if(value>0){
                    float4 noise2=NoiseTexture.SampleLevel(samplerNoiseTexture,uvw*DetailScale,0);
                    value-=(1-noise2.r)*DetailStrength;
                }
                return float2(max(0,value)*DensityMultiplier,value);
            }*/

            float3 lightmarch(float3 position, int steps){
                float3 dirToLight=_WorldSpaceLightPos0.xyz;
                float dstInsideBox=rayBox(BoundsMin,BoundsMax,position,dirToLight).y;
                float stepSize=min(dstInsideBox/steps,lightStepSize);
                float totalDensity=0;
                totalDensity+=saturate(getDensity(position));
                for(int i=0;i<steps;++i){
                    position+=dirToLight*stepSize;
                    totalDensity+=saturate(getDensity(position));
                }
                float3 opticalPath=totalDensity*stepSize*absorption;
                return exp(-opticalPath)*(1-exp(-powderCoeff*opticalPath*opticalPath));
            }

            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                float dst=0;
                float totalDensity=0;
                float3 light=0;

                float3 dirToLight=_WorldSpaceLightPos0.xyz;
                float miePhase=getMiePhase(dot(rayDir,dirToLight));
                float3 transmittance=float3(1,1,1);

                for(int i=0;i<maxInScatteringPoints;++i){
                    if(dst>rayLength)break;
                    float3 pos=rayOrigin+rayDir*dst;
                    float density=getDensity(pos);
                    float step=min(stepSize*clamp(stepSizeDensityCoeff*abs(density),minStepSizeCoeff,1)*clamp(dst/stepSize*stepSizeDistCoeff,1,4),rayLength/minInScatteringPoints-.001f);
                    if(density>0)
                        density+=getDensityDetail(pos);
                    if(density>0){

                        totalDensity+=density*step;
                        transmittance=exp(-totalDensity*absorption);
                        light+=saturate(density)*step*inscattering*transmittance*(miePhase*lightmarch(pos,numStepsLight-1)*lightColor+getAmbient(pos));
                    }
                    dst+=step;
                }
                return color*transmittance+light;
            }


            fixed4 frag (v2f input) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, input.uv);
                float3 rayOrigin=_WorldSpaceCameraPos;
                float3 rayDir=normalize(input.viewVector);
                float2 rayBoxInfo=rayBox(BoundsMin,BoundsMax,rayOrigin,rayDir);
                float dstToBox=rayBoxInfo.x;
                float dstThroughBox=rayBoxInfo.y;
                
                float nonLinearDepth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,input.uv);
                bool hasDepth;
                if(UNITY_REVERSED_Z) hasDepth=nonLinearDepth>0;else hasDepth=nonLinearDepth<1;
                if(hasDepth){
                    float depth=LinearEyeDepth(nonLinearDepth)*length(input.viewVector);
                    dstThroughBox=min(depth-dstToBox,dstThroughBox);
                }

                if(dstThroughBox>0)
                    col.xyz=raymarch(col,rayOrigin+rayDir*dstToBox,rayDir,dstThroughBox);

                return col;
            }
            ENDCG
        }
    }
}
