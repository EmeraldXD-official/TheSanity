using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public class PlutoTail : PlutoBody
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoTail";

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 52;
            NPC.height = 52;
            NPC.defense = 40;   
            NPC.damage = 33;    
            NPC.scale = 1.5f; 
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}