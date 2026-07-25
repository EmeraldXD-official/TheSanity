using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Common.Utilities;

namespace TheSanity.GlobalNPCs
{
    public class HostileNettleBurst : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_150"; 

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.aiStyle = -1;         
            Projectile.hostile = true;       
            Projectile.friendly = false;     
            Projectile.tileCollide = false;  
            Projectile.penetrate = -1;       
            
            // PERBAIKAN 1: Waktu hidup dinaikkan ke 360 Ticks (6 Detik) agar bisa merambat sampai ujung screen
            Projectile.timeLeft = 360; 
        }

        public override void AI()
        {
            Projectile.ai[1]++; // Timer internal
            float timer = Projectile.ai[1];

            // -----------------------------------------------------------------
            // SERANGAN TAMBAHAN: SPINNING EXPANDING NETTLE WHIP (Fase 2 Eksklusif)
            // -----------------------------------------------------------------
            if (Projectile.ai[2] >= 200f)
            {
                float kodeRahasia = Projectile.ai[2] - 200f;
                int idLengan = (int)(kodeRahasia / 20f);      
                float idSegmen = kodeRahasia % 20f;          

                int indexPlantera = (int)Projectile.ai[0];
                if (indexPlantera < 0 || indexPlantera >= Main.maxNPCs || !Main.npc[indexPlantera].active || Main.npc[indexPlantera].type != NPCID.Plantera)
                {
                    Projectile.Kill();
                    return;
                }

                NPC plantera = Main.npc[indexPlantera];

                float kecepatanRotasi = 0.024f;  
                float jarakAntarSegmen = 24f;   
                float kecepatanMeluncur = 3.8f;  

                float sudutSekarang = (idLengan * MathHelper.PiOver2) + (timer * kecepatanRotasi);
                float jarakSekarang = (idSegmen * jarakAntarSegmen) + (timer * kecepatanMeluncur);

                Projectile.Center = plantera.Center + sudutSekarang.ToRotationVector2() * jarakSekarang;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = sudutSekarang + MathHelper.PiOver2; // <--- SEKARANG SUDAH BENAR!

                Lighting.AddLight(Projectile.Center, 0.05f, 0.55f, 0.05f);
                if (Main.rand.NextBool(4))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GrassBlades, 0f, 0f, 0, default, 0.8f);
                    dust.noGravity = true;
                }
                return; 
            }

            // --- PERGERAKAN NORMAL FASE 1 & SERANGAN SPORA TRAIL ---
           // --- PERGERAKAN NORMAL FASE 1 & SERANGAN SPORA TRAIL ---
float sudutDasar = Projectile.ai[0];
bool isPhase2 = Projectile.ai[2] >= 10f; //

// UBAH DI SINI: Dari 15f kita naikkan ekstrim ke 32f supaya langsung melesat jauh!
float kecepatanMaju = isPhase2 ? 32f : 6.5f;       
float frekuensiMeliuk = isPhase2 ? 0.35f : 0.16f; // Disesuaikan frekuensinya agar liukannya tetap rapi
float amplitudoMeliuk = isPhase2 ? 22f : 7.5f;    // Liukan diperlebar sedikit agar efek merambatnya terasa besar

