Shader "Hidden/Cloud2"
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
            #include "Atmosphere.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO 
            };

            v2f vert (appdata v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_OUTPUT(v2f, output); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); 

                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }
            
            //Planet Geometry
            uniform float4x4 worldToPlanetTRS;
            uniform float depthToPlanetS;
            uniform float planetRadius;
            uniform float cloudMaxHeight;
            uniform float atmosphereRadius;
            //Sun
            uniform float3 dirToSun;
            uniform float3 sunColor;
            uniform float4 sunDiscCoeff;

            //Weather Map
            uniform Texture2D WeatherMap;
            uniform SamplerState samplerWeatherMap;
            uniform float weatherMapScale;
            uniform float2 weatherMapOffset;
            
            //Cloud Shape
            uniform Texture3D CloudNoiseTexture,CloudErosionTexture;
            uniform SamplerState samplerCloudNoiseTexture,samplerCloudErosionTexture;
            uniform float cloudNoiseScale, cloudErosionScale;
            uniform float3 cloudNoisePositionOffset, cloudErosionPositionOffset;
            uniform float cloudErosionStrength;
            //
            uniform float cloudCoverage,cloudLowerHeight,cloudUpperHeight,cloudType;
            
            //Cloud Scattering
            uniform float3 cloudAbsorption,cloudInscattering;
            uniform float3 cloudMieCoeff;
            uniform float3 cloudExtinction;

            //LightMarch
            uniform int atmosphereStepNum;
            uniform int cloudStepNum;
            uniform int cloudLightingStepNum;
            uniform float cloudLightingStepQ;
            uniform float cloudNoiseLodShift;
            uniform float cloudErosionLodShift;


            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            
            float3 raymarch_atm(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                //Intersect the ray to the atmosphere
                float rr=dot(rayOrigin,rayOrigin);
                float rCosChi=dot(rayDir,rayOrigin);

                float2 atmosphereHit=min(rayLength,raySphere(atmosphereRadius ,rr,rCosChi));
                float dstThroughAtmosphere=atmosphereHit.y-atmosphereHit.x;
                if(dstThroughAtmosphere<=0)return color;

                float2 atmospherePhaseStrength=getAtmospherePhaseStrength(dot(rayDir,dirToSun));
                float3 totalDepth=0;
                float3 scatteredLight=0;

                int nStep=atmosphereStepNum;
                float step=dstThroughAtmosphere/nStep;

                float dst=atmosphereHit.x;
                for(int i=0;i<nStep;++i){

                    dst+=.5*step;

                    float3 scatterPos=rayOrigin+rayDir*dst;

                    float r=length(scatterPos),h=r-planetRadius,cosChi=dot(scatterPos,dirToSun)/r;

                    Atmosphere_Output output1=atmosphereStep(r,h,cosChi,atmospherePhaseStrength);
                    
                    totalDepth+=.5*step*output1.absorption;
                    scatteredLight+=step*sunColor*output1.scattering*exp(-(totalDepth+output1.inscatteringLightDepth));
                    totalDepth+=.5*step*output1.absorption;

                    dst+=.5*step;
                }
                return color*exp(-totalDepth)+scatteredLight;
            }

            //float2 xyz2LatLong(float3 v){
            //    //Can be optimized
            //    float r=length(v);
            //    //(0,0) is bottom left, (1,1) is top right.
            //    return float2(1.57079632679+arcsin(v.y/r),arctan2(v.z,v.x))/float2(3.14159265359,6.28318530718);
            //}
            
            float2 getCloudDensityLQ(float3 position, float log2Step){
                // returns float2(0-1 density,sdf estimate)

                float h=length(position)-planetRadius;

                float2 uv=(position.xz+weatherMapOffset)/weatherMapScale;
                float4 weatherSample=WeatherMap.SampleLevel(samplerWeatherMap,uv,0);


                float localCloudLowerHeight=weatherSample.g*cloudMaxHeight;
                float localCloudDeltaHeight=weatherSample.b*cloudMaxHeight;
                float localCloudCoverate=weatherSample.r;
                float localCloudType=weatherSample.a;
                //float coverage=cloudCoverage;
                //float mH=cloudLowerHeight;
                //float MH=cloudUpperHeight;

                float height01=saturate((h-localCloudLowerHeight)/localCloudDeltaHeight);
                float heightCoeff=6.5*height01*height01*(1-height01);

                float cloudNoise;
                float3 uvw=(position+cloudNoisePositionOffset).xyz/cloudNoiseScale;
                float2 noiseSample=CloudNoiseTexture.SampleLevel(samplerCloudNoiseTexture,uvw,log2Step+cloudNoiseLodShift);
                cloudNoise=lerp(noiseSample.r,noiseSample.g,localCloudType)-1;

                
                return float2(saturate(localCloudCoverate+cloudNoise)*heightCoeff,0);
            }

            float2 getCloudDensityHQ(float3 position, float log2Step){
                float2 cloudInfo=getCloudDensityLQ(position,log2Step);
                if(cloudInfo.x>0){
                float h=length(position)-planetRadius;

                    float height01=saturate((h-cloudLowerHeight)/(cloudUpperHeight-cloudLowerHeight));
                    
                    float3 uvw=(position+cloudErosionPositionOffset).xyz/cloudErosionScale;
                    float erosionSample=CloudErosionTexture.SampleLevel(samplerCloudErosionTexture,uvw,log2Step+cloudErosionLodShift).r;
                    
                    cloudInfo.x=saturate(cloudInfo.x-cloudErosionStrength*erosionSample*(1-height01));
                }
                return cloudInfo;
            }

            float getCloudDepth(float3 rayOrigin, float3 rayDir){
                
                float rr=dot(rayOrigin,rayOrigin);
                float rCosChi=dot(rayOrigin,rayDir);
                float2 cloudHit=raySphere(planetRadius+cloudMaxHeight,rr,rCosChi);

                float dstThroughCloud=min(cloudHit.y-cloudHit.x,6*cloudMaxHeight);

                int nStep=cloudLightingStepNum;
                float q=cloudLightingStepQ;
                float step=dstThroughCloud/(pow(q,nStep)-1)*(q-1);//get better shadows for thinner clouds and lower sunlights

                float totalDensity=0;
                float dst=cloudHit.x;
                for(int i=0;i<nStep;++i){

                    dst+=.5*step;
                    totalDensity+=getCloudDensityLQ(rayOrigin+rayDir*dst,log2(step)).x*step;
                    dst+=.5*step;

                    step*=q;
                    //TODO early quit optimization
                }
                return totalDensity;
            }



            float3 raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
                //Intersect the ray to the cloud layer
                float rr=dot(rayOrigin,rayOrigin);
                float rCosChi=dot(rayDir,rayOrigin);

                float2 cloudHit=min(rayLength,raySphere(planetRadius+cloudMaxHeight,rr,rCosChi));
                float cosTh=dot(rayDir,dirToSun);
                float2 atmospherePhaseStrength=getAtmospherePhaseStrength(cosTh);
                float cloudMiePhaseStrength=miePhaseFunction(cosTh,cloudMieCoeff);
                
                float3 totalDepth=0;
                float3 scatteredLight=0;


                float dstThroughAtmosphere=rayLength-cloudHit.y;
                if(dstThroughAtmosphere>0)
                    color=raymarch_atm(color,rayOrigin+rayDir*cloudHit.y,rayDir,dstThroughAtmosphere);
                
                int nStep=cloudStepNum;
                float dstThroughCloud=min(cloudHit.y-cloudHit.x,nStep*cloudNoiseScale/8);

                float dst=cloudHit.x;
                float step=dstThroughCloud/nStep;
                if(dstThroughCloud>0)
                    for(int i=0;i<nStep;++i){
                        dst+=.5*step;
                        float3 scatterPos=rayOrigin+rayDir*dst;
                        
                        float r=length(scatterPos),h=r-planetRadius,cosChi=dot(scatterPos,dirToSun)/r;

                        Atmosphere_Output output1=atmosphereStep(r,h,cosChi,atmospherePhaseStrength);
                        
                        float2 cloudInfo=getCloudDensityHQ(scatterPos,log2(step));
                        float cloudDensity=cloudInfo.x;
                        float sdf=cloudInfo.y;
                        
                        output1.absorption+=cloudDensity*cloudAbsorption;
                        output1.scattering+=cloudDensity*cloudInscattering*cloudMiePhaseStrength;
                        //for cloud shadow inside atmosphere, do not skip cloudDensity=0 case
                        output1.inscatteringLightDepth+=getCloudDepth(scatterPos,dirToSun)*cloudAbsorption;

                        //saturate(density)*step*cloudInscattering*transmittance*(miePhase*lightmarch(pos,numStepsLight-1)*lightColor+getAmbient(pos));
                        totalDepth+=.5*step*output1.absorption;
                        scatteredLight+=step*sunColor*output1.scattering*exp(-(totalDepth+output1.inscatteringLightDepth));
                        totalDepth+=.5*step*output1.absorption;
                        dst+=.5*step;

                        //step=(dstThroughCloud-(dst-cloudHit.x))/(nStep-i-1);

                    }
                    
                //totalDepth=cloudAbsorption*(cloudHit.y-cloudHit.x+cloudHit.w-cloudHit.z);

                color=color*exp(-totalDepth)+scatteredLight;

                if(cloudHit.x>0)
                    color=raymarch_atm(color,rayOrigin,rayDir,cloudHit.x);

                return color;
            }
            
            
            //Next: cloud height adjust
            //Next: cloud should not be completely dark
            //Next: global mapping shader variant
            
            
            fixed3 frag (v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //Get the screen depth and camera ray
                fixed nonlinearDepth= SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                bool hasDepth; if(UNITY_REVERSED_Z) hasDepth=nonlinearDepth>0;else hasDepth=nonlinearDepth<1;
                float depth; if(hasDepth)depth=LinearEyeDepth(nonlinearDepth)*length(input.viewVector)*depthToPlanetS; else depth=1e38;
                float3 rayOrigin=mul(worldToPlanetTRS,float4(_WorldSpaceCameraPos,1));
                float3 rayDir=normalize(mul(worldToPlanetTRS,input.viewVector*depthToPlanetS));

                //Get the screen color, convert to Spectral color space
                float3 color=UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex,input.uv);
                color=mul(RGB2spectralColor,color); //The spectral color space is not the rgb color space

                //Draw the sun disc
                float cosTh=dot(rayDir,dirToSun);
                if(!hasDepth)
                    color+=sunColor*sunDisc(cosTh,sunDiscCoeff);

                //Raymarch
                color=raymarch(color, rayOrigin,rayDir,depth);
                
                //Tonemapping HDR to LDR
                if(toneMappingExposure>0)
                    color.xyz=1-exp(-toneMappingExposure*color.xyz);

                //Convert to RGB color space
                color=mul(spectralColor2RGB,color);

                return color;
            }
            ENDCG
        }
    }
}