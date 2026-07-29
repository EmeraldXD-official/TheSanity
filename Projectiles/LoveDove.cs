using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;

namespace TheSanity
{
    public class LoveDoveNPC : ModNPC
    {
        // --- GANTI KE TEXTURE RELEASE DOVES ---
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.ReleaseDoves}";

        public override void SetStaticDefaults()
        {
            // ReleaseDoves hanya memiliki total 4 frame sheet animasi terbang
            Main.npcFrameCount[NPC.type] = 4;
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 24;

            // [DOVE NPC STATS BALANCING LOCATION]
            NPC.damage = 30;       // Base damage tabrakan burung mini-boss
            NPC.lifeMax = 400;     // Darah badak tetap 400 HP
            NPC.defense = 2;       
            
            NPC.noGravity = true;
            NPC.noTileCollide = true; 
            
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath4;
            NPC.aiStyle = -1; // AI Kustom murni mengejar player
        }

        public override void AI()
        {
            // --- MEKANIK ANIMASI FLAPPING (4 FRAME UNTUK RELEASE DOVES) ---
            NPC.frameCounter++;
            if (NPC.frameCounter >= 5) // Kecepatan kepakan sayap
            {
                NPC.frameCounter = 0;
                NPC.frame.Y += 1; 
                if (NPC.frame.Y >= 4) NPC.frame.Y = 0; // Reset ke frame awal jika menyentuh frame ke-4
            }

            // --- MEKANIK HOMING (MENGEJAR PLAYER TERDEKAT) ---
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (target != null && target.active && !target.dead)
            {
                Vector2 direction = target.Center - NPC.Center;
                direction.Normalize();

                // [DOVE FLY SPEED BALANCING LOCATION]
                float flySpeed = 4.2f; 
                NPC.velocity = Vector2.Lerp(NPC.velocity, direction * flySpeed, 0.03f);
            }

            // Menentukan arah gerak horizontal
            NPC.direction = (NPC.velocity.X > 0) ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.05f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            
            // Pembagi tinggi frame diganti menjadi 4 karena jumlah framenya sekarang ada 4
            int frameHeight = texture.Height / 4;
            Rectangle sourceRectangle = new Rectangle(0, NPC.frame.Y * frameHeight, texture.Width, frameHeight);
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            Color doveWeddingColor = new Color(140, 15, 25);

            // --- FIX TOTAL SPRITE UNTUK MERPATI HADAP KANAN ---
            // Karena sprite asli ReleaseDoves sudah menghadap ke KANAN dari sananya:
            // - Jika bergerak ke KANAN (direction == 1), jangan di-flip (SpriteEffects.None) agar paruh tetap di kanan.
            // - Jika bergerak ke KIRI (direction == -1), kita flip secara horizontal agar paruh berbalik ke kiri.
            SpriteEffects effects = (NPC.direction == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.Draw(
                texture,
                NPC.Center - screenPos,
                sourceRectangle,
                doveWeddingColor,
                NPC.rotation,
                drawOrigin,
                1.3f, 
                effects,
                0f
            );

            return false; 
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 3; i++)
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
            }

            if (NPC.life <= 0)
            {
                for (int i = 0; i < 12; i++)
                {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Blood, 0f, 0f, 100, Color.DarkRed, 1.2f);
                    d.noGravity = true;
                }
            }
        }
    }
}