HEADER
{
    DevShader=true;
    Description="Shrinks the screen's frame into the screen light's cookie (MediaPlayer).";
}
MODES
{
    Default();
}
FEATURES
{
}
COMMON
{
    #include "system.fxc"
}
CS
{
    // Every cookie texel averages its block of the frame and is blended over the previous cookie,
    // so the light eases into scene cuts on the GPU.
    RWTexture2D<float4> g_tInput<Attribute("InputTexture");>;
    RWTexture2D<float4> g_tOutput<Attribute("OutputTexture");>;
    float2 g_vInputSize<Attribute("InputSize");>;
    float g_flCookieSize<Attribute("CookieSize");>;
    float3 g_vIdleColor<Attribute("IdleColor");>;
    float g_flHasPicture<Attribute("HasPicture");>;
    float g_flMirror<Attribute("Mirror");>;
    float g_flSaturation<Attribute("Saturation");>;
    float g_flBlend<Attribute("Blend");>;

    // Samples per block side: plenty for an average, cheap for any frame size.
    #define SAMPLES 12

    float3 ToLinear(float3 c) { return pow(max(c,0.0),2.2); }
    float3 ToGamma(float3 c) { return pow(max(c,0.0),1.0/2.2); }

    [numthreads(8,8,1)]
    void MainCs(uint2 vDispatchId:SV_DispatchThreadID)
    {
        int size=int(g_flCookieSize);
        int2 cell=int2(vDispatchId.xy);
        if (cell.x>=size||cell.y>=size) return;

        float3 color=g_vIdleColor;
        if (g_flHasPicture>0.5)
        {
            // Mirror flips the frame when the light's projection looks at it from behind.
            int2 inputMax=int2(g_vInputSize)-1;
            float2 blockSize=g_vInputSize/size;
            float2 blockMin=float2(g_flMirror>0.5?size-1-cell.x:cell.x,cell.y)*blockSize;
            float3 sum=0;
            for (int y=0;y<SAMPLES;++y)
            {
                for (int x=0;x<SAMPLES;++x)
                {
                    int2 p=int2(blockMin+(float2(x,y)+0.5)*blockSize/SAMPLES);
                    sum+=ToLinear(g_tInput[clamp(p,0,inputMax)].rgb);
                }
            }
            color=sum/(SAMPLES*SAMPLES);
        }

        // Averaging washes colors out towards gray.
        float gray=dot(color,float3(0.299,0.587,0.114));
        color=max(0.0,gray+(color-gray)*g_flSaturation);

        float3 previous=ToLinear(g_tOutput[cell].rgb);
        g_tOutput[cell]=float4(ToGamma(lerp(previous,color,g_flBlend)),1.0);
    }
}
