using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class SoulFisherHook : ModProjectile
    {
        // REKUES SPRITE KUSTOM: Menggunakan file Tentacle.png di dalam folder Projectiles mod kamu
        public override string Texture => "TheSanity/Projectiles/Tentacle";

        private bool isPullingPlayer = false;
        private int pullingTargetIndex = -1;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;  // Sesuaikan width/height dengan ukuran resolusi asli sprite Tentacle kamu
            Projectile.height = 30;
            
            Projectile.aiStyle = -1; 
            Projectile.hostile = true; 
            Projectile.friendly = false;
            
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1; 
            Projectile.timeLeft = 240; 

            Projectile.scale = 1f; 
        }

        public override bool CanHitPlayer(Player target)
        {
            // Tetap dipaksa true agar hitbox kustom tentakel ini mengunci target player saat bersentuhan
            return true;
        }

        public override void AI()
        {
            int ownerNPCIndex = (int)Projectile.ai[1];
            if (ownerNPCIndex < 0 || ownerNPCIndex >= Main.maxNPCs || !Main.npc[ownerNPCIndex].active)
            {
                Projectile.Kill(); 
                return;
            }

            NPC ownerNPC = Main.npc[ownerNPCIndex];

            // ---------------------------------------------------------------------
            // FIX ROTASI: MENYESUAIKAN SPRITE YANG MENGHADAP KIRI (FRONT FACING LEFT)
            // ---------------------------------------------------------------------
            if (!isPullingPlayer && Projectile.velocity != Vector2.Zero)
            {
                // Ditambah MathHelper.Pi (180 derajat) karena arah default sprite kamu menghadap ke kiri,
                // sehingga saat velocity bergerak ke kanan (0 rad), sprite otomatis berputar balik ke arah depan dengan benar.
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            }

            // ---------------------------------------------------------------------
            // MEKANIK PULLER: MENARIK PLAYER KE ENEMY
            // ---------------------------------------------------------------------
            if (isPullingPlayer && pullingTargetIndex != -1)
            {
                Player player = Main.player[pullingTargetIndex];

                if (player.active && !player.dead)
                {
                    Projectile.Center = player.Center;

                    Vector2 pullDirection = ownerNPC.Center - player.Center;
                    float distance = pullDirection.Length();

                    if (distance < 42f) 
                    {
                        Projectile.Kill();
                        return;
                    }

                    pullDirection.Normalize();

                    // --- LOKASI BALANCING: KECEPATAN TARIKAN TENTAKEL ---
                    float pullSpeed = 14f; 
                    player.velocity = pullDirection * pullSpeed;

                    // Mengunci rotasi tentakel menghadap ke arah musuh (titik penarik) saat menyeret player
                    Projectile.rotation = pullDirection.ToRotation() + MathHelper.Pi;
                }
                else
                {
                    Projectile.Kill();
                }
            }

            // Partikel hijau (DustID.TerraBlade) agar serasi dengan warna hijau rantai dan tentakel hantu
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TerraBlade, 0f, 0f, 100, Color.White, 0.8f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (!isPullingPlayer)
            {
                isPullingPlayer = true;
                pullingTargetIndex = target.whoAmI;
                Projectile.timeLeft = 90; // Durasi cengkeraman tarikan tentakel (1.5 detik)
            }
        }

        // =========================================================================
        // INTEGRASI VISUAL TALI: MENGGUNAKAN CHAIN 27 (RANTAI VINE HIJAU)
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            int ownerNPCIndex = (int)Projectile.ai[1];
            if (ownerNPCIndex < 0 || ownerNPCIndex >= Main.maxNPCs || !Main.npc[ownerNPCIndex].active)
                return true;

            Vector2 anchorCenter = Main.npc[ownerNPCIndex].Center; 
            Vector2 currentPosition = Projectile.Center;           

            Texture2D chainTexture = TextureAssets.Chain27.Value; 
            Vector2 chainOrigin = new Vector2(chainTexture.Width * 0.5f, chainTexture.Height * 0.5f);
            float chainLength = chainTexture.Height; 

            Vector2 remainingVector = anchorCenter - currentPosition;

            while (remainingVector.Length() > chainLength)
            {
                remainingVector.Normalize();
                remainingVector *= chainLength;

                currentPosition += remainingVector; 
                remainingVector = anchorCenter - currentPosition; 

                float chainRotation = remainingVector.ToRotation() + MathHelper.PiOver2;

                // Mengambil pencahayaan alami di sekitar koordinat rantai
                Color taliColor = Lighting.GetColor((int)currentPosition.X / 16, (int)currentPosition.Y / 16);

                Main.EntitySpriteDraw(
                    chainTexture,
                    currentPosition - Main.screenPosition,
                    null,
                    taliColor, 
                    chainRotation,
                    chainOrigin,
                    1f, 
                    SpriteEffects.None,
                    0
                );
            }

            return true; // Mengembalikan true agar sprite kustom "Tentacle" digambar di ujung rantai hijau
        }
    }
}