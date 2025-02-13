using Sandbox.UI;
namespace Sandbox;
internal sealed class ScenePanelObject : SceneCustomObject
{
    //
    // Summary:
    //     Global scale for panel rendering within a scene world.

    public const float ScreenToWorldScale = 0.05f;

    //
    // Summary:
    //     The panel that will be rendered.

    public RootPanel Panel { get; private set; }

    public ScenePanelObject(SceneWorld world, RootPanel Panel)
        : base(world)
    {

        this.Panel = Panel;
    }
    public override void RenderSceneObject()
    {
        RenderAttributes attributes = Graphics.Attributes;
        string k = "D_WORLDPANEL";
        int value = 1;
        attributes.SetCombo(in k, in value);
        Matrix value2 = Matrix.CreateRotation(Rotation.From(0f, 90f, 90f));
        value2 *= Matrix.CreateScale(ScreenToWorldScale);
        RenderAttributes attributes2 = Graphics.Attributes;
        k = "WorldMat";
        attributes2.Set(in k, in value2);
        Panel?.RenderManual();
    }
}