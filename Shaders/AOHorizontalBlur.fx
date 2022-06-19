sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
float3 uColor;
float3 uSecondaryColor;
float uOpacity : register(C0);
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
float2 uImageSize2;
float4 uShaderSpecificData;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
	float2 pix = float2(uShaderSpecificData.x, 0);

	float4 color = (
		  1 * tex2D(uImage0, coords - 24 * pix)
		+ 1 * tex2D(uImage0, coords + 24 * pix)
		+ 24 * tex2D(uImage0, coords - 22 * pix)
		+ 24 * tex2D(uImage0, coords + 22 * pix)
		+ 276 * tex2D(uImage0, coords - 20 * pix)
		+ 276 * tex2D(uImage0, coords + 20 * pix)
		+ 2024 * tex2D(uImage0, coords - 18 * pix)
		+ 2024 * tex2D(uImage0, coords + 18 * pix)
		+ 10626 * tex2D(uImage0, coords - 16 * pix)
		+ 10626 * tex2D(uImage0, coords + 16 * pix)
		+ 42504 * tex2D(uImage0, coords - 14 * pix)
		+ 42504 * tex2D(uImage0, coords + 14 * pix)
		+ 134596 * tex2D(uImage0, coords - 12 * pix)
		+ 134596 * tex2D(uImage0, coords + 12 * pix)
		+ 346104 * tex2D(uImage0, coords - 10 * pix)
		+ 346104 * tex2D(uImage0, coords + 10 * pix)
		+ 735471 * tex2D(uImage0, coords - 8 * pix)
		+ 735471 * tex2D(uImage0, coords + 8 * pix)
		+ 1307504 * tex2D(uImage0, coords - 6 * pix)
		+ 1307504 * tex2D(uImage0, coords + 6 * pix)
		+ 1961256 * tex2D(uImage0, coords - 4 * pix)
		+ 1961256 * tex2D(uImage0, coords + 4 * pix)
		+ 2496144 * tex2D(uImage0, coords - 2 * pix)
		+ 2496144 * tex2D(uImage0, coords + 2 * pix)
		+ 2704156 * tex2D(uImage0, coords)
	) / 16777216.0;

	color.a = 1;

	return color;
}

technique Technique1
{
	pass HorizontalBlur
	{
		PixelShader = compile ps_2_0 PixelShaderFunction();
	}
}