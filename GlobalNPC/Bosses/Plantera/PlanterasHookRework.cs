using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio; 
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Common.Utilities;

namespace TheSanity.GlobalNPCs
{
    public class PlanterasHookRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.PlanterasHook)
                return base.PreAI(npc);

            if (npc.target < 0 || npc.target == 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
            {
                npc.TargetClosest(true);
            }
            Player playerTarget = Main.player[npc.target];

            // Cari apakah induk Plantera masih ada dan aktif di dalam world
            NPC planteraUtama = Main.npc.FirstOrDefault(n => n.active && n.type == NPCID.Plantera);
            bool indukMasihHidup = planteraUtama != null;

            // JIKA PLANTERA UTAMA SUDAH MATI ATAU DESPAWN, INSTANT HAPUS HOOK INI!
            if (!indukMasihHidup)
            {
                npc.active = false; 
                return false; // Berhenti mengeksekusi kode AI di bawahnya
            }

            // --- PERGERAKAN SWAY KUSTOM (Hanya jalan jika Plantera masih hidup) ---
            ref float efekSwayTimer = ref npc.ai[2];

            float speedMultiplier = 0.015f + (npc.whoAmI % 3 * 0.008f);
            efekSwayTimer += speedMultiplier;

            float timerX = efekSwayTimer + (npc.whoAmI * 1.5f);
            float timerY = efekSwayTimer * 0.85f + (npc.whoAmI * 2.3f);

            float rangeX = 150f + (npc.whoAmI % 4) * 90f;
            float rangeY = 150f + (npc.whoAmI % 3) * 110f;

            Vector2 offsetAcak = new Vector2(
                (float)Math.Sin(timerX) * rangeX,
                (float)Math.Cos(timerY) * rangeY
            );

            Vector2 posisiTargetHook = planteraUtama.Center + offsetAcak;

            Vector2 desiredVelocity = (posisiTargetHook - npc.Center) * 0.1f;
            npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.08f);

            npc.rotation = npc.DirectionTo(playerTarget.Center).ToRotation() + MathHelper.PiOver2;

            // --- MEKANIK TEMBAKAN THORNBALL (CHANCE 1/6) ---
            ref float shootTimer = ref npc.localAI[2];
            shootTimer++;

            if (shootTimer >= 240f)
            {
                shootTimer = 0f; 

                if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(6))
                {
                    float kecepatanProyektil = 7.5f; 
                    Vector2 velProyektil = npc.DirectionTo(playerTarget.Center) * kecepatanProyektil;
                    int damage = npc.damage / 3; 

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        velProyektil,
                        ProjectileID.ThornBall,
                        damage,
                        1f,
                        Main.myPlayer
                    );

                    SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                }
            }

            return false; // Tetap matikan AI asli agar pergerakan kustom tidak bentrok
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.PlanterasHook)
                return base.PreDraw(npc, spriteBatch, screenPos, drawColor);

            NPC planteraUtama = Main.npc.FirstOrDefault(n => n.active && n.type == NPCID.Plantera);

            if (planteraUtama != null)
            {
                Texture2D teksturRantai = TextureAssets.Chain26.Value; 
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

                // LUMINANCE: Rantai berpendar, semakin terang & kemerahan saat hampir menembakkan Thornball
                float progresCharge = Utilities.InverseLerp(180f, 240f, npc.localAI[2]);
                Color warnaRantai = Utilities.MulticolorLerp(progresCharge, new Color(120, 255, 120), new Color(255, 160, 60));

                Utilities.PrepareForShaders(spriteBatch, BlendState.Additive);
                Utilities.DrawBloomLine(spriteBatch, posisiMulai, posisiAkhir, warnaRantai * (0.25f + progresCharge * 0.35f), 1.6f);
                Utilities.ResetToDefault(spriteBatch);
            }

            return true;
        }

        public override bool CheckActive(NPC npc)
        {
            if (npc.type == NPCID.PlanterasHook)
                return false;

            return base.CheckActive(npc);
        }
    }
}