using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;
using Luminance.Common.Utilities;

namespace TheSanity.GlobalNPCs
{
    public class HostileVampireKnife : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_304"; 

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = -1;       
            Projectile.hostile = true;     
            Projectile.friendly = false;   
            Projectile.tileCollide = false; 
            Projectile.penetrate = 1;      
            Projectile.timeLeft = 300;     
        }

        public override void AI()
        {
            Projectile.ai[1]++;

            float sudutTembak = Projectile.ai[0];
            float durasiTelegraph = 45f; 
            float spriteOffset = -MathHelper.PiOver2; 

            Lighting.AddLight(Projectile.Center, 0.7f, 0.05f, 0.05f);

            if (Projectile.ai[1] < durasiTelegraph)
            {
                Projectile.velocity = Vector2.Zero; 
                Projectile.rotation = sudutTembak + spriteOffset; 
                
                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.VampireHeal, 0f, 0f, 0, Color.Red, 0.9f);
                    dust.noGravity = true;
                }
            }
            else if (Projectile.ai[1] == durasiTelegraph)
            {
                float kecepatanMelesat = 14f;
                Projectile.velocity = sudutTembak.ToRotationVector2() * kecepatanMelesat;
                Projectile.tileCollide = true; 
                
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item60, Projectile.Center);
            }
            else
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + spriteOffset;
                
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f);
                    dust.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Selagi telegraph (diam & berkedip), biarkan gambar bawaan vanilla yang menangani.
            if (Projectile.ai[1] < 45f)
                return true;

            // LUMINANCE: Saat sudah melesat, tambahkan jejak cahaya merah mengekor pisaunya
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Color drawColor = Projectile.GetAlpha(lightColor);
            Utilities.DrawAfterimagesCentered(Projectile, 1, drawColor, 2, 4, 0.5f, 0.6f, texture);
            return false;
        }

        public override void PostDraw(Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Color warnaGlow = new Color(255, 40, 40, 0) * 0.85f;

            // PERBAIKAN: Menggunakan Main.spriteBatch yang valid
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                warnaGlow,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale * 1.18f, 
                SpriteEffects.None,
                0f
            );
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // LUMINANCE: Getaran kecil terarah saat pisau vampir menusuk, memberi "impact" ke serangan
            ScreenShakeSystem.StartShakeAtPoint(target.Center, 5f);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.Plantera)
                {
                    int healAmount = (int)(npc.lifeMax * 0.05f); 
                    npc.life += healAmount;
                    
                    if (npc.life > npc.lifeMax)
                        npc.life = npc.lifeMax;

                    npc.HealEffect(healAmount); 

                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                    }
                    break; 
                }
            }
        }
    }
}