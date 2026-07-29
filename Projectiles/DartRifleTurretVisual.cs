using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class DartRifleTurretVisual : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.DartRifle}";

        public override void SetDefaults()
        {
            Projectile.width = 38;               
            Projectile.height = 22;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = false;          
            Projectile.penetrate = -1;           
            Projectile.tileCollide = false;      
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;           

            // --- LOKASI BALANCING: UKURAN SPRITE SENJATA ---
            // Mengubah nilai ini untuk memperbesar tampilan senapan di arena (1.6f = 160% lebih besar dari asli)
            Projectile.scale = 1.6f; 
        }

        public override void AI()
        {
            // --- LOGIKA MENEMPEL PADA MIMIC ---
            // Kita mengambil index WHOAMI dari NPC Corrupt Mimic yang disimpan di Projectile.ai[1]
            int ownerNPCIndex = (int)Projectile.ai[1];

            // Validasi apakah NPC Mimic tersebut masih aktif dan ada di game
            if (ownerNPCIndex >= 0 && ownerNPCIndex < Main.maxNPCs && Main.npc[ownerNPCIndex].active && Main.npc[ownerNPCIndex].type == NPCID.BigMimicCorruption)
            {
                NPC mimic = Main.npc[ownerNPCIndex];
                
                // Paksa posisi koordinat senapan agar selalu menempel persis di tengah-tengah tubuh Corrupt Mimic
                Projectile.Center = mimic.Center;
            }
            else
            {
                // Jika Mimic mati atau menghilang, langsung hancurkan senjata visual ini agar tidak melayang sendirian
                Projectile.Kill();
                return;
            }

            // Mengunci target ke Player terdekat untuk rotasi membidik
            Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            
            if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
            {
                Vector2 directionToPlayer = targetPlayer.Center - Projectile.Center;

                // LOGIKA ROTASI AIMING
                Projectile.rotation = directionToPlayer.ToRotation();

                // LOGIKA ARAH HADAP
                Projectile.spriteDirection = (directionToPlayer.X < 0) ? -1 : 1;
            }

            // BEAUTIFIER (EFEK VISUAL STEADY PARTIKEL)
            if (Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(25f, 25f), DustID.Demonite, Vector2.Zero, 150, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Demonite, dustVel, 100, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            SpriteEffects spriteEffects = SpriteEffects.None;
            float rotationOffset = Projectile.rotation;

            if (Projectile.spriteDirection == -1)
            {
                spriteEffects = SpriteEffects.FlipVertically;
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                rotationOffset,
                drawOrigin,
                Projectile.scale,
                spriteEffects,
                0
            );

            return false; 
        }
    }
}