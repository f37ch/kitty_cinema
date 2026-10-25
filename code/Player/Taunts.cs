using System.Linq;
/// <summary>A taunt: one of the citizen model's own animations, played over the animgraph.</summary>
public record TauntDef(string Id,string Title,string Icon,string Category,string Sequence,bool Loop=false);

public static class Taunts
{
	public const string Greet="Greet";
	public const string Fun="Fun";
	public const string Poses="Poses";

	public static readonly string[] Categories={Greet,Fun,Poses};

	public static readonly TauntDef[] All=
	{
		new("wave","Wave","waving_hand",Greet,"AvatarMenu_Entry_Wave_01"),
		new("hello","Hello","front_hand",Greet,"AvatarMenu_Entry_Wave_02"),
		new("hey","Hey there","emoji_people",Greet,"AvatarMenu_Entry_Wave_03"),
		new("turn","Show off","360",Greet,"AvatarMenu_Entry_TurnAround"),

		new("dab","Dab","sports_martial_arts",Fun,"Dab"),
		new("flex","Flex","fitness_center",Fun,"AvatarMenu_Entry_Flex"),
		new("jump","Hop","directions_run",Fun,"AvatarMenu_Entry_Jump"),
		new("boxing","Shadow boxing","sports_mma",Fun,"Melee_Punch_Idle_Standing_01",true),
		new("special","Fidget","auto_awesome",Fun,"IdleMenu_Special"),

		new("look","Look around","visibility",Poses,"AvatarMenu_ExamineHead_02"),
		new("outfit","Check outfit","checkroom",Poses,"AvatarMenu_ExamineBody_01"),
		new("hands","Look at hands","back_hand",Poses,"AvatarMenu_ExamineArms_01"),
		new("bored","Bored","hourglass_bottom",Poses,"AvatarMenu_FidgetLarge_06",true),
		new("sit","Sit down","event_seat",Poses,"SitGroundPose_Default",true),
		new("sitrelax","Sit back","weekend",Poses,"SitGroundPose_01",true),
	};

	public static TauntDef Find(string id)=>All.FirstOrDefault(x=>x.Id==id);
}
