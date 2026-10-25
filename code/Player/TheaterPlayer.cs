using System.Linq;
using Sandbox.Citizen;
using Sandbox.UI;
public sealed class TheaterPlayer : Component
{
	[Property][Category("Components")] private GameObject Camera {get;set;}
	[Property][Category("Components")] private CharacterController Controller {get;set;}
	[Property][Category("Components")] private CitizenAnimationHelper Animator {get;set;}
	[Property][Category("Stats")][Step(1f)] private float WalkSpeed {get;set;}=120f;
	[Property][Category("Stats")][Step(1f)] private float RunSpeed {get; set;}=250f;
	[Property][Category("Stats")][Step(10f)] private float JumpStrength {get; set;}=400f;
	[Property][Category("Stats")][Step(5f)] private float UseRange {get;set;}=80f;
	[Property][Category("Stats")] private Vector3 Gravity {get;set;}
	[Property] public float NoclipSpeed {get;set;}=1000f;
	[Property] private Vector3 EyePosition {get;set;}
	[Property] private Angles SpawnAngles {get;set;}
	[Property] private bool ThirdPerson {get;set;}
	[Sync] private bool IsRunning {get;set;}
	[Sync] private float DuckLevel {get;set;}
	[Sync] public Guid HoldingWeapon {get;set;}
	[Sync] public Angles EyeAngles {get;set;}
	[Sync] public string Location {get;set;}="Unknown";
	[Sync] private Rotation CameraRotation {get;set;}
	[Sync] public bool IsNoclipping {get;set;}
	[Sync] public RealTimeSince TimeSinceSpray {get;set;}=10;
	/// <summary>Id of the playing taunt (see <see cref="Taunts"/>), null when none.</summary>
	[Sync] public string Taunt {get;set;}
	/// <summary>Bumped on every start, so the same taunt can be replayed.</summary>
	[Sync] private int TauntSerial {get;set;}
	public bool IsTaunting=>!string.IsNullOrEmpty(Taunt);
	/// <summary>This client owned the player when it started, so it's the one this client plays as.</summary>
	private bool StartedLocal;
	/// <summary>
	/// Its player left (e.g. the old host) and ownership moved to us: the object is ours now but isn't our player,
	/// driving it would take over our camera.
	/// </summary>
	private bool IsOrphaned=>!IsProxy&&!StartedLocal;
	/// <summary>The player this client plays as (not a leftover handed to us by a host change).</summary>
	public bool IsLocalPlayer=>!IsProxy&&StartedLocal;
	/// <summary>
	/// The view camera, on the prefab's camera object: every player carries one, only the local player's is on,
	/// so it goes away with its player (e.g. the old host leaving).
	/// </summary>
	private CameraComponent ViewCamera=>Camera?.Components.Get<CameraComponent>(FindMode.EverythingInSelf);
	/// <summary>Drops a leftover player, it would take over the local camera and input.</summary>
	private bool CheckOrphaned()
	{
		if (!IsOrphaned) return false;
		Log.Info($"{GameObject.Name} was left behind by its player, removing it.");
		GameObject.Destroy();
		return true;
	}
	private string AppliedTaunt;
	private int AppliedSerial=-1;
	/// <summary>Distance of the orbit camera while taunting.</summary>
	private const float TauntCameraDistance=110f;
	private Vector3 EyeWorldPosition=>Transform.Local.PointToWorld(EyePosition);
	private Transform StartCameraTransform;
	private Vector3 WishVelocity;
	public string IDHashed {get;set;}="Unknown";
	protected override void DrawGizmos()
	{
		if (!Gizmo.IsSelected) return;
		var draw=Gizmo.Draw;
		draw.LineSphere(EyePosition,10f);
	}
	[Rpc.Broadcast]
	public void OnThrow()
	{
		Animator.Target.Set("b_attack",true);
	}
	/// <summary>Local player: plays a taunt; any movement stops it.</summary>
	public void StartTaunt(string id)
	{
		if (IsProxy||Taunts.Find(id)==null) return;
		if (GameObject.Parent.Components.TryGet<ChairController>(out _)) return;
		if (Controller!=null&&!Controller.IsOnGround) return;
		Taunt=id;
		TauntSerial++;
	}
	public void StopTaunt()
	{
		if (IsProxy||!IsTaunting) return;
		Taunt=null;
	}
	/// <summary>Every client: plays the synced taunt on the model, the animgraph takes over again after it.</summary>
	private void ApplyTaunt()
	{
		var body=Animator?.Target;
		if (body==null) return;
		if (Taunt==AppliedTaunt&&TauntSerial==AppliedSerial) return;
		AppliedTaunt=Taunt;
		AppliedSerial=TauntSerial;
		var def=Taunts.Find(Taunt);
		if (def==null){
			body.UseAnimGraph=true;
			return;
		}
		body.UseAnimGraph=false;
		body.Sequence.Name=def.Sequence;
		body.Sequence.Looping=def.Loop;
		body.Sequence.Time=0;
	}
	/// <summary>Local player: a taunt ends when it finishes or the player does anything else.</summary>
	private void CheckTauntEnd()
	{
		if (!IsTaunting) return;
		var def=Taunts.Find(Taunt);
		bool moved=!Input.AnalogMove.IsNearlyZero(0.1f)||Input.Down("Jump")||Input.Down("Duck")||Input.Pressed("attack1")||Input.Pressed("attack2")||IsNoclipping;
		bool finished=def==null||(!def.Loop&&AppliedTaunt==Taunt&&Animator?.Target!=null&&Animator.Target.Sequence.IsFinished);
		if (moved||finished) StopTaunt();
	}
	[Rpc.Broadcast(NetFlags.HostOnly)]
	public void UpdateNoclip()
	{
		IsNoclipping=!IsNoclipping;
		if (IsNoclipping) Controller.IgnoreLayers.Add("solid");
		else Controller.IgnoreLayers.Remove("solid");
		if (Rpc.Caller==Connection.Local){
			Sandbox.Services.Achievements.Unlock("fly");
		}
	}
	protected override void OnUpdate()
	{
		if (CheckOrphaned()) return;
		if (!IsProxy){
			CameraRotation=Camera.WorldRotation;
			DuckLevel=MathX.Lerp(Animator.DuckLevel,Input.Down("Duck")?1:0,Time.Delta*10.0f);
			//chair stuff
			EyeAngles+=Input.AnalogLook;
			EyeAngles=EyeAngles.WithPitch(MathX.Clamp(EyeAngles.pitch,-80f,80f));
			//
			if (HoldingWeapon!=default){
				if (Input.Pressed("drop")){
					if (Scene.Directory.FindByGuid(HoldingWeapon).Components.TryGet<BaseWeapon>(out var weapon)){
						weapon?.Drop();
					}
				}else if(Input.Pressed("attack2")){
					if (Scene.Directory.FindByGuid(HoldingWeapon).Components.TryGet<BaseWeapon>(out var weapon)){
						weapon?.Throw();
						OnThrow();
						Animator.Target.Set("b_attack",true);
					}
				}
			}
			IsRunning =Input.Down("Run");
			if ( Input.Pressed( "noclip" ) )
			{
				UpdateNoclip();
			}
			if (Input.Pressed("spray"))
			{
				if (TimeSinceSpray>10){
					if (Spray.Place(EyeWorldPosition,EyeAngles.Forward*UseRange)){
						TimeSinceSpray=0;
					}
				}else{
					GameChat.Info("Wait atleast 10 seconds before applying next spray.");
				}
			}
			CheckTauntEnd();
		}
		ApplyTaunt();
		if ( Animator != null && Controller != null )
		{
			Animator.WithVelocity( Controller.Velocity );
			Animator.WithWishVelocity( WishVelocity );
			Animator.IsGrounded = Controller.IsOnGround;
			Animator.WithLook( CameraRotation.Forward, 1, 1, 1.0f );
			Animator.MoveStyle = IsRunning ? CitizenAnimationHelper.MoveStyles.Run : CitizenAnimationHelper.MoveStyles.Walk;
			Animator.DuckLevel = DuckLevel;
			Animator.HoldType = HoldingWeapon != default ? CitizenAnimationHelper.HoldTypes.Pistol : CitizenAnimationHelper.HoldTypes.None;
			Animator.Handedness = CitizenAnimationHelper.Hand.Right;
			Animator.IsNoclipping = IsNoclipping;
			//model.SceneModel.DirectPlayback.Play("HoldItem_RH_Hand_Basic");
			//Animator.Target.Set("holdtype_pose",2);
		}
	}
	private void CheckUsable(){
		var Trace=Scene.Trace
		.Sphere(10,EyeWorldPosition,EyeWorldPosition+EyeAngles.Forward*UseRange)
		.WithoutTags("player")
		.IgnoreGameObjectHierarchy(GameObject)
		.Run();
		if (Trace.Hit){
			if (Trace.GameObject.Components.TryGet<ChairController>(out var Chair)){
				if (Input.Pressed("use")){
					Chair.Sit(GameObject.Id);
				}
				if (GameObject.Parent!=Chair.GameObject){
					Chair.Popup.Text=Chair.ChairInfo;
					Chair.Popup.PopTime=Time.Now+.1f;
				}
			}
			if (Trace.GameObject.Components.TryGet<BaseWeapon>(out var weapon)){
				if (weapon.Throwed) return;
				if (Input.Pressed("use")){
					weapon.PickUp(GameObject.Id);
				}
				if (weapon.GameObject.Parent.Name!="hold_R"){
					weapon.Popup.Text=weapon.PickupInfo;
					weapon.Popup.PopTime=Time.Now+.1f;
				}
			}
		}
	}
	private void ThirdPersonCheck(){
		if (Input.Pressed("ToggleView")){
			if (ThirdPerson){
				ThirdPerson=false;
			}else{
				ThirdPerson=true;
			}
			return;
		}
	}
	private void UpdateCamera(ChairController ChairController, ChairPlace Place=new()){
		ThirdPersonCheck();
		if (Camera!=null)
		{
			if (ChairController!=null){//chair stuff
				var cameraTransform=Transform.Local.WithPosition(Place.eyepos+Transform.Local.Backward*80).RotateAround(Place.eyepos,EyeAngles);
				var cameraPosition=ThirdPerson?ChairController.Transform.Local.PointToWorld(cameraTransform.Position):ChairController.Transform.Local.PointToWorld(ChairController.Transform.Local.WithPosition(Place.eyepos).Position);
				var cameraTrace=Scene.Trace.Ray(WorldPosition.WithZ(70),cameraPosition)
					.Size(5f)
					.IgnoreGameObjectHierarchy(GameObject)
					.WithoutTags(IDHashed,"weapon")
					.Run();
				Camera.WorldPosition=cameraTrace.EndPosition;
				Camera.LocalRotation=cameraTransform.Rotation;
			}else if (IsTaunting){
				// Orbit the player with the mouse while they taunt.
				var pivot=WorldPosition+Vector3.Up*50;
				var rotation=EyeAngles.ToRotation();
				var cameraTrace=Scene.Trace.Ray(pivot,pivot-rotation.Forward*TauntCameraDistance)
					.Size(5f)
					.IgnoreGameObjectHierarchy(GameObject)
					.WithoutTags(IDHashed,"weapon","player")
					.Run();
				Camera.WorldPosition=cameraTrace.EndPosition;
				Camera.WorldRotation=rotation;
			}else{
				var cameraTransform=StartCameraTransform.RotateAround(EyePosition,EyeAngles.WithYaw(0f));
				var cameraPosition=ThirdPerson?Transform.Local.PointToWorld(cameraTransform.Position):EyeWorldPosition;
				cameraPosition=cameraPosition.WithZ((Animator.DuckLevel>0)?cameraPosition.z-20*Animator.DuckLevel:cameraPosition.z);//duck
				var cameraTrace=Scene.Trace.Ray(EyeWorldPosition,cameraPosition)
					.Size(5f)
					.IgnoreGameObjectHierarchy(GameObject)
					.WithoutTags(IDHashed,"weapon")
					.Run();
				Camera.WorldPosition=cameraTrace.EndPosition;
				Camera.LocalRotation=cameraTransform.Rotation;
			}
			var cam=ViewCamera;
			if (cam.IsValid()){
				if (ThirdPerson||IsTaunting){
					if (cam.RenderExcludeTags.Has(IDHashed)){
						cam.RenderExcludeTags.Remove(IDHashed);
					}
				}else{
					if (!cam.RenderExcludeTags.Has(IDHashed)){
						cam.RenderExcludeTags.Add(IDHashed);
					}
				}
			}
		}
	}
	[Rpc.Broadcast]
	public void OnJump()
	{
		Animator?.TriggerJump();
	}
	protected override void OnFixedUpdate()
	{
		if (IsOrphaned) return;
		//check chair stuff before return
		if (GameObject.Parent.Components.TryGet<ChairController>(out var ChairController)){
			if (!IsProxy) {
				foreach (var place in ChairController.UsingList){
					if (place.user==GameObject.Id){
						Transform.Local=ChairController.Transform.Local.WithPosition(place.sitpos);
						WorldRotation=ChairController.WorldRotation;
						UpdateCamera(ChairController,place);
					}
				}
				if (Input.Pressed("Jump")){
					ChairController.Leave(GameObject.Id);
				}
			}
			Animator.IsGrounded=true;
			if (Animator==null) return;
			Animator.Sitting=CitizenAnimationHelper.SittingStyle.Chair1;
			if (Controller==null) return;
			Controller.IsOnGround=true;
			Controller.Velocity=default;
			WishVelocity=default;
			return;
		}else{
			if (!IsProxy) {
				// While taunting the mouse orbits the camera, the body keeps facing where it was.
				if (!IsTaunting) WorldRotation=Rotation.FromYaw(EyeAngles.yaw);
				UpdateCamera(ChairController);
			}
			if (Animator==null) return;
			Animator.Sitting=CitizenAnimationHelper.SittingStyle.None;
		}
		if (IsProxy) return;////////////
		BuildWishVelocity();
		if (Controller.IsOnGround&&Input.Down("Jump"))
		{
			float flGroundFactor=1;
			float flMul=268*1.2f;
			Controller.Punch(Vector3.Up*flMul*flGroundFactor);
			OnJump();
		}

		if (Controller.IsOnGround)
		{
			Controller.Velocity=Controller.Velocity.WithZ(0);
			Controller.Accelerate(WishVelocity);
			Controller.ApplyFriction(4);
		}
		else
		{
			if (!IsNoclipping)
			{
				Controller.Velocity-=Gravity*Time.Delta*.5f;
			}
			Controller.Accelerate(WishVelocity.ClampLength(50));
			Controller.ApplyFriction(0.1f);
		}
		Controller.Move();
		if (!Controller.IsOnGround)
		{
			if (!IsNoclipping)
			{
				Controller.Velocity-=Gravity*Time.Delta*0.5f;
			}
		}
		else
		{
			Controller.Velocity=Controller.Velocity.WithZ(0);
		}
		if (IsNoclipping)
		{
			var vertical=0f;
			if (Input.Down("Jump")) vertical=1f;
			if (Input.Down("Duck")) vertical=-1f;

			Controller.IsOnGround=false;
			Controller.Velocity=EyeAngles.ToRotation()*Input.AnalogMove*NoclipSpeed;
			Controller.Velocity+=Vector3.Up*vertical*NoclipSpeed;
		}
		CheckUsable();
	}
	public void BuildWishVelocity()
	{
		var rot=EyeAngles.ToRotation();
		WishVelocity=rot*Input.AnalogMove;
		WishVelocity=WishVelocity.WithZ(0);
		if ( WishVelocity == Vector3.Zero ) return ;
		if (!WishVelocity.IsNearZeroLength)WishVelocity=WishVelocity.Normal;
		if (Input.Down("Run")&&!Input.Down("Duck")&&!Input.Down("Walk")){
			WishVelocity*=RunSpeed;
		}else if(Input.Down("Walk")&&!Input.Down("Duck")){
			WishVelocity*=80;
		}else{
			WishVelocity*=120;
		}
	}
	protected override void OnStart()
	{
		base.OnStart();
		EyeAngles=SpawnAngles;//i dunno why setting rotation in player prefab didnt work tho...
		if (IsProxy){
			// Someone else's player: its camera may arrive switched on (the old host's, after a handoff).
			if (ViewCamera.IsValid()) ViewCamera.Enabled=false;
			return;
		}
		StartedLocal=true;
		if (Camera!=null) StartCameraTransform=Camera.Transform.Local;
		IDHashed=GameObject.Id.GetHashCode().ToString();//for now it only needs for render exclude camera tag
		Tags.Add(IDHashed);
		var view=ViewCamera;
		if (view.IsValid()){
			view.RenderExcludeTags.RemoveAll();
			view.Enabled=true;
		}
		Sandbox.Services.Achievements.Unlock("newbie");
		Settings.Load();
	}
	protected override void OnEnabled()
	{
		base.OnEnabled();
	}
	
	protected override void OnDisabled()
	{
		base.OnDisabled();
	}
	protected override void OnDestroy()
	{
		base.OnDestroy();
	}
}