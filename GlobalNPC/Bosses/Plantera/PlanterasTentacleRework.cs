using System;
using System.Linq;
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
    public class PlanterasTentacleRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.PlanterasTentacle)
                return base.PreAI(npc);

            if (npc.target < 0 || npc.target == 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
            {
                npc.TargetClosest(true);
            }
            Player playerTarget = Main.player[npc.target];

            NPC planteraUtama = Main.npc.FirstOrDefault(n => n.active && n.type == NPCID.Plantera);
            bool indukMasihHidup = planteraUtama != null;

            if (indukMasihHidup)
            {
                float sudutUnik = npc.localAI[1];
                ref float efekSwayTimer = ref npc.ai[2];

                float pengacakFase = npc.whoAmI * 0.75f; 
                float kecepatanAyunanUnik = 0.03f + (npc.whoAmI % 3 * 0.015f); 
                float lebarAyunanUnik = 0.15f + (npc.whoAmI % 2 * 0.1f);

                efekSwayTimer += kecepatanAyunanUnik; 

                ref float jarakMerayap = ref npc.localAI[0];
                if (jarakMerayap == 0f) 
                {
                    jarakMerayap = 600f; 
                }

                jarakMerayap = MathHelper.Lerp(jarakMerayap, 130f, 0.02f); 

                float sudutSekarang = planteraUtama.ai[2] + sudutUnik + (float)Math.Sin(efekSwayTimer + pengacakFase) * lebarAyunanUnik;
                Vector2 posisiTargetTentakel = planteraUtama.Center + sudutSekarang.ToRotationVector2() * jarakMerayap;

                Vector2 desiredVelocity = (posisiTargetTentakel - npc.Center) * 0.1f;
                npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.15f);
            }
            else
            {
                float kecepatanTerbang = 7f; 
                Vector2 arahKePlayer = npc.DirectionTo(playerTarget.Center);
                npc.velocity = Vector2.Lerp(npc.velocity, arahKePlayer * kecepatanTerbang, 0.05f);
            }

            float rotationOffset = MathHelper.Pi; 
            npc.rotation = npc.DirectionTo(playerTarget.Center).ToRotation() + rotationOffset;

            // --- LOGIKA MENEMBAK UNTUK 1/6 TENTAKEL ---
            if (npc.whoAmI % 6 == 0)
            {
                ref float shootTimer = ref npc.localAI[2]; 
                shootTimer++;

                float maxCycle = 200f;       
                float chargeDuration = 60f;  

                if (shootTimer >= maxCycle)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float kecepatanProyektil = 9f;
                        Vector2 velProyektil = npc.DirectionTo(playerTarget.Center) * kecepatanProyektil;
                        int damage = npc.damage / 2;

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            npc.Center, 
                            velProyektil, 
                            ProjectileID.PoisonSeedPlantera, 
                            damage, 
                            1f, 
                            Main.myPlayer
                        );
                    }
                    
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.Center);

                    // LUMINANCE: Getaran halus tiap tentakel racun menembak
                    ScreenShakeSystem.StartShakeAtPoint(npc.Center, 4f);

                    shootTimer = 0f; 
                }
            }

            return false; 
        }

        // --- HOOK KEMATIAN: SPAWN SPORE NPCID 265 (FIXED) ---
        public override void OnKill(NPC npc)
        {
            if (npc.type != NPCID.PlanterasTentacle)
                return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Menggunakan NPCID.Spore untuk memanggil Spore vanilla (ID 265)
                NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.Spore);
            }
        }

        // --- DRAWING SYSTEM ---
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.PlanterasTentacle)
                return base.PreDraw(npc, spriteBatch, screenPos, drawColor);

            NPC planteraUtama = Main.npc.FirstOrDefault(n => n.active && n.type == NPCID.Plantera);
            
            if (planteraUtama != null)
            {
                Texture2D teksturRantai = TextureAssets.Chain27.Value; 
                Vector2 posisiMulai = npc.Center;
                Vector2 posisiAkhir = planteraUtama.Center;
                
                Vector2 arahRantai = posisiAkhir - posisiMulai;
                float rotasiRantai = arahRantai.ToRotation() - MathHelper.PiOver2;
                float panjangRantai = arahRantai.Length();
                float jarakTerpajang = 0f;

                while (jarakTerpajang < panjangRantai)
                {
                    Vector2 titikDraw = posisiMulai + Vector2.Normalize(arahRantai) * jarakTerpajang;

                    spriteBatch.Draw(
                        teksturRantai,
                        titikDraw - screenPos,
                        null,
                        npc.GetAlpha(drawColor),
                        rotasiRantai,
                        new Vector2(teksturRantai.Width * 0.5f, teksturRantai.Height * 0.5f),
                        1f,
                        SpriteEffects.None,
                        0f
                    );

                    jarakTerpajang += teksturRantai.Height;
                }

                // LUMINANCE: Garis bloom racun tipis di sepanjang rantai, memberi kesan energi mengalir dari induknya
                float denyutRantai = Utilities.Sin01(Main.GlobalTimeWrappedHourly * 3f + npc.whoAmI);
                Color warnaRantaiRacun = new Color(140, 255, 110);

                Utilities.PrepareForShaders(spriteBatch, BlendState.Additive);
                Utilities.DrawBloomLine(spriteBatch, posisiMulai, posisiAkhir, warnaRantaiRacun * (0.18f + denyutRantai * 0.12f), 1.4f);
                Utilities.ResetToDefault(spriteBatch);
            }

            Texture2D teksturTentakel = TextureAssets.Npc[npc.type].Value;
            Vector2 origin = npc.frame.Size() / 2f;
            Color warnaFinal = npc.GetAlpha(drawColor);

            if (npc.whoAmI % 6 == 0)
            {
                float shootTimer = npc.localAI[2];
                float maxCycle = 200f;
                float chargeDuration = 60f;
                float startChargeTime = maxCycle - chargeDuration;

                if (shootTimer > startChargeTime)
                {
                    float progresCharge = (shootTimer - startChargeTime) / chargeDuration;
                    warnaFinal = Color.Lerp(warnaFinal, Color.Red, progresCharge);

                    // LUMINANCE: Titik cahaya bloom di ujung tentakel, membesar seiring charge mendekati selesai
                    Color warnaCharge = Utilities.MulticolorLerp(progresCharge, new Color(150, 255, 120), new Color(255, 70, 60));
                    float panjangFlash = 6f + progresCharge * 16f;

                    Utilities.PrepareForShaders(spriteBatch, BlendState.Additive);
                    Utilities.DrawBloomLine(spriteBatch, npc.Center + new Vector2(-panjangFlash, 0f), npc.Center + new Vector2(panjangFlash, 0f), warnaCharge * progresCharge, 3f);
                    Utilities.DrawBloomLine(spriteBatch, npc.Center + new Vector2(0f, -panjangFlash), npc.Center + new Vector2(0f, panjangFlash), warnaCharge * progresCharge, 3f);
                    Utilities.ResetToDefault(spriteBatch);
                }
            }

            spriteBatch.Draw(
                teksturTentakel,
                npc.Center - screenPos,
                npc.frame,
                warnaFinal,
                npc.rotation,
                origin,
                npc.scale,
                SpriteEffects.None,
                0f
            );

            return false; 
        }

        public override bool CheckActive(NPC npc)
        {
            if (npc.type == NPCID.PlanterasTentacle)
                return false;

            return base.CheckActive(npc);
        }
    }
}