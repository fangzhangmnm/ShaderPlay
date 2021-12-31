#ifndef FZMNM_CLOUD_INCLUDED
#define FZMNM_CLOUD_INCLUDED
#include "Atmosphere.cginc"

    //Planet Geometry
    uniform float cloudMaxHeight;

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
    uniform float cloudStepDensityQ;


    uniform int cloudLightingStepNum;
    uniform float cloudLightingStepMaxDist;
    uniform float cloudLightingStepQ;

    uniform float cloudLightingLQDepth;
    uniform int cloudLightingLQStepNum;
    uniform float cloudLightingLQStepMaxDist;
    uniform float cloudLightingLQStepQ;

    uniform float cloudNoiseLodShift;
    uniform float cloudErosionLodShift;
    uniform float weatherMapLodShift;
            
    uniform float cloudStepQuitDepth;
    uniform float cloudNoiseSDFMultiplier;
    uniform float cloudNoiseSDFShift;
    uniform float cloudHeightSDFMultiplier;



    float2 xyz2LatLong(float3 v){
        //Can be optimized
        float r=length(v);
        //(0,0) is bottom left, (1,1) is top right.
        return float2(1.57079632679+asin(v.y/r),atan2(v.z,v.x))/float2(3.14159265359,6.28318530718);
    }
            
    //https://github.com/wsmind/js-pride/blob/master/shaders/rainbow.glsl
    float3 _rainbow(float level){
	    float r = float(level <= 2.0) + float(level > 4.0) * 0.5;
	    float g = max(1.0 - abs(level - 2.0) * 0.5, 0.0);
	    float b = (1.0 - (level - 4.0) * 0.5) * float(level >= 4.0);
	    return float3(r, g, b);}
    float3 rainbow (float x)
    {
        float level1 = floor(x*6.0);
        float level2 = min(6.0, floor(x*6.0) + 1.0);
    
        float3 a = _rainbow(level1);
        float3 b = _rainbow(level2);
    
        return lerp(a, b, frac(x*6.0));
    }
    
    int cloudDensitySampleCount;
    float4 getWeather(float3 position, float log2Step){
        // return type, lower height, delta height, coverage
        float4 weather;
        #if WEATHERMAP_UNIFORM
            weather= float4(cloudType,cloudLowerHeight,cloudDeltaHeight,cloudCoverage);
        #elif WEATHERMAP_PLANAR
            float2 uv=(position.xz+weatherMapOffset)/weatherMapScale;
            weather= WeatherMap.SampleLevel(samplerWeatherMap,uv,log2Step+weatherMapLodShift);
            cloudDensitySampleCount+=1;
            weather.gb*=cloudMaxHeight;
            weather.a=weather.a*2-1;
            //weather.z=min(cloudMaxHeight-weather.y,weather.z);
        #elif WEATHERMAP_SPHERICAL
            weather= WeatherMap.SampleLevel(samplerWeatherMap,xyz2LatLong(position).yx,log2Step+weatherMapLodShift);
            cloudDensitySampleCount+=1;
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
        float heightCoeff=saturate(4*height01*(1-height01));
        //float heightCoeff=clamp(min(height01,1-height01)*2,0,1);
        //if(heightCoeff+weather.a<1) //no perf boost
        //    return float4(0,height01,.5+heightCoeff+weather.a-2,weather.a);
                
        float cloudNoise;
        float3 uvw=(position+cloudNoisePositionOffset).xyz/cloudNoiseScale;
        float2 noiseSample=CloudNoiseTexture.SampleLevel(samplerCloudNoiseTexture,uvw,log2Step+cloudNoiseLodShift);
        cloudDensitySampleCount+=1;
        cloudNoise=lerp(noiseSample.r,noiseSample.g,weather.r);
        #if WEATHERMAP_PLANAR
            float2 uv=(position.xz+weatherMapOffset)/weatherMapScale-.5;
            cloudNoise=lerp(cloudNoise,.5,smoothstep(.22,.25,dot(uv,uv)));
        #endif

        return float4(saturate(cloudNoise+weather.a-1+heightCoeff-1),height01,cloudNoise,weather.a-1+heightCoeff-1);
    }

    float getCloudDensityHQ(float2 cloudInfo, float3 position, float log2Step){
        //TODO poor visual impact
        float3 uvw=(position+cloudErosionPositionOffset).xyz/cloudErosionScale;
        float erosionSample=CloudErosionTexture.SampleLevel(samplerCloudErosionTexture,uvw,log2Step+cloudErosionLodShift).r;
        cloudDensitySampleCount+=1;

        #if WEATHERMAP_PLANAR
            float2 uv=(position.xz+weatherMapOffset)/weatherMapScale-.5;
            erosionSample=lerp(erosionSample,0,smoothstep(.22,.25,dot(uv,uv))); //fewer erosion at distance
        #endif
                    
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
    float getCloudDepthLQ(float4 weather, float h){
        return (max(h,weather.g+weather.b)-max(h,weather.g))*saturate(weather.a)*.5;
    }


    Raymarch_Output raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength){
        cloudDensitySampleCount=0;

        //Intersect the ray to the cloud layer
        float rr=dot(rayOrigin,rayOrigin);
        float rCosChi=dot(rayDir,rayOrigin);
        float cosChi=rCosChi/sqrt(rr);

        float4 cloudHit=min(rayLength,raySphereShell(planetRadius,planetRadius+cloudMaxHeight,rr,rCosChi));
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
                float stepInsideCloud=smallStep;//reduce artefact when view from space
                float log2Step=log2(stepInsideCloud);
                float4 weather=getWeather(scatterPos,log2Step);
                //return weather.b/cloudMaxHeight;

                float4 cloudInfo=getCloudDensityLQ(scatterPos,weather,log2Step);

                //go back if enter cloud
                //if(step>smallStep && cloudInfo.z>0){
                //    dst-=.5*step;
                //    step=smallStep;
                //    dst+=.5*step;
                //    scatterPos=rayOrigin2+rayDir*dst;
                //    log2Step=log2(step);
                //    weather=getWeather(scatterPos,log2Step);
                //    cloudInfo=getCloudDensityLQ(scatterPos,weather,log2Step);
                //}

                float h=length(scatterPos)-planetRadius;
                if(cloudInfo.x>0)
                    cloudInfo.x=getCloudDensityHQ(cloudInfo,scatterPos,log2Step);
                        
                float r1=length(scatterPos);
                float cosChi1=dot(scatterPos,dirToSun)/r1;
                Atmosphere_Output atm=atmosphereStep(r1,cosChi1);
                
                float fade=1-smoothstep(.5*cloudStepQuitDepth,cloudStepQuitDepth,cloudDepthNoFade);

                //to see cloud shadow inside atmosphere, do not skip cloudDensity=0 case
                float3 cloudInscatteringLightDepth;
                int clsn=6;//lerp(6,3,fade);
                if(cloudInfo.x>0)
                {
                    if(cloudDepthNoFade<cloudLightingLQDepth)
                        cloudInscatteringLightDepth=getCloudDepth(scatterPos,dirToSun,cloudLightingStepNum,cloudLightingStepMaxDist,cloudLightingStepQ)*cloudAbsorption;
                    else
                        cloudInscatteringLightDepth=getCloudDepth(scatterPos,dirToSun,cloudLightingLQStepNum,cloudLightingLQStepMaxDist,cloudLightingLQStepQ)*cloudAbsorption;
                }
                    
                else
                    //cloudInscatteringLightDepth=getCloudDepth(scatterPos,dirToSun,cloudLightingLQStepNum,cloudLightingLQStepMaxDist,cloudLightingLQStepQ)*cloudAbsorption;
                    cloudInscatteringLightDepth=getCloudDepthLQ(weather,h)*cloudAbsorption;


                        
                float cloudCombinedMiePhaseStrength=lerp(cloudForwardMiePhaseStrength,cloudBackwardMiePhaseStrength,exp(-cloudInscatteringLightDepth));
                        
                //Simulate multiscattering. not good
                //float3 expCloudDepth=exp(-(cloudDepth+cloudInscatteringLightDepth)); 
                //expCloudDepth=lerp(exp(-(cloudDepth+cloudInscatteringLightDepth)/cloudMultiScattering),1,expCloudDepth);
                        
                float3 reducedStepInsideCloud=(1-exp(-(atm.absorption+cloudInfo.x*cloudAbsorption)*stepInsideCloud))/(atm.absorption+cloudInfo.x*cloudAbsorption);//it helps get better precision at bigger step lengths
                float3 reducedStep=(1-exp(-(atm.absorption+cloudInfo.x*cloudAbsorption)*step))/(atm.absorption+cloudInfo.x*cloudAbsorption);//it helps get better precision at bigger step lengths
   
                float3 sunVertex=reducedStep*(atm.rayleighScattering*atmosphereSunPhaseStrength.x+atm.mieScattering*atmosphereSunPhaseStrength.y)
                                +reducedStepInsideCloud*(cloudInfo.x*cloudInscattering*cloudCombinedMiePhaseStrength);
                float3 groundVertex=(reducedStep*(atm.rayleighScattering+atm.mieScattering)
                                    +reducedStepInsideCloud*(cloudInfo.x*cloudInscattering))*0.07957747154;
                float3 emissionVertex=cloudInfo.x*cloudEmission;

                
                scatteredLight+=fade*sunColor*sunVertex*exp(-(atmosphereDepth+cloudDepth+atm.inscatteringLightDepth+cloudInscatteringLightDepth));
                float3 localSkyTransmittance=exp(-saturate(weather.a)*weather.b*.25*cloudAbsorption);
                scatteredLight+=fade*sunColor*saturate(cosChi1)*localSkyTransmittance*planetColor*groundVertex*exp(-(atmosphereDepth+cloudDepth));
                scatteredLight+=fade*emissionVertex*exp(-(atmosphereDepth+cloudDepth));
                
                atmosphereDepth+=step*fade*atm.absorption; //here we use full step, not step1
                cloudDepth+=stepInsideCloud*fade*cloudInfo.x*cloudAbsorption;
                cloudDepthNoFade+=stepInsideCloud*cloudInfo.x*cloudAbsorption.r;

                dst+=.5*step;
                        
                // alpha quit optimization
                if(cloudDepthNoFade>cloudStepQuitDepth)break;

                // complete the ray range quit
                if(dst>=maxDst)break;

                //decide next step length
                
                smallStep*=q;
                smallStep*=(1+cloudStepDensityQ*stepInsideCloud*cloudInfo.x*cloudAbsorption.r);

                float biggerStep=smallStep;

                //biggerStep=smallStep*lerp(1,16,saturate(cloudDepthNoFade));
                //biggerStep=smallStep*lerp(8,1,cloudInfo.x);
                //biggerStep=smallStep*max(lerp(1,8,smoothstep(2,3,cloudDepthNoFade)),lerp(1,16,saturate(cloudInfo.x)));
                //biggerStep=max(biggerStep,max(h-(weather.g+weather.b),weather.g-h)*cloudHeightSDFMultiplier); //height sdf estimate will introduce artefact, not much perf help

                
                float biggerStep1=(-cloudInfo.z-cloudNoiseSDFShift)*cloudNoiseScale*cloudNoiseSDFMultiplier;
                biggerStep1+=-cloudInfo.a*cloudMaxHeight*cloudHeightSDFMultiplier;
                biggerStep1=max(0,biggerStep1);

                //biggerStep1=max(-cloudInfo.z-cloudInfo.a-cloudNoiseSDFShift,0)*cloudNoiseScale*cloudNoiseSDFMultiplier;//old wrong method
                //#if WEATHERMAP_PLANAR
                //    biggerStep1=max(biggerStep1,saturate(.1-cloudInfo.a)*weatherMapScale/8);//not quite helpful
                //#endif
                biggerStep=max(biggerStep,biggerStep1);

                step=min(biggerStep,maxDst-dst+smallStep);
                //step=smallStep;

                //if(cloudInfo.x>0)smallStep*=(1+.5*cloudInfo.r*cloudInscattering.x*step); has artefact, coincide with quitDepth
            }
                    

        Raymarch_Output output;output.scatteredLight=0;output.transmittance=1;

        //show number of steps for debugging
        #if CLOUD_DEBUG_SHOW_STEPS 
            output.transmittance=0;
            output.scatteredLight=rainbow(float(i)/(cloudStepNum));
            return output;
        #elif CLOUD_DEBUG_SHOW_SAMPLES
            output.transmittance=0;
            output.scatteredLight=rainbow(float(cloudDensitySampleCount)/(cloudStepNum*5));
            return output;
        #endif 
                
        float cloudRayEnd=cloudHit.y;//cloudHit.x+dst; not good
        float dstThroughAtmosphere=rayLength-cloudRayEnd;
        if(dstThroughAtmosphere>0)
            output=atmosphere_raymarch(color,rayOrigin+rayDir*cloudRayEnd,rayDir,dstThroughAtmosphere,atmosphereStepNum);
                
        float3 expCloudDepth=exp(-cloudDepth);
        expCloudDepth=smoothstep(exp(-cloudStepQuitDepth/2),1,expCloudDepth);//To avoid sun penetrate thick cloud
        output.scatteredLight=output.scatteredLight*expCloudDepth*exp(-atmosphereDepth)+scatteredLight;
        output.transmittance*=expCloudDepth*exp(-atmosphereDepth);
                
        if(cloudHit.x>0){
            Raymarch_Output atm_raymarch=atmosphere_raymarch(color,rayOrigin,rayDir,cloudHit.x,atmosphereStepNum);
            output.scatteredLight=output.scatteredLight*atm_raymarch.transmittance+atm_raymarch.scatteredLight;
            output.transmittance*=atm_raymarch.transmittance;
        }

        return output;
    }


#endif