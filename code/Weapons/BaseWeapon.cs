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
		// Every client simulates its own thrown copy, so the spill is local too.
		PopcornSpill.Spawn(collision.Contact.Point,collision.Contact.Normal);
	}

	/// <summary>Holder: throws a copy of the held weapon, the weapon itself stays in the hand.</summary>
	public void Throw()
	{
		if (!Scene.Directory.FindByGuid(Holder).Components.TryGet<TheaterPlayer>(out var Player)) return;
		SpawnThrown(WorldPosition,(Player.WorldRotation.Forward*700).WithZ(-Player.EyeAngles.pitch*15));
	}
	/// <summary>
	/// Every client spawns and simulates the thrown copy locally: a networked copy only moves on its owner,
	/// proxies would see it hanging in the air.
	/// </summary>
	[Rpc.Broadcast]
	private void SpawnThrown(Vector3 position,Vector3 velocity)
	{
		_=ThrowLocal(position,velocity);
	}
	private async Task ThrowLocal(Vector3 position,Vector3 velocity)
	{
		var obj=GameObject.Clone(new Transform(position,Rotation.Random,WorldScale),startEnabled:false);
		obj.NetworkMode=NetworkMode.Never;
		obj.Tags.Remove("player");
		if (obj.Components.TryGet<BaseWeapon>(out var thrown,FindMode.EverythingInSelf)){
			thrown.Throwed=true;
			thrown.Holder=default;
		}
		if (obj.Components.TryGet<ModelCollider>(out var modelCollider,FindMode.EverythingInSelf)){
			modelCollider.Enabled=true;
		}
		obj.Components.TryGet<Rigidbody>(out var body,FindMode.EverythingInSelf);
		if (body!=null) body.Enabled=true;
		obj.Enabled=true;
		if (body!=null){
			body.Gravity=true;
			body.Velocity=velocity;
		}
		await Task.DelayRealtimeSeconds(2f);
		if (obj.IsValid()) obj.Destroy();
	}
	[Rpc.Broadcast]
	public void PickUp(Guid Userid)
	{
		if (Throwed) return;
		if (Scene.Directory.FindByGuid(Userid).Components.TryGet<TheaterPlayer>(out var Player)){
			if (Player.HoldingWeapon!=default){
				GameChat.InfoForCaller("#notify.dropweaponFirst");
				return;
			}
			Holder=Userid;
			
			Rigidbody.Enabled=false;
			ModelCollider.Enabled=false;
			//Particle.Enabled=false;
			GameObject.Network.Interpolation=false;
			// Every client glues it to the hand itself: synced positions arrive late and drag it behind the hand.
			if (Networking.IsHost) GameObject.Network.Flags|=NetworkFlags.NoTransformSync;
			GameObject.Transform.ClearInterpolation();
			Player.HoldingWeapon=GameObject.Id;
			GameChat.InfoForCaller(PickupInfoChat);
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
			if (Networking.IsHost) GameObject.Network.Flags&=~NetworkFlags.NoTransformSync;
			GameObject.WorldPosition=(Player.WorldPosition+Player.LocalRotation.Forward*20).WithZ(Player.WorldPosition.z+40);

			GameObject.WorldRotation=Player.WorldRotation;
			//Rigidbody.Velocity=GameObject.WorldPosition+=(Holder.WorldRotation.Forward*200).WithZ(-Holder.EyeAngles.pitch*8);
			Holder=default;
		}
	}
	protected override void OnStart()
	{
		base.OnStart();
		// A thrown copy is local to each client.
		if (!Throwed) GameObject.NetworkMode=NetworkMode.Object;
		//if (Particle!=null){Particle.Enabled=false;}
		WorldScale =WeaponScale;
		Popup=Scene.Components.GetInDescendantsOrSelf<InfoPopup>();
		SpawnPosition=GameObject.WorldPosition;
	}
}