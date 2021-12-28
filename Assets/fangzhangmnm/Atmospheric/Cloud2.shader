

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
            #pragma multi_compile WEATHERMAP_UNIFORM WEATHERMAP_PLANAR WEATHERMAP_SPHERICAL
            #pragma shader_feature CLOUD_DEBUG_SHOW_STEPS
            #pragma shader_feature RENDER_TO_TEXTURE

            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

            #if CLOUD_DEBUG_SHOW_STEPS
            float3 rainbow(float level){
	            float r = float(level <= 2.0) + float(level > 4.0) * 0.5;
	            float g = max(1.0 - abs(level - 2.0) * 0.5, 0.0);
	            float b = (1.0 - (level - 4.0) * 0.5) * float(level >= 4.0);
	            return float3(r, g, b);}
            #endif

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
            uniform float3 planetColor;
            uniform float4 sunDiscCoeff;

            //Weather Map
            uniform Texture2D WeatherMap;
            uniform SamplerState samplerWeatherMap;
            #if WEATHERMAP_PLANAR
                uniform float weatherMapScale;
                uniform float2 weatherMapOffset;
            #endif
            #if WEATHERMAP_UNIFORM
                uniform float cloudType,cloudLowerHeight,cloudDeltaHeight,cloudCoverage;
            #endif
            
            //Cloud Shape
            uniform Texture3D CloudNoiseTexture,CloudErosionTexture;
            uniform SamplerState samplerCloudNoiseTexture,samplerCloudErosionTexture;
            uniform float cloudNoiseScale, cloudErosionScale;
            uniform float3 cloudNoisePositionOffset, cloudErosionPositionOffset;
            uniform float cloudErosionStrength;
            
            //Cloud Scattering
            uniform float3 cloudAbsorption,cloudInscattering,cloudEmission;
            uniform float3 cloudBackwardMieCoeff,cloudForwardMieCoeff;
            uniform float cloudMultiScattering;
            uniform float cloudPowderCoeff;

            //LightMarch
            uniform int atmosphereStepNum;

            uniform int cloudStepNum;
            uniform float cloudStepMaxDist;
            uniform float cloudStepQ;


            uniform int cloudLightingStepNum;
            uniform float cloudLightingStepMaxDist;
            uniform float cloudLightingStepQ;

            uniform int cloudLightingLQStepNum;
            uniform float cloudLightingLQStepMaxDist;
            uniform float cloudLightingLQStepQ;

            uniform float cloudNoiseLodShift;
            uniform float cloudErosionLodShift;
            uniform float weatherMapLodShift;
            
            uniform float cloudStepQuitDepth;
            uniform float cloudNoiseSDFMultiplier;
            uniform float cloudHeightSDFMultiplier;


            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            
            float3 atmosphere_raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength, int nStep){
                //Intersect the ray to the atmosphere
                float rr=dot(rayOrigin,rayOrigin);
                float rCosChi=dot(rayDir,rayOrigin);

                float2 atmosphereHit=min(rayLength,raySphere(atmosphereRadius ,rr,rCosChi));
                float dstThroughAtmosphere=atmosphereHit.y-atmosphereHit.x;
                if(dstThroughAtmosphere<=0)return color;

                float2 atmosphereSunPhaseStrength=getAtmospherePhaseStrength(dot(rayDir,dirToSun));
                float3 totalDepth=0;
                float3 scatteredLight=0;

                float step=dstThroughAtmosphere/nStep;

                float dst=atmosphereHit.x;
                for(int i=0;i<nStep;++i){

                    dst+=.5*step;

                    float3 scatterPos=rayOrigin+rayDir*dst;

                    float r=length(scatterPos),h=r-planetRadius,cosChi=dot(scatterPos,dirToSun)/r;

                    Atmosphere_Output output1=atmosphereStep(r,h,cosChi);
                    
                    totalDepth+=.5*step*output1.absorption;

                    float3 sunVertex=output1.rayleighScattering*atmosphereSunPhaseStrength.x+output1.mieScattering*atmosphereSunPhaseStrength.y;
                    float3 groundVertex=(output1.rayleighScattering+output1.mieScattering)*0.07957747154;

                    scatteredLight+=step*sunColor*sunVertex*exp(-(totalDepth+output1.inscatteringLightDepth));
                    scatteredLight+=step*sunColor*saturate(cosChi)*planetColor*groundVertex*exp(-totalDepth);

                    totalDepth+=.5*step*output1.absorption;

                    dst+=.5*step;

                }
                return color*exp(-totalDepth)+scatteredLight;
            }

            float2 xyz2LatLong(float3 v){
                //Can be optimized
                float r=length(v);
                //(0,0) is bottom left, (1,1) is top right.
                return float2(1.57079632679+asin(v.y/r),atan2(v.z,v.x))/float2(3.14159265359,6.28318530718);
            }

            float4 getWeather(float3 position, float log2Step){
                // return type, lower height, delta height, coverage
                float4 weather;
                #if WEATHERMAP_UNIFORM
                    weather= float4(cloudType,cloudLowerHeight,cloudDeltaHeight,cloudCoverage);
                #elif WEATHERMAP_PLANAR
                    float2 uv=(position.xz+weatherMapOffset)/weatherMapScale;
                    weather= WeatherMap.SampleLevel(samplerWeatherMap,uv,log2Step+weatherMapLodShift);
                    weather.gb*=cloudMaxHeight;
                    weather.a=weather.a*2-1;
                    //weather.z=min(cloudMaxHeight-weather.y,weather.z);
                #elif WEATHERMAP_SPHERICAL
                    weather= WeatherMap.SampleLevel(samplerWeatherMap,xyz2LatLong(position).yx,log2Step+weatherMapLodShift);
                    weather.gb*=cloudMaxHeight;
                    weather.a=weather.a*2-1;
                    //weather.z=min(cloudMaxHeight-weather.y,weather.z);
                #endif
                return weather;
            }
            
            float4 getCloudDensityLQ(float3 position, float4 weather, float log2Step){
                // return.x 0-1 density
                // return.y 0-1 height01

                // TODO early quit optimization
                
                float h=length(position)-planetRadius;
                float height01=saturate((h-weather.g)/weather.b);
                float heightCoeff=4*height01*(1-height01);
                
                float cloudNoise;
                float3 uvw=(position+cloudNoisePositionOffset).xyz/cloudNoiseScale;
                float2 noiseSample=CloudNoiseTexture.SampleLevel(samplerCloudNoiseTexture,uvw,log2Step+cloudNoiseLodShift);
                cloudNoise=lerp(noiseSample.r,noiseSample.g,weather.r)-1;
                #if WEATHERMAP_PLANAR
                    float2 uv=(position.xz+weatherMapOffset)/weatherMapScale-.5;
                    cloudNoise=lerp(cloudNoise,-.5,smoothstep(.22,.25,dot(uv,uv)));
                #endif

                return float4(saturate(weather.a+cloudNoise)*heightCoeff,height01,cloudNoise,weather.a);
            }

            float getCloudDensityHQ(float2 cloudInfo, float3 position, float log2Step){
                //TODO poor visual impact
                float3 uvw=(position+cloudErosionPositionOffset).xyz/cloudErosionScale;
                float erosionSample=CloudErosionTexture.SampleLevel(samplerCloudErosionTexture,uvw,log2Step+cloudErosionLodShift).r;
                    
                return saturate(cloudInfo.x-cloudErosionStrength*erosionSample*(1-cloudInfo.y));//TODO BUG
            }

            float getCloudDepth(float3 rayOrigin, float3 rayDir,int nStep, float maxDist, float q){
                
                float rr=dot(rayOrigin,rayOrigin),rCosChi=dot(rayOrigin,rayDir);
                float2 cloudHit=raySphere(planetRadius+cloudMaxHeight,rr,rCosChi);

                float step=min(maxDist,cloudHit.y-cloudHit.x)/(pow(q,nStep)-1)*(q-1);//get better shadows for thinner clouds and lower sunlights

                float totalDensity=0;
                float dst=cloudHit.x;
                for(int i=0;i<nStep;++i){

                    dst+=.5*step;
                    float3 scatterPos=rayOrigin+rayDir*dst;
                    float log2Step=log2(step);
                    totalDensity+=getCloudDensityLQ(scatterPos,getWeather(scatterPos,log2Step),log2Step).r*step;
                    dst+=.5*step;

                    if(dst>cloudHit.y)break;

                    step*=q.x;
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
                float2 atmosphereSunPhaseStrength=getAtmospherePhaseStrength(cosTh);
                float cloudBackwardMiePhaseStrength=miePhaseFunction(cosTh,cloudBackwardMieCoeff);
                float cloudForwardMiePhaseStrength=miePhaseFunction(cosTh,cloudForwardMieCoeff);
                
                float3 atmosphereDepth=0;
                float3 cloudDepth=0;
                float cloudDepthNoFade=0;
                float3 scatteredLight=0;


                
                
                //dstThroughCloud=min(dstThroughCloud,cloudStepNum*cloudNoiseScale/8);
                float dst=0;
                float3 rayOrigin2=rayOrigin+rayDir*cloudHit.x;//to avoid float point precision issues
                float maxDst=cloudHit.y-cloudHit.x;

                //int nStep=cloudStepNum*clamp(saturate(dstThroughCloud/cloudMaxHeight),1,2)/2;
                //float smallStep=dstThroughCloud/nStep;//get better shadows for thinner clouds and lower sunlights
                float q=cloudStepQ;
                float smallStep=cloudStepMaxDist/(pow(q,cloudStepNum)-1)*(q-1);

                float step=smallStep;
                int i=0;
                if(maxDst>0)
                    for(i=0;i<cloudStepNum;++i){
                        dst+=.5*step;
                        float3 scatterPos=rayOrigin2+rayDir*dst;
                        float log2Step=log2(step);
                        float4 weather=getWeather(scatterPos,log2Step);
                        //return weather.b/cloudMaxHeight;

                        float4 cloudInfo=getCloudDensityLQ(scatterPos,weather,log2Step);
                        float h=length(scatterPos)-planetRadius;
                        if(cloudInfo.x>0)
                            cloudInfo.x=getCloudDensityHQ(cloudInfo,scatterPos,log2Step);
                        
                        float r=length(scatterPos);
                        float cosChi=dot(scatterPos,dirToSun)/r;
                        Atmosphere_Output output1=atmosphereStep(r,r-planetRadius,cosChi);

                        //to see cloud shadow inside atmosphere, do not skip cloudDensity=0 case
                        float3 cloudInscatteringLightDepth;
                        if(cloudInfo.x>0)
                            cloudInscatteringLightDepth=getCloudDepth(scatterPos,dirToSun,cloudLightingStepNum,cloudLightingStepMaxDist,cloudLightingStepQ)*cloudAbsorption;
                        else
                            cloudInscatteringLightDepth=getCloudDepth(scatterPos,dirToSun,cloudLightingLQStepNum,cloudLightingLQStepMaxDist,cloudLightingLQStepQ)*cloudAbsorption;

                        float fade=1-smoothstep(.5*cloudStepQuitDepth,cloudStepQuitDepth,cloudDepthNoFade);

                        atmosphereDepth+=.5*step*fade*output1.absorption;
                        cloudDepth+=.5*step*fade*cloudInfo.x*cloudAbsorption;
                        
                        float cloudCombinedMiePhaseStrength=lerp(cloudForwardMiePhaseStrength,cloudBackwardMiePhaseStrength,exp(-cloudInscatteringLightDepth));
                        
                        //Simulate multiscattering. not good
                        //float3 expCloudDepth=exp(-(cloudDepth+cloudInscatteringLightDepth)); 
                        //expCloudDepth=lerp(exp(-(cloudDepth+cloudInscatteringLightDepth)/cloudMultiScattering),1,expCloudDepth);
                        
                        
                        float3 sunVertex=output1.rayleighScattering*atmosphereSunPhaseStrength.x+output1.mieScattering*atmosphereSunPhaseStrength.y+cloudInfo.x*cloudInscattering*cloudCombinedMiePhaseStrength;
                        float3 groundVertex=(output1.rayleighScattering+output1.mieScattering+cloudInfo.x*cloudInscattering)*0.07957747154;
                        float3 emissionVertex=cloudInfo.x*cloudEmission;

                        scatteredLight+=step*fade*sunColor*sunVertex*exp(-(atmosphereDepth+cloudDepth+output1.inscatteringLightDepth+cloudInscatteringLightDepth));
                        float3 localSkyTransmittance=exp(-saturate(weather.a)*weather.b*.25*cloudAbsorption);
                        scatteredLight+=step*fade*sunColor*saturate(cosChi)*localSkyTransmittance*planetColor*groundVertex*exp(-(atmosphereDepth+cloudDepth));
                        scatteredLight+=step*fade*emissionVertex*exp(-(atmosphereDepth+cloudDepth));

                        atmosphereDepth+=.5*step*fade*output1.absorption;
                        cloudDepth+=.5*step*fade*cloudInfo.x*cloudAbsorption;
                        cloudDepthNoFade+=step*cloudInfo.x*cloudAbsorption.r;

                        dst+=.5*step;
                        
                        // alpha quit optimization
                        if(cloudDepthNoFade>cloudStepQuitDepth)break;

                        // complete the ray range quit
                        if(dst>=maxDst)break;

                        //decide next step length
                        //return weather.b/cloudMaxHeight;
                        float biggerStep=max(smallStep,max(h-(weather.g+weather.b),weather.g-h)*cloudHeightSDFMultiplier);

                        float biggerStep1=saturate(-cloudInfo.z-cloudInfo.a)*cloudNoiseScale*cloudNoiseSDFMultiplier;
                        //#if WEATHERMAP_PLANAR
                        //    biggerStep1=max(biggerStep1,saturate(.1-cloudInfo.a)*weatherMapScale/8);//not quite helpful
                        //#endif
                        biggerStep=sqrt(biggerStep*biggerStep+biggerStep1*biggerStep1);

                        step=min(biggerStep,maxDst-dst+smallStep);
                        //step=smallStep;

                        smallStep*=q;
                        //if(cloudInfo.x>0)smallStep*=(1+.5*cloudInfo.r*cloudInscattering.x*step); has artefact, coincide with quitDepth
                    }

                
                float cloudRayEnd=cloudHit.y;//cloudHit.x+dst; not good
                float dstThroughAtmosphere=rayLength-cloudRayEnd;
                if(dstThroughAtmosphere>0)
                    color=atmosphere_raymarch(color,rayOrigin+rayDir*cloudRayEnd,rayDir,dstThroughAtmosphere,atmosphereStepNum);
                
                float3 expCloudDepth=exp(-cloudDepth);
                expCloudDepth=smoothstep(exp(-(cloudStepQuitDepth-1)),1,expCloudDepth);//To avoid sun penetrate thick cloud
                color=color*exp(-(atmosphereDepth))*expCloudDepth+scatteredLight;
                
                if(cloudHit.x>0)
                    color=atmosphere_raymarch(color,rayOrigin,rayDir,cloudHit.x,atmosphereStepNum);

                #if CLOUD_DEBUG_SHOW_STEPS
                    return rainbow(float(i)/cloudStepNum); //show number of steps. important when debugging
                #endif 

                return color;
            }
            
            
            //Next: cloud height adjust
            //Next: cloud should not be completely dark
            //Next: global mapping shader variant
            
            
            fixed3 frag (v2f input) : SV_Target
            {
                #if RENDER_TO_TEXTURE
                    return float3(1,0,0);
                #endif


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