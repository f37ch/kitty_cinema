using System.Threading.Tasks;
/// <summary>
/// Popcorn spilled on impact: kernels burst out of the hit point, fall under gravity, bounce on the floor
/// and lie there for a few seconds before fading out. Built in code, every client plays it locally.
/// </summary>
public static class PopcornSpill
{
	private const int Kernels=55;
	private const float LieTime=8f;

	[Rpc.Broadcast]
	public static void Broadcast(Vector3 position,Vector3 normal)
	{
		Spawn(position,normal);
	}

	public static void Spawn(Vector3 position,Vector3 normal)
	{
		var scene=Game.ActiveScene;
		if (scene==null) return;
		var go=scene.CreateObject();
		go.Name="Popcorn Spill";
		go.NetworkMode=NetworkMode.Never;
		// A little off the surface so the burst doesn't start inside it.
		go.WorldPosition=position+normal.Normal*6;

		var effect=go.Components.Create<ParticleEffect>();
		effect.MaxParticles=Kernels;
		effect.Lifetime=Seeded(LieTime,LieTime+3);
		effect.Force=true;
		effect.ForceDirection=new Vector3(0,0,-800);
		effect.ForceSpace=ParticleEffect.SimulationSpace.World;
		effect.Collision=true;
		effect.CollisionRadius=1.5f;
		effect.CollisionIgnore=new TagSet();
		effect.CollisionIgnore.Add("player");
		effect.CollisionIgnore.Add("weapon");
		effect.Bounce=Seeded(0.2f,0.45f);
		effect.Friction=0.9f;
		effect.Damping=0.4f;
		effect.InitialVelocity=normal.Normal*60+Vector3.Up*50;
		effect.Roll=Seeded(0,360);
		effect.Scale=Seeded(3.5f,5.5f);
		// Kernels stay solid, then fade at the end of their life.
		effect.ApplyAlpha=true;
		effect.Alpha=new ParticleFloat{
			Type=ParticleFloat.ValueType.Curve,
			Evaluation=ParticleFloat.EvaluationType.Life,
			CurveA=new Curve(new Curve.Frame(0,1),new Curve.Frame(0.85f,1),new Curve.Frame(1,0))
		};

		var emitter=go.Components.Create<ParticleSphereEmitter>();
		emitter.Radius=4;
		emitter.Velocity=100;
		emitter.Burst=Kernels;
		emitter.Rate=0;
		emitter.Duration=0.1f;
		emitter.Loop=false;

		var renderer=go.Components.Create<ParticleSpriteRenderer>();
		renderer.Sprite=Sprite.FromTexture(Texture.Load("materials/particle/corn.vtex"));
		renderer.Alignment=ParticleSpriteRenderer.BillboardAlignment.LookAtCamera;
		renderer.FaceVelocity=false;
		renderer.MotionBlur=false;
		renderer.Lighting=true;

		_=DestroyLater(go,LieTime+3.5f);
	}

	private static ParticleFloat Seeded(float min,float max)
	{
		return new ParticleFloat(min,max){Evaluation=ParticleFloat.EvaluationType.Seed};
	}

	private static async Task DestroyLater(GameObject go,float seconds)
	{
		await GameTask.DelayRealtimeSeconds(seconds);
		if (go.IsValid()) go.Destroy();
	}
}
