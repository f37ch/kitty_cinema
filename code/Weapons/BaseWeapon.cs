using System.Threading.Tasks;
using Sandbox.UI;
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
	/// <summary>Spills popcorn where the thrown weapon hits something.</summary>
	[Property] public bool SpillOnImpact {get;set;}
	[Sync] private Guid Holder {get;set;}
	public bool Throwed;
	private bool Spilled;
	public InfoPopup Popup {get;set;}
	private Chat Chat {get;set;}
	private Vector3 SpawnPosition {get;set;}
	/// <summary>
	/// Follows the holder's hand. Not parented to the bone: a child would inherit the holder's tag, which their
	/// own camera hides in first person, and the weapon would vanish from their view.
	/// </summary>
	protected override void OnPreRender()
	{
		base.OnPreRender();
		if (Holder==default) return;
		var holder=Scene.Directory.FindByGuid(Holder);
		if (holder==null||!holder.Components.TryGet<SkinnedModelRenderer>(out var model)) return;
		var bone=model.GetBoneObject(15);
		if (bone==null) return;
		GameObject.WorldTransform=bone.WorldTransform.ToWorld(new Transform(WeaponPosition,Rotation.FromRoll(-50),WeaponScale));
	}
	public void OnCollisionStart(Collision collision)
	{
		if (!Throwed||Spilled||!SpillOnImpact) return;
		Spilled=true;
		PopcornSpill.Broadcast(collision.Contact.Point,collision.Contact.Normal);
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
			GameObject.Network.Interpolation=false;
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
			GameObject.Network.Interpolation=true;
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