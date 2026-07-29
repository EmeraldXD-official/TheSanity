using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class ElectrifiedNPCMod : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal penularan wabah
        private int spreadCooldown = 0;

        // =========================================================================
        // LOGIKA DAMAGE OVER TIME (DoT) - MURNI TRUE DAMAGE (IGNORE DEFENSE & DR)
        // =========================================================================
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (npc.HasBuff(BuffID.Electrified))
            {
                bool isMoving = npc.velocity.Length() > 0.2f;

                // =====================================================================
                // [GUIDE LOCATION 1: TRUE DAMAGE VIA LIFE REGEN]
                // Karena kita memotong HP langsung lewat 'lifeRegen', sistem ini 100% IGNORE 
                // semua Defense dan DR (Damage Reduction) musuh secara bawaan. 
                // Tidak ada armor di Terraria yang bisa menahan efek pengurangan ini.
                // =====================================================================
                if (isMoving)
                {
                    if (npc.lifeRegen > 0) npc.lifeRegen = 0;
                    npc.lifeRegen -= 40; // -20 HP per detik mutlak saat bergerak
                    damage = 10;         // Mengunci teks angka popup damage agar akurat
                }
                else
                {
                    if (npc.lifeRegen > 0) npc.lifeRegen = 0;
                    npc.lifeRegen -= 8;  // -4 HP per detik mutlak saat diam
                    damage = 2;          // Mengunci teks angka popup damage agar akurat
                }
            }
        }

        // =========================================================================
        // LOGIKA PENULARAN WABAH LISTRIK BERANTAI
        // =========================================================================
        public override void PostAI(NPC npc)
        {
            if (!npc.active || npc.friendly || npc.dontTakeDamage)
                return;

            int MathBuffIndex = npc.FindBuffIndex(BuffID.Electrified);
            
            if (MathBuffIndex != -1)
            {
                int remainingDuration = npc.buffTime[MathBuffIndex];

                spreadCooldown++;
                if (spreadCooldown >= 15)
                {
                    float spreadRadius = 80f; // Radius 5 Block

                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC otherNPC = Main.npc[i];

                        if (i != npc.whoAmI && otherNPC.active && !otherNPC.friendly && !otherNPC.dontTakeDamage && !otherNPC.HasBuff(BuffID.Electrified))
                        {
                            float distance = Vector2.Distance(npc.Center, otherNPC.Center);
                            
                            if (distance <= spreadRadius)
                            {
                                otherNPC.AddBuff(BuffID.Electrified, remainingDuration);

                                Vector2 direction = otherNPC.Center - npc.Center;
                                direction.Normalize();
                                
                                float laserSpeed = 9f;
                                int particleDensity = (int)(distance / 8);

                                for (int j = 0; j < particleDensity; j++)
                                {
                                    Vector2 spawnPosition = npc.Center + direction * (j * 8);
                                    spawnPosition += Main.rand.NextVector2Circular(4f, 4f);

                                    Dust d = Dust.NewDustPerfect(spawnPosition, DustID.Electric, direction * laserSpeed, 100, default, 0.55f);
                                    d.noGravity = true;
                                }
                                break;
                            }
                        }
                    }
                    spreadCooldown = 0;
                }
            }
            else
            {
                spreadCooldown = 0;
            }
        }

        // =========================================================================
        // LOGIKA VISUAL PARTIKEL DI BADAN MUSUH & EFEK TINT WARNA
        // =========================================================================
        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (npc.HasBuff(BuffID.Electrified))
            {
                // [GUIDE LOCATION 4: NPC BODY BLUE TINT]
                drawColor.R = (byte)(drawColor.R * 0.5f);
                drawColor.G = (byte)(drawColor.G * 0.7f);
                drawColor.B = (byte)(drawColor.B * 1.3f);

                // [GUIDE LOCATION 5: BODY DUST SPARK FREQUENCY]
                if (Main.rand.NextBool(6)) 
                {
                    int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, Main.rand.NextFloat(0.5f, 0.8f));
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= 0.4f;
                }
            }
        }

        // =========================================================================
        // LOGIKA BLUE OUTLINE GLOW (DIGAMBAR DI BELAKANG MUSUH)
        // =========================================================================
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.HasBuff(BuffID.Electrified) && !npc.IsABestiaryIconDummy)
            {
                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = npc.frame.Size() / 2f;
                Vector2 drawPos = npc.Center - screenPos;
                SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                // [GUIDE LOCATION 6: OUTLINE COLOR & THICKNESS]
                Color glowColor = new Color(0, 140, 255, 0) * 0.85f; 
                float outlineThickness = 2f; 

                for (int i = 0; i < 4; i++)
                {
                    Vector2 drawOffset = new Vector2(outlineThickness, 0f).RotatedBy(MathHelper.PiOver2 * i);
                    
                    spriteBatch.Draw(
                        texture, 
                        drawPos + drawOffset, 
                        npc.frame, 
                        glowColor, 
                        npc.rotation, 
                        drawOrigin, 
                        npc.scale, 
                        effects, 
                        0f
                    );
                }
            }
            return true;
        }
    }
}