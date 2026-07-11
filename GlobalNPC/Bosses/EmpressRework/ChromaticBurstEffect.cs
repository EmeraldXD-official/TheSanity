using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    // Efek burst pendek yang muncul tiap HallowBossLastingRainbow meledak.
    // Sengaja dibuat lebih redup daripada burst indikator di FairyQueenLance (yang puncaknya 0.4f opacity).
    public class ChromaticBurstEffect : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst";

        private const int Lifetime = 20;      // makin kecil, makin cepat burst-nya menghilang
        private const float MaxOpacity = 0.2f; // opacity puncak, sengaja lebih rendah dari 0.4f punya lance
        private const float StartScale = 1.6f;
        private const float EndScale = 0.1f;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lifetime;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // Diam di tempat, cuma efek visual
            Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            float progress = 1f - (Projectile.timeLeft / (float)Lifetime);
            float scale = MathHelper.Lerp(StartScale, EndScale, progress);
            float opacity = MathHelper.Lerp(MaxOpacity, 0f, progress);

            float hue = Main.GlobalTimeWrappedHourly * 0.25f % 1f;
            Color burstColor = Main.hslToRgb(hue, 0.55f, 0.8f) * opacity;
            burstColor.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, burstColor, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
