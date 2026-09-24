using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.IceWand
{
    // Sprite block es yang di-upload (voxel ice block) taruh sebagai
    // Projectiles/IceBlockIndicator.png di folder yang sama dengan file ini.
    // ai[0] = progress "muncul" (0 - 1, diisi oleh IceChargeController)
    // ai[1] = index NPC yang diikuti (mengambang di atas kepalanya)
    public class IceBlockIndicator : ModProjectile
    {
        // Ukuran akhir sprite pas udah full charge (1f = ukuran asli texture, diperkecil biar
        // ga kegedean pas ngambang di atas kepala musuh). Samain angka ini sama Projectile.scale
        // di IceBlockProjectile.cs biar pas blok mulai jatuh ukurannya nyambung mulus, ga ada "loncatan".
        private const float MaxScale = 0.6f;

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255; // mulai transparan penuh
        }

        public override void AI()
        {
            int npcIndex = (int)Projectile.ai[1];
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
            {
                NPC npc = Main.npc[npcIndex];
                if (npc.active)
                {
                    Vector2 target = npc.Top - new Vector2(0f, 60f);
                    Projectile.Center = Vector2.Lerp(Projectile.Center, target, 0.15f);
                }
                else
                {
                    // target mati sebelum charge selesai -> ikut hilang
                    Projectile.Kill();
                    return;
                }
            }

            float progress = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
            Projectile.alpha = (int)(255 * (1f - progress));
            Projectile.scale = MathHelper.Lerp(0.1f, MaxScale, progress); // "muncul dari tengah"

            if (progress > 0.05f && Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 0.9f);
                d.velocity *= 0.3f;
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float fade = 1f - Projectile.alpha / 255f;
            Color color = Color.Lerp(Color.White, new Color(150, 220, 255), 0.5f) * fade;

            // glow tipis di belakang sewaktu masih dalam transisi muncul
            if (Projectile.scale < 0.95f)
            {
                Color glow = new Color(180, 230, 255, 0) * (0.5f * fade);
                Main.EntitySpriteDraw(texture, drawPos, null, glow, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}