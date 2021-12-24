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
            
            //Planet Geometry
            float4x4 worldToPlanetTRS;
            float worldToPlanetS;
            float planetRadius;
            float cloudMinRadius;
            float cloudDeltaRadius;
            float atmosphereRadius;
            
            //Cloud Volume Textures
            Texture3D<float4> NoiseTexture,DetailNoiseTexture;
            SamplerState samplerNoiseTexture,samplerDetailNoiseTexture;

            //Lighting
            float3 lightColor;
            float3 ambientColorUpper,ambientColorLower;

            //Cloud Shape
            float cloudScale, detailScale;
            float3 cloudPositionOffset, detailPositionOffset;
            float densityOffset;
            float detailStrength;
            float cloudType;
            
            //Cloud Scattering
            float3 cloudAbsorption,cloudInscattering;
            float3 cloudMieCoeff;
            float cloudPowderCoeff;

            //Atmosphere Scattering
            float3 atmosphereAbsorption, atmosphereInscattering;

            //LightMarch Parameters
            float stepSize,lightStepSize,stepSizeDistCoeff,stepSizeDensityCoeff,minStepSizeMultiplier;
            int maxInScatteringPoints;
            int minInScatteringPoints;
            int numStepsLight;

            //PostProcessing
            float toneMappingExposure;
            
            sampler2D _MainTex,_CameraDepthTexture;

            float2 raySphere(float sphereRadius, float3 rayOrigin, float3 rayDir){
                //returns dstToSphere,dstThroughSphere
                //dir is normalzied
                float b=2*dot(rayOrigin,rayDir);
                float c=dot(rayOrigin,rayOrigin)-sphereRadius*sphereRadius;
                float d=b*b-4*c;
                if(d<=0)
                    return float2(0,0);
                else{
                    float s=sqrt(d);
                    float dstToSphereNear=max(0,(-b-s)/2);
                    float dstToSphereFar=(-b+s)/2;
                    if(dstToSphereFar>=0)return float2(dstToSphereNear, dstToSphereFar-dstToSphereNear);
                }
            }

            float4 raySphereShell(float innerRadius, float outerRadius, float3 rayOrigin, float3 rayDir, float rayLength){
                //only first cross
                //returns start,end-start,gapStart-start,gatEnd-gapStart
                //dir is normalzied
                float b=2*dot(rayOrigin,rayDir);
                float bb=b*b;
                float rr=dot(rayOrigin,rayOrigin);

                float d1=bb-4*(rr-outerRadius*outerRadius);
                if(d1<=0)
                    return float4(0,0,0,0);
                else{
                    float s1=sqrt(d1);
                    float x4c=min((-b+s1)/2,rayLength);
                    if(x4c<0)
                        return float4(0,0,0,0);
                    else{
                        float x1c=max(0,(-b-s1)/2);
                        float d2=bb-4*(rr-innerRadius*innerRadius);
                        if(d2<0)
                            return float4(x1c,max(x4c-x1c,0),max(x4c-x1c,0),0);
                        else{
                            float s2=sqrt(d2);
                            float x3c=min((-b+s2)/2,rayLength);
                            if(x3c<0)
                                return float4(0,x4c,x4c,0);
                            else{
                                float x2=(-b-s2)/2;
                                if(x2<0)
                                    return float4(x3c,max(x4c-x3c,0),max(x4c-x3c,0),0);
                                else
                                    return float4(x1c,max(x4c-x1c,0),x2-x1c,max(x3c-x2,0));
                            }
                        }
                    }
                }
            }
            


            float getDensity(float3 position){
                float3 uvw=(position-cloudPositionOffset).xzy/cloudScale;
                float2 noise=NoiseTexture.SampleLevel(samplerNoiseTexture,uvw,0)-.5;
                //float height01=(position.y-BoundsMin.y)/(BoundsMax.y-BoundsMin.y);
                float height01=(length(position)-cloudMinRadius)/cloudDeltaRadius;
                float heightCoeff=4*height01*(1-height01)-.75+(height01-.5);
                return lerp(noise.r,noise.g,cloudType)+densityOffset+heightCoeff;
            }
            float getDensityDetail(float3 position){
                float3 uvw=(position-detailPositionOffset).xzy/detailScale;
                return -detailStrength*(NoiseTexture.SampleLevel(samplerNoiseTexture,uvw,0).r);
            }

            float3 getAmbient(float3 position){
                //float height01=(position.y-BoundsMin.y)/(BoundsMax.y-BoundsMin.y);
                float height01=(length(position)-cloudMinRadius)/cloudDeltaRadius;
                return lerp(ambientColorLower,ambientColorUpper,height01);
            }

            float getMiePhase(float cosTh){
                return cloudMieCoeff.x*pow(cloudMieCoeff.y-cloudMieCoeff.z*cosTh,-1.5);
            }           
            float getRayleighPhase(float cosTh){
                return .75*(1+cosTh*cosTh);
            }

            float3 lightmarch(float3 position, int steps){
                float3 dirToLight=_WorldSpaceLightPos0.xyz;
                float dstInsideCloud=raySphereShell(cloudMinRadius,cloudMinRadius+cloudDeltaRadius,position,dirToLight,1e38).y;
                float stepSize=min(dstInsideCloud/steps,lightStepSize);
                float totalDensity=0;
                totalDensity+=saturate(getDensity(position));
                for(int i=0;i<steps;++i){
                    position+=dirToLight*stepSize;
                    totalDensity+=saturate(getDensity(position));
                }
                float3 opticalPath=totalDensity*stepSize*cloudAbsorption;
                return exp(-opticalPath)*(1-exp(-cloudPowderCoeff*opticalPath*opticalPath));
            }

            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength, float gapStart, float gapLength){
                float dst=0;
                float totalDensity=0;
                float3 light=0;
                float3 transmittance=float3(1,1,1);

                float3 dirToLight=_WorldSpaceLightPos0.xyz;
                float miePhase=getMiePhase(dot(rayDir,dirToLight));

                for(int i=0;i<maxInScatteringPoints;++i){
                    if(dst>gapStart){dst+=gapLength;gapStart=1e38;}
                    if(dst>rayLength)break;

                    float3 pos=rayOrigin+rayDir*dst;
                    float density=getDensity(pos);
                    //float step=min(stepSize*clamp(stepSizeDensityCoeff*abs(density),minStepSizeMultiplier,1)*clamp(dst/stepSize*stepSizeDistCoeff,1,10),(rayLength-gapLength)/minInScatteringPoints-.001f);
                    float step=(rayLength-gapLength)/minInScatteringPoints;
                    if(density>0)
                        density+=getDensityDetail(pos);
                    if(density>0){

                        totalDensity+=density*step;
                        transmittance=exp(-totalDensity*cloudAbsorption);
                        light+=saturate(density)*step*cloudInscattering*transmittance*(miePhase*lightmarch(pos,numStepsLight-1)*lightColor+getAmbient(pos));
                    }
                    dst+=step;
                }
                color= color*transmittance+light;
                return color;
            }


            
            float3 toneMapping(float3 color){
                //return 1-exp(-toneMappingExposure*color);
                //float l=max(max(color.r,color.g),color.b);
                float l=dot(float3(.2126,.7152,.0722),color);
                return color/l*(1-exp(-toneMappingExposure*l));
            }

            fixed4 frag (v2f input) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, input.uv);

                float nonLinearDepth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,input.uv);
                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonLinearDepth>0;else hasDepth=nonLinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonLinearDepth)*length(input.viewVector)*worldToPlanetS; else depth=1e38;

                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector));

                float4 cloudHit=raySphereShell(cloudMinRadius,cloudMinRadius+cloudDeltaRadius,rayOrigin,rayDir,depth);
                float dstToCloud=cloudHit.x;
                float dstThroughCloud=cloudHit.y;
                

                if(dstThroughCloud>0)
                    col.xyz=raymarch(col,rayOrigin+rayDir*dstToCloud,rayDir,dstThroughCloud,cloudHit.z,cloudHit.w);
                    
                //HRD mapping, which is very important for realitistic picture
                if(toneMappingExposure>0)
                    col.xyz=toneMapping(col.xyz);

                return col;
            }
            ENDCG
        }
    }
}
