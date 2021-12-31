#ifndef FZMNM_ATMOSPHERE_INCLUDED
#define FZMNM_ATMOSPHERE_INCLUDED


    //Planet Geometry
    uniform float4x4 worldToPlanetTRS;
    uniform float depthToPlanetS;
    uniform float planetRadius;
    uniform float atmosphereRadius;

    //Lighting
    uniform float3 dirToSun;
    uniform float3 sunColor;
    uniform float4 sunDiscCoeff;

    uniform float3 planetColor;

    //Scattering
    uniform float3 atmosphere_rayleighScattering;
    uniform float3 atmosphere_rayleighAbsorption;
    uniform float3 atmosphere_rayleighPhaseCoeff;
    uniform float atmosphere_rayleighScaleHeight;
                
    uniform float3 atmosphere_mieScattering;
    uniform float3 atmosphere_mieAbsorption;
    uniform float3 atmosphere_miePhaseCoeff;
    uniform float atmosphere_mieScaleHeight;
                
    uniform float3 atmosphere_ozoneAbsorption;
    uniform float atmosphere_ozoneMeanRadius;
    uniform float atmosphere_ozoneHalfDeltaRadius;

    //Color Correction
    uniform float toneMappingExposure;
    uniform float4x4 spectralColor2RGB;
    uniform float4x4 RGB2spectralColor;

    //(3/4,0,3/4)/(4pi) rayleigh, or (1.12,.4,0)/(4pi) modded
    float rayleighPhaseFunction(float cosTh, float3 rayleighPhaseCoeff){
        //Th: angle between ray and dirToLight
        return rayleighPhaseCoeff.x+cosTh*(rayleighPhaseCoeff.y+cosTh*(rayleighPhaseCoeff.z));
    }    
    float miePhaseFunction(float cosTh, float3 miePhaseCoeff){
        //Th: angle between ray and dirToLight
        return clamp(miePhaseCoeff.x*pow(miePhaseCoeff.y-miePhaseCoeff.z*cosTh,-1.5),0,100);
    }      

    float sunDisc(float cosTh,float4 sunDiscCoeff){
        //Th: angle between ray and dirToLight
        cosTh = pow(saturate(cosTh), sunDiscCoeff.w);
        return max(0,16*(1-exp(-miePhaseFunction(cosTh,sunDiscCoeff.xyz)))-.01);//-.01 avoid sunlight bleeding to night
    }

    float chapman(float x,float cosChi){
        //x: height of rayOrigin normalized by scaleHeight
        //Chi: angle between ray and local zenith
        //http://www.thetenthplanet.de/archives/4519
        float c=sqrt(1.57079632679*x);
        if(cosChi>=0)
            return c/((c-1)*cosChi+1);
        else{
            float sinChi=sqrt(saturate(1-cosChi*cosChi));
            return c/((c-1)*cosChi-1)+2*c*exp((1-sinChi)*x)*sqrt(sinChi);
        }
    }

    float2 raySphere(float R, float rSquare, float rCosChi){
        //R: planet radius
        //r: dist to planet center
        //cosChi: angle between ray and local zenith
        //return start, end
        float b=-2*rCosChi; //x^2-b x+c=0, c=rSquare-R*R
        float d=b*b-4*(rSquare-R*R);
        if(d<=0) return float2(0,0); // line not overlap circle
        else{
            float s=sqrt(d);
            float x1=(b-s)/2;
            float x2=(b+s)/2;
            return max(0,float2(x1,x2));
        }

    }
    float4 raySphereShell(float R1, float R2, float rSquare, float rCosChi){
        //R1,R2: inner, outer cloud layer radius
        //r: dist to planet center
        //cosChi: angle between ray and local zenith
        //l: rayLength
        //return start1 < end1 < start2 < end2
        float b=-2*rCosChi;float bSquare=b*b; //x^2-b x+c=0
        float d2=bSquare-4*(rSquare-R2*R2);
        if(d2<=0) return float4(0,0,0,0); // line not overlap outer circle
        else{
            float s2=sqrt(d2);
            float x1=(b-s2)/2;
            float x4=(b+s2)/2;
            if(x4<=0) return float4(0,0,0,0); // ray start after x4
            else{
                float d1=bSquare-4*(rSquare-R1*R1);
                if(d1<=0) return max(0,float4(x1,x4,x4,x4)); // line not overlap inner circle
                else{
                    float s1=sqrt(d1);
                    float x2=(b-s1)/2;
                    float x3=(b+s1)/2;
                    if(x2<=0) return max(0,float4(x3,x4,x4,x4)); // ray start after x1-x2
                    else return max(0,float4(x1,x2,x3,x4));
                }
            }
        }
    }
    
    struct Atmosphere_Output{
        float3 rayleighScattering,mieScattering;
        float3 absorption;
        float3 inscatteringLightDepth;
    };

    float2 getAtmospherePhaseStrength(float cosTh){
        return float2(rayleighPhaseFunction(cosTh,atmosphere_rayleighPhaseCoeff),
                      miePhaseFunction(cosTh,atmosphere_miePhaseCoeff));
    }

    Atmosphere_Output atmosphereStep(float r, float cosChi){
        //r: dist to planet center
        //h: dist to planet surface
        //cosChi: angle between dirToLight and local zenith

        Atmosphere_Output output;

        float h=r-planetRadius;
        float rayleighExp=min(1,exp(-h/atmosphere_rayleighScaleHeight));
        float mieExp=min(1,exp(-h/atmosphere_mieScaleHeight));

        float4 ozoneHit=raySphereShell(atmosphere_ozoneMeanRadius-atmosphere_ozoneHalfDeltaRadius,atmosphere_ozoneMeanRadius+atmosphere_ozoneHalfDeltaRadius,r*r,r*cosChi);
        float ozoneExistence=max(0,1-(r-atmosphere_ozoneMeanRadius)/atmosphere_ozoneHalfDeltaRadius);
        
        //absorption at this point
        output.absorption=rayleighExp*atmosphere_rayleighAbsorption+mieExp*atmosphere_mieAbsorption+ozoneExistence*atmosphere_ozoneAbsorption;
        
        //get the depth of inscattering lights
        output.inscatteringLightDepth=
                atmosphere_rayleighAbsorption       *rayleighExp*atmosphere_rayleighScaleHeight*chapman(r/atmosphere_rayleighScaleHeight,cosChi)
            +atmosphere_mieAbsorption            *mieExp*atmosphere_mieScaleHeight*chapman(r/atmosphere_mieScaleHeight,cosChi)
            +atmosphere_ozoneAbsorption          *((ozoneHit.y-ozoneHit.x+ozoneHit.w-ozoneHit.z)/2);
            
        //intersect with planet
        //if(raySphere(planetRadius,r*r,r*cosChi).x>0) //will introduce artefact
        //    output.inscatteringLightDepth=50;
        
        //output.groundLightDepth= //no visible effect
        //     atmosphere_rayleighAbsorption       *(1-rayleighExp)*atmosphere_rayleighScaleHeight
        //    +atmosphere_mieAbsorption            *(1-mieExp)*atmosphere_mieScaleHeight
        //    +atmosphere_ozoneAbsorption          *(min(r,atmosphere_ozoneMeanRadius+atmosphere_ozoneHalfDeltaRadius)-min(r,atmosphere_ozoneMeanRadius-atmosphere_ozoneHalfDeltaRadius));
                    
        output.rayleighScattering=rayleighExp*atmosphere_rayleighScattering;
        output.mieScattering=mieExp*atmosphere_mieScattering;

        return output;
    }
    struct Raymarch_Output{
        float3 transmittance;
        float3 scatteredLight;
    };

    Raymarch_Output atmosphere_raymarch(float3 color, float3 rayOrigin, float3 rayDir, float rayLength, int nStep){
        //Intersect the ray to the atmosphere
        float rr=dot(rayOrigin,rayOrigin);
        float rCosChi=dot(rayDir,rayOrigin);

        float2 atmosphereHit=min(rayLength,raySphere(atmosphereRadius ,rr,rCosChi));
        float dstThroughAtmosphere=atmosphereHit.y-atmosphereHit.x;
        if(dstThroughAtmosphere<=0){Raymarch_Output output;output.scatteredLight=0;output.transmittance=1; return output;}

        float2 atmosphereSunPhaseStrength=getAtmospherePhaseStrength(dot(rayDir,dirToSun));
        float3 totalDepth=0;
        float3 scatteredLight=0;

        float step=dstThroughAtmosphere/nStep;

        float dst=atmosphereHit.x;
        for(int i=0;i<nStep;++i){

            dst+=.5*step;

            float3 scatterPos=rayOrigin+rayDir*dst;

            float r=length(scatterPos),cosChi=dot(scatterPos,dirToSun)/r;

            Atmosphere_Output atm=atmosphereStep(r,cosChi);
                    
            float3 sunVertex=atm.rayleighScattering*atmosphereSunPhaseStrength.x+atm.mieScattering*atmosphereSunPhaseStrength.y;
            float3 groundVertex=(atm.rayleighScattering+atm.mieScattering)*0.07957747154;

            //TODO fix divide by zero
            float3 reducedStep=(1-exp(-atm.absorption*step))/atm.absorption;//it helps get better precision at bigger step lengths

            scatteredLight+=reducedStep*sunColor*sunVertex*exp(-(totalDepth+atm.inscatteringLightDepth));
            scatteredLight+=reducedStep*sunColor*saturate(cosChi)*planetColor*groundVertex*exp(-(totalDepth));
            
            totalDepth+=step*atm.absorption;

            dst+=.5*step;

        }
        Raymarch_Output output;output.transmittance=exp(-totalDepth);output.scatteredLight=scatteredLight; return output;
    }


#endif