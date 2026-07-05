using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    public class DiamondBossClone : ModNPC
    {
        // Menggunakan visual asset yang sama persis dengan boss Diamond utama
        public override string Texture => "TheSanity/GlobalNPC/Bosses/GemGuardian/DiamondBoss";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 48;
            NPC.height = 48;
            NPC.damage = 0; // Mulai dari 0 agar aman sewaktu berputar membidik
            NPC.defense = 5;
            NPC.lifeMax = 99999;
            NPC.dontTakeDamage = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.scale = 0.6f; // Lebih kecil dibanding unit aslinya
        }

        public override void AI() {
            int parentIndex = (int)NPC.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxNPCs || !Main.npc[parentIndex].active || Main.npc[parentIndex].type != ModContent.NPCType<DiamondBoss>()) {
                NPC.active = false;
                return;
            }

            NPC boss = Main.npc[parentIndex];
            NPC.target = boss.target;
            Player player = Main.player[NPC.target];

            int id = (int)NPC.ai[1];
            int total = (int)NPC.localAI[0];
            if (total == 0) total = 8;

            int isThrown = (int)NPC.ai[2]; // 0 = Mengorbit, 1 = Terlempar ke Player

            if (isThrown == 0) {
                // Berputar mengitari pusat tubuh Diamond Boss Utama
                NPC.damage = 0;
                NPC.ai[3] += 0.04f; // Nilai rotasi orbit konstan
                float currentAngle = (MathHelper.TwoPi / total * id) + NPC.ai[3];
                Vector2 targetOrbitPos = boss.Center + currentAngle.ToRotationVector2() * 110f;
                
                NPC.Center = targetOrbitPos;
                NPC.rotation = currentAngle - MathHelper.PiOver2;
            }
            else {
                // Aktifkan damage mematikan kontak fisik ketika mulai dilempar lurus
                NPC.damage = 100;
                NPC.rotation += 0.25f;
                if (Main.rand.NextBool(4)) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemDiamond, 0f, 0f, 120, Color.White, 0.9f);
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            Color whiteIce = new Color(240, 250, 255, 200);

            int isThrown = (int)NPC.ai[2];

            // Menggambar garis pandu bidikan sekian detik tepat sebelum klon dilemparkan
            if (isThrown == 0 && NPC.target >= 0 && NPC.target < 255) {
                Vector2 toTarget = Main.player[NPC.target].Center - NPC.Center;
                float len = Math.Max(toTarget.Length(), 1500f);
                toTarget.Normalize();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), whiteIce * 0.25f, toTarget.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, 1.5f), SpriteEffects.None, 0f);
            }

            // Outline visual bayangan putih tipis melingkar
            for (int i = 0; i < 4; i++) {
                Vector2 off = new Vector2(3f, 0f).RotatedBy(MathHelper.PiOver2 * i);
                spriteBatch.Draw(texture, NPC.Center - screenPos + off, null, whiteIce * 0.45f, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, drawColor * 0.8f, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}