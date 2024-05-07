using System.Threading.Tasks;
public sealed class BaseWeapon: Component, Component.ICollisionListener
{
	[Property] ModelCollider  ModelCollider {get;set;}
	[Property] Rigidbody  Rigidbody {get;set;}
	[Property] public string  PickupInfo {get;set;}
	[Property] public string  PickupInfoChat {get;set;}
	[Property] public string  WeaponName {get;set;}
	[Property] private Vector3 WeaponPosition {get;set;}
	[Property] private Vector3 WeaponScale {get;set;}=1;
	[Property] private LegacyParticleSystem Particle {get;set;}
	public bool Throwed;
	public InfoPopup Popup {get;set;}
	private TheaterPlayer Holder {get;set;}
	private Chat Chat {get;set;}
	private Vector3 SpawnPosition {get;set;}
	protected override void OnUpdate()
	{
		base.OnUpdate();
		if (Holder!=null&&Holder.Components.TryGet<SkinnedModelRenderer>(out var model))
		{
			var bone=model.GetBoneObject(15);
			//var holdBone = model.Model.Bones.AllBones.FirstOrDefault<BoneCollection.Bone>( bone => bone.Name == "arm_upper_R" );
			//Log.Info(holdBone.Index);
			GameObject.SetParent(bone,true);
			GameObject.Transform.Local=bone.Transform.Local.WithRotation(Rotation.FromYaw(90)).WithPosition(WeaponPosition).WithScale(WeaponScale);
		}
	}
	public async Task Throw()
	{
		Rigidbody.Enabled=true;
		var obj=GameObject.Clone(GameObject.Transform.Position,GameObject.Transform.Rotation);
		obj.NetworkSpawn();
		Rigidbody.Enabled=false;
		if (obj.Components.TryGet<BaseWeapon>(out var WeaponController)){
			//WeaponController.Destroy();
			WeaponController.Throwed=true;
		}
		if (obj.Components.TryGet<ModelCollider>(out var modelCollider,FindMode.DisabledInSelf)){
			modelCollider.Enabled=true;
		}
		
		//Holder.Animator.TriggerDeploy();
		
		obj.Transform.LocalRotation=Rotation.Random;
		if (obj.Components.TryGet<Rigidbody>(out var body)){
			body.Gravity=true;
			body.Velocity=(Holder.Transform.Rotation.Forward*700).WithZ(-Holder.EyeAngles.pitch*15);
			await Task.DelayRealtimeSeconds(2f);
			obj.Destroy();
		}
		
	}
	[Broadcast]
	public void BroadcastEffect()
	{
		if (Particle!=null){Particle.Enabled=true;}
	}
	public void OnCollisionStart(Collision o)
	{
		BroadcastEffect();
	}
	[Broadcast]
	public void PickUp(Guid Userid)
	{
		if (Throwed) return;
		if (Scene.Directory.FindByGuid(Userid).Components.TryGet<TheaterPlayer>(out var Player)){
			if (Player.HoldingWeapon!=default){
				Chat.AddLocalText("You need to drop current weapon first!","info",true);
				return;
			}
			Holder=Player;
			ModelCollider.Enabled=false;
			Rigidbody.Enabled=false;
			//Particle.Enabled=false;
			GameObject.Network.DisableInterpolation();
			GameObject.Transform.ClearInterpolation();
			Holder.HoldingWeapon=GameObject.Id;
			Chat.AddLocalText(PickupInfoChat,"info",true);
		}
	}
	[Broadcast]
	public void Drop()
	{
		if (Holder!=null){
			Holder.HoldingWeapon=default;
		}
			
		ModelCollider.Enabled=true;
		Rigidbody.Enabled=true;
		GameObject.SetParent(null,false);
		GameObject.Network.EnableInterpolation();
		GameObject.Transform.Position=(Holder.Transform.Position+Holder.Transform.LocalRotation.Forward*20).WithZ(Holder.Transform.Position.z+40);

		GameObject.Transform.Rotation=Holder.Transform.Rotation;
		//Rigidbody.Velocity=GameObject.Transform.Position+=(Holder.Transform.Rotation.Forward*200).WithZ(-Holder.EyeAngles.pitch*8);
		Holder=null;
	}
	protected override void OnStart()
	{
		GameObject.NetworkMode=NetworkMode.Object;
		base.OnStart();
		
		if (Particle!=null){Particle.Enabled=false;}
		Transform.Scale=WeaponScale;
		Popup=Scene.Components.GetInDescendantsOrSelf<InfoPopup>();
		Chat=Scene.Components.GetInDescendantsOrSelf<Chat>();
		SpawnPosition=GameObject.Transform.Position;
	}
}