Vector2 maju = sudutDasar.ToRotationVector2() * kecepatanMaju; //[cite: 1]
Vector2 samping = sudutDasar.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * MathF.Cos(timer * frekuensiMeliuk) * amplitudoMeliuk; //[cite: 1]

            Projectile.velocity = maju + samping;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (isPhase2 && timer % 7 == 0) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int p = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero, 
                        ProjectileID.SporeGas, 
                        Projectile.damage,
                        0f,
                        Main.myPlayer
                    );

                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].hostile = true;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, 0.05f, 0.55f, 0.05f);
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GrassBlades, 0f, 0f, 0, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float nilaiSegmen = Projectile.ai[2];
            int idTeksturVanilla = 150; 
            
            if (nilaiSegmen >= 200f)
            {
                float seg = (nilaiSegmen - 200f) % 20f;
                if (seg == 0f) idTeksturVanilla = 152;       
                else if (seg == 12f) idTeksturVanilla = 150;  
                else idTeksturVanilla = 151;                  
            }
            else 
            {
                if (nilaiSegmen >= 10f) nilaiSegmen -= 10f;
                if (nilaiSegmen >= 1f && nilaiSegmen <= 5f) idTeksturVanilla = 151;
                else if (nilaiSegmen == 6f) idTeksturVanilla = 152;
            }

            Texture2D texture = TextureAssets.Projectile[idTeksturVanilla].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Color drawColor = Projectile.GetAlpha(lightColor);

            bool sedangMelesatCepat = Projectile.ai[2] >= 10f && Projectile.ai[2] < 200f;

            if (sedangMelesatCepat)
            {
                // LUMINANCE: Sisa cahaya (afterimage) mengekor duri yang melesat kencang di Fase 2.
                // Fungsi ini otomatis tetap menggambar proyektilnya sendiri, jadi tidak perlu Draw manual lagi.
                Utilities.DrawAfterimagesCentered(Projectile, 1, drawColor, 3, 5, 0.55f, 0.65f, texture);
            }
            else
            {
                Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, drawColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);
            }

            return false; 
        }

        public override void PostDraw(Color lightColor)
        {
            float nilaiSegmen = Projectile.ai[2];
            int idTeksturVanilla = 150;

            if (nilaiSegmen >= 200f)
            {
                float seg = (nilaiSegmen - 200f) % 20f;
                if (seg == 0f) idTeksturVanilla = 152;
                else if (seg == 12f) idTeksturVanilla = 150;
                else idTeksturVanilla = 151;
            }
            else
            {
                if (nilaiSegmen >= 10f) nilaiSegmen -= 10f;
                if (nilaiSegmen >= 1f && nilaiSegmen <= 5f) idTeksturVanilla = 151;
                else if (nilaiSegmen == 6f) idTeksturVanilla = 152;
            }

            Texture2D texture = TextureAssets.Projectile[idTeksturVanilla].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Color glowColor = new Color(90, 255, 90, 0) * 0.4f;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, drawOrigin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);

            // LUMINANCE: Garis bloom yang menghubungkan tiap segmen cambuk berputar, biar terlihat seperti satu lecutan utuh
            if (nilaiSegmen >= 200f)
            {
                int indexPlantera = (int)Projectile.ai[0];
                if (indexPlantera >= 0 && indexPlantera < Main.maxNPCs && Main.npc[indexPlantera].active && Main.npc[indexPlantera].type == NPCID.Plantera)
                {
                    NPC plantera = Main.npc[indexPlantera];
                    float timer = Projectile.ai[1];

                    float kodeRahasia = nilaiSegmen - 200f;
                    int idLengan = (int)(kodeRahasia / 20f);
                    float idSegmen = kodeRahasia % 20f;

                    float kecepatanRotasi = 0.024f;
                    float jarakAntarSegmen = 24f;
                    float kecepatanMeluncur = 3.8f;

                    // Titik segmen sebelumnya (lebih dekat ke Plantera), dihitung ulang dari rumus AI yang sama
                    float idSegmenSebelumnya = idSegmen - 1f;
                    if (idSegmenSebelumnya >= 0f)
                    {
                        float sudutSebelumnya = (idLengan * MathHelper.PiOver2) + (timer * kecepatanRotasi);
                        float jarakSebelumnya = (idSegmenSebelumnya * jarakAntarSegmen) + (timer * kecepatanMeluncur);
                        Vector2 titikSebelumnya = plantera.Center + sudutSebelumnya.ToRotationVector2() * jarakSebelumnya;

                        Utilities.PrepareForShaders(Main.spriteBatch, BlendState.Additive);
                        Utilities.DrawBloomLine(Main.spriteBatch, titikSebelumnya, Projectile.Center, new Color(140, 255, 130) * 0.5f, 2.2f);
                        Utilities.ResetToDefault(Main.spriteBatch);
                    }
                }
            }
        }
    }
}