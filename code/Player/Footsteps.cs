public sealed class Footsteps : Component
{
	[Property] private SkinnedModelRenderer ModelRenderer {get;set;}
	public static float FootStepVolume {get;set;}
	private TimeSince TimeSinceLastStep {get;set;}
	protected override void OnEnabled()
	{
		if (!ModelRenderer.IsValid()) return;
		ModelRenderer.OnFootstepEvent+=OnEvent;
	}

	protected override void OnDisabled()
	{
		if (!ModelRenderer.IsValid()) return;
		ModelRenderer.OnFootstepEvent-=OnEvent;
	}
	
	private void OnEvent(SceneModel.FootstepEvent e)
	{
		if (TimeSinceLastStep<.2f) return;
		var trace=Scene.Trace
			.Ray(e.Transform.Position+Vector3.Up*20f,e.Transform.Position+Vector3.Up*-20)
			.Run();
		if (!trace.Hit) return;
		if (trace.Surface is null) return;
		TimeSinceLastStep=0;
		var sound=e.FootId==0?trace.Surface.Sounds.FootLeft:trace.Surface.Sounds.FootRight;
		if (sound is null) return;
		var handle=Sound.Play(sound,trace.HitPosition+trace.Normal*5);
		handle.Volume*=e.Volume*FootStepVolume;
	}
}