using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff // ◄- Pastikan ini TIDAK pakai S
{
    public class HeavyWings : ModBuff // ◄- Pastikan H besar, W besar, ada S nya
    {
        public override string Texture => "TheSanity/Buff/icantfly"; 

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;          
            Main.pvpBuff[Type] = true;         
            Main.buffNoSave[Type] = true;      
            BuffID.Sets.LongerExpertDebuff[Type] = true; 
			Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (Main.rand.NextBool(5))
            {
                Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Ash, 0f, 1f, 150, default, 0.9f);
                d.noGravity = false; 
                d.velocity *= 0.2f;
            }
        }
    }
}