using System.Linq;
using Sandbox.Citizen;
public sealed class TheaterPlayer : Component
{
	[Property][Category("Components")] private GameObject Camera {get;set;}
	[Property][Category("Components")] private CharacterController Controller {get;set;}
	[Property][Category("Components")] private CitizenAnimationHelper Animator {get;set;}
	[Property][Category("Stats")][Range(0f,400f,1f)] private float WalkSpeed {get;set;}=120f;
	[Property][Category("Stats")][Range(0f,800f,1f)] private float RunSpeed {get; set;}=250f;
	[Property][Category("Stats")][Range(0f,1000f,10f)] private float JumpStrength {get; set;}=400f;
	[Property][Category("Stats")][Range(0f,200f,5f)] private float UseRange {get;set;}=80f;
	[Property][Category("Stats")] private Vector3 Gravity {get;set;}
	[Property] private Vector3 EyePosition {get;set;}
	[Property] private Angles SpawnAngles {get;set;}
	[Property] private bool ThirdPerson {get;set;}
	[Sync] private bool IsRunning {get;set;}
	[Sync] private float DuckLevel {get;set;}
	[Sync] public Guid HoldingWeapon {get;set;}
	[Sync] public Angles EyeAngles {get;set;}
	[Sync] public string Location {get;set;}="Unknown";
	[Sync] private Rotation CameraRotation {get;set;}
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
	[Broadcast]
	public void OnThrow()
	{
		Animator.Target.Set("b_attack",true);
	}
	protected override void OnUpdate()
	{
		if (!IsProxy){
			//!
			Scene.Camera.Transform.Position=Camera.Transform.Position;
			Scene.Camera.Transform.Rotation=Camera.Transform.Rotation;
			CameraRotation=Camera.Transform.Rotation;
			DuckLevel=MathX.Lerp(Animator.DuckLevel,Input.Down("Duck")?1:0,Time.Delta*10.0f);
			//chair stuff
			EyeAngles+=Input.AnalogLook;
			EyeAngles=EyeAngles.WithPitch(MathX.Clamp(EyeAngles.pitch,-80f,80f));
			GameObject.Parent.Components.TryGet<ChairController>(out var ChairController);
			if (ChairController!=null){
				foreach (var place in ChairController.UsingList){
					if (place.user==GameObject.Id){
						Transform.Local=ChairController.Transform.Local.WithPosition(place.sitpos);
						Transform.Rotation=ChairController.Transform.Rotation;
						UpdateCamera(ChairController,place);
					}
				}
			}else{
				Transform.Rotation=Rotation.FromYaw(EyeAngles.yaw);
				UpdateCamera(ChairController);
			}
			//
			if (HoldingWeapon!=default){
				if (Input.Pressed("drop")){
					if (Scene.Directory.FindByGuid(HoldingWeapon).Components.TryGet<BaseWeapon>(out var weapon)){
						weapon?.Drop();
					}
				}else if(Input.Pressed("attack2")){
					if (Scene.Directory.FindByGuid(HoldingWeapon).Components.TryGet<BaseWeapon>(out var weapon)){
						_=weapon?.Throw();
						OnThrow();
						Animator.Target.Set("b_attack",true);
					}
				}
			}
			IsRunning=Input.Down("Run");
		}
		if (Animator!=null&&Controller!=null)
		{
			Animator.WithVelocity(Controller.Velocity);
			Animator.WithWishVelocity(WishVelocity);
			Animator.IsGrounded=Controller.IsOnGround;
			Animator.WithLook(CameraRotation.Forward,1,1,1.0f);
			Animator.MoveStyle=IsRunning?CitizenAnimationHelper.MoveStyles.Run:CitizenAnimationHelper.MoveStyles.Walk;
			Animator.DuckLevel=DuckLevel;
			Animator.HoldType=HoldingWeapon!=default?CitizenAnimationHelper.HoldTypes.Pistol:CitizenAnimationHelper.HoldTypes.None;
			Animator.Handedness=CitizenAnimationHelper.Hand.Right;
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
				var cameraTrace=Scene.Trace.Ray(Transform.Position.WithZ(70),cameraPosition)
					.Size(5f)
					.IgnoreGameObjectHierarchy(GameObject)
					.WithoutTags(IDHashed,"weapon")
					.Run();
				Camera.Transform.Position=cameraTrace.EndPosition;
				Camera.Transform.LocalRotation=cameraTransform.Rotation;
			}else{
				var cameraTransform=StartCameraTransform.RotateAround(EyePosition,EyeAngles.WithYaw(0f));
				var cameraPosition=ThirdPerson?Transform.Local.PointToWorld(cameraTransform.Position):EyeWorldPosition;
				cameraPosition=cameraPosition.WithZ((Animator.DuckLevel>0)?cameraPosition.z-20*Animator.DuckLevel:cameraPosition.z);//duck
				var cameraTrace=Scene.Trace.Ray(EyeWorldPosition,cameraPosition)
					.Size(5f)
					.IgnoreGameObjectHierarchy(GameObject)
					.WithoutTags(IDHashed,"weapon")
					.Run();
				Camera.Transform.Position=cameraTrace.EndPosition;
				Camera.Transform.LocalRotation=cameraTransform.Rotation;
			}
			var cam=Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if (cam!=null){
				if (ThirdPerson){
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
	[Broadcast]
	public void OnJump()
	{
		Animator?.TriggerJump();
	}
	protected override void OnFixedUpdate()
	{
		//check chair stuff before return
		if (GameObject.Parent.Components.TryGet<ChairController>(out var ChairController)){
			if (!IsProxy) {
				if (Input.Pressed("Jump")){
					ChairController.Leave(GameObject.Id);
				}
			}
			Animator.IsGrounded=true;
			if (Animator==null) return;
			Animator.Sitting=CitizenAnimationHelper.SittingStyle.Chair;
			if (Controller==null) return;
			Controller.IsOnGround=true;
			Controller.Velocity=default;
			WishVelocity=default;
			return;
		}else{
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
			Controller.Velocity-=Gravity*Time.Delta*.5f;
			Controller.Accelerate(WishVelocity.ClampLength(50));
			Controller.ApplyFriction(0.1f);
		}
		Controller.Move();
		if (!Controller.IsOnGround)
		{
			Controller.Velocity-=Gravity*Time.Delta*.5f;
		}
		else
		{
			Controller.Velocity=Controller.Velocity.WithZ(0);
		}
		CheckUsable();
	}
	public void BuildWishVelocity()
	{
		var rot=EyeAngles.ToRotation();
		WishVelocity=rot*Input.AnalogMove;
		WishVelocity=WishVelocity.WithZ(0);
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
		if (IsProxy) return;
		if (Camera!=null) StartCameraTransform=Camera.Transform.Local;
		IDHashed=GameObject.Id.GetHashCode().ToString();//for now it only needs for render exclude camera tag
		Tags.Add(IDHashed);
		var cam=Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		cam.RenderExcludeTags.RemoveAll();
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