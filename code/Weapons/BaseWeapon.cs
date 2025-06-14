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
	[Property] private ParticleEffect Particle {get;set;}
	[Sync] private Guid Holder {get;set;}
	public bool Throwed;
	public InfoPopup Popup {get;set;}
	private Chat Chat {get;set;}
	private Vector3 SpawnPosition {get;set;}
	protected override void OnUpdate()
	{
		base.OnUpdate();
		if (Holder!=default&&Scene.Directory.FindByGuid(Holder).Components.TryGet<TheaterPlayer>(out var Player)){
			if (Player.Components.TryGet<SkinnedModelRenderer>(out var model))
			{
				//model.TryGetBoneTransform("hold_R",out Transform btransform);
				//Transform ltransform=new Transform(WeaponPosition,Rotation.FromRoll(-10),1);
        		//Transform wtransform=btransform.ToWorld(ltransform);
        		//GameObject.WorldPosition=wtransform.Position;
        		//GameObject.WorldRotation=wtransform.Rotation;
        		//GameObject.WorldScale=wtransform.Scale;
				var bone=model.GetBoneObject(15);
				////var holdBone = model.Model.Bones.AllBones.FirstOrDefault<BoneCollection.Bone>( bone => bone.Name == "arm_upper_R" );
				////Log.Info(holdBone.Index);
				GameObject.SetParent(bone,true);
				GameObject.Transform.Local=bone.Transform.Local.WithRotation(Rotation.FromRoll(-50)).WithPosition(WeaponPosition).WithScale(WeaponScale);
			}
		}
	}

	public async Task Throw()
	{
		if (Scene.Directory.FindByGuid(Holder).Components.TryGet<TheaterPlayer>(out var Player)){
			Rigidbody.Enabled=true;
			
			var obj=GameObject.Clone(GameObject.WorldPosition,GameObject.WorldRotation);
			obj.Tags.Remove("player");
			obj.Tags.Remove(Player.IDHashed);
			obj.NetworkSpawn();
			obj.NetworkMode=NetworkMode.Never;
			//obj.Network.SetOrphanedMode(NetworkOrphaned.Destroy);
			//obj.Network.AssignOwnership(Player.Network.OwnerConnection);
			Rigidbody.Enabled=false;
			if (obj.Components.TryGet<BaseWeapon>(out var WeaponController)){
				//WeaponController.Destroy();
				WeaponController.Throwed=true;
			}
			if (obj.Components.TryGet<ModelCollider>(out var modelCollider,FindMode.DisabledInSelf)){
				modelCollider.Enabled=true;
			}

			//Holder.Animator.TriggerDeploy();

			obj.LocalRotation=Rotation.Random;
			if (obj.Components.TryGet<Rigidbody>(out var body)){
				body.Gravity=true;
				body.Velocity=(Player.WorldRotation.Forward*700).WithZ(-Player.EyeAngles.pitch*15);
				await Task.DelayRealtimeSeconds(2f);
				obj.Destroy();
			}
		}
		
	}
	//[Rpc.Broadcast]
	//public void BroadcastEffect(Vector3 point)
	//{
	//	//if (Particle!=null){Particle.Enabled=true;Particle.ResetEmitters(); }
	//}
	//public void OnCollisionStart(Collision o)
	//{
	//	Log.Info( o.Other.GameObject.Tags.Has( "world" ) );
	//	if ( o.Other.GameObject.Tags.Has( "world" ) )
	//	{
	//		//BroadcastEffect( o.Contact.Point );
	//	}
	//}
	[Rpc.Broadcast]
	public void PickUp(Guid Userid)
	{
		if (Throwed) return;
		if (Scene.Directory.FindByGuid(Userid).Components.TryGet<TheaterPlayer>(out var Player)){
			if (Player.HoldingWeapon!=default){
				Chat.AddLocalText("#notify.dropweaponFirst","info",true);
				return;
			}
			Holder=Userid;
			
			Rigidbody.Enabled=false;
			ModelCollider.Enabled=false;
			//Particle.Enabled=false;
			GameObject.Network.DisableInterpolation();
			GameObject.Transform.ClearInterpolation();
			Player.HoldingWeapon=GameObject.Id;
			Chat.AddLocalText(PickupInfoChat,"info",true);
		}
	}
	[Rpc.Broadcast]
	public void Drop()
	{
		if (Scene.Directory.FindByGuid(Holder).Components.TryGet<TheaterPlayer>(out var Player)){
			if (Player.IsValid){Player.HoldingWeapon=default;}
		
			ModelCollider.Enabled=true;
			Rigidbody.Enabled=true;
			GameObject.SetParent(null,false);
			GameObject.Network.EnableInterpolation();
			GameObject.WorldPosition=(Player.WorldPosition+Player.LocalRotation.Forward*20).WithZ(Player.WorldPosition.z+40);

			GameObject.WorldRotation=Player.WorldRotation;
			//Rigidbody.Velocity=GameObject.WorldPosition+=(Holder.WorldRotation.Forward*200).WithZ(-Holder.EyeAngles.pitch*8);
			Holder=default;
		}
	}
	protected override void OnStart()
	{
		base.OnStart();
		GameObject.NetworkMode=NetworkMode.Object;
		//if (Particle!=null){Particle.Enabled=false;}
		WorldScale =WeaponScale;
		Popup=Scene.Components.GetInDescendantsOrSelf<InfoPopup>();
		Chat=Scene.Components.GetInDescendantsOrSelf<Chat>();
		SpawnPosition=GameObject.WorldPosition;
	}
}