#ifndef FZMNM_ATMOSPHERE_INCLUDED
#define FZMNM_ATMOSPHERE_INCLUDED



    //Planet Geometry
    uniform float4x4 worldToPlanetTRS;
    uniform float depthToPlanetS;
    uniform float planetRadius;
    uniform float atmosphereRadius;
    //Sun
    uniform float3 dirToSun;
    uniform float3 sunColor;
    uniform float4 sunDiscCoeff;
    //Atmosphere
    uniform float3 atmosphere_rayleighScattering;
    uniform float3 atmosphere_rayleighAbsorption;
    uniform float3 atmosphere_rayleighPhaseCoeff;
    uniform float atmosphere_rayleighScaleHeight;
                
    uniform float3 atmosphere_mieScattering;
    uniform float3 atmosphere_mieAbsorption;
    uniform float3 atmosphere_miePhaseCoeff;
    uniform float atmosphere_mieScaleHeight;
                
    uniform float3 atmosphere_ozoneAbsorption;
    uniform float atmosphere_ozoneMinRadius;
    uniform float atmosphere_ozoneMaxRadius;

    
    float2 raySphere(float R, float rSquare, float rCosChi){
        //R: planet radius
        //r: dist to planet center
        //cosChi: angle between ray and local zenith
        float b=2*rCosChi;
        float c=rSquare-R*R;
        float d=b*b-4*c;
        if(d>0){
            float s=sqrt(d);
            float dstToSphereNear=max(0,(-b-s)/2);
            float dstToSphereFar=(-b+s)/2;
            if(dstToSphereFar>=0)return float2(dstToSphereNear, dstToSphereFar-dstToSphereNear);
        }
        return float2(0,0);
    }

    //(3/4,0,3/4)/(4pi) rayleigh, or (1.12,.4,0)/(4pi) modded
    float rayleighPhaseFunction(float cosTh, float3 rayleighPhaseCoeff){
        //Th: angle between ray and dirToLight
        return rayleighPhaseCoeff.x+cosTh*(rayleighPhaseCoeff.y+cosTh*(rayleighPhaseCoeff.z));
    }    
    float miePhaseFunction(float cosTh, float3 miePhaseCoeff){
        //Th: angle between ray and dirToLight
        return clamp(miePhaseCoeff.x*pow(miePhaseCoeff.y-miePhaseCoeff.z*cosTh,-1.5),0,100);
    }      
    float3 sunDisc(float cosTh,float4 sunDiscCoeff){
        //Th: angle between ray and dirToLight
        cosTh = pow(saturate(cosTh), sunDiscCoeff.w);
        return miePhaseFunction(cosTh,sunDiscCoeff.xyz);
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


    struct Atmosphere_Output{
        float3 scattering;
        float3 absorption;
        float3 inscatteringLightDepth;
    };

    Atmosphere_Output atmosphereStep(float r,float h, float cosChi, float rayleighPhaseStrength, float miePhaseStrength){

        //cosChi: angle between dirToLight and local zenith

        Atmosphere_Output output;


        float rayleighExp=exp(-h/atmosphere_rayleighScaleHeight);
        float mieExp=exp(-h/atmosphere_mieScaleHeight);
        float ozoneExistence=step(atmosphere_ozoneMinRadius,r)-step(atmosphere_ozoneMaxRadius,r);
                
        //absorption at this point
        output.absorption=rayleighExp*atmosphere_rayleighAbsorption+mieExp*atmosphere_mieAbsorption+ozoneExistence*atmosphere_ozoneAbsorption;

        //get the depth of inscattering lights
        output.inscatteringLightDepth=
             atmosphere_rayleighAbsorption       *rayleighExp*atmosphere_rayleighScaleHeight*chapman(r/atmosphere_rayleighScaleHeight,cosChi)
            +atmosphere_mieAbsorption            *mieExp*atmosphere_mieScaleHeight*chapman(r/atmosphere_mieScaleHeight,cosChi)
            +atmosphere_ozoneAbsorption          *(raySphere(atmosphere_ozoneMaxRadius,r*r,r*cosChi).y-raySphere(atmosphere_ozoneMinRadius,r*r,r*cosChi).y);
                    
        output.scattering=
                rayleighExp*atmosphere_rayleighScattering*rayleighPhaseStrength
            +mieExp*atmosphere_mieScattering*miePhaseStrength;

        return output;
    }




    




#endif