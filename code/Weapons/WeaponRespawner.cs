using System.Linq;
public class WeaponRespawner: Component
{
	[Property] GameObject  PopcornPrefab {get;set;}
    [Property] GameObject  SodaPrefab {get;set;}
    [Sync] private NetDictionary<Vector3,Guid> PopCornWeapons {get;set;}
    [Sync] private NetDictionary<Vector3,Guid> SodaWeapons {get;set;}
    private RealTimeSince LastChecked;
    private RealTimeSince LastPosChecked;
    protected override void OnFixedUpdate()
	{
        if (!Connection.Local.IsHost) return;
        if (LastChecked>5){
            if (PopcornPrefab!=null){
                foreach(var weapon in PopCornWeapons){
                    if (Scene.Directory.FindByGuid(weapon.Value)==default){
                        var popcorn=PopcornPrefab.Clone(weapon.Key);
		                popcorn.NetworkSpawn();
                        popcorn.Network.DropOwnership();
                        PopCornWeapons[weapon.Key]=popcorn.Id;
                    }
                }
            }
            if (SodaPrefab!=null&&SodaWeapons!=null){
                foreach(var weapon in SodaWeapons){
                    if (Scene.Directory.FindByGuid(weapon.Value)==default){
                        var soda=SodaPrefab.Clone(weapon.Key);
		                soda.NetworkSpawn();
                        soda.Network.DropOwnership();
                        SodaWeapons[weapon.Key]=soda.Id;
                    }
                }
            }
            LastChecked=0;
        }
        if (LastPosChecked>60){
            if (SodaPrefab!=null&&SodaWeapons!=null){
                foreach(var weapon in SodaWeapons){
                    var soda=Scene.Directory.FindByGuid(weapon.Value);
                    if (soda!=default&&soda.Parent==soda.Scene&&!soda.WorldPosition.AlmostEqual(weapon.Key)){
                        soda.WorldPosition=weapon.Key;
                        soda.WorldRotation=Angles.Zero;
                    }
                }
            }
            if (PopcornPrefab!=null&&PopCornWeapons!=null){
                foreach(var weapon in PopCornWeapons){
                    var popcorn=Scene.Directory.FindByGuid(weapon.Value);
                    if (popcorn!=default&&popcorn.Parent==Scene&&!popcorn.WorldPosition.AlmostEqual(weapon.Key)){
                        popcorn.WorldPosition=weapon.Key;
                        popcorn.WorldRotation=Angles.Zero;
                    }
                }
            }
            LastPosChecked=0;
        }
        base.OnFixedUpdate();
    }
    protected override void OnAwake()
	{
        if (IsProxy) return;
        if (PopCornWeapons==null){
            PopCornWeapons=new();
            foreach(var weapon in Game.ActiveScene.Components.GetAll<BaseWeapon>().Where(x=>x.WeaponName=="Popcorn")){
                PopCornWeapons?.Add(weapon.WorldPosition,weapon.GameObject.Id);
            }
        }
        if (SodaWeapons==null){
            SodaWeapons=new();
            foreach(var weapon in Game.ActiveScene.Components.GetAll<BaseWeapon>().Where(x=>x.WeaponName=="Soda")){
                SodaWeapons?.Add(weapon.WorldPosition,weapon.GameObject.Id);
            }
        }
        base.OnAwake();
    }
}