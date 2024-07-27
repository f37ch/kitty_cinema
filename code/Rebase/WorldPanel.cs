//
// Summary:
//     An interactive 2D panel rendered in the 3D world.

public class WorldPanel : Sandbox.UI.RootPanel
{
    public static float ScreenToWorldScale => 0.05f;
    public Vector2 MousePosActual {get; set;}

    //
    // Summary:
    //     Scene object that renders the panel.
    private ScenePanelObject SceneObject { get; set; }

    //
    // Summary:
    //     Transform of the world panel in 3D space.
    public Transform Transform
    {
        get
        {
            return SceneObject.Transform;
        }
        set
        {
            SceneObject.Transform = value;
        }
    }

    //
    // Summary:
    //     Tags that are applied to the underlying SceneObject
    public ITagSet Tags => SceneObject.Tags;

    //
    // Summary:
    //     Position of the world panel in 3D space.
    public Vector3 Position
    {
        get
        {
            return Transform.Position;
        }
        set
        {
            Transform = Transform.WithPosition(value);
        }
    }

    //
    // Summary:
    //     Rotation of the world panel in 3D space.
    public Rotation Rotation
    {
        get
        {
            return Transform.Rotation;
        }
        set
        {
            Transform = Transform.WithRotation(value);
        }
    }

    //
    // Summary:
    //     Scale of the world panel in 3D space.
    public float WorldScale
    {
        get
        {
            return Transform.UniformScale;
        }
        set
        {
            Transform = Transform.WithScale(value);
        }
    }

    // 
    // Summary:
    //     Maximum distance at which a player can interact with this world panel.
    public float MaxInteractionDistance { get; set; }

    public WorldPanel(SceneWorld world)
    {
        ArgumentNullException.ThrowIfNull(world, "world");
        SceneObject = new ScenePanelObject(world, this);
        SceneObject.Flags.IsOpaque = false;
        SceneObject.Flags.IsTranslucent = true;
        RenderedManually = true;
        float x = -500f;
        float y = -500f;
        float width = 1000f;
        float height = 1000f;
        PanelBounds = new Rect(in x, in y, in width, in height);
        Scale = 2f;
        MaxInteractionDistance = 1000f;
        IsWorldPanel = true;
    }

    //
    // Summary:
    //     Update the bounds for this panel. We purposely do nothing here because on world
    //     panels you can change the bounds by setting Sandbox.UI.RootPanel.PanelBounds.
    protected override void UpdateBounds(Rect rect)
    {
        if (!SceneObject.IsValid()) return;
        var right = Rotation.Right;
		var down = Rotation.Down;
		var panelBounds = PanelBounds;
		//
		// Work out the bounds by adding each corner to a bbox
		//
        Vector3 center=right*panelBounds.Left+down*panelBounds.Top;
		BBox bounds=BBox.FromPositionAndSize(center);
		bounds=bounds.AddPoint( right * panelBounds.Left + down * panelBounds.Bottom );
		bounds=bounds.AddPoint( right * panelBounds.Right + down * panelBounds.Top );
		bounds=bounds.AddPoint( right * panelBounds.Right + down * panelBounds.Bottom );
		SceneObject.Bounds=bounds+Position;
    }

    //
    // Summary:
    //     We override this to prevent the scale automatically being set based on screen
    //     size changing.. because that's obviously not needed here.
    protected override void UpdateScale(Rect screenSize)
    {
    }

    public override void Delete(bool immediate = false)
    {
        base.Delete(immediate);
    }

    public override void OnDeleted()
    {
        base.OnDeleted();
        SceneObject?.Delete();
        SceneObject = null;
    }

    public override bool RayToLocalPosition( Ray ray, out Vector2 position, out float distance )
	{
		position = default;
		distance = 0;
		var plane = new Plane( Position, Rotation.Forward );
		var pos = plane.Trace( ray, false, MaxInteractionDistance );
		if ( !pos.HasValue ){
	        return false;
        }

		distance = Vector3.DistanceBetween( pos.Value, ray.Position );
		if ( distance < 1 ){
	        return false;
        }
		

		
		// to local coords
		var localPos3 = Transform.PointToLocal( pos.Value );
		var localPos = new Vector2( localPos3.y, -localPos3.z );
		// convert to screen coords
		localPos *= (1.0f/ScenePanelObject.ScreenToWorldScale);//fixed /WorldScale???
		if ( !IsInside( localPos ) ){
	        return false;
        }

		MousePosActual=localPos;
		position = localPos;
		return true;
	}


}