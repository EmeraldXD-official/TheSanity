using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // GLOBAL PROJECTILE — ditempelin ke SEMUA proyektil yang lahir dari 2 ally
    // Twins (EyeFire custom & DeathLaser vanilla yang dipinjam). Nge-handle 2
    // hal yang diminta di bagian "Global":
    //   1. Ignore Defense + Damage Reduction musuh SETEBEL APAPUN (ArmorPenetration
    //      digedein jauh di atas defense NPC manapun yang wajar).
    //   2. Inflict debuff (CursedInferno buat EyeFire / Ichor buat DeathLaser)
    //      begitu proyektil ini kena musuh, tanpa perlu bikin ModProjectile
    //      terpisah buat DeathLaser (yang notabene proyektil vanilla).
    // InstancePerEntity = true supaya tiap instance proyektil punya flag &
    // pengaturan buff-nya sendiri-sendiri (gak numpuk/ketuker sama proyektil lain).
    // ==========================================
    public class TwinsAllyGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool IsFromTwinsAlly;
        public int InflictBuffType = -1;
        public int InflictBuffTime;

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!IsFromTwinsAlly)
                return;

            // Ignore Defense — dorong ArmorPenetration jauh di atas defense NPC
            // manapun yang realistis, jadi damage-nya gak kepotong sama sekali
            // walau musuhnya "setebel apapun". Ini pola standar tModLoader buat
            // "ignore defense sepenuhnya".
            modifiers.ArmorPenetration += 99999f;

            // Ignore Damage Reduction — DR custom (persentase) biasanya
            // diimplementasi lewat ModifyIncomingHit di GlobalNPC musuh, yang
            // scope-nya di luar file ini. Yang bisa kita jamin dari sisi
            // proyektil sendiri adalah damage-nya TIDAK di-scale ulang di sini
            // (SourceDamage dibiarkan apa adanya, gak ada pengurangan tambahan).
            modifiers.SourceDamage *= 1f;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!IsFromTwinsAlly || InflictBuffType == -1)
                return;

            target.AddBuff(InflictBuffType, InflictBuffTime);
        }

        // Helper dipanggil dari tempat proyektil di-spawn (EyeFire.OnSpawn / Retinazer
        // pas nembak DeathLaser) buat set flag + debuff sekaligus dalam 1 baris.
        public static void Configure(Projectile projectile, int buffType, int buffTimeTicks)
        {
            var global = projectile.GetGlobalProjectile<TwinsAllyGlobalProjectile>();
            global.IsFromTwinsAlly = true;
            global.InflictBuffType = buffType;
            global.InflictBuffTime = buffTimeTicks;
        }
    }

    // ==========================================
    // CHAIN12 — rantai visual yang ngiket Spaz-ally & Ret-ally pas dua-duanya
    // muncul bersamaan. Digambar di PostDrawTiles (dijalankan SEBELUM layer
    // NPC/Projectile), jadi otomatis kegambar DI BAWAH sprite kedua proyektil,
    // gak peduli urutan index di array Main.projectile.
    // ==========================================
    public class TwinsAllyChainSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            int spazIndex = -1;
            int retIndex = -1;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active)
                    continue;

                if (p.ModProjectile is TwinsAllySpazmatism)
                    spazIndex = i;
                else if (p.ModProjectile is TwinsAllyRetinazer)
                    retIndex = i;

                if (spazIndex != -1 && retIndex != -1)
                    break;
            }

            if (spazIndex == -1 || retIndex == -1)
                return;

            DrawChain(Main.projectile[spazIndex].Center, Main.projectile[retIndex].Center);
        }

        private void DrawChain(Vector2 pointA, Vector2 pointB)
        {
            Vector2 screenPos = Main.screenPosition;
            Vector2 diff = pointB - pointA;
            float totalLength = diff.Length();
            if (totalLength < 4f)
                return;

            Vector2 direction = diff / totalLength;
            float rotation = direction.ToRotation();

            const float LinkSize = 14f;
            int linkCount = (int)(totalLength / LinkSize);
            if (linkCount < 2)
                linkCount = 2;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // FIX (penyebab Fatal Error pas item dipegang): PostDrawTiles TIDAK
            // punya SpriteBatch aktif sama sekali (beda dari PreDraw proyektil/NPC
            // yang emang udah di dalam Begin/End milik vanilla) - dokumentasi resmi
            // ModSystem.PostDrawTiles bilang eksplisit "spritebatch should be begun
            // and ended WITHIN this method". Versi lama manggil .End() DULUAN
            // sebelum ada .Begin() apa pun di scope ini -> SpriteBatch.End() tanpa
            // Begin() yang mendahului langsung throw InvalidOperationException
            // ("End was called, but Begin has not yet been called") -> CRASH.
            // Ini kejadian PERSIS pas kedua ally ketemu aktif bareng (yaitu begitu
            // OpticalWrench dipegang, soalnya HoldItem langsung spawn dua-duanya),
            // makanya crash-nya nempel banget sama momen "pas dipegang".
            //
            // Sekarang: Begin() SEKALI di awal (baru, valid - gak ada batch aktif
            // sebelumnya buat di-End() dulu), gambar semua link rantai, End() SEKALI
            // di akhir - biar hook ini nutup balik ke state "gak ada batch aktif",
            // sama kayak kondisi awal dia dipanggil.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < linkCount; i++)
            {
                float t = (i + 0.5f) / linkCount;

                // Sedikit "sag" (melengkung turun kayak rantai beneran) pakai
                // kurva parabolic sederhana, bukan garis lurus kaku.
                float sag = (float)System.Math.Sin(t * System.Math.PI) * 10f;

                Vector2 basePos = Vector2.Lerp(pointA, pointB, t) + new Vector2(0f, sag) - screenPos;

                // Selang-seling terang/gelap biar keliatan kayak mata rantai
                // beruntun, bukan strip solid polos.
                bool bright = i % 2 == 0;
                Color linkColor = (bright ? new Color(210, 200, 190) : new Color(90, 85, 80)) * 0.85f;

                Vector2 origin = new Vector2(0.5f, 0.5f);
                Main.spriteBatch.Draw(pixel, basePos, null, linkColor, rotation, origin, new Vector2(LinkSize * 0.9f, 3.5f), SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
        }
    }

    // ==========================================
    // HELPER TARGETING — dipakai Spaz-ally buat nyari musuh terdekat (baik
    // akuisisi awal maupun re-target pas target lama mati). Prioritas: target
    // manual player (klik-kanan minion) kalau ada & valid, else NPC hostile
    // terdekat dalam radius.
    // ==========================================
    public static class TwinsAllyTargeting
    {
        public const float NearbyEnemyRange = 900f; // "di sekitar" ~56 tile

        public static NPC FindTarget(Player owner, Vector2 from, float range)
        {
            if (owner.HasMinionAttackTargetNPC)
            {
                NPC manual = Main.npc[owner.MinionAttackTargetNPC];
                if (manual.active && manual.CanBeChasedBy())
                    return manual;
            }

            NPC best = null;
            float bestDist = range;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(from, npc.Center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = npc;
                }
            }

            return best;
        }
    }
}
