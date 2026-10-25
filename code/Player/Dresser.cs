public sealed class PlayerDresser : Component,Component.INetworkSpawn
{
	[Property] public SkinnedModelRenderer BodyRenderer {get;set;}

	public void OnNetworkSpawn(Connection owner)
	{
		// The engine's Dresser reads the owner's avatar itself.
		var dresser=Components.GetOrCreate<Dresser>();
		dresser.BodyTarget=BodyRenderer;
		dresser.Source=Dresser.ClothingSource.OwnerConnection;
		_=dresser.Apply();
	}
}
