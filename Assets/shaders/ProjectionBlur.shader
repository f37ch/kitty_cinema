HEADER
{
    DevShader=true;
    Description="Custom shader with multiple blur algorithms.";
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
    RWTexture2D<float4> g_tInput<Attribute("InputTexture");>;
    RWTexture2D<float4> g_tOutput<Attribute("OutputTexture");>;

    float3 LoadColor(int2 pixelCoord)
    {
        return g_tInput[pixelCoord].rgb;
    }

    void StoreColor(int2 pixelCoord, float4 color)
    {
        g_tOutput[pixelCoord] = color;
    }

    float3 BoxBlur(int2 pixelCoord, int radius)
    {
        float3 sum = 0;
        int count = 0;

        for (int y = -radius; y <= radius; ++y)
        {
            for (int x = -radius; x <= radius; ++x)
            {
                sum += LoadColor(pixelCoord + int2(x, y));
                ++count;
            }
        }

        return sum / count;
    }

	float GaussianWeight(int x, int radius, float sigma)
	{
	    float factor = 1.0 / (sqrt(2.0 * 3.14159) * sigma);
	    return factor * exp(-0.5 * (x * x) / (sigma * sigma));
	}
	float3 WeightedBlur(int2 pixelCoord, int radius, float sigma)
	{
	    float3 sum = 0;
	    float weightSum = 0;

	    for (int x = -radius; x <= radius; ++x)
	    {
	        float weight = GaussianWeight(x, radius, sigma);
	        sum += LoadColor(pixelCoord + int2(x, 0)) * weight;
	        weightSum += weight;
	    }

	    return sum / weightSum;
	}

    [numthreads(8, 8, 1)]
    void MainCs(uint2 vGroupID : SV_GroupID, uint2 vGroupThreadID : SV_GroupThreadID, uint2 vDispatchId : SV_DispatchThreadID)
    {
      
		int radius = 30;
        float sigma = 15;
		float3 blurredColor = WeightedBlur(vDispatchId.xy, radius, sigma);
        StoreColor(vDispatchId.xy, float4(blurredColor, 1.0f));
		//int radius = 10;
        //StoreColor(vDispatchId.xy, float4(BoxBlur(vDispatchId.xy, radius), 1.0f));
    }
}